using System;
using System.Globalization;
using System.IO;
using System.Text;
using ClipTray.ClipBar;

namespace ClipTray.Settings
{
    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> as INI text. Malformed values fall
    /// back to defaults rather than throwing, and saving preserves any content the
    /// store does not recognise.
    /// </summary>
    public static class SettingsStore
    {
        public const string FileName = "ClipTray.settings.ini";

        private const string ClipBarSection = "ClipBar";
        private const string GeneralSection = "General";
        private const string ExtrasSection = "Extras";
        private const string RecentSection = "Recent";

        private const string KeyEnabled = "Enabled";
        private const string KeyHotkey = "Hotkey";
        private const string KeyBackdrop = "Backdrop";
        private const string KeyTransparency = "Transparency";
        private const string KeyTheme = "Theme";
        private const string KeyMaxResults = "MaxResults";
        private const string KeySizeMultiplier = "SizeMultiplier";
        private const string KeyWidth = "Width";
        private const string KeyMenuSize = "MenuSize";
        private const string KeyRecentFile = "RecentFile";
        private const string KeyAutoPaste = "AutoPaste";
        private const string KeyRankRecentFirst = "RankRecentFirst";
        private const string KeyResolveTokensInPreview = "ResolveTokensInPreview";
        private const string KeyAltEnterOpensEditor = "AltEnterOpensEditor";

        public static string DefaultPath(string executablePath)
        {
            return Path.Combine(Path.GetDirectoryName(executablePath) ?? string.Empty, FileName);
        }

        public static AppSettings Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new AppSettings();

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                return new AppSettings();
            }
            catch (UnauthorizedAccessException)
            {
                return new AppSettings();
            }

