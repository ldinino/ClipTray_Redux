using System;
using System.IO;
using ClipTray.ClipBar;
using ClipTray.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class SettingsStoreTests
    {
        private string _directory;
        private string _path;

        [TestInitialize]
        public void CreateScratchDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ClipTrayTests_" + Guid.NewGuid().ToString("N"));
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
        public void Load_MissingFile_ReturnsDefaults()
        {
            var settings = SettingsStore.Load(_path);

            Assert.IsTrue(settings.ClipBarEnabled);
            Assert.AreEqual(HotKeyDefinition.Default, settings.ClipBarHotKey);
            Assert.AreEqual(AppSettings.DefaultMaxResults, settings.MaxResults);
            Assert.AreEqual(AppSettings.DefaultMenuSize, settings.MenuSize);
            Assert.AreEqual(AppSettings.DefaultWidth, settings.Width);
            Assert.AreEqual(AppSettings.DefaultSizeMultiplier, settings.SizeMultiplier);
            Assert.IsNull(settings.RecentFile);
        }

        [TestMethod]
        public void SaveThenLoad_RoundTripsEveryPersistedValue()
        {
            HotKeyDefinition hotKey;
            HotKeyDefinition.TryParse("Ctrl+Shift+V", out hotKey);

            var original = new AppSettings
            {
                ClipBarEnabled = false,
                ClipBarHotKey = hotKey,
                MaxResults = 9,
                MenuSize = 42,
                RecentFile = @"C:\templates\other.txt"
            };

            Assert.IsTrue(SettingsStore.Save(_path, original));
            var loaded = SettingsStore.Load(_path);

            Assert.IsFalse(loaded.ClipBarEnabled);
            Assert.AreEqual(hotKey, loaded.ClipBarHotKey);
            Assert.AreEqual(9, loaded.MaxResults);
            Assert.AreEqual(42, loaded.MenuSize);
            Assert.AreEqual(@"C:\templates\other.txt", loaded.RecentFile);
        }

        [TestMethod]
        public void Save_CreatesFileWithoutLeavingTempBehind()
        {
            Assert.IsTrue(SettingsStore.Save(_path, new AppSettings()));

            Assert.IsTrue(File.Exists(_path));
            Assert.IsFalse(File.Exists(_path + ".tmp"), "Atomic write should not leave a temp file");
        }

        [TestMethod]
        public void Load_CorruptFile_FallsBackToDefaults()
        {
            File.WriteAllText(_path, "this is not ini\0\0\0\r\n!!!! [[[ \r\n=====\r\n");

            var settings = SettingsStore.Load(_path);

            Assert.AreEqual(HotKeyDefinition.Default, settings.ClipBarHotKey);
            Assert.AreEqual(AppSettings.DefaultMenuSize, settings.MenuSize);
        }

        [TestMethod]
        public void Load_MalformedValues_FallBackPerKey()
        {
            File.WriteAllText(_path,
                "[ClipBar]\r\n" +
                "Enabled=perhaps\r\n" +
                "Hotkey=Ctrl+Alt+NotAKey\r\n" +
                "MaxResults=banana\r\n" +
                "\r\n[General]\r\nMenuSize=\r\n");

            var settings = SettingsStore.Load(_path);

            Assert.IsTrue(settings.ClipBarEnabled, "Unparseable bool should keep its default");
            Assert.AreEqual(HotKeyDefinition.Default, settings.ClipBarHotKey);
            Assert.AreEqual(AppSettings.DefaultMaxResults, settings.MaxResults);
            Assert.AreEqual(AppSettings.DefaultMenuSize, settings.MenuSize);
        }

        [TestMethod]
        public void Load_OutOfRangeValues_AreClamped()
        {
            File.WriteAllText(_path,
                "[ClipBar]\r\nMaxResults=9999\r\nSizeMultiplier=99\r\nWidth=5\r\n" +
                "\r\n[General]\r\nMenuSize=0\r\n");

            var settings = SettingsStore.Load(_path);

            Assert.AreEqual(AppSettings.MaxMaxResults, settings.MaxResults);
            Assert.AreEqual(AppSettings.MaxSizeMultiplier, settings.SizeMultiplier);
            Assert.AreEqual(AppSettings.MinWidth, settings.Width);
            Assert.AreEqual(AppSettings.MinMenuSize, settings.MenuSize);
        }

        [TestMethod]
        public void Save_PreservesCommentsAndUnknownKeys()
        {
            File.WriteAllText(_path,
                "# hand written header\r\n" +
                "[ClipBar]\r\n" +
                "Enabled=true\r\n" +
                "FutureOption=keep me\r\n" +
                "\r\n" +
                "[Experimental]\r\n" +
                "Something=else\r\n");

            var settings = SettingsStore.Load(_path);
            settings.MenuSize = 33;
            Assert.IsTrue(SettingsStore.Save(_path, settings));

            var text = File.ReadAllText(_path);
            StringAssert.Contains(text, "# hand written header");
            StringAssert.Contains(text, "FutureOption=keep me");
            StringAssert.Contains(text, "[Experimental]");
            StringAssert.Contains(text, "Something=else");
            StringAssert.Contains(text, "MenuSize=33");
        }

        [TestMethod]
        public void Save_PreservesHandAddedAdvancedSizingKeys()
        {
            // SizeMultiplier and Width are read but never written, so a hand-edited
            // value has to survive a settings save untouched.
            File.WriteAllText(_path,
                "[ClipBar]\r\nSizeMultiplier=1.25\r\nWidth=880\r\n");

            var settings = SettingsStore.Load(_path);
            Assert.AreEqual(1.25F, settings.SizeMultiplier, 0.001F);
            Assert.AreEqual(880, settings.Width);

            Assert.IsTrue(SettingsStore.Save(_path, settings));

            var text = File.ReadAllText(_path);
            StringAssert.Contains(text, "SizeMultiplier=1.25");
            StringAssert.Contains(text, "Width=880");

            var reloaded = SettingsStore.Load(_path);
            Assert.AreEqual(1.25F, reloaded.SizeMultiplier, 0.001F);
            Assert.AreEqual(880, reloaded.Width);
        }

        [TestMethod]
        public void Save_DoesNotIntroduceAdvancedKeysOnItsOwn()
        {
            Assert.IsTrue(SettingsStore.Save(_path, new AppSettings()));

            var text = File.ReadAllText(_path);
            Assert.IsFalse(text.Contains("SizeMultiplier"), "Advanced keys stay absent by default");
            Assert.IsFalse(text.Contains("Width="), "Advanced keys stay absent by default");
        }

        [TestMethod]
        public void Save_OverwritesExistingValueInPlace()
        {
            var settings = new AppSettings { MenuSize = 10 };
            SettingsStore.Save(_path, settings);

            settings.MenuSize = 11;
            SettingsStore.Save(_path, settings);

            var text = File.ReadAllText(_path);
            Assert.AreEqual(11, SettingsStore.Load(_path).MenuSize);
            Assert.IsFalse(text.Contains("MenuSize=10"), "Old value should be replaced, not duplicated");
        }

        [TestMethod]
        public void Save_NullRecentFile_LoadsBackAsNull()
        {
            var settings = new AppSettings { RecentFile = null };
            SettingsStore.Save(_path, settings);

            Assert.IsNull(SettingsStore.Load(_path).RecentFile);
        }

        [TestMethod]
        public void DefaultPath_SitsBesideTheExecutable()
        {
            var path = SettingsStore.DefaultPath(@"C:\Apps\ClipTray\ClipTray.exe");

            Assert.AreEqual(@"C:\Apps\ClipTray\" + SettingsStore.FileName, path);
        }
    }
}
