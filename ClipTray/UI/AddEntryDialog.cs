using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private RichTextBox _textBox;
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
            Size = new Size(440, 400);
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
                Size = new Size(400, 20)
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

            _textBox = new ComposerRichTextBox
            {
                Location = new Point(12, 85),
                Size = new Size(400, 170),
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                AcceptsTab = false,
                DetectUrls = true
            };

            var toolbar = new RichTextToolbar(_textBox)
            {
                Location = new Point(12, 260),
                Size = new Size(400, 28)
            };

            _addButton = new Button
            {
                Text = "Add",
                Location = new Point(94, 325),
                Size = new Size(75, 28),
                Enabled = false
            };
            _addButton.Click += AddButton_Click;

            var pasteButton = new Button
            {
                Text = "Paste",
                Location = new Point(175, 325),
                Size = new Size(75, 28)
            };
            pasteButton.Click += (s, e) =>
            {
                RichTextHelpers.PasteRichOrPlain(_textBox);
            };

            var insertButton = new Button
            {
                Text = "Insert ▾",
                Location = new Point(256, 325),
                Size = new Size(75, 28)
            };
            TokenInsertMenu.AttachTo(insertButton, _textBox, this);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(337, 325),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            CancelButton = cancelButton;
            AcceptButton = _addButton;

            Controls.AddRange(new Control[]
            {
                titleLabel, _titleBox, textLabel, _textBox, toolbar,
                _addButton, pasteButton, insertButton, cancelButton
            });
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            var entry = new ClipEntry
            {
                Title = _titleBox.Text.Trim(),
                Text = _textBox.Text,
                Rtf = RichTextHelpers.DetectRichness(_textBox)
            };

            _entries.Add(entry);

            try
            {
                FileWriter.Write(_filePath, _entries);
            }
            catch (IOException ex)
            {
                _entries.Remove(entry);
                MessageBox.Show("Could not save entry:\n" + ex.Message,
                    "ClipTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            EntryAdded?.Invoke(this, EventArgs.Empty);

            _titleBox.Clear();
            _textBox.Clear();
            _titleBox.Focus();
        }
    }
}
