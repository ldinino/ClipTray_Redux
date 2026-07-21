using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClipTray.UI
{
    // RichTextBox that claims Enter as input even when the parent form has an
    // AcceptButton — without this, pressing Enter in the composer fires Save/Add
    // instead of inserting a newline.
    internal class ComposerRichTextBox : RichTextBox
    {
        private const int WmPaste = 0x0302;

        protected override bool IsInputKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Return)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmPaste && RichTextHelpers.PasteRichOrPlain(this))
                return;
            base.WndProc(ref message);
        }
    }

    internal static class RichTextHelpers
    {
        private const int EmGetTextEx = 0x045E;
        private const int GtNoHiddenText = 0x0008;
        private const int GtSelection = 0x0002;
        private const int UnicodeCodePage = 1200;

        private sealed class HyperlinkField
        {
            public int Start { get; set; }
            public int Length { get; set; }
            public string Url { get; set; }
            public string DisplayText { get; set; }
            public string Marker { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GetTextEx
        {
            public int BufferSize;
            public int Flags;
            public int CodePage;
            public IntPtr DefaultChar;
            public IntPtr UsedDefaultChar;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            ref GetTextEx options,
            StringBuilder text);

        public static string GetVisibleText(RichTextBox source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return GetVisibleText(source, GtNoHiddenText, source.TextLength);
        }

        public static string GetVisibleSelectedText(RichTextBox source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return GetVisibleText(
                source,
                GtNoHiddenText | GtSelection,
                source.SelectionLength);
        }

        private static string GetVisibleText(RichTextBox source, int flags, int estimatedLength)
        {
            if (estimatedLength == 0) return "";

            var text = new StringBuilder(estimatedLength + 1);
            var options = new GetTextEx
            {
                BufferSize = text.Capacity * sizeof(char),
                Flags = flags,
                CodePage = UnicodeCodePage
            };

            SendMessage(source.Handle, EmGetTextEx, ref options, text);
            return text.ToString();
        }

        public static string GetVisibleText(string rtf, string fallbackText)
        {
            if (string.IsNullOrEmpty(rtf)) return fallbackText ?? "";

            using (var textBox = new RichTextBox())
            {
                try
                {
                    textBox.Rtf = rtf;
                    return GetVisibleText(textBox);
                }
                catch (ArgumentException)
                {
                    return fallbackText ?? "";
                }
            }
        }

        // Returns the source's .Rtf if its formatting differs from what a
        // baseline RichTextBox would produce for the same plain Text;
        // returns null when the content is effectively plain.
        public static string DetectRichness(RichTextBox source)
        {
            if (string.IsNullOrEmpty(source.Text))
                return null;

            using (var sentinel = new RichTextBox())
            {
                sentinel.Font = source.Font;
                sentinel.Text = source.Text;
                return string.Equals(
                    Normalize(sentinel.Rtf),
                    Normalize(source.Rtf),
                    StringComparison.Ordinal)
                    ? null
                    : source.Rtf;
            }
        }

        // The \generator header carries a version string that drifts across
        // RichTextBox instances; strip it before comparing.
        private static string Normalize(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return "";
            return Regex.Replace(rtf, @"\{\\\*\\generator [^}]*\}", "");
        }

        // Strip all formatting from the document so it becomes byte-identical
        // to the sentinel baseline DetectRichness compares against. After this
        // runs, DetectRichness(target) returns null.
        public static void ConvertToPlain(RichTextBox target)
        {
            string plain = GetVisibleText(target);
            int caret = Math.Min(target.SelectionStart, plain.Length);

            using (var sentinel = new RichTextBox())
            {
                sentinel.Font = target.Font;
                sentinel.Text = plain;
                target.Rtf = sentinel.Rtf;
            }

            target.SelectionStart = caret;
            target.SelectionLength = 0;
        }

        // Build a self-contained RTF fragment containing a HYPERLINK field —
        // the same structure browsers and Word produce. Assign via SelectedRtf;
        // the receiving control merges our colortbl into its own.
        public static string BuildHyperlinkRtf(string url, string displayText)
        {
            var sb = new StringBuilder();
            sb.Append(@"{\rtf1\ansi\deff0");
            sb.Append(@"{\colortbl ;\red0\green0\blue238;}");
            sb.Append(@"{\field{\*\fldinst{HYPERLINK """);
            sb.Append(EscapeRtf(url));
            sb.Append(@"""}}{\fldrslt \cf1\ul ");
            sb.Append(EscapeRtf(displayText));
            sb.Append(@"}}");
            sb.Append("}");
            return sb.ToString();
        }

        public static bool InsertHyperlink(RichTextBox target, string url, string displayText)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(url)) return false;

            string display = string.IsNullOrWhiteSpace(displayText) ? url.Trim() : displayText;
            var previousColor = target.SelectionColor;
            var previousBackColor = target.SelectionBackColor;
            var previousFont = target.SelectionFont ?? target.Font;

            if (!TryInsertRtf(target, BuildHyperlinkRtf(url.Trim(), display)))
                return false;

            if (!previousColor.IsEmpty)
                target.SelectionColor = previousColor;
            if (!previousBackColor.IsEmpty)
                target.SelectionBackColor = previousBackColor;
            target.SelectionFont = previousFont;
            return true;
        }

        internal static bool TryGetHyperlinkUrl(string rtf, out string url)
        {
            url = null;
            if (string.IsNullOrEmpty(rtf)) return false;

            var match = Regex.Match(
                rtf,
                @"HYPERLINK\s+""(?<url>(?:\\.|[^""])*)""",
                RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            string encodedUrl = match.Groups["url"].Value;
            url = Regex.Replace(
                encodedUrl,
                @"\\(?:(?<escaped>[\\{}])|u(?<code>-?\d+)\?)",
                replacement => replacement.Groups["escaped"].Success
                    ? replacement.Groups["escaped"].Value
                    : ((char)(ushort)(short)int.Parse(
                        replacement.Groups["code"].Value,
                        System.Globalization.CultureInfo.InvariantCulture)).ToString());
            return !string.IsNullOrWhiteSpace(url);
        }

        internal static DataObject CreateClipboardData(string plainText, string rtf)
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, plainText ?? "");
            data.SetData(DataFormats.Text, plainText ?? "");

            if (string.IsNullOrEmpty(rtf)) return data;

            data.SetData(DataFormats.Rtf, rtf);
            string html = BuildHtmlClipboard(rtf);
            if (!string.IsNullOrEmpty(html))
                data.SetData(DataFormats.Html, html);
            return data;
        }

        internal static string BuildHtmlClipboard(string rtf)
        {
            var fields = FindHyperlinkFields(rtf);
            if (fields.Count == 0) return null;

            var markedRtf = new StringBuilder(rtf);
            for (int index = fields.Count - 1; index >= 0; index--)
            {
                var field = fields[index];
                field.Marker = "__CLIPTRAY_LINK_" + index + "__";
                while (rtf.Contains(field.Marker))
                    field.Marker += "_";
                markedRtf.Remove(field.Start, field.Length);
                markedRtf.Insert(field.Start, field.Marker);
            }

            string markedText;
            using (var textBox = new RichTextBox())
            {
                try
                {
                    textBox.Rtf = markedRtf.ToString();
                    markedText = GetVisibleText(textBox);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            var fragment = new StringBuilder();
            int position = 0;
            foreach (var field in fields)
            {
                int markerPosition = markedText.IndexOf(
                    field.Marker,
                    position,
                    StringComparison.Ordinal);
                if (markerPosition < 0) return null;

                fragment.Append(EncodeHtmlText(markedText.Substring(position, markerPosition - position)));
                fragment.Append("<a href=\"");
                fragment.Append(WebUtility.HtmlEncode(field.Url));
                fragment.Append("\">");
                fragment.Append(EncodeHtmlText(field.DisplayText));
                fragment.Append("</a>");
                position = markerPosition + field.Marker.Length;
            }
            fragment.Append(EncodeHtmlText(markedText.Substring(position)));

            return WrapHtmlClipboardFragment(fragment.ToString());
        }

        private static List<HyperlinkField> FindHyperlinkFields(string rtf)
        {
            var fields = new List<HyperlinkField>();
            if (string.IsNullOrEmpty(rtf)) return fields;

            var groupStarts = new Stack<int>();
            for (int index = 0; index < rtf.Length; index++)
            {
                if (IsEscapedRtfCharacter(rtf, index)) continue;
                if (rtf[index] == '{')
                {
                    groupStarts.Push(index);
                    continue;
                }
                if (rtf[index] != '}' || groupStarts.Count == 0) continue;

                int start = groupStarts.Pop();
                if (!StartsWithRtfControlWord(rtf, start, "field")) continue;

                string fieldRtf = rtf.Substring(start, index - start + 1);
                if (!TryGetHyperlinkUrl(fieldRtf, out var url)) continue;
                if (!IsSafeHyperlinkUrl(url)) continue;

                string displayText;
                using (var textBox = new RichTextBox())
                {
                    try
                    {
                        textBox.Rtf = @"{\rtf1\ansi " + fieldRtf + "}";
                        displayText = GetVisibleText(textBox);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }
                if (string.IsNullOrEmpty(displayText)) continue;

                fields.Add(new HyperlinkField
                {
                    Start = start,
                    Length = index - start + 1,
                    Url = url,
                    DisplayText = displayText
                });
            }

            fields.Sort((left, right) => left.Start.CompareTo(right.Start));
            return fields;
        }

        private static bool IsSafeHyperlinkUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri)) return false;
            return parsedUri.Scheme != Uri.UriSchemeFile
                && !parsedUri.Scheme.Equals("javascript", StringComparison.OrdinalIgnoreCase)
                && !parsedUri.Scheme.Equals("vbscript", StringComparison.OrdinalIgnoreCase)
                && !parsedUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEscapedRtfCharacter(string rtf, int index)
        {
            int slashCount = 0;
            for (int previous = index - 1; previous >= 0 && rtf[previous] == '\\'; previous--)
                slashCount++;
            return slashCount % 2 != 0;
        }

        private static bool StartsWithRtfControlWord(string rtf, int groupStart, string controlWord)
        {
            int controlStart = groupStart + 1;
            string expected = "\\" + controlWord;
            if (controlStart + expected.Length > rtf.Length) return false;
            if (!string.Equals(
                rtf.Substring(controlStart, expected.Length),
                expected,
                StringComparison.OrdinalIgnoreCase))
                return false;

            int next = controlStart + expected.Length;
            return next >= rtf.Length || !char.IsLetter(rtf[next]);
        }

        private static string EncodeHtmlText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return WebUtility.HtmlEncode(text)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace("\n", "<br>\r\n");
        }

        private static string WrapHtmlClipboardFragment(string fragment)
        {
            const string headerFormat =
                "Version:1.0\r\n" +
                "StartHTML:{0:D10}\r\n" +
                "EndHTML:{1:D10}\r\n" +
                "StartFragment:{2:D10}\r\n" +
                "EndFragment:{3:D10}\r\n";
            const string htmlPrefix = "<html><body><!--StartFragment-->";
            const string htmlSuffix = "<!--EndFragment--></body></html>";

            string emptyHeader = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                headerFormat,
                0,
                0,
                0,
                0);
            int startHtml = Encoding.UTF8.GetByteCount(emptyHeader);
            int startFragment = startHtml + Encoding.UTF8.GetByteCount(htmlPrefix);
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            int endHtml = endFragment + Encoding.UTF8.GetByteCount(htmlSuffix);
            string header = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                headerFormat,
                startHtml,
                endHtml,
                startFragment,
                endFragment);

            return header + htmlPrefix + fragment + htmlSuffix;
        }

        internal static bool TryGetSingleHyperlinkFromHtml(
            string html,
            out string url,
            out string displayText)
        {
            url = null;
            displayText = null;
            if (string.IsNullOrWhiteSpace(html)) return false;

            var fragmentMatch = Regex.Match(
                html,
                @"<!--StartFragment-->(?<fragment>.*?)<!--EndFragment-->",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string fragment = fragmentMatch.Success
                ? fragmentMatch.Groups["fragment"].Value
                : html;

            var anchors = Regex.Matches(
                fragment,
                @"<a\b(?<attributes>[^>]*)>(?<content>.*?)</a\s*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (anchors.Count != 1) return false;

            var anchor = anchors[0];
            string outsideAnchor = fragment.Remove(anchor.Index, anchor.Length);
            if (!string.IsNullOrWhiteSpace(HtmlToText(outsideAnchor))) return false;

            string attributes = anchor.Groups["attributes"].Value;
            var hrefMatch = Regex.Match(
                attributes,
                @"\bhref\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s>]+))",
                RegexOptions.IgnoreCase);
            if (!hrefMatch.Success) return false;

            string href = hrefMatch.Groups["double"].Success
                ? hrefMatch.Groups["double"].Value
                : hrefMatch.Groups["single"].Success
                    ? hrefMatch.Groups["single"].Value
                    : hrefMatch.Groups["bare"].Value;
            href = WebUtility.HtmlDecode(href).Trim();
            if (!IsSafeHyperlinkUrl(href)) return false;

            string display = HtmlToText(anchor.Groups["content"].Value);
            if (string.IsNullOrWhiteSpace(display))
                display = href;

            url = href;
            displayText = display;
            return true;
        }

        private static string HtmlToText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string withLineBreaks = Regex.Replace(
                html,
                @"<br\s*/?>",
                "\n",
                RegexOptions.IgnoreCase);
            string withoutTags = Regex.Replace(withLineBreaks, @"<[^>]+>", "");
            return WebUtility.HtmlDecode(withoutTags)
                .Replace('\u00a0', ' ')
                .Trim();
        }

        private static string EscapeRtf(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (c == '\\') sb.Append(@"\\");
                else if (c == '{') sb.Append(@"\{");
                else if (c == '}') sb.Append(@"\}");
                else if (c < 128) sb.Append(c);
                else sb.Append(@"\u").Append((short)c).Append('?');
            }
            return sb.ToString();
        }

        // LinkClicked handler for read-only RichTextBox previews — opens the
        // URL in the user's default browser. Wraps Process.Start so a missing
        // URL handler or security exception silently no-ops.
        public static void LaunchClickedLink(object sender, LinkClickedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.LinkText)) return;
            try { Process.Start(e.LinkText); }
            catch { /* missing handler, blocked by policy, etc. */ }
        }

        // Best-effort rich paste: try RTF first, then plain text. Returns true
        // when something was inserted.
        public static bool PasteRichOrPlain(RichTextBox target)
        {
            try
            {
                var data = Clipboard.GetDataObject();
                if (data == null) return false;

                if (data.GetDataPresent(DataFormats.Rtf))
                {
                    var rtf = data.GetData(DataFormats.Rtf) as string;
                    if (TryInsertRtf(target, rtf))
                        return true;
                }

                if (data.GetDataPresent(DataFormats.Html))
                {
                    var html = data.GetData(DataFormats.Html) as string;
                    if (TryGetSingleHyperlinkFromHtml(html, out var url, out var displayText)
                        && InsertHyperlink(target, url, displayText))
                        return true;
                }

                if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        target.SelectedText = text;
                        return true;
                    }
                }
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
            return false;
        }

        internal static bool TryInsertRtf(RichTextBox target, string rtf)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(rtf)) return false;

            try
            {
                target.SelectedRtf = rtf;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
