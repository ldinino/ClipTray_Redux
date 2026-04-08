using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;

namespace ClipTray.UI
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private string _filePath;
        private string _recentFilePath;
        private List<ClipEntry> _entries;
        private int _menuSize = 20;
        private bool _previewMode = false;

        public TrayApplicationContext()
        {
            _filePath = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath),
                "ClipTray.txt");

            if (!File.Exists(_filePath))
                FileParser.CreateDefaultFile(_filePath);

            _entries = FileParser.Parse(_filePath);

            _notifyIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = TruncateTooltip(Path.GetFileName(_filePath)),
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };

            _notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
        }

        public void RefreshMenu()
        {
            _entries = FileParser.Parse(_filePath);
            _notifyIcon.ContextMenuStrip = BuildMenu();
            _notifyIcon.Text = TruncateTooltip(Path.GetFileName(_filePath));
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            // Add...
            var addItem = new ToolStripMenuItem("Add...");
            addItem.Click += AddItem_Click;
            menu.Items.Add(addItem);

            // Options submenu
            var optionsMenu = new ToolStripMenuItem("Options");

            // Options > Preview Mode
            var previewItem = new ToolStripMenuItem("Preview Mode");
            previewItem.CheckOnClick = true;
            previewItem.Checked = _previewMode;
            previewItem.Click += (s, e) => { _previewMode = ((ToolStripMenuItem)s).Checked; };
            optionsMenu.DropDownItems.Add(previewItem);

            // Options > Edit...
            var editItem = new ToolStripMenuItem("Edit...");
            editItem.Click += EditItem_Click;
            optionsMenu.DropDownItems.Add(editItem);

            // Options > File submenu
            var fileMenu = new ToolStripMenuItem("File");

            var openCreateItem = new ToolStripMenuItem("Open/Create...");
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
            var aboutItem = new ToolStripMenuItem("About ClipTray");
            aboutItem.Click += AboutItem_Click;
            helpMenu.DropDownItems.Add(aboutItem);
            optionsMenu.DropDownItems.Add(helpMenu);

            menu.Items.Add(optionsMenu);

            // --- separator ---
            menu.Items.Add(new ToolStripSeparator());

            // More...
            var moreItem = new ToolStripMenuItem("More...");
            moreItem.Click += MoreItem_Click;
            menu.Items.Add(moreItem);

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
            var exitItem = new ToolStripMenuItem("Exit ClipTray");
            exitItem.Click += ExitItem_Click;
            menu.Items.Add(exitItem);

            return menu;
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

            try
            {
                Clipboard.SetText(entry.Text);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard locked by another process — silently ignore
            }
        }

        private void ShowAddDialog()
        {
            using (var dlg = new AddEntryDialog(_entries, _filePath))
            {
                dlg.EntryAdded += (s, ev) => RefreshMenu();
                dlg.ShowDialog();
            }
            RefreshMenu();
        }

        private void AddItem_Click(object sender, EventArgs e)
        {
            ShowAddDialog();
        }

        private void EditItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new EditorDialog(_entries, _filePath))
            {
                dlg.ShowDialog();
            }
            RefreshMenu();
        }

        private void OpenCreateItem_Click(object sender, EventArgs e)
        {
            // Placeholder — wired in Phase 4
        }

        private void RecentItem_Click(object sender, EventArgs e)
        {
            // Placeholder — wired in Phase 4
        }

        private void MoreItem_Click(object sender, EventArgs e)
        {
            // Placeholder — wired in Phase 4
        }

        private void AboutItem_Click(object sender, EventArgs e)
        {
            // Placeholder — wired in Phase 5
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
            }
            base.Dispose(disposing);
        }

        private static string TruncateTooltip(string text)
        {
            if (text.Length > 63)
                return text.Substring(0, 60) + "...";
            return text;
        }

        private static string TruncateMenuTitle(string title)
        {
            if (title.Length > 60)
                return title.Substring(0, 57) + "...";
            return title;
        }
    }
}
