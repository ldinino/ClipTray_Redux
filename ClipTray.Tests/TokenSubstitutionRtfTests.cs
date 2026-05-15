using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClipTray.Tokens;

namespace ClipTray.Tests
{
    [TestClass]
    public class TokenSubstitutionRtfTests
    {
        [TestMethod]
        public void ResolveRtf_EscapedDateToken_Substituted()
        {
            // {date} typed in a RichTextBox is stored in RTF as \{date\}.
            var result = TokenSubstitution.ResolveRtf(@"prefix \{date\} suffix");
            // Expect the literal "\{date\}" to be replaced with today's date.
            Assert.IsFalse(result.Contains(@"\{date\}"), "RTF-escaped date token should be substituted");
            Assert.IsTrue(Regex.IsMatch(result, @"prefix \d{2}/\d{2}/\d{4} suffix"),
                "Result should contain date in MM/dd/yyyy format. Actual: " + result);
        }

        [TestMethod]
        public void ResolveRtf_EscapedDateTokenWithFormat_RespectsFormat()
        {
            var result = TokenSubstitution.ResolveRtf(@"\{date:yyyy-MM-dd\}");
            Assert.IsTrue(Regex.IsMatch(result, @"^\d{4}-\d{2}-\d{2}$"),
                "Expected yyyy-MM-dd format. Actual: " + result);
        }

        [TestMethod]
        public void ResolveRtf_PlainBracesNotEscaped_LeftAlone()
        {
            // A literal "{date}" (no backslashes) inside RTF would be an actual
            // RTF group opener — but if it somehow appears, our RTF regex must
            // ignore it (the plain-text regex handles it elsewhere).
            var input = @"\{rtf1 {date} not a token here\par}";
            var result = TokenSubstitution.ResolveRtf(input);
            Assert.AreEqual(input, result, "Unescaped braces should not match the RTF token regex");
        }

        [TestMethod]
        public void ResolveRtf_EscapedDoubleBrace_BecomesSingleBrace()
        {
            // {{ → { escape rule, in RTF: \{\{\{\{ → \{ ... let me think:
            // Plain "{{" stored as RTF "\{\{". The literalOpen+literalOpen check
            // looks for "\{\{" and emits "\{".
            var result = TokenSubstitution.ResolveRtf(@"\{\{");
            Assert.AreEqual(@"\{", result);
        }

        [TestMethod]
        public void ResolveRtf_UnknownToken_PassesThroughUnchanged()
        {
            var input = @"\{unknownthing\}";
            var result = TokenSubstitution.ResolveRtf(input);
            Assert.AreEqual(input, result);
        }

        [TestMethod]
        public void Resolve_PlainBehaviorUnchanged()
        {
            // Sanity check that the refactor didn't break plain-text path.
            var result = TokenSubstitution.Resolve("date is {date:yyyy-MM-dd}");
            Assert.IsTrue(Regex.IsMatch(result, @"^date is \d{4}-\d{2}-\d{2}$"),
                "Plain Resolve should still work. Actual: " + result);
        }

        [TestMethod]
        public void Resolve_EscapedBraces_RestoredAsLiteral()
        {
            Assert.AreEqual("{not a token}", TokenSubstitution.Resolve("{{not a token}}"));
        }
    }
}
