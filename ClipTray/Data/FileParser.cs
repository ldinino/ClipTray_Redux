using System.Collections.Generic;
using System.IO;
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

            var lines = File.ReadAllLines(filePath);
            return ParseLines(lines);
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
