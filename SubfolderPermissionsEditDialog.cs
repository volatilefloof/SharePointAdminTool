using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Graph;
using System.Text;
using Azure.Core;

namespace EntraGroupsApp
{
    public partial class SubfolderPermissionsEditDialog : System.Windows.Forms.Form
    {
        private readonly string _libraryName;
        private readonly IPublicClientApplication _pca;
        private readonly string _siteUrl;
        private readonly GraphServiceClient _graphClient;
        private readonly AuditLogManager _auditLogManager;
        private readonly string _signedInUserId;
        private List<GroupItem> _availableGroups;

        public SubfolderPermissionsEditDialog(string libraryName, IPublicClientApplication pca, string siteUrl, GraphServiceClient graphClient, AuditLogManager auditLogManager, string signedInUserId)
        {
            _libraryName = libraryName;
            _pca = pca;
            _siteUrl = siteUrl;
            _graphClient = graphClient;
            _auditLogManager = auditLogManager;
            _signedInUserId = signedInUserId;
            InitializeComponent();
            this.Text = $"Edit Subfolder Permissions: {libraryName}";
            LoadAvailableGroupsAsync();
            LoadCurrentPermissionsAsync();
        }

        private class GroupItem
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
        }

        private async void LoadAvailableGroupsAsync()
        {
            try
            {
                lblStatus.Text = "Loading groups...";
                var groups = await _graphClient.Groups.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = "startswith(displayName, 'CSG-CLBA-MKTG')";
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName" };
                    requestConfiguration.QueryParameters.Top = 999;
                    requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                });

                _availableGroups = groups?.Value
                    ?.Where(g => g.DisplayName != null && !g.DisplayName.Contains("Mays-Group", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(g => g.DisplayName)
                    .Select(g => new GroupItem { Id = g.Id, DisplayName = g.DisplayName })
                    .ToList() ?? new List<GroupItem>();

                cmbGroups.DataSource = _availableGroups;
                cmbGroups.DisplayMember = "DisplayName";
                cmbGroups.ValueMember = "Id";

                if (_availableGroups.Any())
                {
                    cmbGroups.SelectedIndex = 0;
                    await UpdatePermissionDropdown();
                }
                else
                {
                    lblStatus.Text = "No groups found.";
                    btnChange.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadGroupsError", _libraryName, null, "Subfolder", $"Failed to load groups: {ex.Message}");
                lblStatus.Text = "Error loading groups.";
                btnChange.Enabled = false;
            }
        }

        private async void LoadCurrentPermissionsAsync()
        {
            lvPermissions.Items.Clear();
            try
            {
                lblStatus.Text = "Loading current permissions...";
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
                    var library = web.Lists.GetByTitle(_libraryName);
                    var folder = library.RootFolder;
                    context.Load(folder, f => f.Folders.Include(f => f.Name, f => f.ListItemAllFields.HasUniqueRoleAssignments, f => f.ListItemAllFields.RoleAssignments.Include(
                        ra => ra.Member.Title,
                        ra => ra.Member.LoginName,
                        ra => ra.RoleDefinitionBindings)));
                    await context.ExecuteQueryAsync();

                    var subfolders = folder.Folders.Where(f => !f.Name.StartsWith("Forms")).ToList();
                    foreach (var subfolder in subfolders)
                    {
                        var perms = new List<string>();
                        foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                        {
                            if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase))
                            {
                                var memberName = ra.Member.Title;
                                var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                if (role == "Contribute") role = "Edit";
                                perms.Add($"{memberName}: {role}");
                            }
                        }

                        var item = new ListViewItem(subfolder.Name);
                        item.SubItems.Add(subfolder.ListItemAllFields.HasUniqueRoleAssignments ? string.Join("; ", perms.Any() ? perms : new[] { "No permissions" }) : "Inherited");
                        item.Tag = subfolder; // Store Folder object for operations
                        item.SubItems.Add(subfolder.ListItemAllFields.HasUniqueRoleAssignments ? "Unique" : "Inherited");
                        lvPermissions.Items.Add(item);
                    }
                    lblStatus.Text = "Permissions loaded.";
                    await UpdatePermissionDropdown();
                    lvPermissions_SelectedIndexChanged(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadSubfolderPermissionsError", _libraryName, null, "Subfolder", $"Failed to load subfolder permissions: {ex.Message}");
                lblStatus.Text = "Error loading permissions.";
                btnChange.Enabled = false;
                lvPermissions_SelectedIndexChanged(null, EventArgs.Empty);
            }
        }

