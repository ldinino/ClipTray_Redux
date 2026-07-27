using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClipTray.Models;
using ClipTray.Tokens;
using ClipTray.UI;

namespace ClipTray.Data
{
    /// <summary>
    /// Puts an insert on the clipboard with its dynamic tokens resolved, preserving
    /// rich text when the entry has any. Shared by the tray menu and ClipBar so both
    /// routes behave identically.
    /// </summary>
    public static class ClipboardWriter
    {
        /// <summary>
        /// The resolved plain text and RTF for an entry. Separated from the clipboard
        /// call itself so the substitution logic can be tested without a clipboard.
        /// </summary>
        public struct Payload
        {
            public Payload(string text, string rtf)
            {
                Text = text;
                Rtf = rtf;
            }

            public string Text { get; }

            /// <summary>Null when the entry is plain text.</summary>
            public string Rtf { get; }

            public bool IsEmpty
            {
                get { return string.IsNullOrEmpty(Text); }
            }
        }

        public static Payload Resolve(ClipEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Text))
                return new Payload(null, null);

            var visibleText = RichTextHelpers.GetVisibleText(entry.Rtf, entry.Text);
            var resolvedText = TokenSubstitution.Resolve(visibleText);

            var resolvedRtf = string.IsNullOrEmpty(entry.Rtf)
                ? null
                : TokenSubstitution.ResolveRtf(entry.Rtf);

            return new Payload(resolvedText, resolvedRtf);
        }

        /// <summary>
        /// Returns false when there was nothing to copy or the clipboard was locked
        /// by another process.
        /// </summary>
        public static bool Copy(ClipEntry entry)
        {
            var payload = Resolve(entry);
            if (payload.IsEmpty) return false;

            try
            {
                if (!string.IsNullOrEmpty(payload.Rtf))
                {
                    var data = RichTextHelpers.CreateClipboardData(payload.Text, payload.Rtf);
                    Clipboard.SetDataObject(data, true);
                }
                else
                {
                    Clipboard.SetText(payload.Text);
                }
                return true;
            }
            catch (ExternalException)
            {
                // Clipboard locked by another process - silently ignore.
                return false;
            }
        }
    }
}
