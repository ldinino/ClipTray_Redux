using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClipTray.ClipBar;
using ClipTray.Models;
using ClipTray.Settings;
using ClipTray.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class RecentUseTests
    {
        [TestMethod]
        public void RecordUse_PutsTheNewestFirst()
        {
            var settings = new AppSettings();

            settings.RecordUse("one");
            settings.RecordUse("two");
            settings.RecordUse("three");

            CollectionAssert.AreEqual(
                new[] { "three", "two", "one" }, settings.RecentTitles.ToArray());
        }

        [TestMethod]
        public void RecordUse_MovesAnExistingTitleRatherThanDuplicating()
        {
            var settings = new AppSettings();

            settings.RecordUse("one");
            settings.RecordUse("two");
            settings.RecordUse("one");

            CollectionAssert.AreEqual(new[] { "one", "two" }, settings.RecentTitles.ToArray());
        }

        [TestMethod]
        public void RecordUse_IsCaseInsensitive()
        {
            var settings = new AppSettings();

            settings.RecordUse("Meeting");
            settings.RecordUse("MEETING");

            Assert.AreEqual(1, settings.RecentTitles.Count);
        }

        [TestMethod]
        public void RecordUse_IgnoresBlanks()
        {
            var settings = new AppSettings();

            settings.RecordUse(null);
            settings.RecordUse("");

            Assert.AreEqual(0, settings.RecentTitles.Count);
        }

        [TestMethod]
        public void RecordUse_IsCappedSoTheFileCannotGrowForever()
        {
            var settings = new AppSettings();

            for (int index = 0; index < AppSettings.MaxRecentTitles + 20; index++)
                settings.RecordUse("title " + index);

            Assert.AreEqual(AppSettings.MaxRecentTitles, settings.RecentTitles.Count);
            Assert.AreEqual("title " + (AppSettings.MaxRecentTitles + 19), settings.RecentTitles[0]);
        }
    }

    [TestClass]
    public class InsertSearchRecencyTests
    {
        private static List<ClipEntry> Entries()
        {
            return new List<ClipEntry>
            {
                new ClipEntry { Title = "Alpha report", Text = "body" },
                new ClipEntry { Title = "Alpha summary", Text = "body" },
                new ClipEntry { Title = "Alpha notes", Text = "body" }
            };
        }

        [TestMethod]
        public void Recency_ReordersEquallyGoodMatches()
        {
            var recent = new List<string> { "Alpha notes", "Alpha summary" };

            var results = InsertSearch.Rank(Entries(), "alpha", 5, recent);

            Assert.AreEqual("Alpha notes", results[0].Title);
            Assert.AreEqual("Alpha summary", results[1].Title);
            Assert.AreEqual("Alpha report", results[2].Title, "Never-used entries fall to the back");
        }

        [TestMethod]
        public void Recency_NeverBeatsMatchQuality()
        {
            var entries = new List<ClipEntry>
            {
                new ClipEntry { Title = "Unrelated", Text = "mentions alpha in the body" },
                new ClipEntry { Title = "Alpha exact", Text = "body" }
            };

            // The weak body match is the most recently used, and must still lose.
            var recent = new List<string> { "Unrelated" };

            var results = InsertSearch.Rank(entries, "alpha", 5, recent);

            Assert.AreEqual("Alpha exact", results[0].Title);
        }

        [TestMethod]
        public void Recency_AppliesToTheEmptyQueryListing()
        {
            var recent = new List<string> { "Alpha notes" };

            var results = InsertSearch.Rank(Entries(), "", 5, recent);

            Assert.AreEqual("Alpha notes", results[0].Title);
        }

        [TestMethod]
        public void WithoutRecency_FileOrderIsPreserved()
        {
            var results = InsertSearch.Rank(Entries(), "alpha", 5);

            Assert.AreEqual("Alpha report", results[0].Title);
            Assert.AreEqual("Alpha summary", results[1].Title);
            Assert.AreEqual("Alpha notes", results[2].Title);
        }

        [TestMethod]
        public void UnknownRecentTitles_AreIgnored()
        {
            var recent = new List<string> { "Nothing like this exists" };

            var results = InsertSearch.Rank(Entries(), "alpha", 5, recent);

            Assert.AreEqual("Alpha report", results[0].Title);
        }
    }

    [TestClass]
    public class SettingsStoreExtrasTests
    {
        private string _directory;
        private string _path;

        [TestInitialize]
        public void CreateScratchDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ClipTrayExtras_" + System.Guid.NewGuid().ToString("N"));
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
        public void EveryExtra_IsOffByDefault()
        {
            var settings = SettingsStore.Load(_path);

            Assert.IsFalse(settings.AutoPaste);
            Assert.IsFalse(settings.RankRecentFirst);
            Assert.IsFalse(settings.ResolveTokensInPreview);
            Assert.IsFalse(settings.AltEnterOpensEditor);
        }

        [TestMethod]
        public void Extras_RoundTrip()
        {
            var original = new AppSettings
            {
                AutoPaste = true,
                RankRecentFirst = true,
                ResolveTokensInPreview = true,
                AltEnterOpensEditor = true
            };

            Assert.IsTrue(SettingsStore.Save(_path, original));
            var loaded = SettingsStore.Load(_path);

            Assert.IsTrue(loaded.AutoPaste);
            Assert.IsTrue(loaded.RankRecentFirst);
            Assert.IsTrue(loaded.ResolveTokensInPreview);
            Assert.IsTrue(loaded.AltEnterOpensEditor);
        }

        [TestMethod]
        public void RecentTitles_RoundTripInOrder()
        {
            var original = new AppSettings();
            original.RecordUse("first");
            original.RecordUse("second");
            original.RecordUse("third");

            SettingsStore.Save(_path, original);
            var loaded = SettingsStore.Load(_path);

            CollectionAssert.AreEqual(
                new[] { "third", "second", "first" }, loaded.RecentTitles.ToArray());
        }

        [TestMethod]
        public void RecentTitles_SurviveAwkwardCharacters()
        {
            // Titles are arbitrary user text, which is why numbered keys are used.
            var original = new AppSettings();
            original.RecordUse("has = equals and [brackets]");
            original.RecordUse("# looks like a comment");

            SettingsStore.Save(_path, original);
            var loaded = SettingsStore.Load(_path);

            CollectionAssert.AreEqual(
                new[] { "# looks like a comment", "has = equals and [brackets]" },
                loaded.RecentTitles.ToArray());
        }

        [TestMethod]
        public void RecentTitles_ShorterListSupersedesALongerOne()
        {
            var original = new AppSettings();
            original.RecordUse("a");
            original.RecordUse("b");
            original.RecordUse("c");
            SettingsStore.Save(_path, original);

            var trimmed = SettingsStore.Load(_path);
            trimmed.RecentTitles.RemoveRange(1, 2);
            SettingsStore.Save(_path, trimmed);

            Assert.AreEqual(1, SettingsStore.Load(_path).RecentTitles.Count);
        }

        [TestMethod]
        public void Save_DoesNotPadTheFileWithEmptyRecentKeys()
        {
            var settings = new AppSettings();
            settings.RecordUse("only one");
            SettingsStore.Save(_path, settings);

            var lines = File.ReadAllLines(_path).Where(line => line.StartsWith("1=") || line.StartsWith("2=")).ToArray();

            Assert.AreEqual(2, lines.Length, "One title plus a single terminator");
        }
    }

    [TestClass]
    public class ClipBarSettingsDialogExtrasTests
    {
        private static ClipBarSettingsDialog NewDialog(AppSettings settings)
        {
            return new ClipBarSettingsDialog(settings, definition => true);
        }

        private static CheckBox Box(Form form, string name)
        {
            return (CheckBox)form.Controls.Find(name, true)[0];
        }

        [TestMethod]
        public void Extras_LoadFromSettings()
        {
            var settings = new AppSettings
            {
                AutoPaste = true,
                RankRecentFirst = false,
                ResolveTokensInPreview = true,
                AltEnterOpensEditor = false
            };

            using (var dialog = NewDialog(settings))
            {
                Assert.IsTrue(Box(dialog, "extraAutoPaste").Checked);
                Assert.IsFalse(Box(dialog, "extraRankRecent").Checked);
                Assert.IsTrue(Box(dialog, "extraResolveTokens").Checked);
                Assert.IsFalse(Box(dialog, "extraAltEnter").Checked);
            }
        }

        [TestMethod]
        public void Extras_ApplyBack()
        {
            using (var dialog = NewDialog(new AppSettings()))
            {
                Box(dialog, "extraAutoPaste").Checked = true;
                Box(dialog, "extraAltEnter").Checked = true;

                var target = new AppSettings();
                dialog.ApplyTo(target);

                Assert.IsTrue(target.AutoPaste);
                Assert.IsTrue(target.AltEnterOpensEditor);
                Assert.IsFalse(target.RankRecentFirst);
                Assert.IsFalse(target.ResolveTokensInPreview);
            }
        }

        [TestMethod]
        public void Extras_DefaultToUnchecked()
        {
            using (var dialog = NewDialog(new AppSettings()))
            {
                foreach (var name in new[]
                    { "extraAutoPaste", "extraRankRecent", "extraResolveTokens", "extraAltEnter" })
                {
                    Assert.IsFalse(Box(dialog, name).Checked, name + " must be opt-in");
                }
            }
        }

        [TestMethod]
        public void Extras_MarkTheDialogDirty()
        {
            using (var dialog = NewDialog(new AppSettings()))
            {
                Box(dialog, "extraAutoPaste").Checked = true;

                Assert.IsTrue(((Button)dialog.Controls.Find("applyButton", true)[0]).Enabled);
            }
        }
    }
}
