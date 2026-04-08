using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;

namespace ClipTray.UI
{
    public class AddEntryDialog : Form
    {
        private readonly List<ClipEntry> _entries;
        private readonly string _filePath;
        private TextBox _titleBox;
        private TextBox _textBox;
        private Button _addButton;

        public event EventHandler EntryAdded;

        public AddEntryDialog(List<ClipEntry> entries, string filePath)
        {
            _entries = entries;
            _filePath = filePath;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = "Add New ClipTray Entry";
            Size = new Size(400, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var titleLabel = new Label
            {
                Text = "Name of ClipTray Entry:",
                Location = new Point(12, 15),
                AutoSize = true
            };

            _titleBox = new TextBox
            {
                Location = new Point(12, 35),
                Size = new Size(360, 20)
            };
            _titleBox.TextChanged += (s, e) =>
            {
                _addButton.Enabled = !string.IsNullOrWhiteSpace(_titleBox.Text);
            };

            var textLabel = new Label
            {
                Text = "Entry Text:",
                Location = new Point(12, 65),
                AutoSize = true
            };

            _textBox = new TextBox
            {
                Location = new Point(12, 85),
                Size = new Size(360, 170),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };

            _addButton = new Button
            {
                Text = "Add",
                Location = new Point(135, 270),
                Size = new Size(75, 28),
                Enabled = false
            };
            _addButton.Click += AddButton_Click;

            var pasteButton = new Button
            {
                Text = "Paste",
                Location = new Point(216, 270),
                Size = new Size(75, 28)
            };
            pasteButton.Click += (s, e) =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                        _textBox.Text = Clipboard.GetText();
                }
                catch (System.Runtime.InteropServices.ExternalException) { }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(297, 270),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            CancelButton = cancelButton;
            AcceptButton = _addButton;

            Controls.AddRange(new Control[]
            {
                titleLabel, _titleBox, textLabel, _textBox,
                _addButton, pasteButton, cancelButton
            });
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            var entry = new ClipEntry
            {
                Title = _titleBox.Text.Trim(),
                Text = _textBox.Text
            };

            _entries.Add(entry);
            FileWriter.Write(_filePath, _entries);

            EntryAdded?.Invoke(this, EventArgs.Empty);

            _titleBox.Clear();
            _textBox.Clear();
            _titleBox.Focus();
        }
    }
}
