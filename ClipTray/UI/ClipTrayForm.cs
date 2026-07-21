using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    internal static class ClipTrayIcon
    {
        public static Icon Create()
        {
            string executablePath = typeof(ClipTrayIcon).Assembly.Location;
            return Icon.ExtractAssociatedIcon(executablePath)
                ?? (Icon)SystemIcons.Application.Clone();
        }
    }

    public abstract class ClipTrayForm : Form
    {
        private Icon _clipTrayIcon;

        protected ClipTrayForm()
        {
            _clipTrayIcon = ClipTrayIcon.Create();
            Icon = _clipTrayIcon;
            Font = new Font("Segoe UI", 9F);
        }

        protected void ConfigureDpiScaling()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _clipTrayIcon != null)
            {
                Icon = null;
                _clipTrayIcon.Dispose();
                _clipTrayIcon = null;
            }
            base.Dispose(disposing);
        }
    }
}