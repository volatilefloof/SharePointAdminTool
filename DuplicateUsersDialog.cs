using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;

namespace EntraGroupsApp
{
    public partial class DuplicateUsersDialog : Form
    {
        private ListView lvDuplicates;
        private Button btnOK;
        private Label lblInstructions;

        public DuplicateUsersDialog(List<string> duplicateUsers)
        {
            Debug.WriteLine("Initializing DuplicateUsersDialog with " + (duplicateUsers?.Count ?? 0) + " duplicate users.");
            InitializeComponent();
            PopulateListView(duplicateUsers);
        }

        private void InitializeComponent()
        {
            // lblInstructions
            lblInstructions = new Label
            {
                AutoSize = true,
                Location = new Point(20, 20),
                Name = "lblInstructions",
                Size = new Size(450, 40), // Increased width and height to prevent cutoff
                MaximumSize = new Size(450, 0), // Allow vertical expansion if needed
                Text = "The user currently exists in this group. Skipping.",
                Font = new Font("Segoe UI", 10F)
            };

            // lvDuplicates
            lvDuplicates = new ListView
            {
                Columns = { new ColumnHeader { Text = "User", Width = 300 } },
                FullRowSelect = true,
                Location = new Point(20, 60), // Adjusted to account for taller label
                Name = "lvDuplicates",
                Size = new Size(350, 140), // Adjusted height to fit new layout
                TabIndex = 0,
                View = View.Details,
                Font = new Font("Segoe UI", 10F)
            };

            // btnOK
            btnOK = new Button
            {
                Location = new Point(290, 210),
                Name = "btnOK",
                Size = new Size(80, 30),
                TabIndex = 1,
                Text = "OK",
                UseVisualStyleBackColor = true
            };
            btnOK.Click += btnOK_Click;

            // DuplicateUsersDialog
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(390, 250);
            Controls.Add(lblInstructions);
            Controls.Add(lvDuplicates);
            Controls.Add(btnOK);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "DuplicateUsersDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Duplicate Users";
        }

        private void PopulateListView(List<string> duplicateUsers)
        {
            if (duplicateUsers == null)
            {
                Debug.WriteLine("duplicateUsers list is null.");
                return;
            }

            foreach (var user in duplicateUsers)
            {
                if (string.IsNullOrEmpty(user))
                {
                    Debug.WriteLine("Skipping null or empty user entry.");
                    continue;
                }
                try
                {
                    lvDuplicates.Items.Add(new ListViewItem(user));
                    Debug.WriteLine($"Added user to ListView: {user}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding user {user} to ListView: {ex.Message}");
                }
            }
        }

        private void btnOK_Click(object? sender, EventArgs e)
        {
            Debug.WriteLine("DuplicateUsersDialog OK button clicked.");
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}