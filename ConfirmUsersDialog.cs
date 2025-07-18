using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class ConfirmUsersDialog : Form, IDisposable
    {
        public List<User> SelectedUsers { get; private set; }
        public bool EditListRequested { get; private set; }
        private readonly ListView lvUsers;
        private readonly Button btnConfirm;
        private readonly Button btnCancel;
        private readonly Button btnBack;
        private readonly Label lblInstructions;
        private readonly List<(string Input, User User)> _matchedUsers;
        private bool _disposed;

        public ConfirmUsersDialog(List<(string Input, User User)> matchedUsers)
        {
            SelectedUsers = new List<User>();
            EditListRequested = false;
            _matchedUsers = matchedUsers ?? throw new ArgumentNullException(nameof(matchedUsers));
            lvUsers = new ListView();
            btnConfirm = new Button();
            btnCancel = new Button();
            btnBack = new Button();
            lblInstructions = new Label();
            InitializeComponent();
            PopulateListView(matchedUsers);
        }

        private void InitializeComponent()
        {
            lblInstructions.AutoSize = true;
            lblInstructions.Location = new Point(20, 20);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new Size(550, 20);
            lblInstructions.Text = "Select users to add (deselect to skip):";
            lblInstructions.Font = new Font("Segoe UI", 10F);

            lvUsers.CheckBoxes = true;
            lvUsers.Columns.Add("Input", 150);
            lvUsers.Columns.Add("Display Name", 200);
            lvUsers.Columns.Add("Email", 200);
            lvUsers.FullRowSelect = true;
            lvUsers.Location = new Point(20, 50);
            lvUsers.Name = "lvUsers";
            lvUsers.Size = new Size(550, 220);
            lvUsers.TabIndex = 0;
            lvUsers.View = View.Details;
            lvUsers.Font = new Font("Segoe UI", 10F);

            btnConfirm.Location = new Point(400, 280);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(80, 30);
            btnConfirm.TabIndex = 1;
            btnConfirm.Text = "Confirm";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += btnConfirm_Click;

            btnCancel.Location = new Point(490, 280);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(80, 30);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;

            btnBack.Location = new Point(310, 280);
            btnBack.Name = "btnBack";
            btnBack.Size = new Size(80, 30);
            btnBack.TabIndex = 3;
            btnBack.Text = "Back";
            btnBack.UseVisualStyleBackColor = true;
            btnBack.Click += btnBack_Click;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 350);
            Controls.Add(lblInstructions);
            Controls.Add(lvUsers);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            Controls.Add(btnBack);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ConfirmUsersDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Confirm Users";
        }

        private void PopulateListView(List<(string Input, User User)> matchedUsers)
        {
            foreach (var (input, user) in matchedUsers)
            {
                var item = new ListViewItem(input);
                item.SubItems.Add(user?.DisplayName ?? "Not Found");
                item.SubItems.Add(user?.UserPrincipalName ?? string.Empty);
                item.Tag = user;
                item.Checked = user != null;
                lvUsers.Items.Add(item);
            }
        }

        private void btnConfirm_Click(object? sender, EventArgs e)
        {
            SelectedUsers = lvUsers.Items.Cast<ListViewItem>()
                .Where(item => item.Checked && item.Tag is User user && user != null)
                .Select(item => (User)item.Tag)
                .ToList();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnBack_Click(object? sender, EventArgs e)
        {
            EditListRequested = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Unsubscribe from events
                btnConfirm.Click -= btnConfirm_Click;
                btnCancel.Click -= btnCancel_Click;
                btnBack.Click -= btnBack_Click;

                // Dispose controls
                lvUsers?.Dispose();
                btnConfirm?.Dispose();
                btnCancel?.Dispose();
                btnBack?.Dispose();
                lblInstructions?.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        ~ConfirmUsersDialog()
        {
            Dispose(false);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}