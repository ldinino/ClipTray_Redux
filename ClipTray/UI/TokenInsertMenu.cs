using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public static class TokenInsertMenu
    {
        private static readonly string[] DatePresets =
        {
            "MM/dd/yyyy",
            "yyyy-MM-dd",
            "MMMM d, yyyy",
            "dddd, MMM d",
            "MM-dd-yyyy",
            "d/M/yyyy"
        };

        private static readonly string[] TimePresets =
        {
            "HH:mm:ss",
            "HH:mm",
            "h:mm tt",
            "h:mm:ss tt"
        };

        private static readonly string[] DateTimePresets =
        {
            "MM/dd/yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "MMMM d, yyyy h:mm tt"
        };

        private const string TokenReference =
            "Available tokens — type or use Insert ▾:\r\n\r\n" +
            "  {date}                  Current date (MM/dd/yyyy)\r\n" +
            "  {date:format}           Current date with custom .NET format\r\n" +
            "  {time}                  Current time (HH:mm:ss)\r\n" +
            "  {time:format}           Current time with custom .NET format\r\n" +
            "  {datetime}              Current date + time\r\n" +
            "  {datetime:format}       Date + time with custom .NET format\r\n" +
            "  {clipboard}             Current clipboard text\r\n\r\n" +
            "Examples:\r\n" +
            "  {date:yyyy-MM-dd}       →  2026-05-14\r\n" +
            "  {time:h:mm tt}          →  3:47 PM\r\n" +
            "  {datetime:yyyy-MM-ddTHH:mm:ss}\r\n\r\n" +
            "Escape literal braces with {{ and }}.\r\n" +
            "Unknown tokens pass through unchanged.";

        public static void AttachTo(Button insertButton, TextBox target, IWin32Window owner)
        {
            var menu = new ContextMenuStrip();

            var dateItem = new ToolStripMenuItem("Date...");
            dateItem.Click += (s, e) => InsertDateTimeToken("date", "MM/dd/yyyy", DatePresets, target, owner);
            menu.Items.Add(dateItem);

            var timeItem = new ToolStripMenuItem("Time...");
            timeItem.Click += (s, e) => InsertDateTimeToken("time", "HH:mm:ss", TimePresets, target, owner);
            menu.Items.Add(timeItem);

            var dateTimeItem = new ToolStripMenuItem("Date + Time...");
            dateTimeItem.Click += (s, e) => InsertDateTimeToken("datetime", "MM/dd/yyyy HH:mm:ss", DateTimePresets, target, owner);
            menu.Items.Add(dateTimeItem);

            var clipboardItem = new ToolStripMenuItem("Clipboard");
            clipboardItem.Click += (s, e) => InsertAtCaret(target, "{clipboard}");
            menu.Items.Add(clipboardItem);

            menu.Items.Add(new ToolStripSeparator());

            var helpItem = new ToolStripMenuItem("Token reference...");
            helpItem.Click += (s, e) => MessageBox.Show(owner, TokenReference, "ClipTray Tokens",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            menu.Items.Add(helpItem);

            insertButton.Click += (s, e) => menu.Show(insertButton, new Point(0, insertButton.Height));
        }

        private static void InsertDateTimeToken(string name, string defaultFormat, string[] presets,
            TextBox target, IWin32Window owner)
        {
            using (var dlg = new TokenFormatDialog(name, defaultFormat, presets))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return;

                var fmt = dlg.Format;
                var token = string.IsNullOrEmpty(fmt) || fmt == defaultFormat
                    ? "{" + name + "}"
                    : "{" + name + ":" + fmt + "}";

                InsertAtCaret(target, token);
            }
        }

        private static void InsertAtCaret(TextBox target, string text)
        {
            int start = target.SelectionStart;
            int len = target.SelectionLength;
            var current = target.Text ?? string.Empty;

            target.Text = current.Substring(0, start) + text + current.Substring(start + len);
            target.SelectionStart = start + text.Length;
            target.SelectionLength = 0;
            target.Focus();
        }
    }
}
