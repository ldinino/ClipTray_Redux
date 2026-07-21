using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public class PreviewDialog : ClipTrayForm
    {
        public PreviewDialog(string title, string text, string rtf = null)
        {
            InitializeComponents(title, text, rtf);
            ConfigureDpiScaling();
        }

        private void InitializeComponents(string title, string text, string rtf)
        {
            Text = title;
            ClientSize = new Size(384, 261);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var textBox = new RichTextBox
            {
                Location = new Point(12, 12),
                Size = new Size(360, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = true
            };
            textBox.LinkClicked += RichTextHelpers.LaunchClickedLink;
            if (!string.IsNullOrEmpty(rtf))
                textBox.Rtf = rtf;
            else
                textBox.Text = text ?? "";

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(297, 225),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK
            };

            AcceptButton = okButton;

            Controls.AddRange(new Control[] { textBox, okButton });
        }
    }
}
