using System.Text.RegularExpressions;
using ClipTray.Data;
using ClipTray.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class ClipboardWriterTests
    {
        [TestMethod]
        public void Resolve_NullEntry_IsEmpty()
        {
            Assert.IsTrue(ClipboardWriter.Resolve(null).IsEmpty);
        }

        [TestMethod]
        public void Resolve_EmptyText_IsEmpty()
        {
            var entry = new ClipEntry { Title = "Blank", Text = "" };

            Assert.IsTrue(ClipboardWriter.Resolve(entry).IsEmpty);
        }

        [TestMethod]
        public void Resolve_PlainEntry_SubstitutesTokensAndLeavesRtfNull()
        {
            var entry = new ClipEntry { Title = "Stamp", Text = "Logged {date:yyyy-MM-dd}" };

            var payload = ClipboardWriter.Resolve(entry);

            Assert.IsFalse(payload.IsEmpty);
            Assert.IsNull(payload.Rtf, "A plain entry must stay plain");
            Assert.IsTrue(Regex.IsMatch(payload.Text, @"^Logged \d{4}-\d{2}-\d{2}$"),
                "Token should be resolved. Actual: " + payload.Text);
        }

        [TestMethod]
        public void Resolve_PlainEntryWithoutTokens_PassesTextThrough()
        {
            var entry = new ClipEntry { Title = "Greeting", Text = "Hello there" };

            Assert.AreEqual("Hello there", ClipboardWriter.Resolve(entry).Text);
        }

        [TestMethod]
        public void Resolve_RichEntry_ReturnsBothRepresentations()
        {
            var entry = new ClipEntry
            {
                Title = "Rich",
                Text = "Logged {date:yyyy}",
                Rtf = @"{\rtf1\ansi Logged \{date:yyyy\}\par}"
            };

            var payload = ClipboardWriter.Resolve(entry);

            Assert.IsFalse(payload.IsEmpty);
            Assert.IsNotNull(payload.Rtf, "A rich entry must carry RTF");
            Assert.IsFalse(payload.Rtf.Contains(@"\{date:yyyy\}"),
                "Tokens inside RTF should be resolved too");
            Assert.IsTrue(Regex.IsMatch(payload.Text, @"\d{4}"),
                "Plain fallback should also be resolved. Actual: " + payload.Text);
        }
    }
}