            return Read(IniDocument.Parse(text));
        }

        internal static AppSettings Read(IniDocument document)
        {
            var settings = new AppSettings();

            settings.ClipBarEnabled = ReadBool(
                document.Get(ClipBarSection, KeyEnabled), settings.ClipBarEnabled);

            HotKeyDefinition hotKey;
            if (HotKeyDefinition.TryParse(document.Get(ClipBarSection, KeyHotkey), out hotKey))
                settings.ClipBarHotKey = hotKey;

            settings.Backdrop = ReadEnum(
                document.Get(ClipBarSection, KeyBackdrop), settings.Backdrop);

            settings.Theme = ReadEnum(
                document.Get(ClipBarSection, KeyTheme), settings.Theme);

            settings.Transparency = AppSettings.Clamp(
                ReadInt(document.Get(ClipBarSection, KeyTransparency), settings.Transparency),
                AppSettings.MinTransparency, AppSettings.MaxTransparency);

            settings.MaxResults = AppSettings.Clamp(
                ReadInt(document.Get(ClipBarSection, KeyMaxResults), settings.MaxResults),
                AppSettings.MinMaxResults, AppSettings.MaxMaxResults);

            settings.SizeMultiplier = AppSettings.Clamp(
                ReadFloat(document.Get(ClipBarSection, KeySizeMultiplier), settings.SizeMultiplier),
                AppSettings.MinSizeMultiplier, AppSettings.MaxSizeMultiplier);

            settings.Width = AppSettings.Clamp(
                ReadInt(document.Get(ClipBarSection, KeyWidth), settings.Width),
                AppSettings.MinWidth, AppSettings.MaxWidth);

            settings.MenuSize = AppSettings.Clamp(
                ReadInt(document.Get(GeneralSection, KeyMenuSize), settings.MenuSize),
                AppSettings.MinMenuSize, AppSettings.MaxMenuSize);

            var recentFile = document.Get(GeneralSection, KeyRecentFile);
            settings.RecentFile = string.IsNullOrWhiteSpace(recentFile) ? null : recentFile;

            settings.AutoPaste = ReadBool(
                document.Get(ExtrasSection, KeyAutoPaste), settings.AutoPaste);
            settings.RankRecentFirst = ReadBool(
                document.Get(ExtrasSection, KeyRankRecentFirst), settings.RankRecentFirst);
            settings.ResolveTokensInPreview = ReadBool(
                document.Get(ExtrasSection, KeyResolveTokensInPreview), settings.ResolveTokensInPreview);
            settings.AltEnterOpensEditor = ReadBool(
                document.Get(ExtrasSection, KeyAltEnterOpensEditor), settings.AltEnterOpensEditor);

            // Numbered keys, because insert titles are arbitrary user text and would
            // not survive being used as INI keys.
            settings.RecentTitles.Clear();
            for (int index = 1; index <= AppSettings.MaxRecentTitles; index++)
            {
                var title = document.Get(RecentSection, index.ToString(CultureInfo.InvariantCulture));
                if (string.IsNullOrEmpty(title)) break;
                settings.RecentTitles.Add(title);
            }

            return settings;
        }

        /// <summary>
        /// Writes settings atomically: a sibling temp file is fully written first and
        /// then swapped in, so an interrupted save cannot truncate the real file.
        /// Failures are reported by the return value rather than thrown - losing a
        /// preference must never take the app down.
        /// </summary>
        public static bool Save(string path, AppSettings settings)
        {
            if (string.IsNullOrEmpty(path) || settings == null) return false;

            IniDocument document;
            try
            {
                document = File.Exists(path)
                    ? IniDocument.Parse(File.ReadAllText(path))
                    : CreateSeedDocument();
            }
            catch (IOException)
            {
                document = CreateSeedDocument();
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            Write(document, settings);

            try
            {
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, document.ToString(), new UTF8Encoding(false));

                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void Write(IniDocument document, AppSettings settings)
        {
            document.Set(ClipBarSection, KeyEnabled, settings.ClipBarEnabled ? "true" : "false");
            document.Set(ClipBarSection, KeyHotkey, settings.ClipBarHotKey.ToString());
            document.Set(ClipBarSection, KeyBackdrop, settings.Backdrop.ToString());
            document.Set(ClipBarSection, KeyTransparency,
                settings.Transparency.ToString(CultureInfo.InvariantCulture));
            document.Set(ClipBarSection, KeyTheme, settings.Theme.ToString());
            document.Set(ClipBarSection, KeyMaxResults,
                settings.MaxResults.ToString(CultureInfo.InvariantCulture));

            document.Set(GeneralSection, KeyMenuSize,
                settings.MenuSize.ToString(CultureInfo.InvariantCulture));
            document.Set(GeneralSection, KeyRecentFile, settings.RecentFile ?? string.Empty);

            document.Set(ExtrasSection, KeyAutoPaste, settings.AutoPaste ? "true" : "false");
            document.Set(ExtrasSection, KeyRankRecentFirst, settings.RankRecentFirst ? "true" : "false");
            document.Set(ExtrasSection, KeyResolveTokensInPreview,
                settings.ResolveTokensInPreview ? "true" : "false");
            document.Set(ExtrasSection, KeyAltEnterOpensEditor,
                settings.AltEnterOpensEditor ? "true" : "false");

            // Write the titles plus one empty terminator: the reader stops at the
            // first blank, so a shorter list cleanly supersedes a longer one without
            // padding the file with fifty empty keys.
            for (int index = 0; index < settings.RecentTitles.Count; index++)
            {
                document.Set(
                    RecentSection,
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    settings.RecentTitles[index]);
            }
            document.Set(
                RecentSection,
                (settings.RecentTitles.Count + 1).ToString(CultureInfo.InvariantCulture),
                string.Empty);

            // SizeMultiplier and Width are deliberately not written: they are advanced
            // escape hatches that stay absent unless a user adds them by hand.
        }

        private static IniDocument CreateSeedDocument()
        {
            var document = IniDocument.Empty();
            document.AddCommentLine("ClipTray settings. Safe to delete - defaults will be restored.");
            return document;
        }

        private static bool ReadBool(string text, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            switch (text.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "1":
                    return true;
                case "false":
                case "no":
                case "0":
                    return false;
                default:
                    return fallback;
            }
        }

        private static T ReadEnum<T>(string text, T fallback) where T : struct
        {
            T value;
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            return Enum.TryParse(text.Trim(), true, out value)
                && Enum.IsDefined(typeof(T), value)
                ? value
                : fallback;
        }

        private static int ReadInt(string text, int fallback)
        {
            int value;
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static float ReadFloat(string text, float fallback)
        {
            float value;
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }
    }
}
