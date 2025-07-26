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
using Microsoft.Identity.Client.NativeInterop;

namespace EntraGroupsApp
{
    public partial class SubfolderPermissionsEditDialog : System.Windows.Forms.Form
    {
        private System.ComponentModel.IContainer components = null;
        private readonly string _libraryName;
        private bool isUpdatingDataGridView;
        private bool isUpdatingTreeView;
        private readonly IPublicClientApplication _pca;
        private readonly string _siteUrl;
        private readonly GraphServiceClient _graphClient;
        private readonly AuditLogManager _auditLogManager;
        private readonly string _signedInUserId;
        private List<GroupItem> _availableGroups;
        private DateTime lastRefreshTime = DateTime.MinValue;
        private readonly TimeSpan minimumRefreshInterval = TimeSpan.FromSeconds(5);

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

        private void UpdateUI(Action action)
        {
            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        private class TreeNodeData
        {
            public bool IsSubfolder { get; set; }
            public Folder Subfolder { get; set; }
            public string GroupId { get; set; }
            public string GroupName { get; set; }
            public string Permission { get; set; }
        }

        private async void LoadAvailableGroupsAsync()
        {
            try
            {
                UpdateUI(() => statusLabel.Text = "Loading groups...");
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

                UpdateUI(() =>
                {
                    cmbGroups.DataSource = _availableGroups;
                    cmbGroups.DisplayMember = "DisplayName";
                    cmbGroups.ValueMember = "Id";

                    if (_availableGroups.Any())
                    {
                        cmbGroups.SelectedIndex = 0;
                    }
                    else
                    {
                        statusLabel.Text = "No groups found.";
                        btnChange.Enabled = false;
                        btnAdd.Enabled = false;
                        btnRemove.Enabled = false;
                    }
                    UpdateSidebar(); // Replaced UpdatePermissionDropdown
                });
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to load groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error loading groups.";
                    btnChange.Enabled = false;
                    btnAdd.Enabled = false;
                    btnRemove.Enabled = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadGroupsError", _libraryName, null, "Subfolder", $"Failed to load groups: {ex.Message}");
            }
        }
        private async Task LoadCurrentPermissionsAsync(string debugSessionId = null)
{
    debugSessionId = debugSessionId ?? Guid.NewGuid().ToString();
    if (DateTime.UtcNow - lastRefreshTime < minimumRefreshInterval)
    {
        await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsSkipped", _libraryName, null, "Subfolder",
            $"Refresh skipped due to debounce, time since last refresh: {(DateTime.UtcNow - lastRefreshTime).TotalSeconds}s, Session ID: {debugSessionId}");
        return;
    }

    string selectedSubfolderName = null;
    var nodesToAdd = new List<TreeNode>();
    var auditLogs = new List<(string Action, string GroupName, string Details)>(); // Batch audit logs
    UpdateUI(() =>
    {
        try
        {
            isUpdatingTreeView = true;
            selectedSubfolderName = tvSubfolders.SelectedNode?.Tag is TreeNodeData nodeData && nodeData.IsSubfolder ? nodeData.Subfolder.Name : null;
            tvSubfolders.Nodes.Clear();
            statusLabel.Text = "Loading subfolder permissions...";
            progressBar.Value = 0;
            progressBar.Visible = true;
            btnRefresh.Enabled = false;
            btnClose.Text = "Cancel";
            btnClose.Click -= btnClose_Click;
            btnClose.Click += (s, e) => _cancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            selectedSubfolderName = null;
            statusLabel.Text = "Warning: Subfolder selection invalid during load.";
            auditLogs.Add(("DebugLoadPermissionsSelectionError", null, $"Failed to get selected subfolder: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}"));
        }
        finally
        {
            isUpdatingTreeView = false;
        }
    });

    try
    {
        var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
        auditLogs.Add(("DebugLoadPermissionsAuthStart", null, $"Acquiring authentication token for scopes: {string.Join(", ", scopes)}, Session ID: {debugSessionId}"));
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        if (account == null)
        {
            UpdateUI(() =>
            {
                MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                statusLabel.Text = "Error: No signed-in account.";
                progressBar.Visible = false;
            });
            auditLogs.Add(("LoadSubfolderPermissionsError", null, $"No signed-in account found, Session ID: {debugSessionId}"));
            await LogAuditBatchAsync(auditLogs);
            return;
        }
        var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
        auditLogs.Add(("DebugLoadPermissionsAuthSuccess", null, $"Authentication token acquired, Session ID: {debugSessionId}"));

        using (var context = new ClientContext(_siteUrl))
        {
            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
            auditLogs.Add(("DebugLoadPermissionsContextSetup", null, $"ClientContext initialized for site: {_siteUrl}, Session ID: {debugSessionId}"));

            var web = context.Web;
            var library = web.Lists.GetByTitle(_libraryName);
            var folder = library.RootFolder;
            context.Load(folder, f => f.Folders.Include(f => f.Name, f => f.ServerRelativeUrl,
                f => f.ListItemAllFields.HasUniqueRoleAssignments, f => f.ListItemAllFields.RoleAssignments.Include(
                ra => ra.Member.Title, ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings)));
            await context.ExecuteQueryAsync();
            auditLogs.Add(("DebugLoadPermissionsQueryExecuted", null, $"Query executed, Folder count: {folder.Folders.Count}, Session ID: {debugSessionId}"));

            var subfolders = folder.Folders.Where(f => !f.Name.StartsWith("Forms")).ToList();
            auditLogs.Add(("DebugLoadPermissionsSubfoldersFiltered", null, $"Filtered subfolders: {subfolders.Count}, Names: {string.Join(", ", subfolders.Select(f => f.Name))}, Session ID: {debugSessionId}"));

            if (!subfolders.Any())
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show("No subfolders found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = "No subfolders found.";
                    progressBar.Visible = false;
                    btnChange.Enabled = false;
                    btnAdd.Enabled = false;
                    btnRemove.Enabled = false;
                    UpdateSidebar();
                    isUpdatingTreeView = false;
                });
                auditLogs.Add(("LoadSubfolderPermissionsNoSubfolders", null, $"No subfolders found in library '{_libraryName}', Session ID: {debugSessionId}"));
                await LogAuditBatchAsync(auditLogs);
                return;
            }

            int totalSubfolders = subfolders.Count;
            int processedSubfolders = 0;
            int progressUpdateInterval = Math.Max(1, totalSubfolders / 20); // Update progress every 5%

            foreach (var subfolder in subfolders)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    UpdateUI(() =>
                    {
                        statusLabel.Text = "Loading cancelled by user.";
                        progressBar.Visible = false;
                        btnClose.Text = "Close";
                        btnClose.Click -= (s, e) => _cancellationTokenSource.Cancel();
                        btnClose.Click += btnClose_Click;
                    });
                    auditLogs.Add(("LoadSubfolderPermissionsCancelled", null, $"Loading permissions cancelled, Session ID: {debugSessionId}"));
                    await LogAuditBatchAsync(auditLogs);
                    return;
                }

