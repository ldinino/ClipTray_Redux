using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using ClipTray.ClipBar;
using ClipTray.Data;
using ClipTray.Models;
using ClipTray.Settings;
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
        private AppSettings _settings;
        private string _settingsPath;
        private GlobalHotKey _clipBarHotKey;
        private ClipBarWindow _clipBarWindow;

        public TrayApplicationContext()
        {
            _filePath = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath),
                "ClipTray.txt");

            _settingsPath = SettingsStore.DefaultPath(Application.ExecutablePath);
            _settings = SettingsStore.Load(_settingsPath);
            _menuSize = _settings.MenuSize;
            _recentFilePath = _settings.RecentFile;

            // Write the file on first run so the ClipBar shortcut is discoverable
            // and editable without having to guess the key names.
            if (!File.Exists(_settingsPath))
                SettingsStore.Save(_settingsPath, _settings);

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

            SetUpClipBar();
        }

        private void SetUpClipBar()
        {
            if (!_settings.ClipBarEnabled) return;

            _clipBarHotKey = new GlobalHotKey();
            _clipBarHotKey.Pressed += ClipBarHotKey_Pressed;

            if (_clipBarHotKey.TryRegister(_settings.ClipBarHotKey)) return;

            // Another application already owns the combination. Say so once, in a
            // balloon, and carry on - ClipBar is not worth blocking startup for.
            string detail = _clipBarHotKey.LastError == GlobalHotKey.ErrorHotKeyAlreadyRegistered
                ? "another application is already using it"
                : "Windows refused the shortcut (error " + _clipBarHotKey.LastError + ")";

            _notifyIcon.ShowBalloonTip(
                10000,
                "ClipBar shortcut unavailable",
                "Could not register " + _settings.ClipBarHotKey
                    + " because " + detail + ". Edit Hotkey in " + SettingsStore.FileName
                    + " to pick another.",
                ToolTipIcon.Warning);
        }

        private void ClipBarHotKey_Pressed(object sender, EventArgs e)
        {
            if (_clipBarWindow == null || _clipBarWindow.IsDisposed)
            {
                _clipBarWindow = new ClipBarWindow(_settings);
                _clipBarWindow.EntryCopied += ClipBar_EntryCopied;
                _clipBarWindow.EditRequested += ClipBar_EditRequested;
            }

            if (_clipBarWindow.Visible)
            {
                _clipBarWindow.Hide();
                return;
            }

            _entries = SafeParse(_filePath);
            _clipBarWindow.ShowFor(_entries);
        }

        private void ClipBar_EntryCopied(object sender, ClipEntry entry)
        {
            if (entry != null)
            {
                _settings.RecordUse(entry.Title);
                SaveSettings();
            }

            if (!_settings.AutoPaste) return;

            // Give the restored window a moment to actually take focus before the
            // keystrokes are sent, or they land nowhere.
            var timer = new Timer { Interval = 120 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                AutoPaste.SendPaste();
            };
            timer.Start();
        }

        private void ClipBar_EditRequested(object sender, ClipEntry entry)
        {
            if (entry == null) return;

            _entries = SafeParse(_filePath);
            using (var dlg = new EntriesDialog(_entries, _filePath, _menuSize, false, _settings, entry.Title))
            {
                dlg.ClipBarSettingsRequested += (s, args) => ShowClipBarSettings(dlg);
                dlg.ShowDialog();
                _menuSize = dlg.MenuSize;
            }
            SaveSettings();
            RefreshMenu();
        }

        private void ClipBarSettingsItem_Click(object sender, EventArgs e)
        {
            ShowClipBarSettings(null);
        }

        /// <summary>
        /// Opens the ClipBar settings dialog and applies the result immediately.
        /// </summary>
        internal void ShowClipBarSettings(IWin32Window owner)
        {
            using (var dialog = new ClipBarSettingsDialog(_settings, IsHotKeyAvailable))
            {
                // CenterParent does nothing without an owner, which is the case when
                // the dialog is opened from the tray menu.
                dialog.StartPosition = owner == null
                    ? FormStartPosition.CenterScreen
                    : FormStartPosition.CenterParent;

                dialog.ApplyRequested += (s, e) => ApplyClipBarSettings(dialog);

                if ((owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != DialogResult.OK)
                    return;

                ApplyClipBarSettings(dialog);
            }
        }

        private void ApplyClipBarSettings(ClipBarSettingsDialog dialog)
        {
            dialog.ApplyTo(_settings);
            SaveSettings();
            ReapplyClipBar();
            dialog.NotifyApplied();
        }

        /// <summary>
        /// The shortcut we have already claimed would fail a naive probe, because we
        /// are the application holding it.
        /// </summary>
        private bool IsHotKeyAvailable(ClipBar.HotKeyDefinition definition)
        {
            if (_clipBarHotKey != null && definition.Equals(_clipBarHotKey.Current))
                return true;

            return GlobalHotKey.IsAvailable(definition);
        }

        /// <summary>
        /// Re-registers the shortcut and discards the window so appearance settings,
        /// which are applied when the handle is created, take effect next summon.
        /// </summary>
        private void ReapplyClipBar()
        {
            if (_clipBarWindow != null)
            {
                _clipBarWindow.EntryCopied -= ClipBar_EntryCopied;
                _clipBarWindow.EditRequested -= ClipBar_EditRequested;
                _clipBarWindow.Dispose();
                _clipBarWindow = null;
            }

            if (_clipBarHotKey != null)
            {
                _clipBarHotKey.Pressed -= ClipBarHotKey_Pressed;
                _clipBarHotKey.Dispose();
                _clipBarHotKey = null;
            }

            SetUpClipBar();
        }

        private void SaveSettings()
        {
            _settings.MenuSize = _menuSize;
            _settings.RecentFile = _recentFilePath;
            SettingsStore.Save(_settingsPath, _settings);
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

            var clipBarItem = new ToolStripMenuItem("ClipBar...")
            {
                Name = "clipBarSettingsItem"
            };
            clipBarItem.Click += ClipBarSettingsItem_Click;
            optionsMenu.DropDownItems.Add(clipBarItem);

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
            _settings.RecordUse(entry.Title);
            SaveSettings();
        }

        private void CopyToClipboard(ClipEntry entry)
        {
            ClipboardWriter.Copy(entry);
        }

        private void ShowAddDialog()
        {
            using (var dlg = new EntriesDialog(_entries, _filePath, _menuSize, true, _settings))
            {
                dlg.ClipBarSettingsRequested += (s, args) => ShowClipBarSettings(dlg);
                dlg.ShowDialog();
                _menuSize = dlg.MenuSize;
            }
            SaveSettings();
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
                SaveSettings();
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
            SaveSettings();
            RefreshMenu();
        }

        private void EntriesItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new EntriesDialog(_entries, _filePath, _menuSize, false, _settings))
            {
                dlg.ClipBarSettingsRequested += (s, args) => ShowClipBarSettings(dlg);
                dlg.ShowDialog();
                _menuSize = dlg.MenuSize;
            }
            SaveSettings();
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
                if (_clipBarHotKey != null)
                {
                    _clipBarHotKey.Pressed -= ClipBarHotKey_Pressed;
                    _clipBarHotKey.Dispose();
                    _clipBarHotKey = null;
                }
                if (_clipBarWindow != null)
                {
                    _clipBarWindow.Dispose();
                    _clipBarWindow = null;
                }
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
