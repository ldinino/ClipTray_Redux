using System;
using System.Drawing;
using System.Security;
using Microsoft.Win32;

namespace ClipTray.ClipBar
{
    public enum ThemeMode
    {
        /// <summary>Follow the Windows app theme.</summary>
        System,
        Dark,
        Light
    }

    /// <summary>The colours ClipBar paints with.</summary>
    internal sealed class ClipBarTheme
    {
        private const string PersonalizeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        private ClipBarTheme(
            bool isDark,
            Color background,
            Color inputBand,
            Color title,
            Color preview,
            Color divider,
            Color selection,
            Color magnifier)
        {
            IsDark = isDark;
            Background = background;
            InputBand = inputBand;
            Title = title;
            Preview = preview;
            Divider = divider;
            Selection = selection;
            Magnifier = magnifier;
        }

        public bool IsDark { get; }
        public Color Background { get; }
        public Color InputBand { get; }
        public Color Title { get; }
        public Color Preview { get; }
        public Color Divider { get; }
        public Color Selection { get; }
        public Color Magnifier { get; }

        public static ClipBarTheme Dark
        {
            get
            {
                return new ClipBarTheme(
                    isDark: true,
                    background: Color.FromArgb(26, 26, 31),
                    inputBand: Color.FromArgb(40, 40, 48),
                    title: Color.White,
                    preview: Color.FromArgb(185, 255, 255, 255),
                    divider: Color.FromArgb(60, 255, 255, 255),
                    selection: Color.FromArgb(105, 150, 235),
                    magnifier: Color.FromArgb(170, 255, 255, 255));
            }
        }

        public static ClipBarTheme Light
        {
            get
            {
                return new ClipBarTheme(
                    isDark: false,
                    background: Color.FromArgb(247, 248, 250),
                    inputBand: Color.White,
                    title: Color.FromArgb(24, 27, 32),
                    // Darker than the usual secondary grey: at 12px on white the
                    // lighter value read as barely there.
                    preview: Color.FromArgb(74, 80, 92),
                    divider: Color.FromArgb(48, 0, 0, 0),
                    selection: Color.FromArgb(197, 216, 247),
                    magnifier: Color.FromArgb(130, 0, 0, 0));
            }
        }

        public static ClipBarTheme For(ThemeMode mode)
        {
            switch (mode)
            {
                case ThemeMode.Dark:
                    return Dark;
                case ThemeMode.Light:
                    return Light;
                default:
                    return SystemPrefersDark() ? Dark : Light;
            }
        }

        /// <summary>
        /// Reads the Windows app theme. Anything unreadable is treated as light,
        /// which is the Windows default.
        /// </summary>
        internal static bool SystemPrefersDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key == null) return false;

                    var value = key.GetValue(AppsUseLightThemeValue);
                    if (value is int lightTheme) return lightTheme == 0;
                    return false;
                }
            }
            catch (SecurityException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