                auditLogs.Add(("DebugLoadPermissionsProcessSubfolder", null, $"Processing subfolder: {subfolder.Name}, ServerRelativeUrl: {subfolder.ServerRelativeUrl}, Session ID: {debugSessionId}"));

                var perms = new List<string>();
                int groupCount = 0;
                if (subfolder.ListItemAllFields?.RoleAssignments != null)
                {
                    foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                    {
                        try
                        {
                            if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                if (role == "Contribute") role = "Edit";
                                if (role != "Limited Access")
                                {
                                    perms.Add($"{ra.Member.Title}: {role}");
                                    groupCount++;
                                    auditLogs.Add(("DebugLoadPermissionsRoleAssignment", ra.Member.Title, $"Subfolder: {subfolder.Name}, Group: {ra.Member.Title}, Role: {role}, Session ID: {debugSessionId}"));
                                }
                            }
                        }
                        catch (Exception raEx)
                        {
                            auditLogs.Add(("DebugLoadPermissionsRoleAssignmentError", null, $"Error processing role assignment for subfolder: {subfolder.Name}, Error: {raEx.Message}, Inner: {(raEx.InnerException?.Message ?? "None")}, StackTrace: {raEx.StackTrace}, Session ID: {debugSessionId}"));
                        }
                    }
                }

                auditLogs.Add(("DebugLoadPermissionsUniqueCheck", null, $"Subfolder: {subfolder.Name}, HasUniqueRoleAssignments: {subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false}, Session ID: {debugSessionId}"));

                var subfolderNode = new TreeNode
                {
                    Text = (subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false)
                        ? $"{subfolder.Name} (Unique, {groupCount} CSG group{(groupCount == 1 ? "" : "s")} assigned)"
                        : $"{subfolder.Name} (Inherited)",
                    ImageIndex = 0,
                    SelectedImageIndex = 0,
                    Tag = new TreeNodeData { IsSubfolder = true, Subfolder = subfolder }
                };

                foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                {
                    if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var groupName = ra.Member.Title;
                        var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                        if (role == "Contribute") role = "Edit";
                        if (role == "Limited Access") continue;

                        var groupId = ra.Member.LoginName.Split('|').Last();
                        var groupNode = new TreeNode
                        {
                            Text = $"{groupName}: {role}",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            Tag = new TreeNodeData { IsSubfolder = false, GroupId = groupId, GroupName = groupName, Permission = role }
                        };
                        subfolderNode.Nodes.Add(groupNode);
                        auditLogs.Add(("DebugLoadGroupDetails", groupName, $"Added group '{groupName}' with role '{role}' to TreeView for subfolder: {subfolder.Name}, Session ID: {debugSessionId}"));
                    }
                }

                nodesToAdd.Add(subfolderNode);
                processedSubfolders++;
                if (processedSubfolders % progressUpdateInterval == 0 || processedSubfolders == totalSubfolders)
                {
                    UpdateUI(() => progressBar.Value = totalSubfolders > 0 ? (int)((processedSubfolders / (double)totalSubfolders) * 100) : 0);
                }

                if (subfolder.Name == selectedSubfolderName)
                {
                    subfolderNode.Expand();
                }
            }

            UpdateUI(() =>
            {
                isUpdatingTreeView = true;
                tvSubfolders.Nodes.AddRange(nodesToAdd.ToArray());
                statusLabel.Text = "Permissions loaded.";
                progressBar.Value = 100;
                progressBar.Visible = false;
                UpdateSidebar();
                _originalNodes = CloneTreeNodes(tvSubfolders.Nodes); // Preserve for search
                if (tvSubfolders.Nodes.Cast<TreeNode>().Any(n => (n.Tag as TreeNodeData)?.Subfolder.Name == selectedSubfolderName))
                {
                    tvSubfolders.SelectedNode = tvSubfolders.Nodes.Cast<TreeNode>().First(n => (n.Tag as TreeNodeData)?.Subfolder.Name == selectedSubfolderName);
                }
                isUpdatingTreeView = false;
            });
            auditLogs.Add(("DebugLoadPermissionsUISuccess", null, $"Permissions UI updated successfully, Session ID: {debugSessionId}"));
            await LogAuditBatchAsync(auditLogs);
            lastRefreshTime = DateTime.UtcNow;
        }
    }
    catch (Exception ex)
    {
        UpdateUI(() =>
        {
            isUpdatingTreeView = true;
            MessageBox.Show($"Failed to load permissions: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Error loading permissions.";
            progressBar.Visible = false;
            btnChange.Enabled = false;
            btnAdd.Enabled = false;
            btnRemove.Enabled = false;
            UpdateSidebar();
            isUpdatingTreeView = false;
        });
        auditLogs.Add(("LoadSubfolderPermissionsError", null, $"Failed to load subfolder permissions: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}"));
        await LogAuditBatchAsync(auditLogs);
    }
    finally
    {
        UpdateUI(() =>
        {
            btnRefresh.Enabled = true;
            btnClose.Text = "Close";
            btnClose.Click -= (s, e) => _cancellationTokenSource.Cancel();
            btnClose.Click += btnClose_Click;
        });
    }
}

