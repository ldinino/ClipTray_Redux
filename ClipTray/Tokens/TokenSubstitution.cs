using System;
using System.Runtime.InteropServices;
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

        public static string Resolve(string template)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            var now = DateTime.Now;
            var clipboardText = ReadClipboardText();

            return TokenRegex.Replace(template, m =>
            {
                if (m.Value == "{{") return "{";
                if (m.Value == "}}") return "}";

                var name = m.Groups["name"].Value.ToLowerInvariant();
                var fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;

                switch (name)
                {
                    case "date":
                        return FormatDateTime(now, fmt, DefaultDateFormat);
                    case "time":
                        return FormatDateTime(now, fmt, DefaultTimeFormat);
                    case "datetime":
                        return FormatDateTime(now, fmt, DefaultDateTimeFormat);
                    case "clipboard":
                        return clipboardText;
                    default:
                        return m.Value;
                }
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
    }
}
