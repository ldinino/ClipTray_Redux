using System.Collections.Generic;
using ClipTray.ClipBar;

namespace ClipTray.Settings
{
    /// <summary>
    /// Everything ClipTray persists outside the inserts file. Every property has a
    /// working default, so a missing or unreadable settings file is never fatal.
    /// </summary>
    public sealed class AppSettings
    {
        public const int DefaultMenuSize = 20;
        public const int DefaultMaxResults = 5;
        public const int DefaultWidth = 740;
        public const int DefaultTransparency = 100;
        public const float DefaultSizeMultiplier = 1F;

        public const int MinMenuSize = 1;
        public const int MaxMenuSize = 100;
        public const int MinMaxResults = 3;
        public const int MaxMaxResults = 15;
        public const int MinWidth = 520;
        public const int MaxWidth = 1100;
        public const int MinTransparency = 50;
        public const int MaxTransparency = 100;
        public const float MinSizeMultiplier = 0.5F;
        public const float MaxSizeMultiplier = 3F;

        public bool ClipBarEnabled { get; set; } = true;

        public HotKeyDefinition ClipBarHotKey { get; set; } = HotKeyDefinition.Default;

        /// <summary>
        /// Translucency and blur treatment behind ClipBar. Acrylic via the accent API
        /// is the default because it renders consistently; SystemAcrylic blurs more
        /// convincingly on Windows 11 but relies on a glass client area.
        /// </summary>
        public BackdropMode Backdrop { get; set; } = BackdropMode.Acrylic;

        /// <summary>
        /// Percent opacity, 50 to 100. Defaults to fully opaque; lower it to trade
        /// legibility for translucency and, with a blurring backdrop, blur.
        /// </summary>
        public int Transparency { get; set; } = DefaultTransparency;

        public ThemeMode Theme { get; set; } = ThemeMode.System;

        /// <summary>How many matches ClipBar lists at once.</summary>
        public int MaxResults { get; set; } = DefaultMaxResults;

        /// <summary>
        /// Advanced, file-only escape hatch. Multiplies the automatic scale for
        /// displays where the heuristic guesses wrong. Never written by the UI.
        /// </summary>
        public float SizeMultiplier { get; set; } = DefaultSizeMultiplier;

        /// <summary>Advanced, file-only. Logical ClipBar width at 96 DPI.</summary>
        public int Width { get; set; } = DefaultWidth;

        /// <summary>How many inserts the tray menu lists.</summary>
        public int MenuSize { get; set; } = DefaultMenuSize;

        /// <summary>The previously opened inserts file, for quick switching.</summary>
        public string RecentFile { get; set; }

        // --- Extras. Every one is off by default. ---

        /// <summary>
        /// Sends Ctrl+V to whatever regains focus after copying. Convenient, but it
        /// types into whichever window happens to be in front, so it is opt-in.
        /// </summary>
        public bool AutoPaste { get; set; }

        /// <summary>Orders equally good matches by how recently they were used.</summary>
        public bool RankRecentFirst { get; set; }

        /// <summary>Shows what tokens will produce rather than the raw placeholder.</summary>
        public bool ResolveTokensInPreview { get; set; }

        /// <summary>Alt+Enter opens the highlighted insert in the editor.</summary>
        public bool AltEnterOpensEditor { get; set; }

        /// <summary>Insert titles, most recently used first.</summary>
        public List<string> RecentTitles { get; } = new List<string>();

        /// <summary>How many titles the recently-used list retains.</summary>
        public const int MaxRecentTitles = 50;

        /// <summary>Moves a title to the front of the recently-used list.</summary>
        public void RecordUse(string title)
        {
            if (string.IsNullOrEmpty(title)) return;

            RecentTitles.RemoveAll(entry => string.Equals(entry, title, System.StringComparison.OrdinalIgnoreCase));
            RecentTitles.Insert(0, title);

            while (RecentTitles.Count > MaxRecentTitles)
                RecentTitles.RemoveAt(RecentTitles.Count - 1);
        }

        public static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        public static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
