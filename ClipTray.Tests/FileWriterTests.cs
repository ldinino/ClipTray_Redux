using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClipTray.Data;
using ClipTray.Models;

namespace ClipTray.Tests
{
    [TestClass]
    public class FileWriterTests
    {
        [TestMethod]
        public void Write_EmptyList_WritesPreambleOnly()
        {
            var path = Path.GetTempFileName();
            try
            {
                FileWriter.Write(path, new List<ClipEntry>());
                var content = File.ReadAllText(path);
                Assert.AreEqual("End:\r\n\r\n", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Write_SingleEntry_WritesCorrectFormat()
        {
            var path = Path.GetTempFileName();
            try
            {
                var entries = new List<ClipEntry>
                {
                    new ClipEntry { Title = "Test", Text = "Hello" }
                };
                FileWriter.Write(path, entries);
                var content = File.ReadAllText(path);
                Assert.AreEqual("End:\r\n\r\nTitle:Test\r\nHello\r\nEnd:\r\n\r\n", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Write_MultipleEntries_WritesAllEntries()
        {
            var path = Path.GetTempFileName();
            try
            {
                var entries = new List<ClipEntry>
                {
                    new ClipEntry { Title = "First", Text = "Body one" },
                    new ClipEntry { Title = "Second", Text = "Body two" }
                };
                FileWriter.Write(path, entries);
                var content = File.ReadAllText(path);
                var expected = "End:\r\n\r\nTitle:First\r\nBody one\r\nEnd:\r\n\r\nTitle:Second\r\nBody two\r\nEnd:\r\n\r\n";
                Assert.AreEqual(expected, content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Write_MultilineBody_PreservesNewlines()
        {
            var path = Path.GetTempFileName();
            try
            {
                var entries = new List<ClipEntry>
                {
                    new ClipEntry { Title = "Multi", Text = "Line 1\r\nLine 2\r\nLine 3" }
                };
                FileWriter.Write(path, entries);
                var content = File.ReadAllText(path);
                var expected = "End:\r\n\r\nTitle:Multi\r\nLine 1\r\nLine 2\r\nLine 3\r\nEnd:\r\n\r\n";
                Assert.AreEqual(expected, content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Write_EmptyBody_WritesEntryWithoutBodyLine()
        {
            var path = Path.GetTempFileName();
            try
            {
                var entries = new List<ClipEntry>
                {
                    new ClipEntry { Title = "Empty", Text = "" }
                };
                FileWriter.Write(path, entries);
                var content = File.ReadAllText(path);
                Assert.AreEqual("End:\r\n\r\nTitle:Empty\r\nEnd:\r\n\r\n", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Write_NullBody_WritesEntryWithoutBodyLine()
        {
            var path = Path.GetTempFileName();
            try
            {
                var entries = new List<ClipEntry>
                {
                    new ClipEntry { Title = "NullText", Text = null }
                };
                FileWriter.Write(path, entries);
                var content = File.ReadAllText(path);
                Assert.AreEqual("End:\r\n\r\nTitle:NullText\r\nEnd:\r\n\r\n", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void RoundTrip_WriteAndParse_PreservesEntries()
        {
            var path = Path.GetTempFileName();
            try
            {
                var original = new List<ClipEntry>
                {
                    new ClipEntry { Title = "Greeting", Text = "Hello!" },
                    new ClipEntry { Title = "Email", Text = "Hi there,\r\n\r\nThank you for contacting us." },
                    new ClipEntry { Title = "Empty", Text = "" }
                };
                FileWriter.Write(path, original);
                var parsed = FileParser.Parse(path);

                Assert.AreEqual(original.Count, parsed.Count);
                for (int i = 0; i < original.Count; i++)
                {
                    Assert.AreEqual(original[i].Title, parsed[i].Title, "Title mismatch at index " + i);
                    Assert.AreEqual(original[i].Text, parsed[i].Text, "Text mismatch at index " + i);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
