using System;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public class ReplaceTypeSelectionDialog : Form
    {
        private Button btnNestedGroups;
        private Button btnNetIds;
        public string SelectedType { get; private set; } = string.Empty;

        public ReplaceTypeSelectionDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            btnNestedGroups = new Button();
            btnNetIds = new Button();
            SuspendLayout();

            // btnNestedGroups
            btnNestedGroups.Location = new Point(30, 20);
            btnNestedGroups.Size = new Size(120, 30);
            btnNestedGroups.Text = "Nested Groups";
            btnNestedGroups.UseVisualStyleBackColor = true;
            btnNestedGroups.Click += (s, e) => { SelectedType = "NestedGroups"; DialogResult = DialogResult.OK; Close(); };

            // btnNetIds
            btnNetIds.Location = new Point(160, 20);
            btnNetIds.Size = new Size(120, 30);
            btnNetIds.Text = "NetIDs";
            btnNetIds.UseVisualStyleBackColor = true;
            btnNetIds.Click += (s, e) => { SelectedType = "NetIDs"; DialogResult = DialogResult.OK; Close(); };

            // Form
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(310, 80);
            Controls.Add(btnNestedGroups);
            Controls.Add(btnNetIds);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ReplaceTypeSelectionDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Select Replacement Type";
            ResumeLayout(false);
        }
    }
}