using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;
using ClipTray.Tokens;

namespace ClipTray.UI
{
    public class EntriesDialog : Form
    {
        private readonly List<ClipEntry> _entries;
        private readonly string _filePath;
        private ListBox _listBox;
        private Button _moveUpButton;
        private Button _moveDownButton;
        private Button _newButton;
        private Button _editButton;
        private Button _deleteButton;
        private Button _copyButton;
        private Button _previewButton;
        private NumericUpDown _menuSizeUpDown;

        public int MenuSize
        {
            get { return (int)_menuSizeUpDown.Value; }
        }

        public EntriesDialog(List<ClipEntry> entries, string filePath, int menuSize)
        {
            _entries = entries;
            _filePath = filePath;
            InitializeComponents(menuSize);
            RefreshListBox(-1);
        }

        private void InitializeComponents(int menuSize)
        {
            Text = "Entries";
            Size = new Size(420, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            _listBox = new ListBox
            {
                Location = new Point(12, 12),
                Size = new Size(280, 300)
            };
            _listBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();
            _listBox.DoubleClick += (s, e) => { if (_listBox.SelectedIndex >= 0) EditButton_Click(s, e); };

            _moveUpButton = MakeButton("Move Up", 12, MoveUpButton_Click);
            _moveDownButton = MakeButton("Move Down", 46, MoveDownButton_Click);

            _newButton = MakeButton("New...", 90, NewButton_Click);
            _editButton = MakeButton("Edit...", 124, EditButton_Click);
            _deleteButton = MakeButton("Delete", 158, DeleteButton_Click);

            _copyButton = MakeButton("Copy", 200, CopyButton_Click);
            _previewButton = MakeButton("Preview", 234, PreviewButton_Click);

            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(300, 278),
                Size = new Size(100, 28),
                DialogResult = DialogResult.OK
            };

            var menuSizeLabel = new Label
            {
                Text = "Menu Size:",
                Location = new Point(12, 325),
                AutoSize = true
            };

            _menuSizeUpDown = new NumericUpDown
            {
                Location = new Point(85, 323),
                Size = new Size(60, 20),
                Minimum = 1,
                Maximum = 100,
                Value = menuSize
            };

            var itemsLabel = new Label
            {
                Text = "Items",
                Location = new Point(150, 325),
                AutoSize = true
            };

            AcceptButton = closeButton;

            Controls.AddRange(new Control[]
            {
                _listBox,
                _moveUpButton, _moveDownButton,
                _newButton, _editButton, _deleteButton,
                _copyButton, _previewButton,
                closeButton,
                menuSizeLabel, _menuSizeUpDown, itemsLabel
            });
        }

        private Button MakeButton(string text, int y, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(300, y),
                Size = new Size(100, 28)
            };
            btn.Click += onClick;
            return btn;
        }

        private void RefreshListBox(int selectIndex)
        {
            _listBox.Items.Clear();
            for (int i = 0; i < _entries.Count; i++)
                _listBox.Items.Add((i + 1) + ": " + _entries[i].Title);

            if (selectIndex >= 0 && selectIndex < _listBox.Items.Count)
                _listBox.SelectedIndex = selectIndex;
            else if (_listBox.Items.Count > 0)
                _listBox.SelectedIndex = 0;

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            int idx = _listBox.SelectedIndex;
            bool hasSelection = idx >= 0;

            _moveUpButton.Enabled = idx > 0;
            _moveDownButton.Enabled = hasSelection && idx < _entries.Count - 1;
            _editButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
            _copyButton.Enabled = hasSelection;
            _previewButton.Enabled = hasSelection;
        }

        private void MoveUpButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx <= 0) return;

            var temp = _entries[idx];
            _entries[idx] = _entries[idx - 1];
            _entries[idx - 1] = temp;

            if (!SafeWrite()) { _entries[idx - 1] = _entries[idx]; _entries[idx] = temp; return; }
            RefreshListBox(idx - 1);
        }

        private void MoveDownButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx < 0 || idx >= _entries.Count - 1) return;

            var temp = _entries[idx];
            _entries[idx] = _entries[idx + 1];
            _entries[idx + 1] = temp;

            if (!SafeWrite()) { _entries[idx + 1] = _entries[idx]; _entries[idx] = temp; return; }
            RefreshListBox(idx + 1);
        }

        private bool SafeWrite()
        {
            try
            {
                FileWriter.Write(_filePath, _entries);
                return true;
            }
            catch (IOException ex)
            {
                MessageBox.Show("Could not save file:\n" + ex.Message,
                    "ClipTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void NewButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddEntryDialog(_entries, _filePath))
            {
                dlg.EntryAdded += (s, ev) => RefreshListBox(_entries.Count - 1);
                dlg.ShowDialog(this);
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx < 0 || idx >= _entries.Count) return;

            using (var dlg = new EditEntryDialog(_entries[idx], _entries, _filePath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    RefreshListBox(idx);
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx < 0 || idx >= _entries.Count) return;

            var entry = _entries[idx];
            var result = MessageBox.Show(this,
                "Delete entry \"" + entry.Title + "\"?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            _entries.RemoveAt(idx);
            if (!SafeWrite()) { _entries.Insert(idx, entry); return; }

            int nextSelect = Math.Min(idx, _entries.Count - 1);
            RefreshListBox(nextSelect);
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx < 0 || idx >= _entries.Count) return;

            var entry = _entries[idx];
            if (string.IsNullOrEmpty(entry.Text)) return;

            var resolvedText = TokenSubstitution.Resolve(entry.Text);

            try
            {
                if (!string.IsNullOrEmpty(entry.Rtf))
                {
                    var resolvedRtf = TokenSubstitution.ResolveRtf(entry.Rtf);
                    var data = new DataObject();
                    data.SetData(DataFormats.Rtf, resolvedRtf);
                    data.SetData(DataFormats.UnicodeText, resolvedText);
                    Clipboard.SetDataObject(data, true);
                }
                else
                {
                    Clipboard.SetText(resolvedText);
                }
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }

        private void PreviewButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx < 0 || idx >= _entries.Count) return;

            var entry = _entries[idx];
            var resolvedText = TokenSubstitution.Resolve(entry.Text ?? "");
            string resolvedRtf = string.IsNullOrEmpty(entry.Rtf)
                ? null
                : TokenSubstitution.ResolveRtf(entry.Rtf);

            using (var dlg = new PreviewDialog(entry.Title, resolvedText, resolvedRtf))
                dlg.ShowDialog(this);
        }
    }
}
