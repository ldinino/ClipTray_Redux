using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public class PreviewDialog : Form
    {
        public PreviewDialog(string title, string text)
        {
            InitializeComponents(title, text);
        }

        private void InitializeComponents(string title, string text)
        {
            Text = title;
            Size = new Size(400, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var textBox = new TextBox
            {
                Text = text,
                Location = new Point(12, 12),
                Size = new Size(360, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

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
