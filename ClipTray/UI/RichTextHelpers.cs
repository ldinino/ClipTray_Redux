using System;
using System.Diagnostics;
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
        protected override bool IsInputKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Return)
                return true;
            return base.IsInputKey(keyData);
        }
    }

    internal static class RichTextHelpers
    {
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
            string plain = target.Text ?? "";
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
            sb.Append(@"{\field{\*\fldinst HYPERLINK """);
            sb.Append(EscapeRtf(url));
            sb.Append(@"""}{\fldrslt \cf1\ul ");
            sb.Append(EscapeRtf(displayText));
            sb.Append(@"}}");
            sb.Append("}");
            return sb.ToString();
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
                    if (!string.IsNullOrEmpty(rtf))
                    {
                        target.SelectedRtf = rtf;
                        return true;
                    }
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
    }
}
