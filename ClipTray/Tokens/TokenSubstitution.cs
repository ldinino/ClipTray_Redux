using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClipTray.Tokens
{
    public static class TokenSubstitution
    {
        private const string DefaultDateFormat = "MM/dd/yyyy";
        private const string DefaultTimeFormat = "HH:mm:ss";
        private const string DefaultDateTimeFormat = "MM/dd/yyyy HH:mm:ss";

        private static readonly Regex TokenRegex = new Regex(
            @"\{\{|\}\}|\{(?<name>\w+)(?::(?<fmt>[^}]*))?\}",
            RegexOptions.Compiled);

        // In RTF, literal { and } are escaped as \{ and \}. The format segment
        // can't contain a backslash (would itself be RTF-escaped); this covers
        // the common case of simple format strings like MM/dd/yyyy.
        private static readonly Regex RtfTokenRegex = new Regex(
            @"\\\{\\\{|\\\}\\\}|\\\{(?<name>\w+)(?::(?<fmt>[^\\]*))?\\\}",
            RegexOptions.Compiled);

        public static string Resolve(string template)
        {
            return ResolveCore(template, TokenRegex, literalOpen: "{", literalClose: "}", escapeValue: null);
        }

        public static string ResolveRtf(string rtfTemplate)
        {
            return ResolveCore(rtfTemplate, RtfTokenRegex, literalOpen: @"\{", literalClose: @"\}", escapeValue: EscapeForRtf);
        }

        private static string ResolveCore(string template, Regex regex, string literalOpen, string literalClose, Func<string, string> escapeValue)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            var now = DateTime.Now;
            var clipboardText = ReadClipboardText();

            return regex.Replace(template, m =>
            {
                if (m.Value == literalOpen + literalOpen) return literalOpen;
                if (m.Value == literalClose + literalClose) return literalClose;

                var name = m.Groups["name"].Value.ToLowerInvariant();
                var fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;

                string resolved;
                switch (name)
                {
                    case "date":
                        resolved = FormatDateTime(now, fmt, DefaultDateFormat);
                        break;
                    case "time":
                        resolved = FormatDateTime(now, fmt, DefaultTimeFormat);
                        break;
                    case "datetime":
                        resolved = FormatDateTime(now, fmt, DefaultDateTimeFormat);
                        break;
                    case "clipboard":
                        resolved = clipboardText;
                        break;
                    default:
                        return m.Value;
                }

                return escapeValue != null ? escapeValue(resolved) : resolved;
            });
        }

        private static string FormatDateTime(DateTime when, string fmt, string defaultFmt)
        {
            if (string.IsNullOrEmpty(fmt))
                return when.ToString(defaultFmt);

            try
            {
                return when.ToString(fmt);
            }
            catch (FormatException)
            {
                return when.ToString(defaultFmt);
            }
        }

        private static string ReadClipboardText()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (ExternalException)
            {
                return string.Empty;
            }
        }

        // Escape a resolved string for safe inclusion in an RTF stream:
        // backslash/braces are RTF metachars; chars above ASCII need \uN? escapes.
        private static string EscapeForRtf(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c == '\\') sb.Append(@"\\");
                else if (c == '{') sb.Append(@"\{");
                else if (c == '}') sb.Append(@"\}");
                else if (c == '\r') { /* drop CR; \par handles line breaks */ }
                else if (c == '\n') sb.Append(@"\line ");
                else if (c == '\t') sb.Append(@"\tab ");
                else if (c < 128) sb.Append(c);
                else sb.Append(@"\u").Append((short)c).Append('?');
            }
            return sb.ToString();
        }
    }
}
