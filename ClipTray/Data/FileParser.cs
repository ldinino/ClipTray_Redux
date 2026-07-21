using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClipTray.Models;

namespace ClipTray.Data
{
    public static class FileParser
    {
        private const string TitlePrefix = "Title:";
        private const string RtfPrefix = "Rtf:";
        private const string EndMarker = "End:";

        public static List<ClipEntry> Parse(string filePath)
        {
            var entries = new List<ClipEntry>();
            if (!File.Exists(filePath))
                return entries;

            var lines = ReadAllLines(filePath);
            return ParseLines(lines);
        }

        private static string[] ReadAllLines(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            string text = DecodeFile(bytes);
            var lines = new List<string>();
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    lines.Add(line);
            }
            return lines.ToArray();
        }

        private static string DecodeFile(byte[] bytes)
        {
            if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
                return new UTF32Encoding(true, true).GetString(bytes, 4, bytes.Length - 4);
            if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
                return new UTF32Encoding(false, true).GetString(bytes, 4, bytes.Length - 4);
            if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
                return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            if (HasPrefix(bytes, 0xFE, 0xFF))
                return new UnicodeEncoding(true, true).GetString(bytes, 2, bytes.Length - 2);
            if (HasPrefix(bytes, 0xFF, 0xFE))
                return new UnicodeEncoding(false, true).GetString(bytes, 2, bytes.Length - 2);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1252).GetString(bytes);
            }
        }

        private static bool HasPrefix(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;
            for (int index = 0; index < prefix.Length; index++)
            {
                if (bytes[index] != prefix[index]) return false;
            }
            return true;
        }

        public static List<ClipEntry> ParseLines(string[] lines)
        {
            var entries = new List<ClipEntry>();
            string currentTitle = null;
            var bodyLines = new List<string>();
            var rtfLines = new List<string>();
            bool inBody = false;

            foreach (var line in lines)
            {
                if (line.StartsWith(TitlePrefix))
                {
                    // Title: while InBody → discard incomplete entry, start new one
                    currentTitle = line.Substring(TitlePrefix.Length);
                    bodyLines.Clear();
                    rtfLines.Clear();
                    inBody = true;
                }
                else if (line == EndMarker)
                {
                    if (inBody && currentTitle != null)
                    {
                        // Strip trailing empty line from body if present
                        while (bodyLines.Count > 0 && bodyLines[bodyLines.Count - 1] == "")
                            bodyLines.RemoveAt(bodyLines.Count - 1);

                        entries.Add(new ClipEntry
                        {
                            Title = currentTitle,
                            Text = string.Join("\r\n", bodyLines),
                            Rtf = rtfLines.Count > 0 ? string.Join("\r\n", rtfLines) : null
                        });
                    }
                    // Reset state (handles preamble End: as no-op too)
                    currentTitle = null;
                    bodyLines.Clear();
                    rtfLines.Clear();
                    inBody = false;
                }
                else if (inBody && line.StartsWith(RtfPrefix))
                {
                    rtfLines.Add(line.Substring(RtfPrefix.Length));
                }
                else if (inBody)
                {
                    bodyLines.Add(line);
                }
            }

            // EOF while InBody → discard incomplete entry (no action needed)
            return entries;
        }

        public static void CreateDefaultFile(string filePath)
        {
            File.WriteAllText(filePath, "End:\r\n\r\n");
        }
    }
}