// Helper method to batch audit logs
private async Task LogAuditBatchAsync(List<(string Action, string GroupName, string Details)> logs)
{
    foreach (var log in logs)
    {
        await _auditLogManager.LogAction(_signedInUserId, null, log.Action, _libraryName, log.GroupName, "Subfolder", log.Details);
    }
}
        private void UpdateSidebar()
        {
            UpdateUI(() =>
            {
                isUpdatingTreeView = true;
                try
                {
                    lblSelectedItem.Text = "Selected Item: None";
                    btnAdd.Enabled = false;
                    btnChange.Enabled = false;
                    btnRemove.Enabled = false;
                    btnBreakInheritance.Enabled = false;
                    btnResetPermissions.Enabled = false;
                    cmbGroups.Enabled = false;
                    cmbPermissions.Enabled = false;
                    if (tvSubfolders.SelectedNode == null)
                    {
                        lblSelectedItem.Text = "Selected Item: None";
                        statusLabel.Text = "Select a subfolder to view or modify its permissions.";
                        return;
                    }
                    var nodeData = tvSubfolders.SelectedNode.Tag as TreeNodeData;
                    if (nodeData == null) return;
                    if (nodeData.IsSubfolder)
                    {
                        lblSelectedItem.Text = $"Subfolder: {nodeData.Subfolder.Name}";
                        bool hasUniquePermissions = nodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                        btnBreakInheritance.Enabled = !hasUniquePermissions;
                        btnResetPermissions.Enabled = hasUniquePermissions;
                        btnAdd.Enabled = hasUniquePermissions;
                        btnChange.Enabled = false;
                        btnRemove.Enabled = false;
                        cmbGroups.Enabled = hasUniquePermissions;
                        cmbPermissions.Enabled = hasUniquePermissions;
                        statusLabel.Text = hasUniquePermissions ? "Select a group from the tree or add a new permission." : "Subfolder has inherited permissions. Break inheritance to modify.";
                    }
                    else
                    {
                        lblSelectedItem.Text = $"Group: {nodeData.GroupName} ({nodeData.Permission})";
                        btnBreakInheritance.Enabled = false;
                        btnResetPermissions.Enabled = false;
                        btnAdd.Enabled = false;
                        btnChange.Enabled = true;
                        btnRemove.Enabled = true;
                        cmbGroups.Enabled = false;
                        cmbPermissions.Enabled = false;
                        statusLabel.Text = "Select a new permission type or remove the group.";
                    }
                }
                finally
                {
                    isUpdatingTreeView = false;
                }
            });
        }
        private void tvSubfolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (isUpdatingTreeView) return;
            UpdateSidebar();
        }
        private async void LoadGroupDetailsForSubfolder(Folder subfolder, TreeNode subfolderNode, string debugSessionId)
        {
            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    var reloadedFolder = context.Web.GetFolderByServerRelativeUrl(subfolder.ServerRelativeUrl);
                    context.Load(reloadedFolder.ListItemAllFields.RoleAssignments, ras => ras.Include(
                        ra => ra.Member.Title, ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings));
                    await context.ExecuteQueryAsync();
                    UpdateUI(() =>
                    {
                        isUpdatingTreeView = true;
                        try
                        {
                            subfolderNode.Nodes.Clear();
                            foreach (var ra in reloadedFolder.ListItemAllFields.RoleAssignments)
                            {
                                if (ra.Member != null && ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase))
                                {
                                    var groupName = ra.Member.Title;
                                    var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                    if (role == "Contribute") role = "Edit";
                                    var groupId = ra.Member.LoginName.Split('|').Last();
                                    try
                                    {
                                        var groupNode = new TreeNode
                                        {
                                            Text = $"{groupName}: {role}",
                                            ImageIndex = 1,
                                            SelectedImageIndex = 1,
                                            Tag = new TreeNodeData { IsSubfolder = false, GroupId = groupId, GroupName = groupName, Permission = role }
                                        };
                                        subfolderNode.Nodes.Add(groupNode);
                                        _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadGroupDetails", _libraryName, groupName, "Subfolder",
                                            $"Added group '{groupName}' with role '{role}' to TreeView for subfolder: {subfolder.Name}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                                    }
                                    catch (Exception ex)
                                    {
                                        _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadGroupDetailsError", _libraryName, groupName, "Subfolder",
                                            $"Failed to add group '{groupName}' to TreeView: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            isUpdatingTreeView = false;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to load group details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadGroupDetailsError", _libraryName, null, "Subfolder",
                    $"Failed to load group details: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
        }
        private bool ShouldEnableAddButton()
        {
            if (tvSubfolders.SelectedNode == null)
                return false;
            var nodeData = tvSubfolders.SelectedNode.Tag as TreeNodeData;
            if (nodeData == null || !nodeData.IsSubfolder)
                return false;
            return nodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
        }
        private async void cmbGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            await Task.Run(() => UpdateSidebar());
        }
        private async void btnAdd_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            GroupItem selectedGroup = null;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnAdd.Enabled = false; isUpdatingTreeView = false; });
            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Invalid subfolder selection."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }
                if (cmbGroups.SelectedItem == null || cmbPermissions.SelectedItem == null)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a group and permission level.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Missing selection."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Missing group or permission selection, Session ID: {debugSessionId}");
                    return;
                }
                subfolderName = nodeData.Subfolder.Name;
                bool hasUniquePermissions = nodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }
                selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                string selectedGroupId = selectedGroup.Id;
                string permissionLevel = cmbPermissions.SelectedItem.ToString();
                if (permissionLevel == "No Direct Access")
                {
                    UpdateUI(() => { MessageBox.Show("Use 'Remove Permission' to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Invalid permission level."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Invalid permission level 'No Direct Access', Session ID: {debugSessionId}");
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
                            context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
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
                            var existingRAs = new List<string>();
                            foreach (RoleAssignment ra in roleAssignments)
                            {
                                context.Load(ra.Member, m => m.LoginName, m => m.Title, m => m.PrincipalType);
                                context.Load(ra.RoleDefinitionBindings);
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                                var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null"));
                                existingRAs.Add($"LoginName: {ra.Member.LoginName}, Title: {(ra.Member.Title != null ? ra.Member.Title : "None")}, PrincipalType: {ra.Member.PrincipalType}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                            }
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                $"Pre-addition RAs for '{subfolderName}': {string.Join(" | ", existingRAs)}, Session ID: {debugSessionId}");
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
                                        var roleNames = ra.RoleDefinitionBindings.Select(rdb => rdb.Name).Where(name => name != null && validPermissionRoles.Contains(name)).ToList();
                                        if (roleNames.Any())
                                        {
                                            hasEffectivePermissions = true;
                                            detectedRoles.AddRange(roleNames);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                        $"Non-critical error checking role assignment for '{(ra.Member != null ? ra.Member.LoginName : "Unknown")}' on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                }
                            }
                            if (hasEffectivePermissions)
                                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                    $"Group '{groupPrincipalId}' detected with permissions ({string.Join(", ", detectedRoles)}) on '{subfolderName}'. Proceeding with addition to ensure correct permissions, Session ID: {debugSessionId}");
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
                                UpdateUI(() => { MessageBox.Show($"Permission level '{permissionLevel}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); statusLabel.Text = "Error: Permission level not found."; });
                                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                    $"Permission level '{permissionLevel}' not found for subfolder '{subfolderName}', Session ID: {debugSessionId}");
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
                                var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null"));
                                updatedRAs.Add($"LoginName: {ra.Member.LoginName}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                                if (ra.Member.LoginName == groupPrincipalId && ra.RoleDefinitionBindings.Any(rdb => rdb.Name == targetRoleName))
                                    permissionAdded = true;
                            }
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                $"Post-addition RAs for '{subfolderName}': {string.Join(" | ", updatedRAs)}, Session ID: {debugSessionId}");
                            if (!permissionAdded)
                                throw new Exception($"Permission '{targetRoleName}' for group '{selectedGroup.DisplayName}' was not applied to '{subfolderName}'.");
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                try
                                {
                                    statusLabel.Text = $"Added '{permissionLevel}' permission for '{selectedGroup.DisplayName}' to '{subfolderName}'.";
                                    var subfolderNode = tvSubfolders.Nodes.Cast<TreeNode>().FirstOrDefault(n => (n.Tag as TreeNodeData)?.Subfolder.Name == subfolderName);
                                    if (subfolderNode != null)
                                        subfolderNode.Nodes.Add(new TreeNode
                                        {
                                            Text = $"{selectedGroup.DisplayName}: {permissionLevel}",
                                            ImageIndex = 1,
                                            SelectedImageIndex = 1,
                                            Tag = new TreeNodeData { IsSubfolder = false, GroupId = selectedGroupId, GroupName = selectedGroup.DisplayName, Permission = permissionLevel }
                                        });
                                    LoadCurrentPermissionsAsync(debugSessionId);
                                    tvSubfolders_AfterSelect(null, null);
                                }
                                finally
                                {
                                    isUpdatingTreeView = false;
                                }
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermission", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                $"Added '{permissionLevel}' permission for group '{selectedGroup.DisplayName}' to subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionRetry", _libraryName, selectedGroup != null ? selectedGroup.DisplayName : null, "Subfolder",
                                $"Retry {retryCount} for adding permission to '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to add permission after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error adding permission.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup != null ? selectedGroup.DisplayName : null, "Subfolder",
                            $"Failed to add permission to subfolder '{subfolderName ?? "unknown"}' at '{subfolderRelativeUrl}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to add permission: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error adding permission.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup != null ? selectedGroup.DisplayName : null, "Subfolder",
                    $"Failed to add permission to subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnAdd.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private async void btnRemove_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnRemove.Enabled = false; isUpdatingTreeView = false; });
            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        statusLabel.Text = "Error: No signed-in account.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
                Exception lastException = null;
                string subfolderRelativeUrl = null;

                if (tvSubfolders.SelectedNode != null && tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData && !nodeData.IsSubfolder)
                {
                    var confirm = MessageBox.Show($"Are you sure you want to remove permissions for '{nodeData.GroupName}' from the subfolder?\n\nNote: If the permission remains in the SharePoint UI, check your account's 'Manage Permissions' rights or revoke sharing links manually.",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() =>
                        {
                            statusLabel.Text = "Remove cancelled by user.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, null, "Subfolder",
                            $"User cancelled removal, Session ID: {debugSessionId}");
                        return;
                    }
                    var subfolderNode = nodeData.IsSubfolder ? tvSubfolders.SelectedNode : tvSubfolders.SelectedNode.Parent;
                    var subfolderData = subfolderNode.Tag as TreeNodeData;
                    string subfolderName = subfolderData.Subfolder.Name;
                    UpdateUI(() =>
                    {
                        statusLabel.Text = $"Removing permission for '{nodeData.GroupName}' from '{subfolderName}'...";
                    });
                    string groupLogin = $"c:0t.c|tenant|{nodeData.GroupId}";
                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            using (var context = new ClientContext(_siteUrl))
                            {
                                context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                                context.Load(context.Web, w => w.ServerRelativeUrl);
                                await context.ExecuteQueryAsync();
                                subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                await context.ExecuteQueryAsync();
                                var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Pre-removal RAs for '{subfolderName}': {string.Join(", ", existingRAs)}, Session ID: {debugSessionId}");
                                var raToRemove = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupLogin && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                                if (raToRemove != null)
                                {
                                    foreach (RoleDefinition rd in raToRemove.RoleDefinitionBindings.ToList())
                                        raToRemove.RoleDefinitionBindings.Remove(rd);
                                    raToRemove.Update();
                                    raToRemove.DeleteObject();
                                    await context.ExecuteQueryAsync();
                                    context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                    await context.ExecuteQueryAsync();
                                    var remainingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Post-removal RAs for '{subfolderName}': {string.Join(", ", remainingRAs)}, Session ID: {debugSessionId}");
                                    if (remainingRAs.Contains(groupLogin))
                                        throw new Exception("Permission removal failed to apply (group still present after verification). Check 'Manage Permissions' rights or sharing links in SharePoint UI.");
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Removed permissions for group '{nodeData.GroupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                    UpdateUI(() =>
                                    {
                                        isUpdatingTreeView = true;
                                        try
                                        {
                                            statusLabel.Text = $"Removed permissions for '{nodeData.GroupName}' from '{subfolderName}'.";
                                            tvSubfolders.SelectedNode.Remove();
                                            LoadCurrentPermissionsAsync(debugSessionId);
                                            tvSubfolders_AfterSelect(null, null);
                                        }
                                        finally
                                        {
                                            isUpdatingTreeView = false;
                                        }
                                    });
                                    success = true;
                                }
                                else
                                {
                                    UpdateUI(() =>
                                    {
                                        MessageBox.Show($"Group '{nodeData.GroupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        statusLabel.Text = "Remove cancelled: Group not found.";
                                    });
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Group '{nodeData.GroupName}' not found in permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                    return;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                string errorMessage = ex.Message;
                                string innerMessage = ex.InnerException != null ? ex.InnerException.Message : "None";
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionRetry", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Retry {retryCount} for removing permission from '{subfolderName}': {errorMessage}, Inner: {innerMessage}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            string errorMessageFinal = ex.Message;
                            string innerMessageFinal = ex.InnerException != null ? ex.InnerException.Message : "None";
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                MessageBox.Show($"Failed to remove permission after {maxRetries} attempts: {errorMessageFinal}\nInner Exception: {innerMessageFinal}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                statusLabel.Text = "Error removing permission.";
                                isUpdatingTreeView = false;
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                $"Failed to remove permission for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {errorMessageFinal}, Inner: {innerMessageFinal}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        }
                    }
                }
                else if (tvSubfolders.SelectedNode != null && tvSubfolders.SelectedNode.Tag is TreeNodeData selNodeData && selNodeData.IsSubfolder)
                {
                    string subfolderName = selNodeData.Subfolder.Name;
                    bool hasUniquePermissions = selNodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                    if (!hasUniquePermissions)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            statusLabel.Text = "Remove cancelled: Subfolder has inherited permissions.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                            $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                        return;
                    }
                    var confirm = MessageBox.Show($"Are you sure you want to remove all permissions for subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() =>
                        {
                            statusLabel.Text = "Remove cancelled by user.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, null, "Subfolder",
                            $"User cancelled removal for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }
                    UpdateUI(() =>
                    {
                        statusLabel.Text = $"Removing all permissions for '{subfolderName}'...";
                    });
                    retryCount = 0;
                    success = false;
                    lastException = null;
                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            using (var context = new ClientContext(_siteUrl))
                            {
                                context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                                context.Load(context.Web, w => w.ServerRelativeUrl);
                                await context.ExecuteQueryAsync();
                                subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.PrincipalId, ra => ra.Member.Title, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                context.Load(subfolder.ListItemAllFields, l => l.HasUniqueRoleAssignments);
                                await context.ExecuteQueryAsync();
                                var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsDebug", _libraryName, null, "Subfolder",
                                    $"Pre-reset RAs for '{subfolderName}': {string.Join(", ", existingRAs)}, Session ID: {debugSessionId}");
                                var removedGroups = new List<string>();
                                var removedGroupLogins = new List<string>();
                                foreach (var ra in subfolder.ListItemAllFields.RoleAssignments.ToList())
                                {
                                    if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup)
                                    {
                                        var groupName = ra.Member.Title;
                                        var groupLogin = ra.Member.LoginName;
                                        foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                            ra.RoleDefinitionBindings.Remove(rd);
                                        ra.Update();
                                        ra.DeleteObject();
                                        removedGroups.Add(groupName);
                                        removedGroupLogins.Add(groupLogin);
                                    }
                                }
                                if (removedGroups.Any())
                                {
                                    subfolder.ListItemAllFields.Update();
                                    await context.ExecuteQueryAsync();
                                    context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                    await context.ExecuteQueryAsync();
                                    var remainingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsDebug", _libraryName, null, "Subfolder",
                                        $"Post-reset RAs for '{subfolderName}': {string.Join(", ", remainingRAs)}, Session ID: {debugSessionId}");
                                    if (removedGroupLogins.Any(login => remainingRAs.Contains(login)))
                                        throw new Exception("Permission reset failed to apply (groups still present after verification).");
                                    foreach (var groupName in removedGroups)
                                        await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissions", _libraryName, groupName, "Subfolder",
                                            $"Reset permissions by removing group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                    UpdateUI(() => statusLabel.Text = $"Reset permissions for '{subfolderName}'.");
                                }
                                else
                                {
                                    UpdateUI(() => statusLabel.Text = $"No permissions to reset for '{subfolderName}'.");
                                }
                                UpdateUI(() =>
                                {
                                    isUpdatingTreeView = true;
                                    LoadCurrentPermissionsAsync(debugSessionId);
                                    tvSubfolders_AfterSelect(null, null);
                                    isUpdatingTreeView = false;
                                });
                                success = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsRetry", _libraryName, null, "Subfolder",
                                    $"Retry {retryCount} for resetting permissions on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            string errorMessage = ex.Message;
                            string innerMessage = ex.InnerException != null ? ex.InnerException.Message : "None";
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                MessageBox.Show($"Failed to reset permissions after {maxRetries} attempts: {errorMessage}\nInner Exception: {innerMessage}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                statusLabel.Text = "Error resetting permissions.";
                                isUpdatingTreeView = false;
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                                $"Failed to reset permissions for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        }
                    }
                }
                else
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a subfolder or a group to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Remove cancelled: No subfolder or group selected.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"No subfolder or group selected, Session ID: {debugSessionId}");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                string innerMessage = ex.InnerException != null ? ex.InnerException.Message : "None";
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to remove permissions: {errorMessage}\nInner Exception: {innerMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error removing permissions.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                    $"Failed to remove permissions: {ex.Message}, Inner: {innerMessage}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnRemove.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private async void btnBreakInheritance_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnBreakInheritance.Enabled = false; isUpdatingTreeView = false; });
            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Break inheritance cancelled: Invalid subfolder selection."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }
                string subfolderName = nodeData.Subfolder.Name;
                bool hasUniquePermissions = nodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                if (hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show($"Subfolder '{subfolderName}' already has unique permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Break inheritance cancelled: Already unique permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' already has unique permissions, Session ID: {debugSessionId}");
                    return;
                }
                var confirm = MessageBox.Show($"Are you sure you want to break role inheritance for subfolder '{subfolderName}'? This will clear all permissions and allow unique permissions to be set.",
                    "Confirm Break Inheritance", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => statusLabel.Text = "Break inheritance cancelled by user.");
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceCancelled", _libraryName, null, "Subfolder",
                        $"User cancelled break inheritance for '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }
                UpdateUI(() => statusLabel.Text = $"Breaking inheritance for '{subfolderName}'...");
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
                            context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            context.Load(subfolder.ListItemAllFields, i => i.Id, i => i.HasUniqueRoleAssignments, i => i.RoleAssignments);
                            await context.ExecuteQueryAsync();
                            subfolder.ListItemAllFields.BreakRoleInheritance(false, false);
                            await context.ExecuteQueryAsync();
                            await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritance", _libraryName, null, "Subfolder",
                                $"Broke role inheritance for subfolder '{subfolderName}' in library '{_libraryName}' at '{subfolderRelativeUrl}', Session ID: {debugSessionId}");
                            UpdateUI(() => statusLabel.Text = $"Role inheritance broken for '{subfolderName}'.");
                            success = true;
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                LoadCurrentPermissionsAsync(debugSessionId);
                                tvSubfolders_AfterSelect(null, null);
                                isUpdatingTreeView = false;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceRetry", _libraryName, null, "Subfolder",
                                $"Retry {retryCount} for breaking inheritance on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to break inheritance after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error breaking inheritance.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                            $"Failed to break inheritance for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to break inheritance: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error breaking inheritance.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                    $"Failed to break inheritance: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnBreakInheritance.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private async void btnChange_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => btnChange.Enabled = false);
            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a group.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Change cancelled: No group selected."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"No group selected, Session ID: {debugSessionId}");
                    return;
                }
                var subfolderNode = tvSubfolders.SelectedNode.Parent;
                var subfolderData = subfolderNode.Tag as TreeNodeData;
                string subfolderName = subfolderData.Subfolder.Name;
                bool hasUniquePermissions = subfolderData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Change cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }
                using (var dlg = new ChangeSubPermissionTypeDialog(nodeData.GroupName, nodeData.Permission))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var newPermission = dlg.SelectedPermissionType;
                        if (string.IsNullOrEmpty(newPermission) || newPermission == nodeData.Permission)
                        {
                            UpdateUI(() => { MessageBox.Show("No change in permission type selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Change cancelled: No new permission type selected."; });
                            await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionCancelled", _libraryName, nodeData.GroupName, "Subfolder",
                                $"No change in permission type, Current: {nodeData.Permission}, Selected: {newPermission}, Session ID: {debugSessionId}");
                            return;
                        }
                        await ChangeGroupPermissionType(subfolderName, nodeData.GroupName, nodeData.GroupId, nodeData.Permission, newPermission);
                    }
                    else
                    {
                        UpdateUI(() => statusLabel.Text = "Change cancelled by user.");
                        await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionCancelled", _libraryName, nodeData.GroupName, "Subfolder",
                            $"User cancelled permission type change for group '{nodeData.GroupName}' on subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to initiate permission change: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error initiating permission change.";
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, null, "Subfolder",
                    $"Failed to initiate permission change: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => btnChange.Enabled = true);
            }
        }
        private async Task ChangeGroupPermissionType(string subfolderName, string groupName, string groupId, string oldPermission, string newPermission)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            string groupLogin = $"c:0t.c|tenant|{groupId}";
            UpdateUI(() => btnChange.Enabled = false);
            try
            {
                if (string.IsNullOrEmpty(subfolderName) || string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(newPermission))
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Invalid subfolder, group, or permission data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        statusLabel.Text = "Change cancelled: Invalid data.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                        $"Invalid input data: subfolderName='{subfolderName}', groupName='{groupName}', groupId='{groupId}', newPermission='{newPermission}', Session ID: {debugSessionId}");
                    return;
                }
                var subfolderNode = tvSubfolders.Nodes.Cast<TreeNode>().FirstOrDefault(n => (n.Tag as TreeNodeData)?.Subfolder.Name == subfolderName);
                if (subfolderNode == null || !(subfolderNode.Tag is TreeNodeData subfolderData) || !subfolderData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Change cancelled: Subfolder has inherited permissions.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        statusLabel.Text = "Error: No signed-in account.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
                Exception lastException = null;
                string subfolderRelativeUrl = null;
                if (newPermission == "No Direct Access")
                {
                    var confirm = MessageBox.Show($"Are you sure you want to remove permissions for '{groupName}' from subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                        "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() =>
                        {
                            statusLabel.Text = "Remove cancelled by user.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, groupName, "Subfolder",
                            $"User cancelled removal for group '{groupName}' on subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }
                    UpdateUI(() =>
                    {
                        statusLabel.Text = $"Removing permission for '{groupName}' from '{subfolderName}'...";
                    });
                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            using (var context = new ClientContext(_siteUrl))
                            {
                                context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                                context.Load(context.Web, w => w.ServerRelativeUrl);
                                await context.ExecuteQueryAsync();
                                subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                await context.ExecuteQueryAsync();
                                var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, groupName, "Subfolder",
                                    $"Pre-removal RAs for '{subfolderName}': {string.Join(", ", existingRAs)}, Session ID: {debugSessionId}");
                                var raToRemove = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupLogin && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                                if (raToRemove != null)
                                {
                                    foreach (RoleDefinition rd in raToRemove.RoleDefinitionBindings.ToList())
                                        raToRemove.RoleDefinitionBindings.Remove(rd);
                                    raToRemove.Update();
                                    raToRemove.DeleteObject();
                                    await context.ExecuteQueryAsync();
                                    context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                    await context.ExecuteQueryAsync();
                                    var remainingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, groupName, "Subfolder",
                                        $"Post-removal RAs for '{subfolderName}': {string.Join(", ", remainingRAs)}, Session ID: {debugSessionId}");
                                    if (remainingRAs.Contains(groupLogin))
                                        throw new Exception("Permission removal failed to apply (group still present after verification). Check 'Manage Permissions' rights or sharing links in SharePoint UI.");
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, groupName, "Subfolder",
                                        $"Removed permissions for group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                    UpdateUI(() =>
                                    {
                                        isUpdatingTreeView = true;
                                        try
                                        {
                                            statusLabel.Text = $"Removed permissions for '{groupName}' from '{subfolderName}'.";
                                            tvSubfolders.SelectedNode.Remove();
                                            LoadCurrentPermissionsAsync(debugSessionId);
                                            tvSubfolders_AfterSelect(null, null);
                                        }
                                        finally
                                        {
                                            isUpdatingTreeView = false;
                                        }
                                    });
                                    success = true;
                                }
                                else
                                {
                                    UpdateUI(() =>
                                    {
                                        MessageBox.Show($"Group '{groupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        statusLabel.Text = "Change cancelled: Group not found.";
                                    });
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                                        $"Group '{groupName}' not found in permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                    return;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                string errorMessage = ex.Message;
                                string innerMessage = ex.InnerException != null ? ex.InnerException.Message : "None";
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionRetry", _libraryName, groupName, "Subfolder",
                                    $"Retry {retryCount} for removing permission from '{subfolderName}': {errorMessage}, Inner: {innerMessage}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            string errorMessageFinal = ex.Message;
                            string innerMessageFinal = ex.InnerException != null ? ex.InnerException.Message : "None";
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                MessageBox.Show($"Failed to remove permission after {maxRetries} attempts: {errorMessageFinal}\nInner Exception: {innerMessageFinal}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                statusLabel.Text = "Error removing permission.";
                                isUpdatingTreeView = false;
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                                $"Failed to remove permission for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {errorMessageFinal}, Inner: {innerMessageFinal}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                            return;
                        }
                    }
                    return;
                }
                UpdateUI(() =>
                {
                    statusLabel.Text = $"Changing permission for '{groupName}' to '{newPermission}' in '{subfolderName}'...";
                });
                retryCount = 0;
                success = false;
                lastException = null;
                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                            await context.ExecuteQueryAsync();
                            var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                            await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionDebug", _libraryName, groupName, "Subfolder",
                                $"Pre-change RAs for '{subfolderName}': {string.Join(", ", existingRAs)}, Session ID: {debugSessionId}");
                            var raToUpdate = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupLogin && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                            if (raToUpdate == null)
                            {
                                UpdateUI(() =>
                                {
                                    MessageBox.Show($"Group '{groupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    statusLabel.Text = "Change cancelled: Group not found.";
                                });
                                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                                    $"Group '{groupName}' not found in permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                return;
                            }
                            context.Load(context.Web.RoleDefinitions);
                            await context.ExecuteQueryAsync();
                            string targetRoleName = newPermission == "Edit" ? "Contribute" : newPermission;
                            var roleDefinition = context.Web.RoleDefinitions.FirstOrDefault(rd => rd.Name == targetRoleName);
                            if (roleDefinition == null)
                            {
                                UpdateUI(() =>
                                {
                                    MessageBox.Show($"Permission level '{newPermission}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    statusLabel.Text = "Error: Permission level not found.";
                                });
                                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                                    $"Permission level '{newPermission}' not found for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                return;
                            }
                            foreach (RoleDefinition rd in raToUpdate.RoleDefinitionBindings.ToList())
                                raToUpdate.RoleDefinitionBindings.Remove(rd);
                            raToUpdate.RoleDefinitionBindings.Add(roleDefinition);
                            raToUpdate.Update();
                            await context.ExecuteQueryAsync();
                            context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings));
                            await context.ExecuteQueryAsync();
                            var updatedRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                            await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionDebug", _libraryName, groupName, "Subfolder",
                                $"Post-change RAs for '{subfolderName}': {string.Join(", ", updatedRAs)}, Session ID: {debugSessionId}");
                            bool permissionChanged = updatedRAs.Any(ra => ra.Contains(groupLogin) && ra.Contains(targetRoleName));
                            if (!permissionChanged)
                                throw new Exception($"Permission '{targetRoleName}' for group '{groupName}' was not applied to '{subfolderName}'.");
                            await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermission", _libraryName, groupName, "Subfolder",
                                $"Changed permission for group '{groupName}' from '{oldPermission}' to '{newPermission}' on subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                try
                                {
                                    statusLabel.Text = $"Changed permission for '{groupName}' to '{newPermission}' on '{subfolderName}'.";
                                    var groupNode = tvSubfolders.SelectedNode;
                                    if (groupNode != null)
                                        groupNode.Text = $"{groupName}: {newPermission}";
                                    LoadCurrentPermissionsAsync(debugSessionId);
                                    tvSubfolders_AfterSelect(null, null);
                                }
                                finally
                                {
                                    isUpdatingTreeView = false;
                                }
                            });
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            string errorMessage = ex.Message;
                            string innerMessage = ex.InnerException != null ? ex.InnerException.Message : "None";
                            await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionRetry", _libraryName, groupName, "Subfolder",
                                $"Retry {retryCount} for changing permission on '{subfolderName}': {errorMessage}, Inner: {innerMessage}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        string errorMessageFinal = ex.Message;
                        string innerMessageFinal = ex.InnerException != null ? ex.InnerException.Message : "None";
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to change permission after {maxRetries} attempts: {errorMessageFinal}\nInner Exception: {innerMessageFinal}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error changing permission.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                            $"Failed to change permission for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {errorMessageFinal}, Inner: {innerMessageFinal}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                string innerMessage = ex.InnerException != null ? ex.InnerException.Message : "None";
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to change permission: {errorMessage}\nInner Exception: {innerMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error changing permission.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                    $"Failed to change permission: {errorMessage}, Inner: {innerMessage}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => btnChange.Enabled = true);
            }
        }
        private async void btnResetPermissions_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnResetPermissions.Enabled = false; isUpdatingTreeView = false; });
            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Reset cancelled: Invalid subfolder selection."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }
                string subfolderName = nodeData.Subfolder.Name;
                bool hasUniquePermissions = nodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Reset cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }
                var confirm = MessageBox.Show($"Are you sure you want to remove all permissions for subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                    "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => statusLabel.Text = "Reset cancelled by user.");
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsCancelled", _libraryName, null, "Subfolder",
                        $"User cancelled removal for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }
                UpdateUI(() => statusLabel.Text = $"Removing all permissions for '{subfolderName}'...");
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
                            context.ExecutingWebRequest += (s, ev) => { ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.PrincipalId, ra => ra.Member.Title, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                            context.Load(subfolder.ListItemAllFields, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();
                            var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                            await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsDebug", _libraryName, null, "Subfolder",
                                $"Pre-reset RAs for '{subfolderName}': {string.Join(", ", existingRAs)}, Session ID: {debugSessionId}");
                            var removedGroups = new List<string>();
                            var removedGroupLogins = new List<string>();
                            foreach (var ra in subfolder.ListItemAllFields.RoleAssignments.ToList())
                            {
                                if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup)
                                {
                                    var groupName = ra.Member.Title;
                                    var groupLogin = ra.Member.LoginName;
                                    foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                        ra.RoleDefinitionBindings.Remove(rd);
                                    ra.Update();
                                    ra.DeleteObject();
                                    removedGroups.Add(groupName);
                                    removedGroupLogins.Add(groupLogin);
                                }
                            }
                            if (removedGroups.Any())
                            {
                                subfolder.ListItemAllFields.Update();
                                await context.ExecuteQueryAsync();
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                await context.ExecuteQueryAsync();
                                var remainingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsDebug", _libraryName, null, "Subfolder",
                                    $"Post-reset RAs for '{subfolderName}': {string.Join(", ", remainingRAs)}, Session ID: {debugSessionId}");
                                if (removedGroupLogins.Any(login => remainingRAs.Contains(login)))
                                    throw new Exception("Permission reset failed to apply (groups still present after verification).");
                                foreach (var groupName in removedGroups)
                                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissions", _libraryName, groupName, "Subfolder",
                                        $"Reset permissions by removing group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                UpdateUI(() => statusLabel.Text = $"Reset permissions for '{subfolderName}'.");
                            }
                            else
                            {
                                UpdateUI(() => statusLabel.Text = $"No permissions to reset for '{subfolderName}'.");
                            }
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                LoadCurrentPermissionsAsync(debugSessionId);
                                tvSubfolders_AfterSelect(null, null);
                                isUpdatingTreeView = false;
                            });
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsRetry", _libraryName, null, "Subfolder",
                                $"Retry {retryCount} for resetting permissions on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to reset permissions after {maxRetries} attempts: {ex.Message}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error resetting permissions.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                            $"Failed to reset permissions for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to reset permissions: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error resetting permissions.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                    $"Failed to reset permissions: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnResetPermissions.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private void tvSubfolders_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                tvSubfolders.SelectedNode = e.Node;
                var nodeData = e.Node.Tag as TreeNodeData;
                if (nodeData == null) return;
                var contextMenu = new ContextMenuStrip();
                if (nodeData.IsSubfolder)
                {
                    if (nodeData.Subfolder.ListItemAllFields.HasUniqueRoleAssignments)
                    {
                        contextMenu.Items.Add("Add Permission", null, (s, ev) => btnAdd_Click(s, ev));
                        contextMenu.Items.Add("Reset Permissions", null, (s, ev) => btnResetPermissions_Click(s, ev));
                    }
                    else
                    {
                        contextMenu.Items.Add("Break Inheritance", null, (s, ev) => btnBreakInheritance_Click(s, ev));
                    }
                }
                else
                {
                    contextMenu.Items.Add("Change Permission", null, (s, ev) => btnChange_Click(s, ev));
                    contextMenu.Items.Add("Remove Permission", null, (s, ev) => btnRemove_Click(s, ev));
                }
                contextMenu.Show(tvSubfolders, e.Location);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            UpdateUI(() => this.Close());
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            UpdateUI(() => btnRefresh.Enabled = false);
            try
            {
                // Bypass debounce for manual refresh
                lastRefreshTime = DateTime.MinValue;
                await LoadCurrentPermissionsAsync();
            }
            finally
            {
                UpdateUI(() => btnRefresh.Enabled = true);
            }
        }
    }
}
