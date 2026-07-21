using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;
using ClipTray.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class RichTextHelpersTests
    {
        [TestMethod]
        public void TitledHyperlink_RoundTrip_PreservesVisibleTitleAndUrl()
        {
            using (var textBox = new RichTextBox())
            {
                string rtf = RichTextHelpers.BuildHyperlinkRtf(
                    "https://example.com/docs",
                    "Example docs");
                textBox.SelectedRtf = rtf;

                Assert.AreEqual("Example docs", RichTextHelpers.GetVisibleText(textBox));
                StringAssert.Contains(textBox.Rtf, "https://example.com/docs");
                Assert.AreEqual("Example docs", RichTextHelpers.GetVisibleText(rtf, "broken fallback"));

                RichTextHelpers.ConvertToPlain(textBox);

                Assert.AreEqual("Example docs", textBox.Text);
                Assert.IsNull(RichTextHelpers.DetectRichness(textBox));
            }
        }

        [TestMethod]
        public void TryInsertRtf_MalformedRtf_ReturnsFalseWithoutChangingText()
        {
            using (var textBox = new RichTextBox { Text = "Existing" })
            {
                textBox.SelectionStart = textBox.TextLength;

                Assert.IsFalse(RichTextHelpers.TryInsertRtf(textBox, "not rtf"));
                Assert.AreEqual("Existing", textBox.Text);
            }
        }

        [TestMethod]
        public void InsertHyperlink_ReplacesSelectionWithDisplayTitle()
        {
            using (var textBox = new RichTextBox { Text = "Before https://example.com after" })
            {
                int start = textBox.Text.IndexOf("https://", StringComparison.Ordinal);
                textBox.Select(start, "https://example.com".Length);

                Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                    textBox,
                    "https://example.com/docs",
                    "Friendly title"));

                Assert.AreEqual("Before Friendly title after", RichTextHelpers.GetVisibleText(textBox));
                StringAssert.Contains(textBox.Rtf, "https://example.com/docs");
                Assert.IsTrue(RichTextHelpers.TryGetHyperlinkUrl(textBox.Rtf, out var url));
                Assert.AreEqual("https://example.com/docs", url);
            }
        }

        [TestMethod]
        public void GetVisibleSelectedText_ExcludesUnselectedText()
        {
            using (var textBox = new RichTextBox { Text = "Before selected after" })
            {
                textBox.Select(7, 8);

                Assert.AreEqual("selected", RichTextHelpers.GetVisibleSelectedText(textBox));
            }
        }

        [TestMethod]
        public void TryGetHyperlinkUrl_UnicodeAddress_RoundTripsRtfEscapes()
        {
            const string expected = "https://example.com/café/😀";
            string rtf = RichTextHelpers.BuildHyperlinkRtf(expected, "Unicode link");

            Assert.IsTrue(RichTextHelpers.TryGetHyperlinkUrl(rtf, out var url));
            Assert.AreEqual(expected, url);
        }

        [TestMethod]
        public void TryGetSingleHyperlinkFromHtml_ChromiumAnchor_PreservesTitleAndAddress()
        {
            const string html =
                "Version:1.0\r\n" +
                "StartHTML:0000000105\r\n" +
                "<!--StartFragment-->" +
                "<a href=\"https://example.com/docs?a=1&amp;b=2\">Friendly &amp; useful title</a>" +
                "<!--EndFragment-->";

            Assert.IsTrue(RichTextHelpers.TryGetSingleHyperlinkFromHtml(
                html,
                out var url,
                out var displayText));
            Assert.AreEqual("https://example.com/docs?a=1&b=2", url);
            Assert.AreEqual("Friendly & useful title", displayText);
        }

        [TestMethod]
        public void TryGetSingleHyperlinkFromHtml_MixedContent_ReturnsFalse()
        {
            const string html =
                "<!--StartFragment-->" +
                "Read <a href=\"https://example.com/docs\">the docs</a> first" +
                "<!--EndFragment-->";

            Assert.IsFalse(RichTextHelpers.TryGetSingleHyperlinkFromHtml(
                html,
                out _,
                out _));
        }

        [TestMethod]
        public void TitledHyperlink_FileRoundTrip_PreservesTitleAndDestination()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (var source = new RichTextBox())
                {
                    Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                        source,
                        "https://example.com/docs",
                        "Friendly title"));

                    FileWriter.Write(path, new List<ClipEntry>
                    {
                        new ClipEntry
                        {
                            Title = "Link test",
                            Text = RichTextHelpers.GetVisibleText(source),
                            Rtf = RichTextHelpers.DetectRichness(source)
                        }
                    });
                }

                var parsed = FileParser.Parse(path);
                Assert.AreEqual(1, parsed.Count);
                Assert.AreEqual("Friendly title", parsed[0].Text);
                StringAssert.Contains(parsed[0].Rtf, "https://example.com/docs");

                using (var reloaded = new RichTextBox { Rtf = parsed[0].Rtf })
                {
                    Assert.AreEqual("Friendly title", RichTextHelpers.GetVisibleText(reloaded));
                    Assert.IsTrue(RichTextHelpers.TryGetHyperlinkUrl(reloaded.Rtf, out var url));
                    Assert.AreEqual("https://example.com/docs", url);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void CreateClipboardData_TitledLink_PublishesValidHtmlFormat()
        {
            using (var textBox = new RichTextBox())
            {
                Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                    textBox,
                    "https://example.com/book?a=1&b=2",
                    "Book time with Lucíanó"));

                var data = RichTextHelpers.CreateClipboardData(
                    RichTextHelpers.GetVisibleText(textBox),
                    textBox.Rtf);

                Assert.IsTrue(data.GetDataPresent(DataFormats.Rtf, false));
                Assert.IsTrue(data.GetDataPresent(DataFormats.Html, false));
                Assert.IsTrue(data.GetDataPresent(DataFormats.UnicodeText, false));

                string html = (string)data.GetData(DataFormats.Html, false);
                StringAssert.Contains(html, "<a href=\"https://example.com/book?a=1&amp;b=2\">");
                StringAssert.Contains(
                    System.Net.WebUtility.HtmlDecode(html),
                    "Book time with Lucíanó</a>");
                AssertHtmlClipboardOffsets(html);
            }
        }

        [TestMethod]
        public void CreateClipboardData_LinkWithinText_PreservesSurroundingText()
        {
            using (var textBox = new RichTextBox { Text = "Before link after" })
            {
                textBox.Select(7, 4);
                Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                    textBox,
                    "https://example.com",
                    "friendly link"));

                var data = RichTextHelpers.CreateClipboardData(
                    RichTextHelpers.GetVisibleText(textBox),
                    textBox.Rtf);
                string html = (string)data.GetData(DataFormats.Html, false);

                StringAssert.Contains(
                    html,
                    "Before <a href=\"https://example.com\">friendly link</a> after");
            }
        }

        [TestMethod]
        public void CreateClipboardData_TwoLinksWithSameTitle_PreservesOrderAndDestinations()
        {
            using (var textBox = new RichTextBox { Text = "first and second" })
            {
                textBox.Select(10, 6);
                Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                    textBox,
                    "https://example.com/second",
                    "open"));
                textBox.Select(0, 5);
                Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                    textBox,
                    "https://example.com/first",
                    "open"));

                var data = RichTextHelpers.CreateClipboardData(
                    RichTextHelpers.GetVisibleText(textBox),
                    textBox.Rtf);
                string html = (string)data.GetData(DataFormats.Html, false);

                StringAssert.Contains(
                    html,
                    "<a href=\"https://example.com/first\">open</a> and " +
                    "<a href=\"https://example.com/second\">open</a>");
            }
        }

        [TestMethod]
        public void CreateClipboardData_UnsafeLink_OmitsHtmlFormat()
        {
            using (var textBox = new RichTextBox())
            {
                Assert.IsTrue(RichTextHelpers.InsertHyperlink(
                    textBox,
                    "javascript:alert(1)",
                    "unsafe"));

                var data = RichTextHelpers.CreateClipboardData(
                    RichTextHelpers.GetVisibleText(textBox),
                    textBox.Rtf);

                Assert.IsFalse(data.GetDataPresent(DataFormats.Html, false));
                Assert.IsTrue(data.GetDataPresent(DataFormats.Rtf, false));
                Assert.IsTrue(data.GetDataPresent(DataFormats.UnicodeText, false));
            }
        }

        private static void AssertHtmlClipboardOffsets(string html)
        {
            int startHtml = ReadOffset(html, "StartHTML:");
            int endHtml = ReadOffset(html, "EndHTML:");
            int startFragment = ReadOffset(html, "StartFragment:");
            int endFragment = ReadOffset(html, "EndFragment:");
            byte[] bytes = Encoding.UTF8.GetBytes(html);

            Assert.AreEqual(bytes.Length, endHtml);
            Assert.AreEqual("<html>", Encoding.UTF8.GetString(bytes, startHtml, 6));
            string fragment = Encoding.UTF8.GetString(
                bytes,
                startFragment,
                endFragment - startFragment);
            Assert.IsFalse(string.IsNullOrEmpty(fragment));
            Assert.AreEqual(
                "<!--EndFragment-->",
                Encoding.UTF8.GetString(bytes, endFragment, "<!--EndFragment-->".Length));
        }

        private static int ReadOffset(string html, string name)
        {
            int start = html.IndexOf(name, StringComparison.Ordinal) + name.Length;
            return int.Parse(html.Substring(start, 10));
        }
    }
}