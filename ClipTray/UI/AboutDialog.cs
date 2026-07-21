using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public class AboutDialog : ClipTrayForm
    {
        public AboutDialog()
        {
            InitializeComponents();
            ConfigureDpiScaling();
        }

        private void InitializeComponents()
        {
            Text = "About ClipTray";
            ClientSize = new Size(324, 171);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var nameLabel = new Label
            {
                Text = "ClipTray",
                Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
                Location = new Point(12, 15),
                AutoSize = true
            };

            var v = Assembly.GetExecutingAssembly().GetName().Version;
            var versionLabel = new Label
            {
                Text = "Version " + v.Major + "." + v.Minor + "." + v.Build,
                Location = new Point(12, 50),
                AutoSize = true
            };

            var descLabel = new Label
            {
                Text = "A system tray clipboard manager.",
                Location = new Point(12, 75),
                AutoSize = true
            };

            var authorText = "Rebuilt for the future by Luciano DiNino.\nPlease email ldinino@microsoft.com if you find a bug.";
            var emailStart = authorText.IndexOf("ldinino@microsoft.com");
            var authorLabel = new LinkLabel
            {
                Text = authorText,
                Location = new Point(12, 95),
                AutoSize = true
            };
            authorLabel.Links.Clear();
            authorLabel.Links.Add(emailStart, "ldinino@microsoft.com".Length, "mailto:ldinino@microsoft.com");
            authorLabel.LinkClicked += (s, e) =>
            {
                Process.Start(e.Link.LinkData.ToString());
            };

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(240, 135),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK
            };

            AcceptButton = okButton;

            Controls.AddRange(new Control[] { nameLabel, versionLabel, descLabel, authorLabel, okButton });
        }
    }
}
