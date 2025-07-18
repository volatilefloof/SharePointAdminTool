using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class InvalidUsersDialog : Form
    {
        public bool SkipInvalid { get; private set; }
        private readonly ListView lvInvalidUsers;
        private readonly Button btnSkipInvalid;
        private readonly Button btnEditList;
        private readonly Label lblInstructions;

        public InvalidUsersDialog(List<string> invalidUsers, int validCount)
        {
            SkipInvalid = false;
            lvInvalidUsers = new ListView();
            btnSkipInvalid = new Button();
            btnEditList = new Button();
            lblInstructions = new Label();
            InitializeComponent();
            PopulateListView(invalidUsers);
            lblInstructions.Text = $"Found {validCount} valid user(s). The following user(s) are invalid:";
        }

        private void InitializeComponent()
        {
            // lblInstructions
            lblInstructions.AutoSize = true;
            lblInstructions.Location = new Point(20, 20);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new Size(350, 20);
            lblInstructions.Text = "Found valid users. The following user(s) are invalid:";
            lblInstructions.Font = new Font("Segoe UI", 10F);

            // lvInvalidUsers
            lvInvalidUsers.Columns.Add("User", 300);
            lvInvalidUsers.FullRowSelect = true;
            lvInvalidUsers.Location = new Point(20, 50);
            lvInvalidUsers.Name = "lvInvalidUsers";
            lvInvalidUsers.Size = new Size(350, 150);
            lvInvalidUsers.TabIndex = 0;
            lvInvalidUsers.View = View.Details;
            lvInvalidUsers.Font = new Font("Segoe UI", 10F);

            // btnSkipInvalid
            btnSkipInvalid.Location = new Point(200, 210);
            btnSkipInvalid.Name = "btnSkipInvalid";
            btnSkipInvalid.Size = new Size(80, 30);
            btnSkipInvalid.TabIndex = 1;
            btnSkipInvalid.Text = "Skip Invalid";
            btnSkipInvalid.UseVisualStyleBackColor = true;
            btnSkipInvalid.Click += btnSkipInvalid_Click;

            // btnEditList
            btnEditList.Location = new Point(290, 210);
            btnEditList.Name = "btnEditList";
            btnEditList.Size = new Size(80, 30);
            btnEditList.TabIndex = 2;
            btnEditList.Text = "Edit List";
            btnEditList.UseVisualStyleBackColor = true;
            btnEditList.Click += btnEditList_Click;

            // InvalidUsersDialog
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(390, 250);
            Controls.Add(lblInstructions);
            Controls.Add(lvInvalidUsers);
            Controls.Add(btnSkipInvalid);
            Controls.Add(btnEditList);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "InvalidUsersDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Invalid Users";
        }

        private void PopulateListView(List<string> invalidUsers)
        {
            foreach (var user in invalidUsers)
            {
                lvInvalidUsers.Items.Add(new ListViewItem(user));
            }
        }

        private void btnSkipInvalid_Click(object? sender, EventArgs e)
        {
            SkipInvalid = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnEditList_Click(object? sender, EventArgs e)
        {
            SkipInvalid = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}