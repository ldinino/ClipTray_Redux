using System.Collections.Generic;
using System.IO;
using System.Text;
using ClipTray.Models;

namespace ClipTray.Data
{
    public static class FileWriter
    {
        public static void Write(string filePath, List<ClipEntry> entries)
        {
            var sb = new StringBuilder();

            // Preamble
            sb.Append("End:\r\n\r\n");

            foreach (var entry in entries)
            {
                sb.Append("Title:");
                sb.Append(entry.Title);
                sb.Append("\r\n");
                if (!string.IsNullOrEmpty(entry.Text))
                {
                    sb.Append(entry.Text);
                    sb.Append("\r\n");
                }
                if (!string.IsNullOrEmpty(entry.Rtf))
                {
                    foreach (var line in entry.Rtf.Split(new[] { "\r\n" }, System.StringSplitOptions.None))
                    {
                        sb.Append("Rtf:");
                        sb.Append(line);
                        sb.Append("\r\n");
                    }
                }
                sb.Append("End:\r\n\r\n");
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
