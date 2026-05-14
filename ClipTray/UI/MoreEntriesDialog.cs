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
    public class MoreEntriesDialog : Form
    {
        private readonly List<ClipEntry> _entries;
        private readonly string _filePath;
        private readonly bool _previewMode;
        private ListBox _listBox;
        private Button _moveUpButton;
        private Button _moveDownButton;
        private NumericUpDown _menuSizeUpDown;

        public int MenuSize
        {
            get { return (int)_menuSizeUpDown.Value; }
        }

        public MoreEntriesDialog(List<ClipEntry> entries, string filePath, int menuSize, bool previewMode)
        {
            _entries = entries;
            _filePath = filePath;
            _previewMode = previewMode;
            InitializeComponents(menuSize);
            RefreshListBox(-1);
        }

        private void InitializeComponents(int menuSize)
        {
            Text = "More ClipTray Entries";
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
            _listBox.SelectedIndexChanged += (s, e) => UpdateMoveButtons();

            _moveUpButton = new Button
            {
                Text = "Move Up",
                Location = new Point(300, 12),
                Size = new Size(100, 28)
            };
            _moveUpButton.Click += MoveUpButton_Click;

            _moveDownButton = new Button
            {
                Text = "Move Down",
                Location = new Point(300, 46),
                Size = new Size(100, 28)
            };
            _moveDownButton.Click += MoveDownButton_Click;

            var editButton = new Button
            {
                Text = "Edit...",
                Location = new Point(300, 90),
                Size = new Size(100, 28)
            };
            editButton.Click += EditButton_Click;

            var copyButton = new Button
            {
                Text = "Copy",
                Location = new Point(300, 124),
                Size = new Size(100, 28)
            };
            copyButton.Click += CopyButton_Click;

            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(300, 168),
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
                _listBox, _moveUpButton, _moveDownButton,
                editButton, copyButton, closeButton,
                menuSizeLabel, _menuSizeUpDown, itemsLabel
            });
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

            UpdateMoveButtons();
        }

        private void UpdateMoveButtons()
        {
            int idx = _listBox.SelectedIndex;
            _moveUpButton.Enabled = idx > 0;
            _moveDownButton.Enabled = idx >= 0 && idx < _entries.Count - 1;
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

        private void EditButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new EditorDialog(_entries, _filePath))
            {
                dlg.ShowDialog(this);
            }
            RefreshListBox(_listBox.SelectedIndex);
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            int idx = _listBox.SelectedIndex;
            if (idx < 0 || idx >= _entries.Count) return;

            var entry = _entries[idx];
            if (string.IsNullOrEmpty(entry.Text)) return;

            var resolved = TokenSubstitution.Resolve(entry.Text);

            try
            {
                Clipboard.SetText(resolved);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return;
            }

            if (_previewMode)
            {
                using (var dlg = new PreviewDialog(entry.Title, resolved))
                    dlg.ShowDialog(this);
            }
        }
    }
}
