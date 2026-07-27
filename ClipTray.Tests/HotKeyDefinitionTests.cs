using System.Windows.Forms;
using ClipTray.ClipBar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class HotKeyDefinitionTests
    {
        [TestMethod]
        public void Default_IsCtrlAltSpace()
        {
            // Ctrl+Win+Space, Win+Space, Ctrl+Shift+Space and Shift+Win+Space are all
            // owned by the Windows text-input stack and cannot be registered.
            var definition = HotKeyDefinition.Default;

            Assert.IsTrue(definition.Control);
            Assert.IsTrue(definition.Alt);
            Assert.IsFalse(definition.Shift);
            Assert.IsFalse(definition.Windows);
            Assert.AreEqual(Keys.Space, definition.Key);
            Assert.AreEqual("Ctrl+Alt+Space", definition.ToString());
        }

        [TestMethod]
        public void TryParse_RoundTripsToString()
        {
            var inputs = new[]
            {
                "Ctrl+Alt+Space",
                "Ctrl+Shift+V",
                "Alt+Win+Space",
                "Ctrl+Alt+Shift+Win+F9",
                "Win+A",
                "Ctrl+Alt+1"
            };

            foreach (var input in inputs)
            {
                HotKeyDefinition parsed;
                Assert.IsTrue(HotKeyDefinition.TryParse(input, out parsed), "Should parse: " + input);
                Assert.AreEqual(input, parsed.ToString(), "Round trip failed for: " + input);
            }
        }

        [TestMethod]
        public void TryParse_IsCaseAndWhitespaceInsensitive()
        {
            HotKeyDefinition parsed;
            Assert.IsTrue(HotKeyDefinition.TryParse("  ctrl + ALT + space ", out parsed));
            Assert.AreEqual(HotKeyDefinition.Default, parsed);
        }

        [TestMethod]
        public void TryParse_AcceptsControlAndWindowsAliases()
        {
            HotKeyDefinition control, windows;
            Assert.IsTrue(HotKeyDefinition.TryParse("Control+Alt+Space", out control));
            Assert.IsTrue(HotKeyDefinition.TryParse("Windows+A", out windows));

            Assert.AreEqual(HotKeyDefinition.Default, control);
            Assert.IsTrue(windows.Windows);
        }

        [TestMethod]
        public void TryParse_DigitKeysMapToDigitNames()
        {
            HotKeyDefinition parsed;
            Assert.IsTrue(HotKeyDefinition.TryParse("Ctrl+Alt+7", out parsed));

            Assert.AreEqual(Keys.D7, parsed.Key);
            Assert.AreEqual("Ctrl+Alt+7", parsed.ToString());
        }

        [TestMethod]
        public void TryParse_RejectsInvalidInput()
        {
            var invalid = new[]
            {
                null,
                "",
                "   ",
                "Space",              // no modifier
                "Ctrl",               // no key
                "Ctrl+Alt",           // still no key
                "Ctrl+",              // empty segment
                "Ctrl+Alt+Space+F1",  // two non-modifier keys
                "Ctrl+Alt+NotAKey",   // unknown key name
                "Ctrl+Alt+ShiftKey",  // modifier used as the main key
                "Ctrl+Alt+LWin"       // modifier used as the main key
            };

            foreach (var input in invalid)
            {
                HotKeyDefinition parsed;
                Assert.IsFalse(HotKeyDefinition.TryParse(input, out parsed),
                    "Should have been rejected: " + (input ?? "<null>"));
                Assert.IsNull(parsed);
            }
        }

        [TestMethod]
        public void Modifiers_MapToWin32FlagsAndSuppressAutoRepeat()
        {
            HotKeyDefinition parsed;
            HotKeyDefinition.TryParse("Ctrl+Alt+Shift+Win+Space", out parsed);

            uint expected = HotKeyDefinition.ModControl
                | HotKeyDefinition.ModAlt
                | HotKeyDefinition.ModShift
                | HotKeyDefinition.ModWindows
                | HotKeyDefinition.ModNoRepeat;

            Assert.AreEqual(expected, parsed.Modifiers);
            Assert.AreEqual((uint)Keys.Space, parsed.VirtualKey);
        }

        [TestMethod]
        public void Modifiers_AlwaysIncludeNoRepeat()
        {
            // Without MOD_NOREPEAT, holding the combination machine-guns the window open.
            Assert.AreEqual(
                HotKeyDefinition.ModNoRepeat,
                HotKeyDefinition.Default.Modifiers & HotKeyDefinition.ModNoRepeat);
        }

        [TestMethod]
        public void IsValid_RequiresAtLeastOneModifier()
        {
            Assert.IsFalse(new HotKeyDefinition(false, false, false, false, Keys.Space).IsValid);
            Assert.IsTrue(new HotKeyDefinition(true, false, false, false, Keys.Space).IsValid);
        }

        [TestMethod]
        public void Equals_ComparesAllComponents()
        {
            var left = new HotKeyDefinition(true, true, false, false, Keys.Space);
            var same = new HotKeyDefinition(true, true, false, false, Keys.Space);
            var differentKey = new HotKeyDefinition(true, true, false, false, Keys.A);
            var differentModifier = new HotKeyDefinition(true, true, true, false, Keys.Space);

            Assert.AreEqual(left, same);
            Assert.AreEqual(left.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(left, differentKey);
            Assert.AreNotEqual(left, differentModifier);
        }
    }
}
