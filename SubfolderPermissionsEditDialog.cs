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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Threading;

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
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly object _progressLock = new object();
        private System.Windows.Forms.ProgressBar progressBar;
        private readonly Color _placeholderColor = SystemColors.GrayText;
        private readonly Color _normalColor = SystemColors.ControlText;
        private readonly string _placeholderText = "Search subfolders or groups...";
        private TreeNodeCollection _originalNodes; // Store original tree nodes for restoring
        private List<(string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups)> _subfolderCache;
        private System.Windows.Forms.ToolTip toolTipGroups;
        private System.Windows.Forms.ComboBox cmbGroups;

        public SubfolderPermissionsEditDialog(string libraryName, IPublicClientApplication pca, string siteUrl, GraphServiceClient graphClient, AuditLogManager auditLogManager, string signedInUserId)
        {
            if (string.IsNullOrEmpty(libraryName)) throw new ArgumentNullException(nameof(libraryName));
            if (pca == null) throw new ArgumentNullException(nameof(pca));
            if (string.IsNullOrEmpty(siteUrl)) throw new ArgumentNullException(nameof(siteUrl));
            if (auditLogManager == null) throw new ArgumentNullException(nameof(auditLogManager));
            if (string.IsNullOrEmpty(signedInUserId)) throw new ArgumentNullException(nameof(signedInUserId));

            _libraryName = libraryName;
            _pca = pca;
            _siteUrl = siteUrl;
            _graphClient = graphClient;
            _auditLogManager = auditLogManager;
            _signedInUserId = signedInUserId;
            _subfolderCache = new List<(string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)>)>();
            _cancellationTokenSource = new CancellationTokenSource();

            InitializeComponent();
            this.Text = $"Edit Subfolder Permissions: {libraryName}";

            // Initialize search bar and view filter
            UpdateUI(() =>
            {
                if (txtSearch != null)
                {
                    txtSearch.ForeColor = _placeholderColor;
                    txtSearch.Text = _placeholderText;
                    txtSearch.Enter += txtSearch_Enter;
                    txtSearch.Leave += txtSearch_Leave;
                    txtSearch.TextChanged += txtSearch_TextChanged;
                }
                if (cmbView != null)
                {
                    cmbView.SelectedIndex = 0; // Default to "All Subfolders"
                    cmbView.SelectedIndexChanged += cmbView_SelectedIndexChanged;
                }
            });

            // Clear cache on dialog close
            this.FormClosing += (s, e) => _subfolderCache = null;

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
            public string SubfolderName { get; set; }
            public string GroupId { get; set; }
            public string GroupName { get; set; }
            public string Permission { get; set; }
            public bool IsPreview { get; set; } // Added to identify preview nodes
        }
        private void txtSearch_Enter(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (txtSearch.Text == _placeholderText)
                {
                    txtSearch.Text = string.Empty;
                    txtSearch.ForeColor = _normalColor;
                }
            });
        }

        // Event handler to restore placeholder text if empty
        private void txtSearch_Leave(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = _placeholderText;
                    txtSearch.ForeColor = _placeholderColor;
                }
            });
        }

          private void PopulateTreeView(List<(string SubfolderName, bool HasUniquePermissions,
            List<(string GroupName, string GroupId, string Role)> Groups)> cache, string viewFilter)
            {
            var nodes = new List<TreeNode>();
            foreach (var subfolder in cache)
            {
                var subfolderNode = new TreeNode
                {
                    Text = subfolder.HasUniquePermissions
                        ? $"{subfolder.SubfolderName} (Unique, {subfolder.Groups.Count} CSG group{(subfolder.Groups.Count == 1 ? "" : "s")} assigned)"
                        : $"{subfolder.SubfolderName} (Inherited)",
                    ImageIndex = 0, // Folder icon
                    SelectedImageIndex = 0,
                    Tag = new TreeNodeData { IsSubfolder = true, SubfolderName = subfolder.SubfolderName }
                };

                foreach (var group in subfolder.Groups)
                {
                    subfolderNode.Nodes.Add(new TreeNode
                    {
                        Text = $"{group.GroupName}: {group.Role}",
                        ImageIndex = 1, // Group icon
                        SelectedImageIndex = 1,
                        Tag = new TreeNodeData { IsSubfolder = false, GroupId = group.GroupId, GroupName = group.GroupName, Permission = group.Role }
                    });
                }

                nodes.Add(subfolderNode);
            }
            tvSubfolders.Nodes.AddRange(nodes.ToArray());
        }
        private void cmbView_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (isUpdatingTreeView || _subfolderCache == null)
                    return;

                string query = txtSearch.Text.Trim().ToLower() == _placeholderText.ToLower() ? string.Empty : txtSearch.Text.Trim().ToLower();
                string viewFilter = cmbView.SelectedItem?.ToString() ?? "All Subfolders";

                tvSubfolders.Nodes.Clear();
                var filteredCache = _subfolderCache
                    .Where(s =>
                        (viewFilter == "All Subfolders" ||
                         (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                         (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions)) &&
                        (string.IsNullOrWhiteSpace(query) || s.SubfolderName.ToLower().Contains(query) || s.Groups.Any(g => g.GroupName.ToLower().Contains(query))))
                    .ToList();

                PopulateTreeView(filteredCache, viewFilter);
                tvSubfolders.ExpandAll();
            });
        }

        // Event handler for search filtering
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (isUpdatingTreeView || _subfolderCache == null)
                    return;

                string query = txtSearch.Text.Trim().ToLower();
                string viewFilter = cmbView.SelectedItem?.ToString() ?? "All Subfolders";
                tvSubfolders.Nodes.Clear();

                if (query == _placeholderText.ToLower() || string.IsNullOrWhiteSpace(query))
                {
                    PopulateTreeView(_subfolderCache.Where(s =>
                        viewFilter == "All Subfolders" ||
                        (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                        (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions)).ToList(), viewFilter);
                    return;
                }

                var filteredCache = _subfolderCache
                    .Where(s =>
                        (viewFilter == "All Subfolders" ||
                         (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                         (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions)) &&
                        (s.SubfolderName.ToLower().Contains(query) || s.Groups.Any(g => g.GroupName.ToLower().Contains(query))))
                    .ToList();

                PopulateTreeView(filteredCache, viewFilter);
                tvSubfolders.ExpandAll();
            });
        }        // Helper method to clone a single TreeNode
        private TreeNode CloneTreeNode(TreeNode node)
        {
            var newNode = new TreeNode
            {
                Text = node.Text,
                ImageIndex = node.ImageIndex,
                SelectedImageIndex = node.SelectedImageIndex,
                Tag = node.Tag // Shallow copy is fine for TreeNodeData
            };
            foreach (TreeNode child in node.Nodes)
            {
                newNode.Nodes.Add(CloneTreeNode(child));
            }
            return newNode;
        }

        // Helper method to clone a TreeNodeCollection
        private TreeNodeCollection CloneTreeNodes(TreeNodeCollection nodes)
        {
            var newNodes = new TreeNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                newNodes[i] = CloneTreeNode(nodes[i]);
            }
            var tempTreeView = new System.Windows.Forms.TreeView();
            tempTreeView.Nodes.AddRange(newNodes);
            return tempTreeView.Nodes;
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
            if (DateTime.UtcNow - lastRefreshTime < minimumRefreshInterval && debugSessionId != null)
            {
                await _auditLogManager?.LogAction(_signedInUserId, null, "DebugLoadPermissionsSkipped", _libraryName, null, "Subfolder",
                    $"Refresh skipped due to debounce, time since last refresh: {(DateTime.UtcNow - lastRefreshTime).TotalSeconds}s, Session ID: {debugSessionId}");
                return;
            }

            string selectedSubfolderName = null;
            var nodesToAdd = new List<TreeNode>();
            var auditLogs = new List<(string Action, string GroupName, string Details)>();
            _subfolderCache = new List<(string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)>)>();
            UpdateUI(() =>
            {
                try
                {
                    isUpdatingTreeView = true;
                    if (tvSubfolders != null && tvSubfolders.SelectedNode?.Tag is TreeNodeData nodeData && nodeData.IsSubfolder)
                    {
                        selectedSubfolderName = nodeData.SubfolderName;
                    }
                    tvSubfolders?.Nodes.Clear();
                    if (statusLabel != null) statusLabel.Text = "Loading subfolder permissions...";
                    if (progressBar != null)
                    {
                        progressBar.Value = 0;
                        progressBar.Visible = true;
                    }
                    if (btnRefresh != null) btnRefresh.Enabled = false;
                }
                catch (Exception ex)
                {
                    selectedSubfolderName = null;
                    if (statusLabel != null) statusLabel.Text = "Warning: Subfolder selection invalid during load.";
                    auditLogs.Add(("DebugLoadPermissionsSelectionError", null, $"Failed to get selected subfolder: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}"));
                }
                finally
                {
                    isUpdatingTreeView = false;
                }
            });

            try
            {
                if (_pca == null || string.IsNullOrEmpty(_siteUrl))
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Authentication or site configuration is invalid.", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        if (statusLabel != null) statusLabel.Text = "Error: Invalid configuration.";
                        if (progressBar != null) progressBar.Visible = false;
                    });
                    auditLogs.Add(("LoadSubfolderPermissionsError", null, $"Invalid PCA or site URL, Session ID: {debugSessionId}"));
                    await LogAuditBatchAsync(auditLogs);
                    return;
                }

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                auditLogs.Add(("DebugLoadPermissionsAuthStart", null, $"Acquiring authentication token for scopes: {string.Join(", ", scopes)}, Session ID: {debugSessionId}"));
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (statusLabel != null) statusLabel.Text = "Error: No signed-in account.";
                        if (progressBar != null) progressBar.Visible = false;
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
                    var folder = library?.RootFolder;
                    if (folder == null)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Library '{_libraryName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            if (statusLabel != null) statusLabel.Text = "Error: Library not found.";
                            if (progressBar != null) progressBar.Visible = false;
                        });
                        auditLogs.Add(("LoadSubfolderPermissionsError", null, $"Library '{_libraryName}' not found, Session ID: {debugSessionId}"));
                        await LogAuditBatchAsync(auditLogs);
                        return;
                    }
                    context.Load(folder, f => f.Folders.Include(f => f.Name, f => f.ServerRelativeUrl,
                        f => f.ListItemAllFields.HasUniqueRoleAssignments, f => f.ListItemAllFields.RoleAssignments.Include(
                        ra => ra.Member.Title, ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings)));
                    await context.ExecuteQueryAsync();
                    auditLogs.Add(("DebugLoadPermissionsQueryExecuted", null, $"Query executed, Folder count: {folder.Folders?.Count ?? 0}, Session ID: {debugSessionId}"));

                    var subfolders = folder.Folders?.Where(f => !f.Name.StartsWith("Forms")).ToList() ?? new List<Folder>();
                    auditLogs.Add(("DebugLoadPermissionsSubfoldersFiltered", null, $"Filtered subfolders: {subfolders.Count}, Names: {string.Join(", ", subfolders.Select(f => f.Name))}, Session ID: {debugSessionId}"));

                    if (!subfolders.Any())
                    {
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show("No subfolders found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (statusLabel != null) statusLabel.Text = "No subfolders found.";
                            if (progressBar != null) progressBar.Visible = false;
                            if (btnChange != null) btnChange.Enabled = false;
                            if (btnAdd != null) btnAdd.Enabled = false;
                            if (btnRemove != null) btnRemove.Enabled = false;
                            UpdateSidebar();
                            isUpdatingTreeView = false;
                        });
                        auditLogs.Add(("LoadSubfolderPermissionsNoSubfolders", null, $"No subfolders found in library '{_libraryName}', Session ID: {debugSessionId}"));
                        await LogAuditBatchAsync(auditLogs);
                        return;
                    }

                    int totalSubfolders = subfolders.Count;
                    int processedSubfolders = 0;
                    int progressUpdateInterval = Math.Max(1, totalSubfolders / 20);

                    foreach (var subfolder in subfolders)
                    {
                        if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                        {
                            UpdateUI(() =>
                            {
                                if (statusLabel != null) statusLabel.Text = "Loading cancelled by user.";
                                if (progressBar != null) progressBar.Visible = false;
                            });
                            auditLogs.Add(("LoadSubfolderPermissionsCancelled", null, $"Loading permissions cancelled, Session ID: {debugSessionId}"));
                            await LogAuditBatchAsync(auditLogs);
                            return;
                        }

                        auditLogs.Add(("DebugLoadPermissionsProcessSubfolder", null, $"Processing subfolder: {subfolder.Name}, ServerRelativeUrl: {subfolder.ServerRelativeUrl}, Session ID: {debugSessionId}"));

                        var perms = new List<string>();
                        var groupList = new List<(string GroupName, string GroupId, string Role)>();
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
                                            var groupId = ra.Member.LoginName?.Split('|').Last() ?? string.Empty;
                                            groupList.Add((ra.Member.Title, groupId, role));
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
                            Tag = new TreeNodeData { IsSubfolder = true, SubfolderName = subfolder.Name }
                        };

                        foreach (var group in groupList)
                        {
                            subfolderNode.Nodes.Add(new TreeNode
                            {
                                Text = $"{group.GroupName}: {group.Role}",
                                ImageIndex = 1,
                                SelectedImageIndex = 1,
                                Tag = new TreeNodeData { IsSubfolder = false, GroupId = group.GroupId, GroupName = group.GroupName, Permission = group.Role }
                            });
                            auditLogs.Add(("DebugLoadGroupDetails", group.GroupName, $"Added group '{group.GroupName}' with role '{group.Role}' to TreeView for subfolder: {subfolder.Name}, Session ID: {debugSessionId}"));
                        }

                        _subfolderCache.Add((subfolder.Name, subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false, groupList));
                        nodesToAdd.Add(subfolderNode);
                        processedSubfolders++;
                        if (processedSubfolders % progressUpdateInterval == 0 || processedSubfolders == totalSubfolders)
                        {
                            UpdateUI(() => { if (progressBar != null) progressBar.Value = totalSubfolders > 0 ? (int)((processedSubfolders / (double)totalSubfolders) * 100) : 0; });
                        }

                        if (string.Equals(subfolder.Name, selectedSubfolderName, StringComparison.OrdinalIgnoreCase))
                        {
                            subfolderNode.Expand();
                        }
                    }

                    UpdateUI(() =>
                    {
                        isUpdatingTreeView = true;
                        if (tvSubfolders != null) tvSubfolders.Nodes.AddRange(nodesToAdd.ToArray());
                        if (statusLabel != null) statusLabel.Text = "Permissions loaded.";
                        if (progressBar != null)
                        {
                            progressBar.Value = 100;
                            progressBar.Visible = false;
                        }
                        UpdateSidebar();
                        if (tvSubfolders != null && tvSubfolders.Nodes.Cast<TreeNode>().Any(n => (n.Tag as TreeNodeData)?.SubfolderName == selectedSubfolderName))
                        {
                            tvSubfolders.SelectedNode = tvSubfolders.Nodes.Cast<TreeNode>().First(n => (n.Tag as TreeNodeData)?.SubfolderName == selectedSubfolderName);
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
                    if (statusLabel != null) statusLabel.Text = "Error loading permissions.";
                    if (progressBar != null) progressBar.Visible = false;
                    if (btnChange != null) btnChange.Enabled = false;
                    if (btnAdd != null) btnAdd.Enabled = false;
                    if (btnRemove != null) btnRemove.Enabled = false;
                    UpdateSidebar();
                    isUpdatingTreeView = false;
                });
                auditLogs.Add(("LoadSubfolderPermissionsError", null, $"Failed to load subfolder permissions: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}"));
                await LogAuditBatchAsync(auditLogs);
            }
            finally
            {
                UpdateUI(() => { if (btnRefresh != null) btnRefresh.Enabled = true; });
            }
        }
        private async Task LogAuditBatchAsync(List<(string Action, string GroupName, string Details)> logs)
        {
            foreach (var log in logs)
            {
                await _auditLogManager.LogAction(_signedInUserId, null, log.Action, _libraryName, log.GroupName, "Subfolder", log.Details);
            }
        }
        private void UpdateSidebar()
        {
            // Removed the redundant outer UpdateUI call; now using a single UpdateUI to marshal the entire block to the UI thread.
            // This ensures thread safety in WinForms without unnecessary nesting.
            UpdateUI(() =>
            {
                isUpdatingTreeView = true;
                try
                {
                    // Explicit null checks for safety; these prevent NullReferenceExceptions if controls aren't initialized.
                    if (lblSelectedItem == null || btnAdd == null || btnChange == null || btnRemove == null ||
                        btnBreakInheritance == null || btnResetPermissions == null || chkRead == null ||
                        chkEdit == null || chkNoAccess == null || cmbGroups == null || tvSubfolders == null || statusLabel == null)
                    {
                        // Log or handle if needed; for now, just return to avoid crashes.
                        return;
                    }

                    lblSelectedItem.Text = "Selected Item: None";
                    btnAdd.Enabled = false;
                    btnChange.Enabled = false;
                    btnRemove.Enabled = false;
                    btnBreakInheritance.Enabled = false;
                    btnResetPermissions.Enabled = false;
                    chkRead.Checked = false;
                    chkEdit.Checked = false;
                    chkNoAccess.Checked = false;
                    chkRead.Enabled = false;
                    chkEdit.Enabled = false;
                    chkNoAccess.Enabled = false;
                    // Preserve group selection
                    var currentGroupSelection = cmbGroups?.SelectedItem;
                    cmbGroups.Enabled = false;

                    if (tvSubfolders?.SelectedNode == null)
                    {
                        lblSelectedItem.Text = "Selected Item: None";
                        statusLabel.Text = "Select a subfolder to view or modify its permissions.";
                        return;
                    }

                    var nodeData = tvSubfolders.SelectedNode.Tag as TreeNodeData;
                    if (nodeData == null)
                    {
                        statusLabel.Text = "Invalid selection.";
                        return;
                    }

                    if (nodeData.IsSubfolder)
                    {
                        lblSelectedItem.Text = $"Subfolder: {nodeData.SubfolderName}";
                        var cacheEntry = _subfolderCache?.FirstOrDefault(s => string.Equals(s.SubfolderName, nodeData.SubfolderName, StringComparison.OrdinalIgnoreCase)) ??
                           (nodeData.SubfolderName, false, new List<(string GroupName, string GroupId, string Role)>());
                        bool hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                        bool hasAssignedPermissions = cacheEntry.Groups.Any();

                        btnBreakInheritance.Enabled = !hasUniquePermissions;
                        btnResetPermissions.Enabled = hasUniquePermissions && hasAssignedPermissions; // Disable if no permissions assigned
                        btnAdd.Enabled = hasUniquePermissions;
                        btnChange.Enabled = false;
                        btnRemove.Enabled = false;
                        cmbGroups.Enabled = hasUniquePermissions;
                        chkRead.Enabled = hasUniquePermissions;
                        chkEdit.Enabled = hasUniquePermissions;
                        // Fixed: For subfolder nodes, disable chkNoAccess entirely, as "No Direct Access" doesn't make sense for adding a new permission (it's for removal on existing groups). Enabling it only if hasAssignedPermissions was inconsistent with add logic.
                        chkNoAccess.Enabled = false; // Changed from 'hasUniquePermissions && hasAssignedPermissions' to false for consistency.
                        statusLabel.Text = hasUniquePermissions ? "Select a group and permission level to add or modify permissions." :
                            "Subfolder has inherited permissions. Break inheritance to modify.";
                    }
                    else
                    {
                        lblSelectedItem.Text = $"Group: {nodeData.GroupName} ({nodeData.Permission})";
                        btnBreakInheritance.Enabled = false;
                        btnResetPermissions.Enabled = false;
                        btnAdd.Enabled = false;
                        btnChange.Enabled = false;
                        btnRemove.Enabled = true;
                        cmbGroups.Enabled = false;
                        chkRead.Enabled = true;
                        chkEdit.Enabled = true;
                        chkNoAccess.Enabled = true;
                        chkRead.Checked = nodeData.Permission == "Read";
                        chkEdit.Checked = nodeData.Permission == "Edit";
                        chkNoAccess.Checked = nodeData.Permission == "No Direct Access";
                        statusLabel.Text = "Select a permission type or remove the group.";
                    }

                    // Restore group selection
                    if (currentGroupSelection != null && cmbGroups.Enabled)
                    {
                        cmbGroups.SelectedItem = currentGroupSelection;
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
        private void cmbGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingTreeView || tvSubfolders?.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData selectedNodeData) || !selectedNodeData.IsSubfolder)
                return;

            string permissionLevel = chkRead.Checked ? "Read" : chkEdit.Checked ? "Edit" : chkNoAccess.Checked ? "No Direct Access" : null;

            UpdateUI(() =>
            {
                isUpdatingTreeView = true;
                // Remove any existing preview node
                foreach (TreeNode node in tvSubfolders.SelectedNode.Nodes)
                {
                    if (node.Tag is TreeNodeData nodeData && nodeData.IsPreview)
                        node.Remove();
                }

                if (permissionLevel != null && cmbGroups.SelectedItem is GroupItem selectedGroup)
                {
                    // Add a preview group node in green
                    var previewNode = new TreeNode
                    {
                        Text = $"{selectedGroup.DisplayName}: {permissionLevel} (Pending)",
                        ImageIndex = 1,
                        SelectedImageIndex = 1,
                        ForeColor = System.Drawing.Color.Green, // Highlight preview node in green
                        Tag = new TreeNodeData
                        {
                            IsSubfolder = false,
                            GroupId = selectedGroup.Id,
                            GroupName = selectedGroup.DisplayName,
                            Permission = permissionLevel,
                            IsPreview = true
                        }
                    };
                    tvSubfolders.SelectedNode.Nodes.Add(previewNode);
                    statusLabel.Text = $"Selected '{permissionLevel}' for group '{selectedGroup.DisplayName}' on '{selectedNodeData.SubfolderName}'. Click 'Add Permission' to confirm.";
                }
                else
                {
                    statusLabel.Text = permissionLevel != null ? $"Selected '{permissionLevel}' for '{selectedNodeData.SubfolderName}'. Select a group and click 'Add Permission' to assign." : "Select a permission level to add to a group.";
                }

                // Restore TreeView selection
                if (tvSubfolders.SelectedNode != null && tvSubfolders.Nodes.Contains(tvSubfolders.SelectedNode))
                    tvSubfolders.SelectedNode = tvSubfolders.SelectedNode;
                tvSubfolders.ExpandAll();
                isUpdatingTreeView = false;
            });
        }
        private async void btnAdd_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            GroupItem selectedGroup = null;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnAdd.Enabled = false; isUpdatingTreeView = false; });
            try
            {
                if (tvSubfolders?.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Invalid subfolder selection."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }
                if (cmbGroups?.SelectedItem == null)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a group from the dropdown.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: No group selected."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"No group selected, Session ID: {debugSessionId}");
                    return;
                }
                bool readChecked = chkRead?.Checked ?? false;
                bool editChecked = chkEdit?.Checked ?? false;
                bool noAccessChecked = chkNoAccess?.Checked ?? false;
                await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, null, "Subfolder",
                    $"Checkbox states: Read={readChecked}, Edit={editChecked}, NoAccess={noAccessChecked}, Session ID: {debugSessionId}");

                if (!readChecked && !editChecked && !noAccessChecked)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a permission level (Read, Edit, or No Direct Access).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: No permission level selected."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"No permission level selected, Session ID: {debugSessionId}");
                    return;
                }
                subfolderName = nodeData.SubfolderName;
                var cacheEntry = _subfolderCache?.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase)) ??
                    (subfolderName, false, new List<(string, string, string)>());
                bool hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }
                selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                string selectedGroupId = selectedGroup.Id;
                string permissionLevel = readChecked ? "Read" : editChecked ? "Edit" : "No Direct Access";
                if (permissionLevel == "No Direct Access")
                {
                    UpdateUI(() => { MessageBox.Show("Use 'Remove Permission' to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Add cancelled: Invalid permission level."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Invalid permission level 'No Direct Access', Session ID: {debugSessionId}");
                    return;
                }

                // Prompt for confirmation
                var confirm = MessageBox.Show($"Are you sure you want to add '{permissionLevel}' permission for group '{selectedGroup.DisplayName}' to subfolder '{subfolderName}'?",
                    "Confirm Add Permission", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { statusLabel.Text = "Add permission cancelled by user."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionCancelled", _libraryName, selectedGroup.DisplayName, "Subfolder",
                        $"User cancelled adding '{permissionLevel}' permission for group '{selectedGroup.DisplayName}' to subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                // Remove preview node before adding actual permission
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    foreach (TreeNode node in tvSubfolders.SelectedNode.Nodes)
                    {
                        if (node.Tag is TreeNodeData nodeData && nodeData.IsPreview)
                            node.Remove();
                    }
                    isUpdatingTreeView = false;
                });

                UpdateUI(() => statusLabel.Text = $"Adding permission for '{selectedGroup.DisplayName}' to '{subfolderName}'...");
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca?.GetAccountsAsync();
                var authResult = await _pca?.AcquireTokenSilent(scopes, accounts?.FirstOrDefault()).ExecuteAsync();
                if (authResult == null)
                {
                    UpdateUI(() => { MessageBox.Show("Authentication failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); statusLabel.Text = "Error: Authentication failed."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder", $"Authentication failed, Session ID: {debugSessionId}");
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
                            await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder",
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
                                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                        $"Non-critical error checking role assignment for '{(ra.Member != null ? ra.Member.LoginName : "Unknown")}' on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                }
                            }
                            if (hasEffectivePermissions)
                                await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
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
                                await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder",
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
                            await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                $"Post-addition RAs for '{subfolderName}': {string.Join(" | ", updatedRAs)}, Session ID: {debugSessionId}");
                            if (!permissionAdded)
                                throw new Exception($"Permission '{targetRoleName}' for group '{selectedGroup.DisplayName}' was not applied to '{subfolderName}'.");
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                statusLabel.Text = $"Added '{permissionLevel}' permission for '{selectedGroup.DisplayName}' to '{subfolderName}'.";
                                isUpdatingTreeView = false;
                            });
                            await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermission", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                $"Added '{permissionLevel}' permission for group '{selectedGroup.DisplayName}' to subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                            success = true;

                            // Refresh cache after permission change
                            lastRefreshTime = DateTime.MinValue;
                            await LoadCurrentPermissionsAsync(debugSessionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionRetry", _libraryName, selectedGroup?.DisplayName ?? "unknown", "Subfolder",
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
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup?.DisplayName ?? "unknown", "Subfolder",
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
                await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup?.DisplayName ?? "unknown", "Subfolder",
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
                    var subfolderNode = tvSubfolders.SelectedNode.Parent;
                    var subfolderData = subfolderNode.Tag as TreeNodeData;
                    string subfolderName = subfolderData.SubfolderName;

                    // Prompt for confirmation
                    var confirm = MessageBox.Show($"Are you sure you want to remove permissions for '{nodeData.GroupName}' from subfolder '{subfolderName}'?\n\nNote: If the permission remains in the SharePoint UI, check your account's 'Manage Permissions' rights or revoke sharing links manually.",
                        "Confirm Remove Permission", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => { statusLabel.Text = "Remove permission cancelled by user."; });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, nodeData.GroupName, "Subfolder",
                            $"User cancelled removal of permissions for group '{nodeData.GroupName}' from subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }

                    UpdateUI(() => { statusLabel.Text = $"Removing permission for '{nodeData.GroupName}' from '{subfolderName}'..."; });
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
                                        statusLabel.Text = $"Removed permissions for '{nodeData.GroupName}' from '{subfolderName}'.";
                                        chkRead.Checked = false;
                                        chkEdit.Checked = false;
                                        chkNoAccess.Checked = false;
                                        isUpdatingTreeView = false;
                                    });
                                    success = true;

                                    // Refresh cache after permission change
                                    lastRefreshTime = DateTime.MinValue;
                                    await LoadCurrentPermissionsAsync(debugSessionId);
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
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionRetry", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Retry {retryCount} for removing permission from '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                MessageBox.Show($"Failed to remove permission after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                statusLabel.Text = "Error removing permission.";
                                isUpdatingTreeView = false;
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                $"Failed to remove permission for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        }
                    }
                }
                else if (tvSubfolders.SelectedNode != null && tvSubfolders.SelectedNode.Tag is TreeNodeData selNodeData && selNodeData.IsSubfolder)
                {
                    string subfolderName = selNodeData.SubfolderName;
                    bool hasUniquePermissions = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase)).HasUniquePermissions;
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
                    // Prompt for confirmation
                    var confirm = MessageBox.Show($"Are you sure you want to remove all permissions for subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                        "Confirm Remove All Permissions", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => { statusLabel.Text = "Remove all permissions cancelled by user."; });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, null, "Subfolder",
                            $"User cancelled removal of all permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }
                    UpdateUI(() => { statusLabel.Text = $"Removing all permissions for '{subfolderName}'..."; });
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
                                    lastRefreshTime = DateTime.MinValue;
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
                else
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a group or subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Remove cancelled: Invalid selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Invalid selection for removal, Session ID: {debugSessionId}");
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to remove permissions: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error removing permissions.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                    $"Failed to remove permissions: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnRemove.Enabled = true; isUpdatingTreeView = false; });
            }
        }

        private async void chkPermission_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox changedCheckbox = sender as CheckBox;
            if (changedCheckbox == null || isUpdatingTreeView) return;

            // Preserve UI state
            var currentGroupSelection = cmbGroups?.SelectedItem;
            var currentSelectedNode = tvSubfolders?.SelectedNode;
            var expandedNodes = tvSubfolders?.Nodes.Cast<TreeNode>()
                .Where(n => n.IsExpanded)
                .Select(n => (n.Tag as TreeNodeData)?.SubfolderName)
                .ToList();

            // Ensure mutual exclusivity and store original permission
            string originalPermission = null;
            UpdateUI(() =>  // Removed 'await' and 'async' from lambda (no awaits inside)
            {
                isUpdatingTreeView = true;
                if (tvSubfolders?.SelectedNode?.Tag is TreeNodeData nodeData)
                    originalPermission = nodeData.Permission;
                if (changedCheckbox == chkRead && chkRead.Checked)
                {
                    chkEdit.Checked = false;
                    chkNoAccess.Checked = false;
                }
                else if (changedCheckbox == chkEdit && chkEdit.Checked)
                {
                    chkRead.Checked = false;
                    chkNoAccess.Checked = false;
                }
                else if (changedCheckbox == chkNoAccess && chkNoAccess.Checked)
                {
                    chkRead.Checked = false;
                    chkEdit.Checked = false;
                }
                // Restore group selection and TreeView selection
                if (currentGroupSelection != null && cmbGroups.Enabled)
                    cmbGroups.SelectedItem = currentGroupSelection;
                if (currentSelectedNode != null && tvSubfolders.Nodes.Contains(currentSelectedNode))
                    tvSubfolders.SelectedNode = currentSelectedNode;
                isUpdatingTreeView = false;
            });

            // Only process permission changes for group nodes or preview for subfolders
            if (tvSubfolders?.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData selectedNodeData))
            {
                return;
            }

            string subfolderName = null;
            TreeNodeData nodeData = null;
            string debugSessionId = Guid.NewGuid().ToString();
            string permissionLevel = chkRead.Checked ? "Read" : chkEdit.Checked ? "Edit" : chkNoAccess.Checked ? "No Direct Access" : null;

            if (selectedNodeData.IsSubfolder)
            {
                // For subfolder nodes, update status and add preview group node if a group is selected
                UpdateUI(() =>  // Removed 'await' and 'async' from lambda
                {
                    isUpdatingTreeView = true;
                    // Remove any existing preview node
                    foreach (TreeNode node in tvSubfolders.SelectedNode.Nodes)
                    {
                        if (node.Tag is TreeNodeData nodeData && nodeData.IsPreview)
                            node.Remove();
                    }

                    if (permissionLevel != null && cmbGroups.SelectedItem is GroupItem selectedGroup)
                    {
                        // Add a preview group node in green
                        var previewNode = new TreeNode
                        {
                            Text = $"{selectedGroup.DisplayName}: {permissionLevel} (Pending)",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            ForeColor = System.Drawing.Color.Green, // Highlight preview node in green
                            Tag = new TreeNodeData
                            {
                                IsSubfolder = false,
                                GroupId = selectedGroup.Id,
                                GroupName = selectedGroup.DisplayName,
                                Permission = permissionLevel,
                                IsPreview = true
                            }
                        };
                        tvSubfolders.SelectedNode.Nodes.Add(previewNode);
                        statusLabel.Text = $"Selected '{permissionLevel}' for group '{selectedGroup.DisplayName}' on '{selectedNodeData.SubfolderName}'. Click 'Add Permission' to confirm.";
                    }
                    else
                    {
                        statusLabel.Text = permissionLevel != null ? $"Selected '{permissionLevel}' for '{selectedNodeData.SubfolderName}'. Select a group and click 'Add Permission' to assign." : "Select a permission level to add to a group.";
                    }

                    // Restore TreeView selection
                    if (currentSelectedNode != null && tvSubfolders.Nodes.Contains(currentSelectedNode))
                        tvSubfolders.SelectedNode = currentSelectedNode;
                    tvSubfolders.ExpandAll();
                    isUpdatingTreeView = false;
                });
                return;
            }

            // Handle group node permission changes
            UpdateUI(() => { isUpdatingTreeView = true; statusLabel.Text = "Updating permission..."; });  // Removed 'await' and 'async'

            try
            {
                nodeData = selectedNodeData;
                var subfolderNode = tvSubfolders.SelectedNode.Parent;
                var subfolderData = subfolderNode?.Tag as TreeNodeData;
                subfolderName = subfolderData?.SubfolderName;
                if (subfolderName == null)
                {
                    UpdateUI(() =>  // Removed 'await' and 'async'
                    {
                        MessageBox.Show("Invalid subfolder data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        statusLabel.Text = "Change cancelled: Invalid subfolder data.";
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, null, "Subfolder", $"Invalid subfolder data, Session ID: {debugSessionId}");
                    return;
                }

                var cacheEntry = _subfolderCache?.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase)) ??
                    (subfolderName, false, new List<(string, string, string)>());
                bool hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                if (!hasUniquePermissions)
                {
                    UpdateUI(() =>  // Removed 'await' and 'async'
                    {
                        MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Change cancelled: Subfolder has inherited permissions.";
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, null, "Subfolder", $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }

                if (permissionLevel == null)
                {
                    UpdateUI(() =>  // Removed 'await' and 'async'
                    {
                        MessageBox.Show("No permission selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Change cancelled: No permission selected.";
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder", $"No permission selected, Session ID: {debugSessionId}");
                    return;
                }

                // Skip if permission hasn't changed
                if (permissionLevel == originalPermission)
                {
                    UpdateUI(() => { statusLabel.Text = "No change in permission."; });  // Removed 'await' and 'async'
                    return;
                }

                // Prompt for confirmation
                var confirmMessage = permissionLevel == "No Direct Access"
                    ? $"Are you sure you want to remove permissions for '{nodeData.GroupName}' from subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links."
                    : $"Are you sure you want to change the permission for '{nodeData.GroupName}' on subfolder '{subfolderName}' from '{originalPermission}' to '{permissionLevel}'?";
                var confirm = MessageBox.Show(confirmMessage, "Confirm Permission Change", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() =>  // Removed 'await' and 'async'
                    {
                        isUpdatingTreeView = true;
                        statusLabel.Text = "Permission change cancelled by user.";
                        // Revert checkboxes to original state
                        chkRead.Checked = originalPermission == "Read";
                        chkEdit.Checked = originalPermission == "Edit";
                        chkNoAccess.Checked = originalPermission == "No Direct Access";
                        // Restore TreeView selection
                        if (currentSelectedNode != null && tvSubfolders.Nodes.Contains(currentSelectedNode))
                            tvSubfolders.SelectedNode = currentSelectedNode;
                        isUpdatingTreeView = false;
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionCancelled" : "ChangeSubfolderPermissionCancelled", _libraryName, nodeData.GroupName, "Subfolder",
                        $"User cancelled {(permissionLevel == "No Direct Access" ? "removal" : "change")} for group '{nodeData.GroupName}' on subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                UpdateUI(() =>  // Removed 'await' and 'async'
                {
                    statusLabel.Text = permissionLevel == "No Direct Access"
                        ? $"Removing permission for '{nodeData.GroupName}' from '{subfolderName}'..."
                        : $"Changing permission for '{nodeData.GroupName}' on '{subfolderName}' to '{permissionLevel}'...";
                });

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca?.GetAccountsAsync();
                var authResult = await _pca?.AcquireTokenSilent(scopes, accounts?.FirstOrDefault()).ExecuteAsync();
                if (authResult == null)
                {
                    UpdateUI(() =>  // Removed 'await' and 'async'
                    {
                        MessageBox.Show("Authentication failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        statusLabel.Text = "Error: Authentication failed.";
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder", $"Authentication failed, Session ID: {debugSessionId}");
                    return;
                }

                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
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

                            RoleAssignmentCollection roleAssignments = listItem.RoleAssignments;
                            context.Load(roleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
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
                            await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionDebug" : "ChangeSubfolderPermissionDebug", _libraryName, nodeData.GroupName, "Subfolder",
                                $"Pre-change RAs for '{subfolderName}': {string.Join(" | ", existingRAs)}, Session ID: {debugSessionId}");

                            string groupPrincipalId = $"c:0t.c|tenant|{nodeData.GroupId}";
                            var raToModify = roleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupPrincipalId && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                            if (raToModify == null)
                            {
                                UpdateUI(() =>  // Removed 'await' and 'async'
                                {
                                    MessageBox.Show($"Group '{nodeData.GroupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    statusLabel.Text = "Change cancelled: Group not found.";
                                });
                                await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Group '{nodeData.GroupName}' not found in permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                return;
                            }

                            if (permissionLevel == "No Direct Access")
                            {
                                // Remove the role assignment
                                foreach (RoleDefinition rd in raToModify.RoleDefinitionBindings.ToList())
                                    raToModify.RoleDefinitionBindings.Remove(rd);
                                raToModify.Update();
                                raToModify.DeleteObject();
                                await context.ExecuteQueryAsync().ConfigureAwait(false);

                                // Verify removal
                                context.Load(roleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                                var remainingRAs = roleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                if (remainingRAs.Contains(groupPrincipalId))
                                    throw new Exception("Permission removal failed to apply (group still present after verification). Check 'Manage Permissions' rights or sharing links in SharePoint UI.");

                                // Update local UI and cache
                                UpdateUI(() =>  // Removed 'await' and 'async'
                                {
                                    isUpdatingTreeView = true;
                                    currentSelectedNode.Remove();
                                    statusLabel.Text = $"Removed permissions for '{nodeData.GroupName}' from '{subfolderName}'.";
                                    chkRead.Checked = false;
                                    chkEdit.Checked = false;
                                    chkNoAccess.Checked = false;

                                    // Update cache
                                    var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        var updatedGroups = cacheEntry.Groups.Where(g => g.GroupId != nodeData.GroupId).ToList();
                                        _subfolderCache[cacheIndex] = (subfolderName, cacheEntry.HasUniquePermissions, updatedGroups);
                                    }

                                    // Restore UI state
                                    if (currentGroupSelection != null && cmbGroups.Enabled)
                                        cmbGroups.SelectedItem = currentGroupSelection;
                                    UpdateSidebar();
                                    // Restore expanded nodes
                                    foreach (var node in tvSubfolders.Nodes.Cast<TreeNode>())
                                    {
                                        if (expandedNodes.Contains((node.Tag as TreeNodeData)?.SubfolderName))
                                            node.Expand();
                                    }
                                    isUpdatingTreeView = false;
                                });

                                await _auditLogManager?.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Removed permissions for group '{nodeData.GroupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                success = true;
                            }
                            else
                            {
                                // Change permission level
                                var roleDefinitions = context.Web.RoleDefinitions;
                                context.Load(roleDefinitions);
                                await context.ExecuteQueryAsync().ConfigureAwait(false);
                                string targetRoleName = permissionLevel == "Edit" ? "Contribute" : permissionLevel;
                                var roleDefinition = roleDefinitions.FirstOrDefault(rd => rd.Name == targetRoleName);
                                if (roleDefinition == null)
                                {
                                    UpdateUI(() =>  // Removed 'await' and 'async'
                                    {
                                        MessageBox.Show($"Permission level '{permissionLevel}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        statusLabel.Text = "Error: Permission level not found.";
                                    });
                                    await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Permission level '{permissionLevel}' not found for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                    return;
                                }

                                foreach (RoleDefinition rd in raToModify.RoleDefinitionBindings.ToList())
                                    raToModify.RoleDefinitionBindings.Remove(rd);
                                raToModify.RoleDefinitionBindings.Add(roleDefinition);
                                raToModify.Update();
                                await context.ExecuteQueryAsync().ConfigureAwait(false);

                                // Verify update
                                context.Load(roleAssignments, ras => ras.Include(ra => ra.Member, ra => ra.RoleDefinitionBindings));
                                foreach (RoleAssignment ra in roleAssignments)
                                {
                                    context.Load(ra.Member, m => m.LoginName);
                                    context.Load(ra.RoleDefinitionBindings);
                                }
                                await context.ExecuteQueryAsync().ConfigureAwait(false);

                                var updatedRAs = new List<string>();
                                bool permissionChanged = false;
                                foreach (RoleAssignment ra in roleAssignments)
                                {
                                    var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null"));
                                    updatedRAs.Add($"LoginName: {ra.Member.LoginName}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                                    if (ra.Member.LoginName == groupPrincipalId && ra.RoleDefinitionBindings.Any(rdb => rdb.Name == targetRoleName))
                                        permissionChanged = true;
                                }
                                await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionDebug", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Post-change RAs for '{subfolderName}': {string.Join(" | ", updatedRAs)}, Session ID: {debugSessionId}");

                                if (!permissionChanged)
                                    throw new Exception($"Permission '{targetRoleName}' for group '{nodeData.GroupName}' was not applied to '{subfolderName}'.");

                                // Update local UI and cache
                                UpdateUI(() =>  // Removed 'await' and 'async'
                                {
                                    isUpdatingTreeView = true;
                                    currentSelectedNode.Text = $"{nodeData.GroupName}: {permissionLevel}";
                                    (currentSelectedNode.Tag as TreeNodeData).Permission = permissionLevel;
                                    statusLabel.Text = $"Changed permission for '{nodeData.GroupName}' on '{subfolderName}' to '{permissionLevel}'.";

                                    // Update cache
                                    var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        var updatedGroups = cacheEntry.Groups.ToList();
                                        var groupIndex = updatedGroups.FindIndex(g => g.GroupId == nodeData.GroupId);
                                        if (groupIndex >= 0)
                                            updatedGroups[groupIndex] = (nodeData.GroupName, nodeData.GroupId, permissionLevel);
                                        _subfolderCache[cacheIndex] = (subfolderName, cacheEntry.HasUniquePermissions, updatedGroups);
                                    }

                                    // Restore UI state
                                    if (currentGroupSelection != null && cmbGroups.Enabled)
                                        cmbGroups.SelectedItem = currentGroupSelection;
                                    tvSubfolders.SelectedNode = currentSelectedNode;
                                    UpdateSidebar();
                                    // Restore expanded nodes
                                    foreach (var node in tvSubfolders.Nodes.Cast<TreeNode>())
                                    {
                                        if (expandedNodes.Contains((node.Tag as TreeNodeData)?.SubfolderName))
                                            node.Expand();
                                    }
                                    isUpdatingTreeView = false;
                                });

                                await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermission", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Changed permission to '{permissionLevel}' for group '{nodeData.GroupName}' on subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                success = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionRetry" : "ChangeSubfolderPermissionRetry", _libraryName, nodeData?.GroupName ?? "unknown", "Subfolder",
                                $"Retry {retryCount} for {(permissionLevel == "No Direct Access" ? "removing" : "changing")} permission on '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }

                        UpdateUI(() =>  // Removed 'await' and 'async'
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = $"Error {(permissionLevel == "No Direct Access" ? "removing" : "changing")} permission.";
                            // Revert checkboxes to original state
                            chkRead.Checked = nodeData?.Permission == "Read";
                            chkEdit.Checked = nodeData?.Permission == "Edit";
                            chkNoAccess.Checked = nodeData?.Permission == "No Direct Access";
                            // Restore UI state
                            if (currentGroupSelection != null && cmbGroups.Enabled)
                                cmbGroups.SelectedItem = currentGroupSelection;
                            if (currentSelectedNode != null && tvSubfolders.Nodes.Contains(currentSelectedNode))
                                tvSubfolders.SelectedNode = currentSelectedNode;
                            UpdateSidebar();
                            // Restore expanded nodes
                            foreach (var node in tvSubfolders.Nodes.Cast<TreeNode>())
                            {
                                if (expandedNodes.Contains((node.Tag as TreeNodeData)?.SubfolderName))
                                    node.Expand();
                            }
                            isUpdatingTreeView = false;
                        });

                        await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionError" : "ChangeSubfolderPermissionError", _libraryName, nodeData?.GroupName ?? "unknown", "Subfolder",
                            $"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission for subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>  // Removed 'await' and 'async'
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = $"Error {(permissionLevel == "No Direct Access" ? "removing" : "changing")} permission.";
                    // Revert checkboxes to original state
                    chkRead.Checked = nodeData?.Permission == "Read";
                    chkEdit.Checked = nodeData?.Permission == "Edit";
                    chkNoAccess.Checked = nodeData?.Permission == "No Direct Access";
                    // Restore UI state
                    if (currentGroupSelection != null && cmbGroups.Enabled)
                        cmbGroups.SelectedItem = currentGroupSelection;
                    if (currentSelectedNode != null && tvSubfolders.Nodes.Contains(currentSelectedNode))
                        tvSubfolders.SelectedNode = currentSelectedNode;
                    UpdateSidebar();
                    // Restore expanded nodes
                    foreach (var node in tvSubfolders.Nodes.Cast<TreeNode>())
                    {
                        if (expandedNodes.Contains((node.Tag as TreeNodeData)?.SubfolderName))
                            node.Expand();
                    }
                    isUpdatingTreeView = false;
                });

                await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionError" : "ChangeSubfolderPermissionError", _libraryName, nodeData?.GroupName ?? "unknown", "Subfolder",
                    $"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission for subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
        }
        private async void btnBreakInheritance_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnBreakInheritance.Enabled = false; isUpdatingTreeView = false; });
            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Break inheritance cancelled: Invalid subfolder selection."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder", $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }
                subfolderName = nodeData.SubfolderName;
                bool hasUniquePermissions = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase)).HasUniquePermissions;
                if (hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show("Subfolder already has unique permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Break inheritance cancelled: Subfolder already has unique permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder", $"Subfolder '{subfolderName}' already has unique permissions, Session ID: {debugSessionId}");
                    return;
                }
                var confirm = MessageBox.Show($"Are you sure you want to break permission inheritance for '{subfolderName}'? This will copy existing permissions and allow modifications.", "Confirm Break Inheritance", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { statusLabel.Text = "Break inheritance cancelled by user."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceCancelled", _libraryName, null, "Subfolder", $"User cancelled breaking inheritance for subfolder '{subfolderName}', Session ID: {debugSessionId}");
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
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            var listItem = subfolder.ListItemAllFields;
                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            listItem.BreakRoleInheritance(true, false);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync().ConfigureAwait(false);
                            if (!listItem.HasUniqueRoleAssignments)
                                throw new Exception("Failed to break inheritance: Subfolder still has inherited permissions.");
                            await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritance", _libraryName, null, "Subfolder",
                                $"Broke permission inheritance for subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                statusLabel.Text = $"Broke permission inheritance for '{subfolderName}'.";
                                // Update cache immediately
                                var cacheEntry = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                if (cacheEntry.SubfolderName != null)
                                {
                                    var index = _subfolderCache.IndexOf(cacheEntry);
                                    _subfolderCache[index] = (cacheEntry.SubfolderName, true, cacheEntry.Groups);
                                }
                                UpdateSidebar();
                                isUpdatingTreeView = false;
                            });
                            success = true;

                            // Full cache refresh to ensure consistency
                            lastRefreshTime = DateTime.MinValue;
                            await LoadCurrentPermissionsAsync(debugSessionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceRetry", _libraryName, null, "Subfolder",
                                $"Retry {retryCount} for breaking inheritance on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to break inheritance after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error breaking inheritance.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
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
                await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
                    $"Failed to break inheritance for subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
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
                string subfolderName = nodeData.SubfolderName;
                var cacheEntry = _subfolderCache?.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase)) ??
                    (subfolderName, false, new List<(string GroupName, string GroupId, string Role)>());
                bool hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Reset cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }
                if (!cacheEntry.Groups.Any())
                {
                    UpdateUI(() => { MessageBox.Show($"No permissions to reset for subfolder '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); statusLabel.Text = "Reset cancelled: No permissions assigned."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"No permissions to reset for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                // Prompt for confirmation
                var confirm = MessageBox.Show($"Are you sure you want to remove all permissions for subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                    "Confirm Reset Permissions", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { statusLabel.Text = "Reset permissions cancelled by user."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsCancelled", _libraryName, null, "Subfolder",
                        $"User cancelled reset of permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
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
                                lastRefreshTime = DateTime.MinValue;
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
