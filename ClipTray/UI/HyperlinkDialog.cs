using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public class HyperlinkDialog : ClipTrayForm
    {
        private TextBox _urlBox;
        private TextBox _displayBox;
        private Button _okButton;

        public string Url { get; private set; }
        public string DisplayText { get; private set; }

        public HyperlinkDialog(string defaultDisplay)
            : this("", defaultDisplay)
        {
        }

        public HyperlinkDialog(string defaultUrl, string defaultDisplay)
        {
            Url = defaultUrl ?? "";
            DisplayText = defaultDisplay ?? "";
            InitializeComponents();
            ConfigureDpiScaling();
        }

        private void InitializeComponents()
        {
            Text = "Insert or Edit Link";
            ClientSize = new Size(404, 151);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var urlLabel = new Label
            {
                Text = "Link address:",
                Location = new Point(12, 18),
                AutoSize = true
            };

            _urlBox = new TextBox
            {
                Name = "linkAddress",
                Text = Url,
                Location = new Point(95, 15),
                Size = new Size(297, 21)
            };
            _urlBox.TextChanged += (s, e) =>
            {
                _okButton.Enabled = !string.IsNullOrWhiteSpace(_urlBox.Text);
            };

            var displayLabel = new Label
            {
                Text = "Text to display:",
                Location = new Point(12, 50),
                AutoSize = true
            };

            _displayBox = new TextBox
            {
                Name = "linkDisplayText",
                Text = DisplayText,
                Location = new Point(95, 47),
                Size = new Size(297, 21)
            };

            var hint = new Label
            {
                Text = "Leave display text empty to show the URL itself.",
                Location = new Point(12, 80),
                Size = new Size(380, 18),
                ForeColor = SystemColors.GrayText
            };

            _okButton = new Button
            {
                Text = "Insert",
                Location = new Point(236, 112),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK,
                Enabled = !string.IsNullOrWhiteSpace(Url)
            };
            _okButton.Click += (s, e) =>
            {
                Url = _urlBox.Text.Trim();
                DisplayText = _displayBox.Text;
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(317, 112),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = _okButton;
            CancelButton = cancelButton;
            Shown += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_urlBox.Text))
                {
                    _urlBox.Focus();
                }
                else
                {
                    _displayBox.Focus();
                    _displayBox.SelectAll();
                }
            };

            Controls.AddRange(new Control[]
            {
                urlLabel, _urlBox, displayLabel, _displayBox, hint,
                _okButton, cancelButton
            });
        }
    }
}
