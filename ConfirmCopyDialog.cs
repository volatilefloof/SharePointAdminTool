using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class ConfirmCopyDialog : Form
    {
        public ConfirmCopyDialog(List<(string Id, string DisplayName)> users, List<Group> targetGroups)
        {
            InitializeComponent();
            LoadData(users, targetGroups);
        }

        private void LoadData(List<(string Id, string DisplayName)> users, List<Group> targetGroups)
        {
            txtUsers.Text = string.Join(Environment.NewLine, users.Select(u => u.DisplayName));
            txtGroups.Text = string.Join(Environment.NewLine, targetGroups.Select(g => g.DisplayName));
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}