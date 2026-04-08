using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;

namespace ClipTray.UI
{
    public class EditorDialog : Form
    {
        private readonly List<ClipEntry> _entries;
        private readonly string _filePath;
        private ComboBox _comboBox;
        private TextBox _textBox;
        private Button _deleteButton;
        private Button _editButton;

        public EditorDialog(List<ClipEntry> entries, string filePath)
        {
            _entries = entries;
            _filePath = filePath;
            InitializeComponents();
            RefreshComboBox();
        }

        private void InitializeComponents()
        {
            Text = "ClipTray Editor";
            Size = new Size(450, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var nameLabel = new Label
            {
                Text = "Name of ClipTray Entry:",
                Location = new Point(12, 15),
                AutoSize = true
            };

            _comboBox = new ComboBox
            {
                Location = new Point(12, 35),
                Size = new Size(410, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _comboBox.SelectedIndexChanged += ComboBox_SelectedIndexChanged;

            var textLabel = new Label
            {
                Text = "Entry text:",
                Location = new Point(12, 65),
                AutoSize = true
            };

            _textBox = new TextBox
            {
                Location = new Point(12, 85),
                Size = new Size(410, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            _deleteButton = new Button
            {
                Text = "Delete",
                Location = new Point(12, 300),
                Size = new Size(75, 28)
            };
            _deleteButton.Click += DeleteButton_Click;

            var newButton = new Button
            {
                Text = "New...",
                Location = new Point(93, 300),
                Size = new Size(75, 28)
            };
            newButton.Click += NewButton_Click;

            _editButton = new Button
            {
                Text = "Edit Current...",
                Location = new Point(174, 300),
                Size = new Size(105, 28)
            };
            _editButton.Click += EditButton_Click;

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(347, 300),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK
            };

            AcceptButton = okButton;

            Controls.AddRange(new Control[]
            {
                nameLabel, _comboBox, textLabel, _textBox,
                _deleteButton, newButton, _editButton, okButton
            });
        }

        private void RefreshComboBox()
        {
            _comboBox.Items.Clear();
            foreach (var entry in _entries)
                _comboBox.Items.Add(entry.Title);

            if (_comboBox.Items.Count > 0)
                _comboBox.SelectedIndex = 0;
            else
                UpdateButtonState();
        }

        private void ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_comboBox.SelectedIndex >= 0 && _comboBox.SelectedIndex < _entries.Count)
                _textBox.Text = _entries[_comboBox.SelectedIndex].Text;
            else
                _textBox.Clear();

            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            bool hasSelection = _comboBox.SelectedIndex >= 0;
            _deleteButton.Enabled = hasSelection;
            _editButton.Enabled = hasSelection;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (_comboBox.SelectedIndex < 0)
                return;

            var title = _entries[_comboBox.SelectedIndex].Title;
            var result = MessageBox.Show(
                "Delete entry \"" + title + "\"?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _entries.RemoveAt(_comboBox.SelectedIndex);
                FileWriter.Write(_filePath, _entries);
                RefreshComboBox();
            }
        }

        private void NewButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddEntryDialog(_entries, _filePath))
            {
                dlg.EntryAdded += (s, ev) => RefreshComboBox();
                dlg.ShowDialog(this);
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            if (_comboBox.SelectedIndex < 0)
                return;

            var entry = _entries[_comboBox.SelectedIndex];
            using (var dlg = new EditEntryDialog(entry, _entries, _filePath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    RefreshComboBox();
            }
        }
    }
}
