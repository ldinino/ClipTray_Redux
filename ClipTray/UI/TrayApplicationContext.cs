using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;
using ClipTray.Tokens;

namespace ClipTray.UI
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private string _filePath;
        private string _recentFilePath;
        private List<ClipEntry> _entries;
        private int _menuSize = 20;
        private Icon _applicationIcon;

        public TrayApplicationContext()
        {
            _filePath = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath),
                "ClipTray.txt");

            if (!File.Exists(_filePath))
            {
                try { FileParser.CreateDefaultFile(_filePath); }
                catch (IOException ex)
                {
                    MessageBox.Show("Could not create default file:\n" + ex.Message,
                        "ClipTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            _entries = SafeParse(_filePath);

            _applicationIcon = ClipTrayIcon.Create();
            _notifyIcon = new NotifyIcon
            {
                Icon = _applicationIcon,
                Text = TruncateTooltip(Path.GetFileName(_filePath)),
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };

            _notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
        }

        public void RefreshMenu()
        {
            _entries = SafeParse(_filePath);
            _notifyIcon.ContextMenuStrip = BuildMenu();
            _notifyIcon.Text = TruncateTooltip(Path.GetFileName(_filePath));
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip
            {
                ImageScalingSize = SystemInformation.SmallIconSize,
                RenderMode = ToolStripRenderMode.Professional,
                ShowCheckMargin = true
            };

            var addItem = new ToolStripMenuItem("New insert...");
            addItem.Click += AddItem_Click;
            menu.Items.Add(addItem);

            // Options submenu
            var optionsMenu = new ToolStripMenuItem("Options");

            var startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
            {
                Name = "startWithWindowsItem",
                Checked = ReadStartupRegistration(),
                CheckOnClick = false
            };
            startWithWindowsItem.Click += StartWithWindowsItem_Click;
            optionsMenu.DropDownItems.Add(startWithWindowsItem);
            optionsMenu.DropDownItems.Add(new ToolStripSeparator());

            // Options > File submenu
            var fileMenu = new ToolStripMenuItem("File");

            var openCreateItem = new ToolStripMenuItem("Open...");
            openCreateItem.Click += OpenCreateItem_Click;
            fileMenu.DropDownItems.Add(openCreateItem);

            if (!string.IsNullOrEmpty(_recentFilePath))
            {
                var recentItem = new ToolStripMenuItem(_recentFilePath);
                recentItem.Click += RecentItem_Click;
                fileMenu.DropDownItems.Add(recentItem);
            }

            optionsMenu.DropDownItems.Add(fileMenu);

            // Options > Help submenu
            var helpMenu = new ToolStripMenuItem("Help");
            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += AboutItem_Click;
            helpMenu.DropDownItems.Add(aboutItem);
            optionsMenu.DropDownItems.Add(helpMenu);

            menu.Items.Add(optionsMenu);

            // --- separator ---
            menu.Items.Add(new ToolStripSeparator());

            var entriesItem = new ToolStripMenuItem("Open editor...");
            entriesItem.Click += EntriesItem_Click;
            menu.Items.Add(entriesItem);

            // --- separator ---
            menu.Items.Add(new ToolStripSeparator());

            // Entry items (up to _menuSize)
            int count = Math.Min(_entries.Count, _menuSize);
            for (int i = 0; i < count; i++)
            {
                var entry = _entries[i];
                var title = TruncateMenuTitle(entry.Title);
                var item = new ToolStripMenuItem(title);
                item.Tag = entry;
                item.Click += EntryItem_Click;
                menu.Items.Add(item);
            }

            // --- separator ---
            menu.Items.Add(new ToolStripSeparator());

            // Exit ClipTray
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += ExitItem_Click;
            menu.Items.Add(exitItem);

            ApplyMenuDpiMetrics(menu.Items);
            return menu;
        }

        private static void ApplyMenuDpiMetrics(ToolStripItemCollection items)
        {
            float scale = Math.Max(1F, SystemInformation.SmallIconSize.Width / 16F);
            int verticalPadding = Math.Max(1, (int)Math.Round(1F + 4F * (scale - 1F)));

            foreach (ToolStripItem item in items)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null) continue;

                menuItem.Padding = new Padding(
                    0,
                    verticalPadding,
                    0,
                    verticalPadding);
                ApplyMenuDpiMetrics(menuItem.DropDownItems);
            }
        }

        private void EntryItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            var entry = (ClipEntry)item.Tag;
            CopyToClipboard(entry);
        }

        private void CopyToClipboard(ClipEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Text))
                return;

            var visibleText = RichTextHelpers.GetVisibleText(entry.Rtf, entry.Text);
            var resolvedText = TokenSubstitution.Resolve(visibleText);

            try
            {
                if (!string.IsNullOrEmpty(entry.Rtf))
                {
                    var resolvedRtf = TokenSubstitution.ResolveRtf(entry.Rtf);
                    var data = RichTextHelpers.CreateClipboardData(resolvedText, resolvedRtf);
                    Clipboard.SetDataObject(data, true);
                }
                else
                {
                    Clipboard.SetText(resolvedText);
                }
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard locked by another process — silently ignore
            }
        }

        private void ShowAddDialog()
        {
            using (var dlg = new EntriesDialog(_entries, _filePath, _menuSize, true))
            {
                dlg.ShowDialog();
                _menuSize = dlg.MenuSize;
            }
            RefreshMenu();
        }

        private void AddItem_Click(object sender, EventArgs e)
        {
            ShowAddDialog();
        }

        private void StartWithWindowsItem_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            try
            {
                StartupRegistration.SetEnabled(!item.Checked);
                item.Checked = StartupRegistration.IsEnabled();
            }
            catch (Exception ex) when (IsStartupRegistrationError(ex))
            {
                item.Checked = ReadStartupRegistration();
                MessageBox.Show(
                    "Could not update the Windows startup setting:\n" + ex.Message,
                    "ClipTray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenCreateItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                dlg.CheckFileExists = false;

                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                var newPath = dlg.FileName;

                if (!File.Exists(newPath))
                {
                    var result = MessageBox.Show(
                        "\"" + Path.GetFileName(newPath) + "\" does not exist. Do you wish to create it?",
                        "Create File",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                        return;

                    try { FileParser.CreateDefaultFile(newPath); }
                    catch (IOException ex)
                    {
                        MessageBox.Show("Could not create file:\n" + ex.Message,
                            "ClipTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                _recentFilePath = _filePath;
                _filePath = newPath;
                RefreshMenu();
            }
        }

        private void RecentItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_recentFilePath))
                return;

            var temp = _filePath;
            _filePath = _recentFilePath;
            _recentFilePath = temp;
            RefreshMenu();
        }

        private void EntriesItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new EntriesDialog(_entries, _filePath, _menuSize))
            {
                dlg.ShowDialog();
                _menuSize = dlg.MenuSize;
            }
            RefreshMenu();
        }

        private void AboutItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new AboutDialog())
                dlg.ShowDialog();
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ShowAddDialog();
        }

        private void ExitItem_Click(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _applicationIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private List<ClipEntry> SafeParse(string filePath)
        {
            try
            {
                return FileParser.Parse(filePath);
            }
            catch (IOException ex)
            {
                MessageBox.Show("Could not read file:\n" + ex.Message,
                    "ClipTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<ClipEntry>();
            }
        }

        private static string TruncateTooltip(string text)
        {
            if (text.Length > 63)
                return text.Substring(0, 60) + "...";
            return text;
        }

        private static bool ReadStartupRegistration()
        {
            try
            {
                return StartupRegistration.IsEnabled();
            }
            catch (Exception ex) when (IsStartupRegistrationError(ex))
            {
                return false;
            }
        }

        private static bool IsStartupRegistrationError(Exception exception)
        {
            return exception is IOException
                || exception is SecurityException
                || exception is UnauthorizedAccessException;
        }

        private static string TruncateMenuTitle(string title)
        {
            if (title.Length > 60)
                return title.Substring(0, 57) + "...";
            return title;
        }
    }
}