        private async Task UpdatePermissionDropdown()
        {
            if (lvPermissions.SelectedItems.Count == 1)
            {
                var selectedItem = lvPermissions.SelectedItems[0];
                string subfolderName = selectedItem.Text;
                bool hasUniquePermissions = selectedItem.SubItems[2].Text == "Unique";
                if (hasUniquePermissions)
                {
                    var perms = selectedItem.SubItems[1].Text.Split(';').Select(p => p.Trim()).ToList();
                    var selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                    if (selectedGroup != null)
                    {
                        string groupPrincipalId = $"c:0t.c|tenant|{selectedGroup.Id}";
                        var perm = perms.FirstOrDefault(p => p.StartsWith(selectedGroup.DisplayName + ":"));
                        if (perm != null)
                        {
                            var role = perm.Substring(perm.IndexOf(':') + 1).Trim();
                            cmbPermissions.SelectedItem = role;
                            btnChange.Enabled = true;
                        }
                        else
                        {
                            cmbPermissions.SelectedItem = "Read";
                            btnChange.Enabled = false;
                        }
                    }
                    else
                    {
                        cmbPermissions.SelectedItem = "Read";
                        btnChange.Enabled = false;
                    }
                }
                else
                {
                    cmbPermissions.SelectedItem = "Read";
                    btnChange.Enabled = false;
                }
                cmbGroups.Enabled = false;
                btnBreakInheritance.Enabled = !hasUniquePermissions;
            }
            else
            {
                if (lvPermissions.SelectedItems.Count > 1)
                {
                    cmbPermissions.SelectedIndex = -1;
                    btnChange.Enabled = false;
                    btnBreakInheritance.Enabled = false;
                    cmbGroups.Enabled = false;
                }
                else if (cmbGroups.SelectedItem != null)
                {
                    try
                    {
                        var selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                        string groupPrincipalId = $"c:0t.c|tenant|{selectedGroup.Id}";
                        var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                        var accounts = await _pca.GetAccountsAsync();
                        var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, e) =>
                            {
                                e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                            };

                            var library = context.Web.Lists.GetByTitle(_libraryName);
                            context.Load(library.RootFolder.Folders, f => f.Include(
                                folder => folder.Name,
                                folder => folder.ListItemAllFields.RoleAssignments.Include(
                                    ra => ra.Member.LoginName,
                                    ra => ra.RoleDefinitionBindings)));
                            await context.ExecuteQueryAsync();

                            bool hasPermission = false;
                            string role = "Read";
                            foreach (var folder in library.RootFolder.Folders.Where(f => !f.Name.StartsWith("Forms")))
                            {
                                var roleAssignment = folder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupPrincipalId);
                                if (roleAssignment != null)
                                {
                                    hasPermission = true;
                                    var binding = roleAssignment.RoleDefinitionBindings.FirstOrDefault();
                                    role = binding != null ? binding.Name : "Read";
                                    if (role == "Contribute") role = "Edit";
                                    break;
                                }
                            }

                            cmbPermissions.SelectedItem = role;
                            btnChange.Enabled = hasPermission;
                            cmbGroups.Enabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to load group permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        await _auditLogManager.LogAction(_signedInUserId, null, "LoadGroupPermissionError", _libraryName, null, "Subfolder", $"Failed to load group permission: {ex.Message}");
                        cmbPermissions.SelectedItem = "Read";
                        btnChange.Enabled = false;
                        cmbGroups.Enabled = true;
                    }
                }
                else
                {
                    cmbPermissions.SelectedIndex = 0;
                    btnChange.Enabled = false;
                    btnBreakInheritance.Enabled = false;
                    cmbGroups.Enabled = true;
                }
            }
        }
        private void lvPermissions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvPermissions.SelectedItems.Count >= 1)
            {
                if (lvPermissions.SelectedItems.Count == 1)
                {
                    var selectedItem = lvPermissions.SelectedItems[0];
                    string groupName = selectedItem.Text;
                    var matchingGroup = _availableGroups.FirstOrDefault(g => g.DisplayName == groupName);
                    if (matchingGroup != null)
                    {
                        cmbGroups.SelectedItem = matchingGroup;
                    }
                    cmbGroups.Enabled = false;
                    btnBreakInheritance.Enabled = selectedItem.SubItems[2].Text == "Inherited";
                    UpdatePermissionDropdown();
                }
                else
                {
                    cmbGroups.Enabled = false;
                    cmbPermissions.SelectedIndex = -1;
                    btnChange.Enabled = false;
                    btnBreakInheritance.Enabled = false;
                }
            }
            else
            {
                cmbGroups.Enabled = true;
                btnBreakInheritance.Enabled = false;
                UpdatePermissionDropdown();
            }
        }

        private async void cmbGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvPermissions.SelectedItems.Count == 0)
            {
                await UpdatePermissionDropdown();
            }
        }

                private async void btnAdd_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            GroupItem selectedGroup = null;
            UpdateUI(() => btnAdd.Enabled = false);
            try
            {
                if (lvSubfolders.SelectedItems.Count != 1)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Add cancelled: Invalid subfolder selection.";
                    });
                    return;
                }

                if (cmbGroups.SelectedItem == null || cmbPermissions.SelectedItem == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a group and permission level.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Add cancelled: Missing selection.";
                    });
                    return;
                }

                var selectedItem = lvSubfolders.SelectedItems[0];
                subfolderName = selectedItem.Text;
                bool hasUniquePermissions = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Unique";
                if (!hasUniquePermissions)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Add cancelled: Subfolder has inherited permissions.";
                    });
                    return;
                }

                selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                string selectedGroupId = selectedGroup.Id;
                string permissionLevel = cmbPermissions.SelectedItem.ToString();

                if (permissionLevel == "No Direct Access")
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Use 'Remove Permission' to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Add cancelled: Invalid permission level.";
                    });
                    return;
                }

                UpdateUI(() => statusLabel.Text = $"Adding permission for '{selectedGroup.DisplayName}' to '{subfolderName}'...");
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
                Exception lastException = null;
                string subfolderRelativeUrl = null;

                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, ev) =>
                            {
                                ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                            };

                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                            subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/");

                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            var listItem = subfolder.ListItemAllFields;
                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            context.Load(context.Web, s => s.RoleDefinitions);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);

                            if (!listItem.HasUniqueRoleAssignments)
                            {
                                listItem.BreakRoleInheritance(true, false);
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                            }

                            RoleAssignmentCollection roleAssignments = listItem.RoleAssignments;
                            context.Load(roleAssignments);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);

                            // Log existing role assignments with detailed info
                            var existingRAs = new List<string>();
                            foreach (RoleAssignment ra in roleAssignments)
                            {
                                context.Load(ra.Member, m => m.LoginName, m => m.Title, m => m.PrincipalType);
                                context.Load(ra.RoleDefinitionBindings);
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                                var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null").ToList());
                                existingRAs.Add($"LoginName: {ra.Member.LoginName}, Title: {ra.Member.Title ?? "None"}, PrincipalType: {ra.Member.PrincipalType}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                            }
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Pre-addition RAs for '{subfolderName}': {string.Join(" | ", existingRAs)}");

                            string groupPrincipalId = $"c:0t.c|tenant|{selectedGroupId}";
                            bool hasEffectivePermissions = false;
                            var detectedRoles = new List<string>();
                            foreach (RoleAssignment ra in roleAssignments)
                            {
                                try
                                {
                                    context.Load(ra.Member, m => m.LoginName, m => m.PrincipalType);
                                    context.Load(ra.RoleDefinitionBindings);
                                    await context.ExecuteQueryAsync().ConfigureAwait(false);
                                    if (ra.Member.LoginName == groupPrincipalId)
                                    {
                                        var validPermissionRoles = new[] { "Read", "Contribute", "Edit", "Full Control", "Limited Access" };
                                        var roleNames = ra.RoleDefinitionBindings
                                            .Select(rdb => rdb.Name)
                                            .Where(name => name != null && validPermissionRoles.Contains(name))
                                            .ToList();
                                        if (roleNames.Any())
                                        {
                                            hasEffectivePermissions = true;
                                            detectedRoles.AddRange(roleNames);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Log potential CSOM errors but continue checking other role assignments
                                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Non-critical error checking role assignment for '{ra.Member?.LoginName ?? "Unknown"}' on '{subfolderName}': {ex.Message}, Inner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}");
                                }
                            }

                            if (hasEffectivePermissions)
                            {
                                // Log the warning instead of showing a message box
                                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Group '{groupPrincipalId}' detected with permissions ({string.Join(", ", detectedRoles)}) on '{subfolderName}'. Proceeding with addition to ensure correct permissions.");
                            }

                            var principal = context.Web.EnsureUser(groupPrincipalId);
                            context.Load(principal, p => p.LoginName, p => p.Title, p => p.PrincipalType);
                            try
                            {
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Failed to ensure user '{groupPrincipalId}': {ex.Message}", ex);
                            }

                            var roleDefinitions = context.Web.RoleDefinitions;
                            context.Load(roleDefinitions);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);

                            string targetRoleName = permissionLevel == "Edit" ? "Contribute" : permissionLevel;
                            var roleDefinition = roleDefinitions.FirstOrDefault(rd => rd.Name == targetRoleName);
                            if (roleDefinition == null)
                            {
                                UpdateUI(() =>
                                {
                                    MessageBox.Show($"Permission level '{permissionLevel}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    statusLabel.Text = "Error: Permission level not found.";
                                });
                                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Permission level '{permissionLevel}' not found for subfolder '{subfolderName}'");
                                return;
                            }

                            var roleDefinitionBindings = new RoleDefinitionBindingCollection(context);
                            roleDefinitionBindings.Add(roleDefinition);
                            listItem.RoleAssignments.Add(principal, roleDefinitionBindings);
                            listItem.Update();
                            try
                            {
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Failed to add role assignment for '{groupPrincipalId}' with role '{targetRoleName}': {ex.Message}", ex);
                            }

                            // Verify addition
                            context.Load(roleAssignments, ras => ras.Include(ra => ra.Member, ra => ra.RoleDefinitionBindings));
                            foreach (RoleAssignment ra in roleAssignments)
                            {
                                context.Load(ra.Member, m => m.LoginName);
                                context.Load(ra.RoleDefinitionBindings);
                            }
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            var updatedRAs = new List<string>();
                            bool permissionAdded = false;
                            foreach (RoleAssignment ra in roleAssignments)
                            {
                                var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null").ToList());
                                updatedRAs.Add($"LoginName: {ra.Member.LoginName}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                                if (ra.Member.LoginName == groupPrincipalId && ra.RoleDefinitionBindings.Any(rdb => rdb.Name == targetRoleName))
                                {
                                    permissionAdded = true;
                                }
                            }
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Post-addition RAs for '{subfolderName}': {string.Join(" | ", updatedRAs)}");
                            if (!permissionAdded)
                            {
                                throw new Exception($"Permission '{targetRoleName}' for group '{selectedGroup.DisplayName}' was not applied to '{subfolderName}'.");
                            }

                            // Ensure DataGridView columns are initialized
                            UpdateUI(() =>
                            {
                                // Check if columns exist, add them if missing
                                if (dgvGroupPermissions.Columns.Count == 0)
                                {
                                    dgvGroupPermissions.Columns.Add("colGroupName", "Group Name");
                                    dgvGroupPermissions.Columns.Add("colPermission", "Permission");
                                    dgvGroupPermissions.Columns.Add("colGroupId", "Group ID");
                                    dgvGroupPermissions.Columns["colGroupName"].Width = 300;
                                    dgvGroupPermissions.Columns["colPermission"].Width = 100;
                                    dgvGroupPermissions.Columns["colGroupId"].Visible = false;
                                }

                                statusLabel.Text = $"Added '{permissionLevel}' permission for '{selectedGroup.DisplayName}' to '{subfolderName}'.";
                                try
                                {
                                    dgvGroupPermissions.Rows.Add(selectedGroup.DisplayName, permissionLevel, selectedGroupId);
                                }
                                catch (Exception ex)
                                {
                                    // Log the DataGridView error but don't show to user
                                    _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Failed to update DataGridView for '{subfolderName}': {ex.Message}, StackTrace: {ex.StackTrace}");
                                }
                                LoadCurrentPermissionsAsync();
                                lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                            });

                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermission", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Added '{permissionLevel}' permission for group '{selectedGroup.DisplayName}' to subfolder '{subfolderName}' in library '{_libraryName}'");

                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionRetry", _libraryName, selectedGroup?.DisplayName, "Subfolder", $"Retry {retryCount} for adding permission to '{subfolderName}': {ex.Message}, Inner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }

                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Failed to add permission after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error adding permission.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup?.DisplayName, "Subfolder", $"Failed to add permission to subfolder '{subfolderName ?? "unknown"}' at '{subfolderRelativeUrl}': {ex.Message}, Inner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to add permission: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error adding permission.";
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup?.DisplayName, "Subfolder", $"Failed to add permission to subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}");
            }
            finally
            {
                UpdateUI(() => btnAdd.Enabled = true);
            }
        }


        private async void btnRemove_Click(object sender, EventArgs e)
        {
            if (lvPermissions.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more subfolders to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Remove cancelled: No subfolders selected.";
                return;
            }

            var selectedCount = lvPermissions.SelectedItems.Count;
            var confirmMessage = selectedCount == 1
                ? $"Are you sure you want to remove permissions for the selected subfolder? This cannot be undone."
                : $"Are you sure you want to remove permissions for {selectedCount} selected subfolders? This cannot be undone.";

            var confirm = MessageBox.Show(confirmMessage, "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                lblStatus.Text = "Remove cancelled by user.";
                return;
            }

            try
            {
                lblStatus.Text = $"Removing permissions for {selectedCount} subfolder(s)...";
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, ev) =>
                    {
                        ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };

                    var library = context.Web.Lists.GetByTitle(_libraryName);
                    var removedSubfolders = new List<string>();
                    foreach (ListViewItem selectedItem in lvPermissions.SelectedItems)
                    {
                        string subfolderName = selectedItem.Text;
                        bool hasUniquePermissions = selectedItem.SubItems[2].Text == "Unique";
                        if (!hasUniquePermissions)
                        {
                            MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            continue;
                        }

                        var subfolder = library.RootFolder.Folders.FirstOrDefault(f => f.Name == subfolderName);
                        if (subfolder == null)
                        {
                            MessageBox.Show($"Subfolder '{subfolderName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            continue;
                        }

                        context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.PrincipalId));
                        await context.ExecuteQueryAsync();

                        var removedGroups = new List<string>();
                        foreach (var ra in subfolder.ListItemAllFields.RoleAssignments.ToList()) // ToList to avoid modification during enumeration
                        {
                            if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase))
                            {
                                var groupName = ra.Member.Title;
                                ra.DeleteObject();
                                removedGroups.Add(groupName);
                            }
                        }

                        if (removedGroups.Any())
                        {
                            await context.ExecuteQueryAsync();
                            foreach (var groupName in removedGroups)
                            {
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, groupName, "Subfolder", $"Removed permissions for group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}'");
                            }
                            removedSubfolders.Add(subfolderName);
                        }
                    }

                    if (removedSubfolders.Any())
                    {
                        lblStatus.Text = $"Removed permissions for {removedSubfolders.Count} subfolder(s).";
                    }
                    else
                    {
                        lblStatus.Text = "No permissions removed.";
                    }
                    LoadCurrentPermissionsAsync();
                    lvPermissions_SelectedIndexChanged(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder", $"Failed to remove permissions: {ex.Message}");
                lblStatus.Text = "Error removing permissions.";
            }
        }

        private async void btnBreakInheritance_Click(object sender, EventArgs e)
        {
            if (lvPermissions.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Break inheritance cancelled: Invalid subfolder selection.";
                return;
            }

            var selectedItem = lvPermissions.SelectedItems[0];
            string subfolderName = selectedItem.Text;
            bool hasUniquePermissions = selectedItem.SubItems[2].Text == "Unique";
            if (hasUniquePermissions)
            {
                MessageBox.Show($"Subfolder '{subfolderName}' already has unique permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Break inheritance cancelled: Already unique permissions.";
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to break role inheritance for subfolder '{subfolderName}'? This will allow unique permissions but may affect existing access.",
                "Confirm Break Inheritance",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                lblStatus.Text = "Break inheritance cancelled by user.";
                return;
            }

            const int maxRetries = 3;
            int retryCount = 0;
            bool success = false;
            Exception lastException = null;
            string subfolderRelativeUrl = null;

            while (retryCount < maxRetries && !success)
            {
                try
                {
                    lblStatus.Text = $"Breaking inheritance for '{subfolderName}' (Attempt {retryCount + 1}/{maxRetries})...";
                    var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                    var accounts = await _pca.GetAccountsAsync();
                    var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                    using (var context = new ClientContext(_siteUrl))
                    {
                        context.ExecutingWebRequest += (s, ev) =>
                        {
                            ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                        };

                        // Step 1: Load the web and construct the server-relative URL for the subfolder
                        context.Load(context.Web, w => w.ServerRelativeUrl);
                        await context.ExecuteQueryAsync();
                        subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                        subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/"); // Clean up any double slashes

                        // Step 2: Load the subfolder using GetFolderByServerRelativeUrl
                        Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                        context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                        context.Load(subfolder.ListItemAllFields, i => i.Id, i => i.HasUniqueRoleAssignments, i => i.RoleAssignments);
                        await context.ExecuteQueryAsync();

                        // Step 3: Break inheritance (using your preferred parameters)
                        subfolder.ListItemAllFields.BreakRoleInheritance(true, false); // Copy parent permissions, preserve child permissions
                        await context.ExecuteQueryAsync();

                        // Optional: Add permission level to a user or group (from SharePoint Pals example, commented out; uncomment to use)
                        // subfolder.ListItemAllFields.AddPermissionLevelToUser("User1@****.Onmicrosoft.com", "Read", false);
                        // or for a group from cmbGroups:
                        // if (cmbGroups.SelectedItem != null)
                        // {
                        //     var selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                        //     subfolder.ListItemAllFields.AddPermissionLevelToGroup(selectedGroup.DisplayName, "Read", false);
                        // }
                        // await context.ExecuteQueryAsync();

                        await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritance", _libraryName, null, "Subfolder",
                            $"Broke role inheritance for subfolder '{subfolderName}' in library '{_libraryName}' at '{subfolderRelativeUrl}'");
                        lblStatus.Text = $"Role inheritance broken for '{subfolderName}'.";
                        success = true;
                        LoadCurrentPermissionsAsync();
                        lvPermissions_SelectedIndexChanged(null, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        await Task.Delay(1000 * retryCount); // Exponential backoff: 1s, 2s, 3s
                        continue;
                    }

                    MessageBox.Show($"Failed to break inheritance after {maxRetries} attempts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                        $"Failed to break inheritance for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, StackTrace: {ex.StackTrace}");
                    lblStatus.Text = "Error breaking inheritance.";
                }
            }
        }
        private async void btnChange_Click(object sender, EventArgs e)
        {
            if (lvPermissions.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Change cancelled: Invalid subfolder selection.";
                return;
            }

            if (cmbPermissions.SelectedItem == null)
            {
                MessageBox.Show("Please select a permission level.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Change cancelled: Missing permission selection.";
                return;
            }

            var selectedItem = lvPermissions.SelectedItems[0];
            string subfolderName = selectedItem.Text;
            bool hasUniquePermissions = selectedItem.SubItems[2].Text == "Unique";
            if (!hasUniquePermissions)
            {
                MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Change cancelled: Subfolder has inherited permissions.";
                return;
            }

            var selectedGroup = (GroupItem)cmbGroups.SelectedItem;
            if (selectedGroup == null)
            {
                MessageBox.Show("Please select a group.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = "Change cancelled: Missing group selection.";
                return;
            }

            string groupName = selectedGroup.DisplayName;
            string groupLogin = $"c:0t.c|tenant|{selectedGroup.Id}";
            string newPermission = cmbPermissions.SelectedItem.ToString();

            if (newPermission == "No Direct Access")
            {
                var confirm = MessageBox.Show(
                    $"Are you sure you want to remove permissions for '{groupName}' from subfolder '{subfolderName}'? This cannot be undone.",
                    "Confirm Remove",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    lblStatus.Text = "Remove cancelled by user.";
                    return;
                }

                try
                {
                    lblStatus.Text = $"Removing permission for '{groupName}' from '{subfolderName}'...";
                    var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                    var accounts = await _pca.GetAccountsAsync();
                    var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                    using (var context = new ClientContext(_siteUrl))
                    {
                        context.ExecutingWebRequest += (s, ev) =>
                        {
                            ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                        };

                        var library = context.Web.Lists.GetByTitle(_libraryName);
                        var subfolder = library.RootFolder.Folders.FirstOrDefault(f => f.Name == subfolderName);
                        if (subfolder == null)
                        {
                            MessageBox.Show($"Subfolder '{subfolderName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Change cancelled: Subfolder not found.";
                            return;
                        }

                        context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                        await context.ExecuteQueryAsync();

                        var raToRemove = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupLogin);
                        if (raToRemove != null)
                        {
                            raToRemove.DeleteObject();
                            await context.ExecuteQueryAsync();
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, groupName, "Subfolder", $"Removed permissions for group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}' via Change");
                            lblStatus.Text = $"Removed permissions for '{groupName}' from '{subfolderName}'.";
                            LoadCurrentPermissionsAsync();
                            lvPermissions_SelectedIndexChanged(null, EventArgs.Empty);
                        }
                        else
                        {
                            MessageBox.Show($"Group '{groupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            lblStatus.Text = "Change cancelled: Group not found.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to remove permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Failed to remove permission via Change: {ex.Message}");
                    lblStatus.Text = "Error removing permission.";
                }
                return;
            }

            try
            {
                lblStatus.Text = $"Changing permission for '{groupName}' to '{newPermission}' in '{subfolderName}'...";
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, ev) =>
                    {
                        ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };

                    var library = context.Web.Lists.GetByTitle(_libraryName);
                    var subfolder = library.RootFolder.Folders.FirstOrDefault(f => f.Name == subfolderName);
                    if (subfolder == null)
                    {
                        MessageBox.Show($"Subfolder '{subfolderName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Change cancelled: Subfolder not found.";
                        return;
                    }

                    context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings));
                    await context.ExecuteQueryAsync();

                    var ra = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(r => r.Member.LoginName == groupLogin);
                    if (ra != null)
                    {
                        ra.RoleDefinitionBindings.RemoveAll();

                        var roleDefinitions = context.Web.RoleDefinitions;
                        context.Load(roleDefinitions);
                        await context.ExecuteQueryAsync();

                        string roleName = newPermission == "Edit" ? "Contribute" : newPermission;
                        var roleDefinition = roleDefinitions.FirstOrDefault(rd => rd.Name == roleName);
                        if (roleDefinition == null)
                        {
                            MessageBox.Show($"Permission level '{newPermission}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Error: Permission level not found.";
                            await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Permission level '{newPermission}' not found for subfolder '{subfolderName}'");
                            return;
                        }

                        ra.RoleDefinitionBindings.Add(roleDefinition);
                        ra.Update();
                        await context.ExecuteQueryAsync();

                        await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermission", _libraryName, groupName, "Subfolder", $"Changed permission for group '{groupName}' to '{newPermission}' on subfolder '{subfolderName}' in library '{_libraryName}'");
                        lblStatus.Text = $"Changed permission for '{groupName}' to '{newPermission}' in '{subfolderName}'.";
                        LoadCurrentPermissionsAsync();
                        lvPermissions_SelectedIndexChanged(null, EventArgs.Empty);
                    }
                    else
                    {
                        MessageBox.Show($"Group '{groupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "Change cancelled: Group not found.";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Failed to change permission: {ex.Message}");
                lblStatus.Text = "Error changing permission.";
            }
        }
    private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
