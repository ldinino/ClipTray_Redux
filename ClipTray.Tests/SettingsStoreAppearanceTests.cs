using System.IO;
using ClipTray.ClipBar;
using ClipTray.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    /// <summary>Round-trip and fallback coverage for the Phase 2 appearance keys.</summary>
    [TestClass]
    public class SettingsStoreAppearanceTests
    {
        private string _directory;
        private string _path;

        [TestInitialize]
        public void CreateScratchDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ClipTrayAppearance_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, SettingsStore.FileName);
        }

        [TestCleanup]
        public void RemoveScratchDirectory()
        {
            try { Directory.Delete(_directory, true); }
            catch (IOException) { }
        }

        [TestMethod]
        public void Defaults_MatchThePhaseZeroDecision()
        {
            var settings = SettingsStore.Load(_path);

            Assert.AreEqual(BackdropMode.SystemAcrylic, settings.Backdrop);
            Assert.AreEqual(100, settings.Transparency);
            Assert.AreEqual(ThemeMode.System, settings.Theme);
        }

        [TestMethod]
        public void AppearanceSettings_RoundTrip()
        {
            var original = new AppSettings
            {
                Backdrop = BackdropMode.Translucent,
                Transparency = 70,
                Theme = ThemeMode.Light
            };

            Assert.IsTrue(SettingsStore.Save(_path, original));
            var loaded = SettingsStore.Load(_path);

            Assert.AreEqual(BackdropMode.Translucent, loaded.Backdrop);
            Assert.AreEqual(70, loaded.Transparency);
            Assert.AreEqual(ThemeMode.Light, loaded.Theme);
        }

        [TestMethod]
        public void EnumValues_AreCaseInsensitive()
        {
            File.WriteAllText(_path, "[ClipBar]\r\nBackdrop=translucent\r\nTheme=DARK\r\n");

            var settings = SettingsStore.Load(_path);

            Assert.AreEqual(BackdropMode.Translucent, settings.Backdrop);
            Assert.AreEqual(ThemeMode.Dark, settings.Theme);
        }

        [TestMethod]
        public void RetiredBackdropValues_FallBackToTheDefault()
        {
            // Settings files written before Blur and Acrylic were dropped must still
            // load, landing on the backdrop that replaced them.
            foreach (var retired in new[] { "Blur", "Acrylic" })
            {
                File.WriteAllText(_path, "[ClipBar]\r\nBackdrop=" + retired + "\r\n");

                Assert.AreEqual(BackdropMode.SystemAcrylic, SettingsStore.Load(_path).Backdrop, retired);
            }
        }

        [TestMethod]
        public void UnknownEnumValues_FallBackToDefaults()
        {
            File.WriteAllText(_path, "[ClipBar]\r\nBackdrop=hologram\r\nTheme=sepia\r\n");

            var settings = SettingsStore.Load(_path);

            Assert.AreEqual(BackdropMode.SystemAcrylic, settings.Backdrop);
            Assert.AreEqual(ThemeMode.System, settings.Theme);
        }

        [TestMethod]
        public void NumericEnumValues_OutOfRangeFallBack()
        {
            // Enum.TryParse happily accepts "99"; IsDefined has to reject it.
            File.WriteAllText(_path, "[ClipBar]\r\nBackdrop=99\r\n");

            Assert.AreEqual(BackdropMode.SystemAcrylic, SettingsStore.Load(_path).Backdrop);
        }

        [TestMethod]
        public void Transparency_IsClampedToLegibleRange()
        {
            File.WriteAllText(_path, "[ClipBar]\r\nTransparency=5\r\n");
            Assert.AreEqual(AppSettings.MinTransparency, SettingsStore.Load(_path).Transparency);

            File.WriteAllText(_path, "[ClipBar]\r\nTransparency=250\r\n");
            Assert.AreEqual(AppSettings.MaxTransparency, SettingsStore.Load(_path).Transparency);
        }

        [TestMethod]
        public void Transparency_NonNumericFallsBack()
        {
            File.WriteAllText(_path, "[ClipBar]\r\nTransparency=very\r\n");

            Assert.AreEqual(AppSettings.DefaultTransparency, SettingsStore.Load(_path).Transparency);
        }
    }
}
