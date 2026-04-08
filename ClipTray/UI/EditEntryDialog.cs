using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;

namespace ClipTray.UI
{
    public class EditEntryDialog : Form
    {
        private readonly ClipEntry _entry;
        private readonly List<ClipEntry> _entries;
        private readonly string _filePath;
        private TextBox _titleBox;
        private TextBox _textBox;

        public EditEntryDialog(ClipEntry entry, List<ClipEntry> entries, string filePath)
        {
            _entry = entry;
            _entries = entries;
            _filePath = filePath;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = "Edit - \"" + _entry.Title + "\"";
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
                Text = _entry.Title,
                Location = new Point(12, 35),
                Size = new Size(360, 20)
            };

            var textLabel = new Label
            {
                Text = "Entry text:",
                Location = new Point(12, 65),
                AutoSize = true
            };

            _textBox = new TextBox
            {
                Text = _entry.Text,
                Location = new Point(12, 85),
                Size = new Size(360, 170),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };

            var saveButton = new Button
            {
                Text = "Save",
                Location = new Point(216, 270),
                Size = new Size(75, 28)
            };
            saveButton.Click += SaveButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(297, 270),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            Controls.AddRange(new Control[]
            {
                titleLabel, _titleBox, textLabel, _textBox,
                saveButton, cancelButton
            });
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            _entry.Title = _titleBox.Text.Trim();
            _entry.Text = _textBox.Text;
            FileWriter.Write(_filePath, _entries);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
