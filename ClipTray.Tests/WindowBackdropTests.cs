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
        private static readonly Version Windows10_1709 = new Version(10, 0, 16299);
        private static readonly Version Windows10_1803 = new Version(10, 0, 17134);
        private static readonly Version Windows11 = new Version(10, 0, 22000);
        private static readonly Version Windows11_22H2 = new Version(10, 0, 22621);

        [TestMethod]
        public void Resolve_SystemAcrylic_DegradesThroughEveryTier()
        {
            Assert.AreEqual(BackdropMode.SystemAcrylic,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows11_22H2));

            // Windows 11 21H2 predates DWMWA_SYSTEMBACKDROP_TYPE.
            Assert.AreEqual(BackdropMode.Acrylic,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows11));
            Assert.AreEqual(BackdropMode.Acrylic,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows10_1803));
            Assert.AreEqual(BackdropMode.Blur,
                WindowBackdrop.Resolve(BackdropMode.SystemAcrylic, Windows10_1709));
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
        public void Resolve_Acrylic_DegradesByOsVersion()
        {
            Assert.AreEqual(BackdropMode.Acrylic,
                WindowBackdrop.Resolve(BackdropMode.Acrylic, Windows11));
            Assert.AreEqual(BackdropMode.Acrylic,
                WindowBackdrop.Resolve(BackdropMode.Acrylic, Windows10_1803));

            // 1709 has blur but not acrylic.
            Assert.AreEqual(BackdropMode.Blur,
                WindowBackdrop.Resolve(BackdropMode.Acrylic, Windows10_1709));

            // Older than blur support: plain translucency.
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Acrylic, Windows10Rtm));
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Acrylic, Windows81));
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Acrylic, Windows7));
        }

        [TestMethod]
        public void Resolve_Blur_FallsBackToTranslucentBeforeWindows10_1709()
        {
            Assert.AreEqual(BackdropMode.Blur,
                WindowBackdrop.Resolve(BackdropMode.Blur, Windows10_1709));
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Blur, Windows10Rtm));
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Blur, Windows7));
        }

        [TestMethod]
        public void Resolve_TranslucentAndNone_AreAlwaysHonoured()
        {
            foreach (var os in new[] { Windows7, Windows81, Windows10Rtm, Windows11 })
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
            // Asking for Translucent on Windows 11 must not silently become Acrylic.
            Assert.AreEqual(BackdropMode.Translucent,
                WindowBackdrop.Resolve(BackdropMode.Translucent, Windows11));
        }

        [TestMethod]
        public void OpacityFor_NoneIsFullyOpaque()
        {
            Assert.AreEqual(1D, WindowBackdrop.OpacityFor(BackdropMode.None, 50), 0.0001);
        }

        [TestMethod]
        public void OpacityFor_UsesTransparencyPercent()
        {
            Assert.AreEqual(0.85D, WindowBackdrop.OpacityFor(BackdropMode.Acrylic, 85), 0.0001);
            Assert.AreEqual(0.60D, WindowBackdrop.OpacityFor(BackdropMode.Blur, 60), 0.0001);
        }

        [TestMethod]
        public void OpacityFor_ClampsOutOfRangeValues()
        {
            // A hand-edited INI must never make the window invisible.
            Assert.AreEqual(0.50D, WindowBackdrop.OpacityFor(BackdropMode.Acrylic, 0), 0.0001);
            Assert.AreEqual(0.50D, WindowBackdrop.OpacityFor(BackdropMode.Acrylic, -20), 0.0001);
            Assert.AreEqual(1.00D, WindowBackdrop.OpacityFor(BackdropMode.Acrylic, 500), 0.0001);
        }

        [TestMethod]
        public void ToAbgr_SwapsRedAndBlueAndAppliesAlpha()
        {
            // The accent API wants ABGR; getting this backwards tints the bar wrongly.
            var colour = Color.FromArgb(0x1A, 0x2B, 0x3C); // R=1A G=2B B=3C

            uint packed = WindowBackdrop.ToAbgr(colour, 100);

            Assert.AreEqual(0xFFu, (packed >> 24) & 0xFF, "alpha");
            Assert.AreEqual(0x3Cu, (packed >> 16) & 0xFF, "blue");
            Assert.AreEqual(0x2Bu, (packed >> 8) & 0xFF, "green");
            Assert.AreEqual(0x1Au, packed & 0xFF, "red");
        }

        [TestMethod]
        public void ToAbgr_ZeroPercentIsFullyTransparent()
        {
            Assert.AreEqual(0u, (WindowBackdrop.ToAbgr(Color.White, 0) >> 24) & 0xFF);
        }

        [TestMethod]
        public void UsesAccentPolicy_IsSkippedAtFullOpacity()
        {
            // The accent policy makes GDI child controls composite as transparent, so
            // it must not be applied when it cannot show any blur anyway. Leaving it on
            // made the ClipBar query box and everything typed into it invisible.
            Assert.IsFalse(WindowBackdrop.UsesAccentPolicy(BackdropMode.Acrylic, 1D));
            Assert.IsFalse(WindowBackdrop.UsesAccentPolicy(BackdropMode.Blur, 1D));
        }

        [TestMethod]
        public void UsesAccentPolicy_AppliesBelowFullOpacity()
        {
            Assert.IsTrue(WindowBackdrop.UsesAccentPolicy(BackdropMode.Acrylic, 0.85D));
            Assert.IsTrue(WindowBackdrop.UsesAccentPolicy(BackdropMode.Blur, 0.5D));
        }

        [TestMethod]
        public void UsesAccentPolicy_IgnoresModesThatDoNotUseIt()
        {
            Assert.IsFalse(WindowBackdrop.UsesAccentPolicy(BackdropMode.None, 0.85D));
            Assert.IsFalse(WindowBackdrop.UsesAccentPolicy(BackdropMode.Translucent, 0.85D));
            Assert.IsFalse(WindowBackdrop.UsesAccentPolicy(BackdropMode.SystemAcrylic, 0.85D));
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
