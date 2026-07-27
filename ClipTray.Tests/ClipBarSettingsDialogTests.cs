using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ClipTray.ClipBar;
using ClipTray.Settings;
using ClipTray.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class ClipBarSettingsDialogTests
    {
        /// <summary>
        /// Every combination reports as free, so the tests never touch real system
        /// hotkeys or depend on what else is running.
        /// </summary>
        private static ClipBarSettingsDialog NewDialog(AppSettings settings, bool available = true)
        {
            return new ClipBarSettingsDialog(settings, definition => available);
        }

        private static AppSettings Settings()
        {
            return new AppSettings();
        }

        private static T Control<T>(Form form, string name) where T : Control
        {
            var matches = form.Controls.Find(name, true);
            Assert.AreEqual(1, matches.Length, "Expected one control named " + name);
            return (T)matches[0];
        }

        [TestMethod]
        public void Dialog_LoadsEverySettingIntoItsControl()
        {
            HotKeyDefinition hotKey;
            HotKeyDefinition.TryParse("Ctrl+Shift+F4", out hotKey);

            var settings = new AppSettings
            {
                ClipBarEnabled = true,
                ClipBarHotKey = hotKey,
                Backdrop = BackdropMode.Translucent,
                Transparency = 70,
                MaxResults = 9,
                Theme = ThemeMode.Light
            };

            using (var dialog = NewDialog(settings))
            {
                Assert.IsTrue(Control<CheckBox>(dialog, "clipBarEnabled").Checked);
                Assert.IsTrue(Control<CheckBox>(dialog, "modCtrl").Checked);
                Assert.IsFalse(Control<CheckBox>(dialog, "modAlt").Checked);
                Assert.IsTrue(Control<CheckBox>(dialog, "modShift").Checked);
                Assert.IsFalse(Control<CheckBox>(dialog, "modWin").Checked);
                Assert.AreEqual(70, Control<TrackBar>(dialog, "transparencySlider").Value);
                Assert.AreEqual(9M, Control<NumericUpDown>(dialog, "maxResults").Value);
                Assert.AreEqual(hotKey, dialog.SelectedHotKey);
            }
        }

        [TestMethod]
        public void ApplyTo_WritesEveryEditedValueBack()
        {
            var settings = Settings();

            using (var dialog = NewDialog(settings))
            {
                Control<CheckBox>(dialog, "modCtrl").Checked = true;
                Control<CheckBox>(dialog, "modAlt").Checked = false;
                Control<CheckBox>(dialog, "modShift").Checked = true;
                Control<CheckBox>(dialog, "modWin").Checked = false;
                SelectKey(dialog, Keys.F9);
                Control<TrackBar>(dialog, "transparencySlider").Value = 65;
                Control<NumericUpDown>(dialog, "maxResults").Value = 12;

                var target = new AppSettings();
                dialog.ApplyTo(target);

                Assert.AreEqual("Ctrl+Shift+F9", target.ClipBarHotKey.ToString());
                Assert.AreEqual(65, target.Transparency);
                Assert.AreEqual(12, target.MaxResults);
            }
        }

        [TestMethod]
        public void ApplyTo_RoundTripsBackdropAndTheme()
        {
            var settings = new AppSettings
            {
                Backdrop = BackdropMode.SystemAcrylic,
                Theme = ThemeMode.Dark
            };

            using (var dialog = NewDialog(settings))
            {
                var target = new AppSettings();
                dialog.ApplyTo(target);

                Assert.AreEqual(BackdropMode.SystemAcrylic, target.Backdrop);
                Assert.AreEqual(ThemeMode.Dark, target.Theme);
            }
        }

        [TestMethod]
        public void SelectedHotKey_IsNullWithoutAModifier()
        {
            using (var dialog = NewDialog(Settings()))
            {
                Control<CheckBox>(dialog, "modCtrl").Checked = false;
                Control<CheckBox>(dialog, "modAlt").Checked = false;
                Control<CheckBox>(dialog, "modShift").Checked = false;
                Control<CheckBox>(dialog, "modWin").Checked = false;

                Assert.IsNull(dialog.SelectedHotKey, "A bare key is not a global shortcut");
            }
        }

        [TestMethod]
        public void ApplyTo_KeepsExistingHotKeyWhenSelectionIsIncomplete()
        {
            var settings = Settings();
            var original = settings.ClipBarHotKey;

            using (var dialog = NewDialog(settings))
            {
                Control<CheckBox>(dialog, "modCtrl").Checked = false;
                Control<CheckBox>(dialog, "modAlt").Checked = false;
                Control<CheckBox>(dialog, "modShift").Checked = false;
                Control<CheckBox>(dialog, "modWin").Checked = false;

                var target = new AppSettings { ClipBarHotKey = original };
                dialog.ApplyTo(target);

                Assert.AreEqual(original, target.ClipBarHotKey,
                    "An unusable selection must not wipe the working shortcut");
            }
        }

        [TestMethod]
        public void Status_ReportsAvailability()
        {
            using (var dialog = NewDialog(Settings(), available: true))
            {
                StringAssert.Contains(Control<Label>(dialog, "hotkeyStatus").Text, "available");
            }

            using (var dialog = NewDialog(Settings(), available: false))
            {
                StringAssert.Contains(
                    Control<Label>(dialog, "hotkeyStatus").Text, "already used by another application");
            }
        }

        [TestMethod]
        public void Status_ReportsWhenClipBarIsTurnedOff()
        {
            var settings = new AppSettings { ClipBarEnabled = false };

            using (var dialog = NewDialog(settings))
            {
                StringAssert.Contains(Control<Label>(dialog, "hotkeyStatus").Text, "turned off");
                Assert.IsFalse(Control<ComboBox>(dialog, "hotkeyKey").Enabled,
                    "Shortcut controls are pointless while ClipBar is disabled");
            }
        }

        [TestMethod]
        public void DisablingClipBar_GreysOutAppearanceControls()
        {
            using (var dialog = NewDialog(Settings()))
            {
                Control<CheckBox>(dialog, "clipBarEnabled").Checked = false;

                Assert.IsFalse(Control<ComboBox>(dialog, "backdropCombo").Enabled);
                Assert.IsFalse(Control<TrackBar>(dialog, "transparencySlider").Enabled);
                Assert.IsFalse(Control<NumericUpDown>(dialog, "maxResults").Enabled);
                Assert.IsFalse(Control<ComboBox>(dialog, "themeCombo").Enabled);
            }
        }

        [TestMethod]
        public void SizingControls_AreDeliberatelyAbsent()
        {
            // Sizing is automatic with INI-only escape hatches; surfacing it here was
            // an explicit decision to leave out.
            using (var dialog = NewDialog(Settings()))
            {
                Assert.AreEqual(0, dialog.Controls.Find("sizeMultiplier", true).Length);
                Assert.AreEqual(0, dialog.Controls.Find("widthInput", true).Length);
            }
        }

        [TestMethod]
        public void OutOfRangeStoredValues_DoNotThrowOnLoad()
        {
            // A hand-edited INI can hold anything; the dialog clamps rather than crashes.
            var settings = new AppSettings { Transparency = 5, MaxResults = 99 };

            using (var dialog = NewDialog(settings))
            {
                Assert.AreEqual(AppSettings.MinTransparency,
                    Control<TrackBar>(dialog, "transparencySlider").Value);
                Assert.AreEqual((decimal)AppSettings.MaxMaxResults,
                    Control<NumericUpDown>(dialog, "maxResults").Value);
            }
        }

        [TestMethod]
        public void Apply_StartsDisabledUntilSomethingChanges()
        {
            using (var dialog = NewDialog(Settings()))
            {
                var apply = Control<Button>(dialog, "applyButton");
                Assert.IsFalse(apply.Enabled, "Nothing has been edited yet");

                Control<TrackBar>(dialog, "transparencySlider").Value = 70;

                Assert.IsTrue(apply.Enabled, "An edit should enable Apply");
            }
        }

        [TestMethod]
        public void Apply_RaisesApplyRequested()
        {
            using (var dialog = NewDialog(Settings()))
            {
                int raised = 0;
                dialog.ApplyRequested += (s, e) => raised++;

                Control<NumericUpDown>(dialog, "maxResults").Value = 11;
                Click(dialog, "applyButton");

                Assert.AreEqual(1, raised);
            }
        }

        [TestMethod]
        public void NotifyApplied_DisablesApplyUntilTheNextEdit()
        {
            using (var dialog = NewDialog(Settings()))
            {
                var apply = Control<Button>(dialog, "applyButton");

                Control<NumericUpDown>(dialog, "maxResults").Value = 11;
                Assert.IsTrue(apply.Enabled);

                dialog.NotifyApplied();
                Assert.IsFalse(apply.Enabled, "Applied changes are no longer pending");

                Control<NumericUpDown>(dialog, "maxResults").Value = 12;
                Assert.IsTrue(apply.Enabled, "A further edit re-enables Apply");
            }
        }

        [TestMethod]
        public void Apply_StaysDisabledWhileTheShortcutIsIncomplete()
        {
            using (var dialog = NewDialog(Settings()))
            {
                Control<CheckBox>(dialog, "modCtrl").Checked = false;
                Control<CheckBox>(dialog, "modAlt").Checked = false;
                Control<CheckBox>(dialog, "modShift").Checked = false;
                Control<CheckBox>(dialog, "modWin").Checked = false;

                Assert.IsNull(dialog.SelectedHotKey);
                Assert.IsFalse(Control<Button>(dialog, "applyButton").Enabled,
                    "Applying an unusable shortcut would drop the working one");
            }
        }

        [TestMethod]
        public void Apply_IsAllowedWhileClipBarIsDisabled()
        {
            // With ClipBar off the shortcut is irrelevant, so turning it off must
            // still be appliable.
            using (var dialog = NewDialog(Settings()))
            {
                Control<CheckBox>(dialog, "clipBarEnabled").Checked = false;

                Assert.IsTrue(Control<Button>(dialog, "applyButton").Enabled);
            }
        }

        [TestMethod]
        public void Backdrop_OffersOnlyTheThreeSupportedModes()
        {
            // Blur and Acrylic were withdrawn; offering them again would put the
            // accent-policy rendering bugs back in front of the user.
            using (var dialog = NewDialog(Settings()))
            {
                var combo = Control<ComboBox>(dialog, "backdropCombo");
                var offered = new List<BackdropMode>();
                var target = new AppSettings();

                for (int index = 0; index < combo.Items.Count; index++)
                {
                    combo.SelectedIndex = index;
                    dialog.ApplyTo(target);
                    offered.Add(target.Backdrop);
                }

                CollectionAssert.AreEquivalent(
                    new[] { BackdropMode.None, BackdropMode.Translucent, BackdropMode.SystemAcrylic },
                    offered);
            }
        }

        [TestMethod]
        public void ApplyButton_DoesNotCloseTheDialog()
        {
            using (var dialog = NewDialog(Settings()))
            {
                Assert.AreEqual(DialogResult.None,
                    Control<Button>(dialog, "applyButton").DialogResult,
                    "Apply must leave the dialog open");
            }
        }

        private static void Click(Form form, string name)
        {
            var button = form.Controls.Find(name, true)[0];

            // PerformClick is a no-op while the form has never been shown.
            typeof(Control)
                .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(button, new object[] { System.EventArgs.Empty });
        }

        private static void SelectKey(ClipBarSettingsDialog dialog, Keys key)
        {
            var combo = Control<ComboBox>(dialog, "hotkeyKey");
            for (int index = 0; index < combo.Items.Count; index++)
            {
                if (combo.Items[index].ToString() == HotKeyDefinition.Describe(key))
                {
                    combo.SelectedIndex = index;
                    return;
                }
            }
            Assert.Fail("Key not offered by the dialog: " + key);
        }
    }

    /// <summary>
    /// Layout invariants for the settings rows. The measurements are relative - a
    /// caption against the control beside it - so they hold at the test host's fixed
    /// 96 DPI even though absolute sizes there mean nothing.
    /// </summary>
    [TestClass]
    public class ClipBarSettingsDialogLayoutTests
    {
        private static ClipBarSettingsDialog LaidOut()
        {
            var dialog = new ClipBarSettingsDialog(new AppSettings(), definition => true);

            // Realises the layout without putting the window on screen.
            var forceHandleCreation = dialog.Handle;
            dialog.PerformLayout();
            return dialog;
        }

        private static TableLayoutPanel RowPanel(Control root)
        {
            return (TableLayoutPanel)root.Controls.Find("backdropCombo", true)[0].Parent;
        }

        [TestMethod]
        public void EveryCaption_IsCentredOnTheControlItNames()
        {
            using (var dialog = LaidOut())
            {
                var layout = RowPanel(dialog);
                var captions = new Dictionary<int, Control>();
                var controls = new Dictionary<int, Control>();

                foreach (Control child in layout.Controls)
                {
                    var cell = layout.GetPositionFromControl(child);
                    if (cell.Column == 0 && !string.IsNullOrEmpty(child.Text)) captions[cell.Row] = child;
                    else if (cell.Column == 1) controls[cell.Row] = child;
                }

                int compared = 0;
                foreach (var caption in captions)
                {
                    Control control;
                    if (!controls.TryGetValue(caption.Key, out control)) continue;

                    Assert.IsTrue(control.Height > 0 && caption.Value.Height > 0,
                        "Nothing was measured, so this test would pass vacuously");

                    int drift = (caption.Value.Top + caption.Value.Height / 2)
                        - (control.Top + control.Height / 2);
                    Assert.IsTrue(Math.Abs(drift) <= 3,
                        "'" + caption.Value.Text + "' sits " + drift
                        + "px off the vertical centre of the control it names");
                    compared++;
                }

                Assert.AreEqual(5, compared,
                    "Expected a caption for Shortcut, Backdrop, Transparency, Results shown and Theme");
            }
        }

        [TestMethod]
        public void Rows_AreNotStretchedBeyondTheirContent()
        {
            // A hard-coded ClientSize left roughly 90px of dead space under the last
            // row, and a different amount of it at every DPI.
            using (var dialog = LaidOut())
            {
                var layout = RowPanel(dialog);
                int slack = layout.Height - layout.PreferredSize.Height;

                Assert.IsTrue(slack <= 4,
                    "The settings rows are stretched " + slack + "px past their content");
                Assert.IsTrue(dialog.AutoSize && dialog.AutoSizeMode == AutoSizeMode.GrowAndShrink,
                    "The dialog must take its size from its contents");
            }
        }
    }
}
