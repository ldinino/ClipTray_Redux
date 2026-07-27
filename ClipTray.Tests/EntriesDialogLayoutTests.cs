using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ClipTray.Models;
using ClipTray.Settings;
using ClipTray.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    /// <summary>
    /// Guards two layout invariants that only misbehave at high DPI. Pixel
    /// measurements cannot cover them here because the test host is not DPI aware
    /// and always reports 96 DPI, so these assert the structure instead.
    /// </summary>
    [TestClass]
    public class EntriesDialogLayoutTests
    {
        // Built with settings so the footer carries the ClipBar button too, matching
        // what the tray actually constructs.
        private static EntriesDialog NewDialog()
        {
            return new EntriesDialog(
                new List<ClipEntry> { new ClipEntry { Title = "Sample", Text = "Body" } },
                @"C:\temp\ClipTray.txt",
                20,
                false,
                new AppSettings());
        }

        private static Control Find(Control root, string name)
        {
            var matches = root.Controls.Find(name, true);
            Assert.AreEqual(1, matches.Length, "Expected exactly one control named " + name);
            return matches[0];
        }

        [TestMethod]
        public void InsertFooter_HasAFlexibleColumn()
        {
            // With every column AutoSize, non-linear text growth pushed the menu-size
            // spinner 26px off the panel at 200%. One flexible column absorbs that.
            using (var dialog = NewDialog())
            {
                var footer = (TableLayoutPanel)Find(dialog, "menuSizeHost").Parent;

                Assert.IsTrue(
                    footer.ColumnStyles.Cast<ColumnStyle>()
                        .Any(style => style.SizeType == SizeType.Percent),
                    "The insert footer needs a percent-sized column so the menu-size "
                    + "spinner cannot be pushed out of view at high DPI.");
            }
        }

        [TestMethod]
        public void DraftHeader_AutoSizes()
        {
            // A fixed 48px header clipped its action buttons by 16px at 200%.
            using (var dialog = NewDialog())
            {
                var header = (TableLayoutPanel)Find(dialog, "draftTitle").Parent;

                Assert.IsTrue(header.AutoSize,
                    "The draft header must auto-size; a fixed height clips the Copy/"
                    + "Preview/Duplicate/Delete buttons at high DPI.");
            }
        }

        [TestMethod]
        public void ClipBarButton_AppearsOnlyWhenSettingsAreSupplied()
        {
            using (var withSettings = NewDialog())
            {
                Assert.AreEqual(1, withSettings.Controls.Find("clipBarSettingsButton", true).Length);
            }

            using (var withoutSettings = new EntriesDialog(
                new List<ClipEntry> { new ClipEntry { Title = "Sample", Text = "Body" } },
                @"C:\temp\ClipTray.txt", 20))
            {
                Assert.AreEqual(0, withoutSettings.Controls.Find("clipBarSettingsButton", true).Length,
                    "Without settings there is nothing for the button to edit.");
            }
        }

        [TestMethod]
        public void ClipBarButton_RaisesTheRequestEvent()
        {
            using (var dialog = NewDialog())
            {
                bool raised = false;
                dialog.ClipBarSettingsRequested += (s, e) => raised = true;

                var button = dialog.Controls.Find("clipBarSettingsButton", true)[0];

                // PerformClick is a no-op while the form has never been shown, so the
                // Click event is raised directly.
                typeof(Control)
                    .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(button, new object[] { EventArgs.Empty });

                Assert.IsTrue(raised, "The tray owns the dialog, so the button must raise the event.");
            }
        }
    }
}
