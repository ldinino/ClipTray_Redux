using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClipTray.Settings
{
    /// <summary>
    /// A minimal INI reader/writer that preserves the file verbatim apart from the
    /// keys it is asked to change. Comments, blank lines, ordering and unknown keys
    /// all survive a round trip, which keeps hand-edited files (and settings written
    /// by a future version) intact.
    /// </summary>
    internal sealed class IniDocument
    {
        private readonly List<string> _lines;

        private IniDocument(List<string> lines)
        {
            _lines = lines;
        }

        public static IniDocument Empty()
        {
            return new IniDocument(new List<string>());
        }

        public static IniDocument Parse(string text)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(text))
            {
                using (var reader = new StringReader(text))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        lines.Add(line);
                }
            }
            return new IniDocument(lines);
        }

        public string Get(string section, string key)
        {
            string currentSection = null;

            foreach (var line in _lines)
            {
                string header = ReadSectionHeader(line);
                if (header != null)
                {
                    currentSection = header;
                    continue;
                }

                if (!Matches(currentSection, section)) continue;

                string foundKey, value;
                if (TryReadPair(line, out foundKey, out value) && Matches(foundKey, key))
                    return value;
            }

            return null;
        }

        public void Set(string section, string key, string value)
        {
            string currentSection = null;
            int sectionEnd = -1;

            for (int index = 0; index < _lines.Count; index++)
            {
                string header = ReadSectionHeader(_lines[index]);
                if (header != null)
                {
                    if (Matches(currentSection, section)) break;
                    currentSection = header;
                    continue;
                }

                if (!Matches(currentSection, section)) continue;

                sectionEnd = index;

                string foundKey, existing;
                if (TryReadPair(_lines[index], out foundKey, out existing) && Matches(foundKey, key))
                {
                    _lines[index] = key + "=" + value;
                    return;
                }
            }

            if (sectionEnd >= 0)
            {
                // Append inside the existing section, after its last populated line.
                while (sectionEnd + 1 < _lines.Count
                    && ReadSectionHeader(_lines[sectionEnd + 1]) == null
                    && _lines[sectionEnd + 1].Trim().Length == 0)
                {
                    sectionEnd++;
                }
                _lines.Insert(sectionEnd + 1, key + "=" + value);
                return;
            }

            if (!SectionExists(section))
            {
                if (_lines.Count > 0 && _lines[_lines.Count - 1].Trim().Length != 0)
                    _lines.Add(string.Empty);
                _lines.Add("[" + section + "]");
            }

            _lines.Add(key + "=" + value);
        }

        private bool SectionExists(string section)
        {
            foreach (var line in _lines)
            {
                if (Matches(ReadSectionHeader(line), section)) return true;
            }
            return false;
        }

        public void AddCommentLine(string comment)
        {
            _lines.Add(string.IsNullOrEmpty(comment) ? string.Empty : "# " + comment);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (var line in _lines)
                builder.Append(line).Append("\r\n");
            return builder.ToString();
        }

        private static string ReadSectionHeader(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[trimmed.Length - 1] != ']')
                return null;
            return trimmed.Substring(1, trimmed.Length - 2).Trim();
        }

        private static bool TryReadPair(string line, out string key, out string value)
        {
            key = null;
            value = null;

            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed[0] == ';') return false;

            int separator = trimmed.IndexOf('=');
            if (separator <= 0) return false;

            key = trimmed.Substring(0, separator).Trim();
            value = trimmed.Substring(separator + 1).Trim();
            return key.Length > 0;
        }

        private static bool Matches(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
