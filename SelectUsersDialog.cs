using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class SelectUserDialog : Form
    {
        private readonly GraphServiceClient _graphClient;
        public User SelectedUser { get; private set; }

        public SelectUserDialog(GraphServiceClient graphClient)
        {
            InitializeComponent();
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            string input = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Please enter an email or NetID.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                lblStatus.Text = "Searching...";
                lblStatus.Visible = true;

                string searchTerm = input.Contains("@") ? input : $"{input}@tamu.edu";
                var users = await _graphClient.Users.GetAsync(request =>
                {
                    request.QueryParameters.Filter = $"userPrincipalName eq '{searchTerm}' or mail eq '{searchTerm}'";
                    request.QueryParameters.Select = new[] { "id", "displayName", "userPrincipalName", "mail" };
                });

                if (users?.Value?.Count > 0)
                {
                    SelectedUser = users.Value[0];
                    lblResult.Text = $"Found: {SelectedUser.DisplayName} ({SelectedUser.Mail ?? SelectedUser.UserPrincipalName})";
                    lblResult.Visible = true;
                    btnOK.Enabled = true;
                }
                else
                {
                    lblResult.Text = "No user found.";
                    lblResult.Visible = true;
                    btnOK.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching user: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblResult.Text = "Search failed.";
                lblResult.Visible = true;
                btnOK.Enabled = false;
            }
            finally
            {
                Cursor = Cursors.Default;
                lblStatus.Visible = false;
            }
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