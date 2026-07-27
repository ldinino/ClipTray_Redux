using System.Collections.Generic;
using ClipTray.ClipBar;
using ClipTray.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class InsertSearchTests
    {
        private static List<ClipEntry> SampleEntries()
        {
            return new List<ClipEntry>
            {
                Entry("Meeting follow-up", "Thanks for your time today."),
                Entry("Support signature", "Best regards, Luis"),
                Entry("Today's date stamp", "{date:yyyy-MM-dd}"),
                Entry("Escalation template", "This ticket has been escalated to Tier 2."),
                Entry("Password reset steps", "Go to the sign-in page and choose Forgot password."),
                Entry("VPN troubleshooting", "Flush DNS with ipconfig /flushdns.")
            };
        }

        private static ClipEntry Entry(string title, string text)
        {
            return new ClipEntry { Title = title, Text = text };
        }

        [TestMethod]
        public void Rank_EmptyQuery_ReturnsFirstEntriesInFileOrder()
        {
            var results = InsertSearch.Rank(SampleEntries(), "", 3);

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("Meeting follow-up", results[0].Title);
            Assert.AreEqual("Support signature", results[1].Title);
            Assert.AreEqual("Today's date stamp", results[2].Title);
        }

        [TestMethod]
        public void Rank_WhitespaceQuery_TreatedAsEmpty()
        {
            var results = InsertSearch.Rank(SampleEntries(), "   ", 2);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("Meeting follow-up", results[0].Title);
        }

        [TestMethod]
        public void Rank_HonoursLimit()
        {
            Assert.AreEqual(2, InsertSearch.Rank(SampleEntries(), "s", 2).Count);
            Assert.AreEqual(0, InsertSearch.Rank(SampleEntries(), "s", 0).Count);
        }

        [TestMethod]
        public void Rank_NullEntries_ReturnsEmpty()
        {
            Assert.AreEqual(0, InsertSearch.Rank(null, "anything", 5).Count);
        }

        [TestMethod]
        public void Rank_NoMatch_ReturnsEmpty()
        {
            Assert.AreEqual(0, InsertSearch.Rank(SampleEntries(), "zzzzqqq", 5).Count);
        }

        [TestMethod]
        public void Rank_IsCaseInsensitive()
        {
            var lower = InsertSearch.Rank(SampleEntries(), "meeting", 5);
            var upper = InsertSearch.Rank(SampleEntries(), "MEETING", 5);

            Assert.AreEqual(lower[0].Title, upper[0].Title);
            Assert.AreEqual("Meeting follow-up", upper[0].Title);
        }

        [TestMethod]
        public void Rank_PrefersTitlePrefixOverWordStart()
        {
            var entries = new List<ClipEntry>
            {
                Entry("Support signature", "body"),   // word-start match on "sig"
                Entry("Signature block", "body")      // title prefix match on "sig"
            };

            var results = InsertSearch.Rank(entries, "sig", 5);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("Signature block", results[0].Title, "Prefix should outrank word-start");
        }

        [TestMethod]
        public void Rank_PrefersExactTitleOverPrefix()
        {
            var entries = new List<ClipEntry>
            {
                Entry("VPN troubleshooting", "body"),
                Entry("VPN", "body")
            };

            var results = InsertSearch.Rank(entries, "vpn", 5);

            Assert.AreEqual("VPN", results[0].Title, "Exact title should rank first");
        }

        [TestMethod]
        public void Rank_WordStartMatchesMidTitleWord()
        {
            // "sig" should find the "signature" inside "Support signature".
            var results = InsertSearch.Rank(SampleEntries(), "sig", 5);

            Assert.IsTrue(results.Count > 0);
            Assert.AreEqual("Support signature", results[0].Title);
        }

        [TestMethod]
        public void Rank_MatchesSubsequenceInTitle()
        {
            // "mfu" is not a substring of "Meeting follow-up" but is a subsequence.
            var results = InsertSearch.Rank(SampleEntries(), "mfu", 5);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Meeting follow-up", results[0].Title);
        }

        [TestMethod]
        public void Rank_TitleMatchesOutrankBodyMatches()
        {
            var entries = new List<ClipEntry>
            {
                Entry("Unrelated title", "this body mentions escalated once"),
                Entry("Escalated ticket", "body text")
            };

            var results = InsertSearch.Rank(entries, "escalated", 5);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("Escalated ticket", results[0].Title, "Title match should beat body match");
        }

        [TestMethod]
        public void Rank_FindsBodyOnlyMatches()
        {
            var results = InsertSearch.Rank(SampleEntries(), "flushdns", 5);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("VPN troubleshooting", results[0].Title);
        }

        [TestMethod]
        public void Rank_EqualScores_KeepOriginalOrder()
        {
            var entries = new List<ClipEntry>
            {
                Entry("Alpha report", "body"),
                Entry("Alpha summary", "body"),
                Entry("Alpha notes", "body")
            };

            var results = InsertSearch.Rank(entries, "alpha", 5);

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("Alpha report", results[0].Title);
            Assert.AreEqual("Alpha summary", results[1].Title);
            Assert.AreEqual("Alpha notes", results[2].Title);
        }

        [TestMethod]
        public void Rank_ToleratesNullTitlesAndBodies()
        {
            var entries = new List<ClipEntry>
            {
                new ClipEntry { Title = null, Text = null },
                Entry("Real entry", "body")
            };

            var results = InsertSearch.Rank(entries, "real", 5);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Real entry", results[0].Title);
        }
    }
}
