using System;
using System.Drawing;
using ClipTray.ClipBar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class WindowBackdropTests
    {
        private static readonly Version Windows7 = new Version(6, 1, 7601);
        private static readonly Version Windows81 = new Version(6, 3, 9600);
        private static readonly Version Windows10Rtm = new Version(10, 0, 10240);
        private static readonly Version Windows10_1803 = new Version(10, 0, 17134);
        private static readonly Version Windows11 = new Version(10, 0, 22000);
        private static readonly Version Windows11_22H2 = new Version(10, 0, 22621);

        [TestMethod]
        public void BackdropModes_AreOnlyTheThreeThatBehave()
        {
            // The accent-API blur and acrylic modes were dropped: they made GDI child
            // controls composite as transparent and could not be relied on.
            CollectionAssert.AreEqual(
                new[] { "None", "Translucent", "SystemAcrylic" },
                Enum.GetNames(typeof(BackdropMode)));
        }

        [TestMethod]
        public void Resolve_SystemAcrylic_FallsBackToTranslucentBeforeWindows11_22H2()
        {
            Assert.AreEqual(BackdropMode.SystemAcrylic,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows11_22H2));

            // Windows 11 21H2 predates DWMWA_SYSTEMBACKDROP_TYPE.
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows11));
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows10_1803));
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows7));
        }

        [TestMethod]
        public void OpacityFor_SystemAcrylicStaysFullyOpaque()
        {
            // DWM composites the backdrop behind the window, so layered opacity would
            // only wash out the text without revealing any more blur.
            Assert.AreEqual(1D, WindowBackdrop.OpacityFor(BackdropMode.SystemAcrylic, 85), 0.0001);
            Assert.AreEqual(1D, WindowBackdrop.OpacityFor(BackdropMode.SystemAcrylic, 50), 0.0001);
        }

        [TestMethod]
        public void Resolve_TranslucentAndNone_AreAlwaysHonoured()
        {
            foreach (var os in new[] { Windows7, Windows81, Windows10Rtm, Windows11, Windows11_22H2 })
            {
                Assert.AreEqual(BackdropMode.Translucent,
                    WindowBackdrop.Resolve(BackdropMode.Translucent, os));
                Assert.AreEqual(BackdropMode.None,
                    WindowBackdrop.Resolve(BackdropMode.None, os));
            }
        }

        [TestMethod]
        public void Resolve_NeverUpgradesBeyondTheRequestedMode()
        {
            // Asking for Translucent on Windows 11 must not silently become acrylic.
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Translucent, Windows11_22H2));
        }

        [TestMethod]
        public void OpacityFor_NoneIsFullyOpaque()
        {
            Assert.AreEqual(1D, WindowBackdrop.OpacityFor(BackdropMode.None, 50), 0.0001);
        }

        [TestMethod]
        public void OpacityFor_UsesTransparencyPercent()
        {
            Assert.AreEqual(0.85D, WindowBackdrop.OpacityFor(BackdropMode.Translucent, 85), 0.0001);
            Assert.AreEqual(0.60D, WindowBackdrop.OpacityFor(BackdropMode.Translucent, 60), 0.0001);
        }

        [TestMethod]
        public void OpacityFor_ClampsOutOfRangeValues()
        {
            // A hand-edited INI must never make the window invisible.
            Assert.AreEqual(0.50D, WindowBackdrop.OpacityFor(BackdropMode.Translucent, 0), 0.0001);
            Assert.AreEqual(0.50D, WindowBackdrop.OpacityFor(BackdropMode.Translucent, -20), 0.0001);
            Assert.AreEqual(1.00D, WindowBackdrop.OpacityFor(BackdropMode.Translucent, 500), 0.0001);
        }
    }

    [TestClass]
    public class ClipBarThemeTests
    {
        [TestMethod]
        public void Dark_And_Light_AreDistinctAndSelfConsistent()
        {
            var dark = ClipBarTheme.Dark;
            var light = ClipBarTheme.Light;

            Assert.IsTrue(dark.IsDark);
            Assert.IsFalse(light.IsDark);
            Assert.AreNotEqual(dark.Background, light.Background);
            Assert.AreNotEqual(dark.Title, light.Title);
        }

        [TestMethod]
        public void For_ExplicitModes_IgnoreTheSystemSetting()
        {
            Assert.IsTrue(ClipBarTheme.For(ThemeMode.Dark).IsDark);
            Assert.IsFalse(ClipBarTheme.For(ThemeMode.Light).IsDark);
        }

        [TestMethod]
        public void For_System_MatchesTheRegistryPreference()
        {
            Assert.AreEqual(
                ClipBarTheme.SystemPrefersDark(),
                ClipBarTheme.For(ThemeMode.System).IsDark);
        }

        [TestMethod]
        public void Themes_KeepTitleReadableAgainstTheirBackground()
        {
            // Guards against a palette edit that makes text vanish.
            foreach (var theme in new[] { ClipBarTheme.Dark, ClipBarTheme.Light })
            {
                double contrast = Math.Abs(
                    Brightness(theme.Title) - Brightness(theme.Background));
                Assert.IsTrue(contrast > 0.4,
                    "Title/background contrast too low: " + contrast);
            }
        }

        private static double Brightness(Color color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255D;
        }
    }
}
