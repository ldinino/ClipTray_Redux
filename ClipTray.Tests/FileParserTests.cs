using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClipTray.Data;

namespace ClipTray.Tests
{
    [TestClass]
    public class FileParserTests
    {
        [TestMethod]
        public void Parse_EmptyFile_ReturnsEmptyList()
        {
            var entries = FileParser.ParseLines(new string[0]);
            Assert.AreEqual(0, entries.Count);
        }

        [TestMethod]
        public void Parse_PreambleOnly_ReturnsEmptyList()
        {
            var lines = new[] { "End:", "" };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(0, entries.Count);
        }

        [TestMethod]
        public void Parse_SingleEntry_ReturnsSingleEntry()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Greeting",
                "Hello, world!",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Greeting", entries[0].Title);
            Assert.AreEqual("Hello, world!", entries[0].Text);
        }

        [TestMethod]
        public void Parse_MultipleEntries_ReturnsAll()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:First",
                "Body one",
                "End:",
                "",
                "Title:Second",
                "Body two",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("First", entries[0].Title);
            Assert.AreEqual("Body one", entries[0].Text);
            Assert.AreEqual("Second", entries[1].Title);
            Assert.AreEqual("Body two", entries[1].Text);
        }

        [TestMethod]
        public void Parse_MultilineBody_PreservesAllLines()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Email",
                "Hi there,",
                "",
                "Thank you for contacting us.",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Email", entries[0].Title);
            Assert.AreEqual("Hi there,\r\n\r\nThank you for contacting us.", entries[0].Text);
        }

        [TestMethod]
        public void Parse_MissingEnd_DiscardsIncompleteEntry()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Complete",
                "Body",
                "End:",
                "",
                "Title:Incomplete",
                "No end marker"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Complete", entries[0].Title);
        }

        [TestMethod]
        public void Parse_MissingEndFollowedByTitle_DiscardsFirst()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Broken",
                "No end here",
                "Title:Good",
                "Valid body",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Good", entries[0].Title);
            Assert.AreEqual("Valid body", entries[0].Text);
        }

        [TestMethod]
        public void Parse_EmptyBody_ReturnsEmptyText()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:EmptyBody",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("EmptyBody", entries[0].Title);
            Assert.AreEqual("", entries[0].Text);
        }

        [TestMethod]
        public void Parse_GarbageInput_ReturnsEmptyList()
        {
            var lines = new[]
            {
                "random text",
                "more garbage",
                "nothing useful"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(0, entries.Count);
        }

        [TestMethod]
        public void Parse_EndOutsideEntry_IsNoOp()
        {
            var lines = new[]
            {
                "End:",
                "End:",
                "",
                "Title:After",
                "Text",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("After", entries[0].Title);
        }

        [TestMethod]
        public void Parse_FromFile_ReadsCorrectly()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "End:\r\n\r\nTitle:FileTest\r\nFile body\r\nEnd:\r\n");
                var entries = FileParser.Parse(path);
                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("FileTest", entries[0].Title);
                Assert.AreEqual("File body", entries[0].Text);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Parse_Windows1252LegacyFile_DecodesNonBreakingSpaces()
        {
            var path = Path.GetTempFileName();
            try
            {
                const string content =
                    "End:\r\n\r\n" +
                    "Title:Legacy\r\n" +
                    "Before\u00a0after • Customer’s\r\n" +
                    "\u00a0\r\n" +
                    "End:\r\n";
                File.WriteAllBytes(path, Encoding.GetEncoding(1252).GetBytes(content));

                var entries = FileParser.Parse(path);

                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("Before\u00a0after • Customer’s\r\n\u00a0", entries[0].Text);
                Assert.IsFalse(entries[0].Text.Contains("\ufffd"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Parse_Utf8WithoutBom_PreservesUnicode()
        {
            var path = Path.GetTempFileName();
            try
            {
                const string content =
                    "End:\r\n\r\n" +
                    "Title:Modern\r\n" +
                    "Café 😀\r\n" +
                    "End:\r\n";
                File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(content));

                var entries = FileParser.Parse(path);

                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("Café 😀", entries[0].Text);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Parse_Utf8WithBom_PreservesUnicode()
        {
            var path = Path.GetTempFileName();
            try
            {
                const string content =
                    "End:\r\n\r\n" +
                    "Title:Unicode\r\n" +
                    "Résumé 日本語\r\n" +
                    "End:\r\n";
                File.WriteAllText(path, content, new UTF8Encoding(true));

                var entries = FileParser.Parse(path);

                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("Résumé 日本語", entries[0].Text);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_Utf16WithBom_PreservesUnicode(bool bigEndian)
        {
            var path = Path.GetTempFileName();
            try
            {
                const string content =
                    "End:\r\n\r\n" +
                    "Title:Unicode\r\n" +
                    "Résumé 日本語\r\n" +
                    "End:\r\n";
                File.WriteAllText(path, content, new UnicodeEncoding(bigEndian, true));

                var entries = FileParser.Parse(path);

                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("Résumé 日本語", entries[0].Text);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Parse_Utf32WithBom_PreservesUnicode(bool bigEndian)
        {
            var path = Path.GetTempFileName();
            try
            {
                const string content =
                    "End:\r\n\r\n" +
                    "Title:Unicode\r\n" +
                    "Résumé 日本語 😀\r\n" +
                    "End:\r\n";
                File.WriteAllText(path, content, new UTF32Encoding(bigEndian, true));

                var entries = FileParser.Parse(path);

                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("Résumé 日本語 😀", entries[0].Text);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Parse_NonexistentFile_ReturnsEmptyList()
        {
            var entries = FileParser.Parse(@"C:\nonexistent_path_12345\file.txt");
            Assert.AreEqual(0, entries.Count);
        }

        [TestMethod]
        public void CreateDefaultFile_CreatesFileWithPreamble()
        {
            var path = Path.GetTempFileName();
            try
            {
                FileParser.CreateDefaultFile(path);
                var content = File.ReadAllText(path);
                Assert.AreEqual("End:\r\n\r\n", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Parse_BodyWithTrailingBlankLines_StripsTrailingBlanks()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Trailing",
                "Line one",
                "",
                "",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Line one", entries[0].Text);
        }

        [TestMethod]
        public void Parse_EntryWithRtfLines_PopulatesRtfField()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Rich",
                "Plain fallback",
                @"Rtf:{\rtf1\ansi",
                @"Rtf:{\fonttbl{\f0 Calibri;}}",
                @"Rtf:\b Hello\b0 \par}",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Rich", entries[0].Title);
            Assert.AreEqual("Plain fallback", entries[0].Text);
            Assert.AreEqual(
                "{\\rtf1\\ansi\r\n{\\fonttbl{\\f0 Calibri;}}\r\n\\b Hello\\b0 \\par}",
                entries[0].Rtf);
        }

        [TestMethod]
        public void Parse_OldFormatNoRtf_RtfIsNull()
        {
            var lines = new[]
            {
                "End:",
                "",
                "Title:Plain",
                "Just text",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.IsNull(entries[0].Rtf);
        }

        [TestMethod]
        public void Parse_RtfLineContainsEndSubstring_DoesNotTerminateEarly()
        {
            // An Rtf:-prefixed line containing the substring "End:" must not
            // terminate the entry — only a standalone "End:" line does.
            var lines = new[]
            {
                "End:",
                "",
                "Title:Tricky",
                "Body",
                @"Rtf:{\rtf1 contains End: substring inside\par}",
                "End:"
            };
            var entries = FileParser.ParseLines(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Tricky", entries[0].Title);
            Assert.AreEqual(@"{\rtf1 contains End: substring inside\par}", entries[0].Rtf);
        }
    }
}
