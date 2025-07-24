using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class SiteAdminForm : System.Windows.Forms.Form
    {
        private readonly IPublicClientApplication _pca;
        private readonly System.Windows.Forms.Form _parentForm;
        private readonly AuditLogManager _auditLogManager;
        private readonly string _signedInUserId;
        private readonly string _siteUrl = "https://tamucs.sharepoint.com/teams/MKTGTeamSite";
        private readonly List<string> _departments = new List<string> { "MKTG" };

        public SiteAdminForm(IPublicClientApplication pca, System.Windows.Forms.Form parentForm, AuditLogManager auditLogManager, string signedInUserId)
        {
            InitializeComponent();
            _pca = pca;
            _parentForm = parentForm;
            _auditLogManager = auditLogManager;
            _signedInUserId = signedInUserId;
            InitializeDepartments();
        }

        private void InitializeDepartments()
        {
            cmbDepartments.Items.AddRange(_departments.ToArray());
            cmbDepartments.SelectedIndex = -1; // No selection by default
            btnLoad.Enabled = false;
            cmbDepartments.SelectedIndexChanged += cmbDepartments_SelectedIndexChanged;
        }

        private void cmbDepartments_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnLoad.Enabled = cmbDepartments.SelectedIndex != -1;
        }

        private async void menuItemViewSubfolderPermissions_Click(object sender, EventArgs e)
        {
            await ViewSelectedLibrarySubfolderPermissionsAsync();
        }

        private async void btnViewSubfolderPermissions_Click(object sender, EventArgs e)
        {
            await ViewSelectedLibrarySubfolderPermissionsAsync();
        }

        private async Task ViewSelectedLibrarySubfolderPermissionsAsync()
        {
            if (lstLibraries.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a document library.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string libraryName = lstLibraries.SelectedItems[0].Text;
            lblStatus.Text = $"Loading subfolder permissions for '{libraryName}'...";
            progressBar.Visible = true;
            progressBar.Value = 0;

            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) =>
                    {
                        e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };

                    var web = context.Web;
                    var lists = web.Lists;
                    context.Load(lists, l => l.Include(list => list.Title));
                    await context.ExecuteQueryAsync();

                    var library = lists.FirstOrDefault(l => l.Title == libraryName);
                    if (library == null)
                    {
                        MessageBox.Show("Library not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var folder = library.RootFolder;
                    context.Load(folder, f => f.Folders.Include(f => f.Name, f => f.ListItemAllFields.HasUniqueRoleAssignments, f => f.ListItemAllFields.RoleAssignments.Include(
                        ra => ra.Member.Title,
                        ra => ra.Member.LoginName,
                        ra => ra.RoleDefinitionBindings)));
                    await context.ExecuteQueryAsync();

                    var subfolders = folder.Folders.Where(f => !f.Name.StartsWith("Forms")).ToList(); // Exclude system folders
                    if (!subfolders.Any())
                    {
                        MessageBox.Show("No subfolders found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    progressBar.Maximum = subfolders.Count;
                    progressBar.Value = 0;

                    var subfolderPermissions = new List<SubfolderPermissionInfo>();
                    foreach (var subfolder in subfolders)
                    {
                        var perms = new List<string>();
                        foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                        {
                            if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase))
                            {
                                var member = ra.Member.Title ?? ra.Member.LoginName;
                                var roles = ra.RoleDefinitionBindings.Select(r => r.Name).ToList();
                                perms.Add($"{member}: {string.Join(", ", roles)}");
                            }
                        }

                        if (perms.Any())
                        {
                            subfolderPermissions.Add(new SubfolderPermissionInfo
                            {
                                SubfolderName = subfolder.Name,
                                IsInherited = !subfolder.ListItemAllFields.HasUniqueRoleAssignments,
                                Permissions = perms
                            });
                        }
                        progressBar.Value++;
                    }

                    if (!subfolderPermissions.Any())
                    {
                        MessageBox.Show("No CSG group permissions found for subfolders.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    using (var dialog = new SubfolderPermissionsDialog(libraryName, subfolderPermissions))
                    {
                        dialog.ShowDialog(this);
                    }

                    lblStatus.Text = $"Loaded subfolder permissions for '{libraryName}'.";
                    await _auditLogManager.LogAction(_signedInUserId, null, "ViewSubfolderPermissions", libraryName, null, "DocumentLibrary", $"Viewed subfolder permissions for {libraryName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load subfolder permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error loading subfolder permissions.";
                await _auditLogManager.LogAction(_signedInUserId, null, "ViewSubfolderPermissionsError", null, null, null, $"Failed to load subfolder permissions: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private void lstLibraries_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = lstLibraries.HitTest(e.Location).Item;
                if (item != null)
                {
                    item.Selected = true;
                    contextMenuLibraries.Show(lstLibraries, e.Location);
                }
            }
        }

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            if (cmbDepartments.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a department.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                "Would you like to load all document libraries?\n\nClick Yes to load all, No to select from a list.",
                "Load Options",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return;

            if (result == DialogResult.Yes)
            {
                await LoadDocumentLibrariesAsync();
            }
            else if (result == DialogResult.No)
            {
                List<string> libraryNames = await GetDocumentLibraryNamesAsync();
                if (libraryNames.Count == 0)
                {
                    MessageBox.Show("No document libraries found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dialog = new LibrarySelectionDialog(libraryNames))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedLibraries.Count > 0)
                    {
                        await LoadDocumentLibrariesAsync(dialog.SelectedLibraries);
                    }
                }
            }
        }

        private async Task<List<string>> GetDocumentLibraryNamesAsync()
        {
            var names = new List<string>();
            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) =>
                    {
                        e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };

                    var web = context.Web;
                    var lists = web.Lists;
                    context.Load(lists, l => l.Include(list => list.Title, list => list.BaseType));
                    await context.ExecuteQueryAsync();

                    names = lists.Where(l => l.BaseType == BaseType.DocumentLibrary)
                                 .Select(l => l.Title)
                                 .ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch library names: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return names;
        }

        private async Task LoadDocumentLibrariesAsync()
        {
            try
            {
                lstLibraries.Items.Clear();
                lblStatus.Text = "Loading document libraries...";
                progressBar.Visible = true;
                progressBar.Value = 0;

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) =>
                    {
                        e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };

                    var web = context.Web;
                    var lists = web.Lists;
                    context.Load(lists, l => l.Include(
                        list => list.Title,
                        list => list.BaseType,
                        list => list.HasUniqueRoleAssignments,
                        list => list.RoleAssignments.Include(
                            ra => ra.Member.Title,
                            ra => ra.Member.LoginName,
                            ra => ra.RoleDefinitionBindings)));
                    await context.ExecuteQueryAsync();

                    var documentLibraries = lists.Where(l => l.BaseType == BaseType.DocumentLibrary).ToList();
                    progressBar.Maximum = documentLibraries.Count;
                    progressBar.Value = 0;

                    foreach (var library in documentLibraries)
                    {
                        var permissions = new List<string>();
                        foreach (var roleAssignment in library.RoleAssignments)
                        {
                            if (roleAssignment.Member.Title != null && roleAssignment.Member.Title.StartsWith("FSG-", StringComparison.OrdinalIgnoreCase))
                            {
                                var memberName = roleAssignment.Member.Title ?? roleAssignment.Member.LoginName;
                                var roles = string.Join(", ", roleAssignment.RoleDefinitionBindings.Select(rdb => rdb.Name));
                                permissions.Add($"{memberName}: {roles}");
                            }
                        }

                        var item = new ListViewItem(library.Title);
                        item.SubItems.Add(string.Join("; ", permissions));
                        lstLibraries.Items.Add(item);
                        progressBar.Value++;
                    }

                    string dept = cmbDepartments.SelectedItem?.ToString() ?? "N/A";
                    lblStatus.Text = $"Loaded {lstLibraries.Items.Count} document libraries for {dept}.";
                    await _auditLogManager.LogAction(_signedInUserId, null, "LoadDocumentLibraries", null, null, null, $"Loaded {lstLibraries.Items.Count} document libraries for {dept}");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading document libraries.";
                MessageBox.Show($"Failed to load document libraries: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadDocumentLibrariesError", null, null, null, $"Failed to load document libraries: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task LoadDocumentLibrariesAsync(List<string> filterLibraries)
        {
            try
            {
                lstLibraries.Items.Clear();
                lblStatus.Text = "Loading selected document libraries...";
                progressBar.Visible = true;
                progressBar.Value = 0;

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) =>
                    {
                        e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };

                    var web = context.Web;
                    var lists = web.Lists;
                    context.Load(lists, l => l.Include(
                        list => list.Title,
                        list => list.BaseType,
                        list => list.HasUniqueRoleAssignments,
                        list => list.RoleAssignments.Include(
                            ra => ra.Member.Title,
                            ra => ra.Member.LoginName,
                            ra => ra.RoleDefinitionBindings)));
                    await context.ExecuteQueryAsync();

                    var documentLibraries = lists
                        .Where(l => l.BaseType == BaseType.DocumentLibrary && filterLibraries.Contains(l.Title))
                        .ToList();

                    progressBar.Maximum = documentLibraries.Count;
                    progressBar.Value = 0;

                    foreach (var library in documentLibraries)
                    {
                        var permissions = new List<string>();
                        foreach (var roleAssignment in library.RoleAssignments)
                        {
                            if (roleAssignment.Member.Title != null && roleAssignment.Member.Title.StartsWith("FSG-", StringComparison.OrdinalIgnoreCase))
                            {
                                var memberName = roleAssignment.Member.Title ?? roleAssignment.Member.LoginName;
                                var roles = string.Join(", ", roleAssignment.RoleDefinitionBindings.Select(rdb => rdb.Name));
                                permissions.Add($"{memberName}: {roles}");
                            }
                        }

                        var item = new ListViewItem(library.Title);
                        item.SubItems.Add(string.Join("; ", permissions));
                        lstLibraries.Items.Add(item);
                        progressBar.Value++;
                    }

                    string dept = cmbDepartments.SelectedItem?.ToString() ?? "N/A";
                    lblStatus.Text = $"Loaded {lstLibraries.Items.Count} selected document libraries for {dept}.";
                    await _auditLogManager.LogAction(_signedInUserId, null, "LoadSelectedDocumentLibraries", null, null, null, $"Loaded {lstLibraries.Items.Count} selected document libraries for {dept}");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading document libraries.";
                MessageBox.Show($"Failed to load document libraries: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadSelectedDocumentLibrariesError", null, null, null, $"Failed to load document libraries: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnReturn_Click(object sender, EventArgs e)
        {
            try
            {
                if (_parentForm != null && !_parentForm.IsDisposed)
                {
                    _parentForm.Show();
                }
                else
                {
                    throw new InvalidOperationException("Parent form is null or disposed.");
                }

                await _auditLogManager.LogAction(_signedInUserId, null, "ReturnToMainForm", null, null, null, "Returned to main form from Site Administration Form");
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to return to main form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "ReturnToMainFormError", null, null, null, $"Failed to return to main form: {ex.Message}");
            }
        }

        private async void btnCreateLibrary_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Creating document library...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                await _auditLogManager.LogAction(_signedInUserId, null, "CreateDocumentLibrary", null, null, null, "Document library creation initiated");
                MessageBox.Show("Document library creation not implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create document library: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "CreateDocumentLibraryError", null, null, null, $"Failed to create document library: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnApplyGroup_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Applying security group...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                await _auditLogManager.LogAction(_signedInUserId, null, "ApplySecurityGroup", null, null, null, "Security group application initiated");
                MessageBox.Show("Applying security group not implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply security group: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "ApplySecurityGroupError", null, null, null, $"Failed to apply security group: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnDeleteLibrary_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Deleting document library...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                await _auditLogManager.LogAction(_signedInUserId, null, "DeleteDocumentLibrary", null, null, null, "Document library deletion initiated");
                MessageBox.Show("Document library deletion not implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete document library: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "DeleteDocumentLibraryError", null, null, null, $"Failed to create document library: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnAddToNavigation_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Adding to navigation...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                await _auditLogManager.LogAction(_signedInUserId, null, "AddToNavigation", null, null, null, "Add to navigation initiated");
                MessageBox.Show("Adding to navigation not implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add to navigation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "AddToNavigationError", null, null, null, $"Failed to add to navigation: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnRemoveFromNavigation_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Removing from navigation...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveFromNavigation", null, null, null, "Remove from navigation initiated");
                MessageBox.Show("Removing from navigation not implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove from navigation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveFromNavigationError", null, null, null, $"Failed to remove from navigation: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void btnExportPermissions_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Exporting permissions...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                await _auditLogManager.LogAction(_signedInUserId, null, "ExportPermissions", null, null, null, "Permissions export initiated");
                MessageBox.Show("Exporting permissions not implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "ExportPermissionsError", null, null, null, $"Failed to export permissions: {ex.Message}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < lstLibraries.Items.Count; i++)
                {
                    lstLibraries.Items[i].Selected = true;
                }
                lblStatus.Text = "All libraries selected.";
                _auditLogManager.LogAction(_signedInUserId, null, "SelectAllLibraries", null, null, null, "Selected all document libraries");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to select all libraries: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _auditLogManager.LogAction(_signedInUserId, null, "SelectAllLibrariesError", null, null, null, $"Failed to select all libraries: {ex.Message}");
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}