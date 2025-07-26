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
        private bool isUpdatingDataGridView;
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
                });

                await UpdatePermissionDropdown();
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

        private async void LoadCurrentPermissionsAsync(string debugSessionId = null)
        {
            debugSessionId = debugSessionId ?? Guid.NewGuid().ToString();
            string selectedSubfolderName = null;
            UpdateUI(() =>
            {
                try
                {
                    isUpdatingDataGridView = true;
                    selectedSubfolderName = lvSubfolders.SelectedItems.Count > 0 ? lvSubfolders.SelectedItems[0].Text : null;
                    _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsStart", _libraryName, null, "Subfolder",
                        $"Starting LoadCurrentPermissionsAsync, Selected Subfolder: {(selectedSubfolderName != null ? selectedSubfolderName : "None")}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                    lvSubfolders.Items.Clear();
                    dgvGroupPermissions.Rows.Clear();
                    statusLabel.Text = "Loading current permissions...";
                }
                catch (Exception ex)
                {
                    selectedSubfolderName = null;
                    statusLabel.Text = "Warning: Subfolder selection invalid during load.";
                    _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsSelectionError", _libraryName, null, "Subfolder",
                        $"Failed to get selected subfolder: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                }
                finally
                {
                    isUpdatingDataGridView = false;
                }
            });

            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsAuthStart", _libraryName, null, "Subfolder",
                    $"Acquiring authentication token for scopes: {string.Join(", ", scopes)}, Session ID: {debugSessionId}");

                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();

                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        statusLabel.Text = "Error: No signed-in account.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "LoadSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }

                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsAuthSuccess", _libraryName, null, "Subfolder",
                    $"Authentication token acquired, Session ID: {debugSessionId}");

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) =>
                    {
                        e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                    };
                    await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsContextSetup", _libraryName, null, "Subfolder",
                        $"ClientContext initialized for site: {_siteUrl}, Session ID: {debugSessionId}");

                    var web = context.Web;
                    var library = web.Lists.GetByTitle(_libraryName);
                    var folder = library.RootFolder;

                    context.Load(folder, f => f.Folders.Include(
                        f => f.Name,
                        f => f.ServerRelativeUrl,
                        f => f.ListItemAllFields.HasUniqueRoleAssignments,
                        f => f.ListItemAllFields.RoleAssignments.Include(
                            ra => ra.Member.Title,
                            ra => ra.Member.LoginName,
                            ra => ra.RoleDefinitionBindings)));
                    await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsQuerySetup", _libraryName, null, "Subfolder",
                        $"Query setup for library: {_libraryName}, Session ID: {debugSessionId}");

                    await context.ExecuteQueryAsync();
                    await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsQueryExecuted", _libraryName, null, "Subfolder",
                        $"Query executed, Folder count: {folder.Folders.Count}, Session ID: {debugSessionId}");

                    var subfolders = folder.Folders.Where(f => !f.Name.StartsWith("Forms")).ToList();
                    await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsSubfoldersFiltered", _libraryName, null, "Subfolder",
                        $"Filtered subfolders: {subfolders.Count}, Names: {string.Join(", ", subfolders.Select(f => f.Name))}, Session ID: {debugSessionId}");

                    foreach (var subfolder in subfolders)
                    {
                        await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsProcessSubfolder", _libraryName, null, "Subfolder",
                            $"Processing subfolder: {subfolder.Name}, ServerRelativeUrl: {subfolder.ServerRelativeUrl}, Session ID: {debugSessionId}");

                        var perms = new List<string>();
                        int groupCount = 0;

                        try
                        {
                            if (subfolder.ListItemAllFields != null && subfolder.ListItemAllFields.RoleAssignments != null)
                            {
                                foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                                {
                                    try
                                    {
                                        if (ra.Member != null && ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var role = ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "Unknown";
                                            if (role == "Contribute") role = "Edit";
                                            perms.Add($"{ra.Member.Title}: {role}");
                                            groupCount++;
                                            await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsRoleAssignment", _libraryName, null, "Subfolder",
                                                $"Subfolder: {subfolder.Name}, Group: {ra.Member.Title}, Role: {role}, Session ID: {debugSessionId}");
                                        }
                                    }
                                    catch (Exception raEx)
                                    {
                                        await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsRoleAssignmentError", _libraryName, null, "Subfolder",
                                            $"Error processing role assignment for subfolder: {subfolder.Name}, Error: {raEx.Message}, Inner: {(raEx.InnerException != null ? raEx.InnerException.Message : "None")}, StackTrace: {raEx.StackTrace}, Session ID: {debugSessionId}");
                                    }
                                }
                            }
                        }
                        catch (Exception permEx)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsPermissionsError", _libraryName, null, "Subfolder",
                                $"Error accessing role assignments for subfolder: {subfolder.Name}, Error: {permEx.Message}, Inner: {(permEx.InnerException != null ? permEx.InnerException.Message : "None")}, StackTrace: {permEx.StackTrace}, Session ID: {debugSessionId}");
                        }

                        string summary = groupCount > 0 ? $"{groupCount} CSG group(s) assigned" : "No CSG groups";

                        var item = new ListViewItem(subfolder.Name);
                        item.SubItems.Add(summary);
                        item.Tag = subfolder;

                        bool hasUnique = false;
                        try
                        {
                            hasUnique = subfolder.ListItemAllFields != null && subfolder.ListItemAllFields.HasUniqueRoleAssignments;
                            await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsUniqueCheck", _libraryName, null, "Subfolder",
                                $"Subfolder: {subfolder.Name}, HasUniqueRoleAssignments: {hasUnique}, Session ID: {debugSessionId}");
                        }
                        catch (Exception uniqueEx)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsUniqueCheckError", _libraryName, null, "Subfolder",
                                $"Error checking HasUniqueRoleAssignments for subfolder: {subfolder.Name}, Error: {uniqueEx.Message}, Inner: {(uniqueEx.InnerException != null ? uniqueEx.InnerException.Message : "None")}, StackTrace: {uniqueEx.StackTrace}, Session ID: {debugSessionId}");
                        }

                        item.SubItems.Add(hasUnique ? "Unique" : "Inherited");

                        UpdateUI(() =>
                        {
                            try
                            {
                                isUpdatingDataGridView = true;
                                lvSubfolders.Items.Add(item);
                                if (subfolder.Name == selectedSubfolderName)
                                {
                                    item.Selected = true;
                                }
                            }
                            catch (Exception uiEx)
                            {
                                _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsUIUpdateError", _libraryName, null, "Subfolder",
                                    $"UI update error for subfolder: {subfolder.Name}, Error: {uiEx.Message}, Inner: {(uiEx.InnerException != null ? uiEx.InnerException.Message : "None")}, StackTrace: {uiEx.StackTrace}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                            }
                            finally
                            {
                                isUpdatingDataGridView = false;
                            }
                        });
                    }

                    UpdateUI(() =>
                    {
                        isUpdatingDataGridView = true;
                        statusLabel.Text = "Permissions loaded.";
                        _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsUISuccess", _libraryName, null, "Subfolder",
                            $"Permissions UI updated successfully, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                        isUpdatingDataGridView = false;
                    });
                    await UpdatePermissionDropdown();
                    UpdateUI(() => lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty));
                    await _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadPermissionsComplete", _libraryName, null, "Subfolder",
                        $"LoadCurrentPermissionsAsync completed, Session ID: {debugSessionId}");
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingDataGridView = true;
                    MessageBox.Show($"Failed to load permissions: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error loading permissions.";
                    btnChange.Enabled = false;
                    btnAdd.Enabled = false;
                    btnRemove.Enabled = false;
                    lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                    isUpdatingDataGridView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadSubfolderPermissionsError", _libraryName, null, "Subfolder",
                    $"Failed to load subfolder permissions: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
            }
        }
        private async Task UpdatePermissionDropdown()
        {
            UpdateUI(() =>
            {
                isUpdatingDataGridView = true;
                try
                {
                    if (lvSubfolders.SelectedItems.Count == 1)
                    {
                        var selectedItem = lvSubfolders.SelectedItems[0];
                        string subfolderName = selectedItem.Text;
                        lblSelectedSubfolder.Text = $"Selected Subfolder: {subfolderName}";
                        bool hasUniquePermissions = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Unique";

                        if (hasUniquePermissions)
                        {
                            if (dgvGroupPermissions.SelectedRows.Count > 0)
                            {
                                // Handled by dgvGroupPermissions_SelectionChanged
                            }
                            else
                            {
                                cmbPermissions.SelectedItem = "Read";
                                btnChange.Enabled = false;
                                btnAdd.Enabled = true;
                                btnRemove.Enabled = dgvGroupPermissions.Rows.Count > 0;
                            }
                        }
                        else
                        {
                            cmbPermissions.SelectedItem = "Read";
                            btnChange.Enabled = false;
                            btnAdd.Enabled = false;
                            btnRemove.Enabled = false;
                        }
                        cmbGroups.Enabled = true;
                        btnBreakInheritance.Enabled = !hasUniquePermissions;
                        btnResetPermissions.Enabled = hasUniquePermissions;
                    }
                    else
                    {
                        lblSelectedSubfolder.Text = lvSubfolders.SelectedItems.Count > 1 ? "Selected Subfolder: Multiple selected" : "Selected Subfolder: None";
                        cmbPermissions.SelectedIndex = -1;
                        btnChange.Enabled = false;
                        btnBreakInheritance.Enabled = false;
                        btnAdd.Enabled = false;
                        btnResetPermissions.Enabled = false;
                        btnRemove.Enabled = false;
                        cmbGroups.Enabled = lvSubfolders.SelectedItems.Count <= 1;
                        dgvGroupPermissions.Rows.Clear();
                    }
                }
                finally
                {
                    isUpdatingDataGridView = false;
                }
            });
        }
        private void lvSubfolders_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                isUpdatingDataGridView = true;
                try
                {
                    dgvGroupPermissions.Rows.Clear();
                    if (lvSubfolders.SelectedItems.Count == 1)
                    {
                        var selectedItem = lvSubfolders.SelectedItems[0];
                        string subfolderName = selectedItem.Text;
                        var matchingGroup = _availableGroups.FirstOrDefault(g => g.DisplayName == subfolderName);
                        if (matchingGroup != null)
                        {
                            cmbGroups.SelectedItem = matchingGroup;
                        }
                        cmbGroups.Enabled = true;
                        btnBreakInheritance.Enabled = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Inherited";
                        btnResetPermissions.Enabled = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Unique";
                        var subfolder = (Folder)selectedItem.Tag;
                        LoadGroupDetailsForSubfolder(subfolder);
                    }
                    else if (lvSubfolders.SelectedItems.Count > 1)
                    {
                        lblSelectedSubfolder.Text = "Selected Subfolder: Multiple selected";
                        cmbGroups.Enabled = false;
                        cmbPermissions.SelectedIndex = -1;
                        btnChange.Enabled = false;
                        btnBreakInheritance.Enabled = false;
                        btnAdd.Enabled = false;
                        btnResetPermissions.Enabled = false;
                        btnRemove.Enabled = false;
                    }
                    else
                    {
                        lblSelectedSubfolder.Text = "Selected Subfolder: None";
                        cmbGroups.Enabled = true;
                        btnBreakInheritance.Enabled = false;
                        btnAdd.Enabled = false;
                        btnResetPermissions.Enabled = false;
                        btnRemove.Enabled = false;
                    }
                }
                finally
                {
                    isUpdatingDataGridView = false;
                }
            });
            UpdatePermissionDropdown();
        }
        private async void LoadGroupDetailsForSubfolder(Folder subfolder)
        {
            string debugSessionId = Guid.NewGuid().ToString();
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
                        ra => ra.Member.Title,
                        ra => ra.Member.LoginName,
                        ra => ra.RoleDefinitionBindings));
                    await context.ExecuteQueryAsync();

                    UpdateUI(() =>
                    {
                        isUpdatingDataGridView = true;
                        try
                        {
                            dgvGroupPermissions.Rows.Clear();
                            foreach (var ra in reloadedFolder.ListItemAllFields.RoleAssignments)
                            {
                                if (ra.Member != null && ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase))
                                {
                                    var groupName = ra.Member.Title;
                                    var role = ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "Unknown";
                                    if (role == "Contribute") role = "Edit";
                                    var groupId = ra.Member.LoginName.Split('|').Last();
                                    try
                                    {
                                        dgvGroupPermissions.Rows.Add(groupName, role, groupId);
                                        _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadGroupDetails", _libraryName, groupName, "Subfolder",
                                            $"Added group '{groupName}' with role '{role}' to DataGridView for subfolder: {subfolder.Name}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                                    }
                                    catch (Exception ex)
                                    {
                                        _auditLogManager.LogAction(_signedInUserId, null, "DebugLoadGroupDetailsError", _libraryName, groupName, "Subfolder",
                                            $"Failed to add group '{groupName}' to DataGridView: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            isUpdatingDataGridView = false;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingDataGridView = true;
                    MessageBox.Show($"Failed to load group details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    isUpdatingDataGridView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "LoadGroupDetailsError", _libraryName, null, "Subfolder",
                    $"Failed to load group details: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
        }
        private void dgvGroupPermissions_SelectionChanged(object sender, EventArgs e)
        {
            if (isUpdatingDataGridView) return;

            UpdateUI(() =>
            {
                isUpdatingDataGridView = true;
                try
                {
                    if (dgvGroupPermissions.SelectedRows.Count > 0)
                    {
                        var selectedRow = dgvGroupPermissions.SelectedRows[0];

                        if (!dgvGroupPermissions.Columns.Contains("colGroupName") ||
                            !dgvGroupPermissions.Columns.Contains("colPermission") ||
                            !dgvGroupPermissions.Columns.Contains("colGroupId"))
                        {
                            _auditLogManager.LogAction(_signedInUserId, null, "DebugSelectionChangedError", _libraryName, null, "Subfolder",
                                "DataGridView columns missing.").GetAwaiter().GetResult();
                            return;
                        }

                        var groupName = selectedRow.Cells["colGroupName"] != null ? selectedRow.Cells["colGroupName"].Value?.ToString() : null;
                        var permission = selectedRow.Cells["colPermission"] != null ? selectedRow.Cells["colPermission"].Value?.ToString() : null;
                        var groupId = selectedRow.Cells["colGroupId"] != null ? selectedRow.Cells["colGroupId"].Value?.ToString() : null;

                        if (groupName != null && permission != null && groupId != null)
                        {
                            var matchingGroup = _availableGroups.FirstOrDefault(g => g.Id == groupId);
                            if (matchingGroup != null)
                            {
                                cmbGroups.SelectedItem = matchingGroup;
                                cmbPermissions.SelectedItem = permission;
                                btnChange.Enabled = true;
                                btnAdd.Enabled = false;
                                btnRemove.Enabled = true;
                            }
                            else
                            {
                                _auditLogManager.LogAction(_signedInUserId, null, "DebugSelectionChangedWarning", _libraryName, groupName, "Subfolder",
                                    $"Matching group not found for ID: {groupId}").GetAwaiter().GetResult();
                            }
                        }
                        else
                        {
                            _auditLogManager.LogAction(_signedInUserId, null, "DebugSelectionChangedWarning", _libraryName, null, "Subfolder",
                                $"Invalid row data: GroupName={groupName}, Permission={permission}, GroupId={groupId}").GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        cmbGroups.SelectedIndex = -1;
                        cmbPermissions.SelectedIndex = -1;
                        btnChange.Enabled = false;
                        btnAdd.Enabled = lvSubfolders.SelectedItems.Count == 1 &&
                                         lvSubfolders.SelectedItems[0].SubItems.Count >= 3 &&
                                         lvSubfolders.SelectedItems[0].SubItems[2].Text == "Unique";
                        btnRemove.Enabled = dgvGroupPermissions.Rows.Count > 0;
                    }
                }
                catch (Exception ex)
                {
                    _auditLogManager.LogAction(_signedInUserId, null, "DebugSelectionChangedError", _libraryName, null, "Subfolder",
                        $"Error in SelectionChanged: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}").GetAwaiter().GetResult();
                }
                finally
                {
                    isUpdatingDataGridView = false;
                }
            });
        }
        private async void cmbGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            await UpdatePermissionDropdown();
        }
        private async void btnAdd_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            GroupItem selectedGroup = null;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() =>
            {
                isUpdatingDataGridView = true;
                btnAdd.Enabled = false;
                isUpdatingDataGridView = false;
            });

            try
            {
                if (lvSubfolders.SelectedItems.Count != 1)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Add cancelled: Invalid subfolder selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Count: {lvSubfolders.SelectedItems.Count}, Session ID: {debugSessionId}");
                    return;
                }

                if (cmbGroups.SelectedItem == null || cmbPermissions.SelectedItem == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a group and permission level.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Add cancelled: Missing selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Missing group or permission selection, Session ID: {debugSessionId}");
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
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
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
                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder",
                        $"Invalid permission level 'No Direct Access', Session ID: {debugSessionId}");
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
                                    await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                        $"Non-critical error checking role assignment for '{(ra.Member != null ? ra.Member.LoginName : "Unknown")}' on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                }
                            }

                            if (hasEffectivePermissions)
                            {
                                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                    $"Group '{groupPrincipalId}' detected with permissions ({string.Join(", ", detectedRoles)}) on '{subfolderName}'. Proceeding with addition to ensure correct permissions, Session ID: {debugSessionId}");
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
                            await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionDebug", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                $"Post-addition RAs for '{subfolderName}': {string.Join(" | ", updatedRAs)}, Session ID: {debugSessionId}");
                            if (!permissionAdded)
                            {
                                throw new Exception($"Permission '{targetRoleName}' for group '{selectedGroup.DisplayName}' was not applied to '{subfolderName}'.");
                            }

                            UpdateUI(() =>
                            {
                                isUpdatingDataGridView = true;
                                try
                                {
                                    statusLabel.Text = $"Added '{permissionLevel}' permission for '{selectedGroup.DisplayName}' to '{subfolderName}'.";
                                    try
                                    {
                                        dgvGroupPermissions.Rows.Add(selectedGroup.DisplayName, permissionLevel, selectedGroupId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionWarning", _libraryName, selectedGroup.DisplayName, "Subfolder",
                                            $"Failed to update DataGridView for '{subfolderName}': {ex.Message}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                                    }
                                    LoadCurrentPermissionsAsync(debugSessionId);
                                    lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                                }
                                finally
                                {
                                    isUpdatingDataGridView = false;
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
                            isUpdatingDataGridView = true;
                            MessageBox.Show($"Failed to add permission after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error adding permission.";
                            isUpdatingDataGridView = false;
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
                    isUpdatingDataGridView = true;
                    MessageBox.Show($"Failed to add permission: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error adding permission.";
                    isUpdatingDataGridView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup != null ? selectedGroup.DisplayName : null, "Subfolder",
                    $"Failed to add permission to subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() =>
                {
                    isUpdatingDataGridView = true;
                    btnAdd.Enabled = true;
                    isUpdatingDataGridView = false;
                });
            }
        }
        private async void btnRemove_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() =>
            {
                isUpdatingDataGridView = true;
                btnRemove.Enabled = false;
                isUpdatingDataGridView = false;
            });

            try
            {
                if (dgvGroupPermissions.SelectedRows.Count > 0)
                {
                    var confirm = MessageBox.Show($"Are you sure you want to remove the selected group(s) from the subfolder?\n\nNote: If the permission remains in the SharePoint UI, check your account's 'Manage Permissions' rights or revoke sharing links manually.", "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => statusLabel.Text = "Remove cancelled by user.");
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, null, "Subfolder", $"User cancelled removal, Session ID: {debugSessionId}");
                        return;
                    }

                    var selectedItem = lvSubfolders.SelectedItems[0];
                    string subfolderName = selectedItem.Text;
                    UpdateUI(() => statusLabel.Text = $"Removing selected group permissions from '{subfolderName}'...");
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
                                await context.ExecuteQueryAsync();
                                subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                                subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/");

                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                var listItem = subfolder.ListItemAllFields;
                                context.Load(listItem, l => l.HasUniqueRoleAssignments);
                                context.Load(context.Web, s => s.RoleDefinitions);
                                await context.ExecuteQueryAsync();

                                if (!listItem.HasUniqueRoleAssignments)
                                {
                                    listItem.BreakRoleInheritance(true, false);
                                    await context.ExecuteQueryAsync();
                                }

                                RoleAssignmentCollection roleAssignments = listItem.RoleAssignments;
                                context.Load(roleAssignments);
                                await context.ExecuteQueryAsync();

                                // Log existing role assignments
                                var existingRAs = new List<string>();
                                foreach (RoleAssignment ra in roleAssignments)
                                {
                                    context.Load(ra.Member, m => m.LoginName, m => m.Title, m => m.PrincipalType);
                                    context.Load(ra.RoleDefinitionBindings);
                                    await context.ExecuteQueryAsync();
                                    var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null").ToList());
                                    existingRAs.Add($"LoginName: {ra.Member.LoginName}, Title: {(ra.Member.Title != null ? ra.Member.Title : "None")}, PrincipalType: {ra.Member.PrincipalType}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                                }
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, null, "Subfolder", $"Pre-removal RAs for '{subfolderName}': {string.Join(" | ", existingRAs)}, Session ID: {debugSessionId}");

                                var removedGroups = new List<string>();
                                var removedGroupLogins = new List<string>();

                                foreach (DataGridViewRow row in dgvGroupPermissions.SelectedRows.Cast<DataGridViewRow>().ToList())
                                {
                                    var groupId = row.Cells["colGroupId"] != null ? row.Cells["colGroupId"].Value?.ToString() : null;
                                    var groupName = row.Cells["colGroupName"] != null ? row.Cells["colGroupName"].Value?.ToString() : null;
                                    if (groupId != null && groupName != null)
                                    {
                                        var groupLogin = $"c:0t.c|tenant|{groupId}";
                                        foreach (RoleAssignment ra in roleAssignments.ToList())
                                        {
                                            context.Load(ra.Member, m => m.LoginName, m => m.Title, m => m.PrincipalType);
                                            context.Load(ra.RoleDefinitionBindings);
                                            await context.ExecuteQueryAsync();

                                            if (ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup && ra.Member.LoginName == groupLogin)
                                            {
                                                foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                                {
                                                    context.Load(rd);
                                                    ra.RoleDefinitionBindings.Remove(rd);
                                                }
                                                ra.Update();
                                                context.Load(ra, r => r.Member, r => r.RoleDefinitionBindings);
                                                listItem.Update();
                                                await context.ExecuteQueryAsync();
                                                removedGroups.Add(groupName);
                                                removedGroupLogins.Add(groupLogin);
                                            }
                                        }
                                    }
                                }

                                if (removedGroups.Any())
                                {
                                    // Verify removal
                                    context.Load(roleAssignments, ras => ras.Include(ra => ra.Member));
                                    foreach (RoleAssignment ra in roleAssignments)
                                    {
                                        context.Load(ra.Member, m => m.LoginName);
                                    }
                                    await context.ExecuteQueryAsync();
                                    var remainingRAs = roleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, null, "Subfolder", $"Post-removal RAs for '{subfolderName}': {string.Join(" | ", remainingRAs)}, Session ID: {debugSessionId}");
                                    if (removedGroupLogins.Any(login => remainingRAs.Contains(login)))
                                    {
                                        throw new Exception("Permission removal failed to apply (groups still present after verification). Check 'Manage Permissions' rights or sharing links in SharePoint UI.");
                                    }

                                    foreach (var groupName in removedGroups)
                                    {
                                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, groupName, "Subfolder", $"Removed permissions for group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                        UpdateUI(() =>
                                        {
                                            isUpdatingDataGridView = true;
                                            try
                                            {
                                                foreach (DataGridViewRow row in dgvGroupPermissions.Rows.Cast<DataGridViewRow>().ToList())
                                                {
                                                    if (row.Cells["colGroupName"] != null && row.Cells["colGroupName"].Value?.ToString() == groupName)
                                                    {
                                                        dgvGroupPermissions.Rows.Remove(row);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionWarning", _libraryName, groupName, "Subfolder",
                                                    $"Failed to update DataGridView for '{subfolderName}': {ex.Message}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}").GetAwaiter().GetResult();
                                            }
                                            finally
                                            {
                                                isUpdatingDataGridView = false;
                                            }
                                        });
                                    }
                                    UpdateUI(() => statusLabel.Text = $"Removed permissions for {removedGroups.Count} group(s) from '{subfolderName}'.");
                                }
                                else
                                {
                                    UpdateUI(() => statusLabel.Text = "No permissions removed.");
                                }
                                success = true;
                                UpdateUI(() =>
                                {
                                    isUpdatingDataGridView = true;
                                    LoadCurrentPermissionsAsync(debugSessionId);
                                    lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                                    isUpdatingDataGridView = false;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionRetry", _libraryName, null, "Subfolder",
                                    $"Retry {retryCount} for removing permissions from '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }

                            UpdateUI(() =>
                            {
                                isUpdatingDataGridView = true;
                                MessageBox.Show($"Failed to remove permissions after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                statusLabel.Text = "Error removing permissions.";
                                isUpdatingDataGridView = false;
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                                $"Failed to remove permissions for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        }
                    }
                }
                else if (lvSubfolders.SelectedItems.Count > 0)
                {
                    var selectedCount = lvSubfolders.SelectedItems.Count;
                    var confirmMessage = selectedCount == 1
                        ? $"Are you sure you want to remove permissions for the selected subfolder? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links."
                        : $"Are you sure you want to remove permissions for {selectedCount} selected subfolders? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.";

                    var confirm = MessageBox.Show(confirmMessage, "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => statusLabel.Text = "Remove cancelled by user.");
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, null, "Subfolder",
                            $"User cancelled removal for {selectedCount} subfolder(s), Session ID: {debugSessionId}");
                        return;
                    }

                    UpdateUI(() => statusLabel.Text = $"Removing permissions for {selectedCount} subfolder(s)...");
                    var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                    var accounts = await _pca.GetAccountsAsync();
                    var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                    var removedSubfolders = new List<string>();
                    foreach (ListViewItem selectedItem in lvSubfolders.SelectedItems)
                    {
                        string subfolderName = selectedItem.Text;
                        bool hasUniquePermissions = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Unique";
                        if (!hasUniquePermissions)
                        {
                            UpdateUI(() =>
                            {
                                MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                                $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                            continue;
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
                                    context.ExecutingWebRequest += (s, ev) =>
                                    {
                                        ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                                    };

                                    context.Load(context.Web, w => w.ServerRelativeUrl);
                                    await context.ExecuteQueryAsync();
                                    subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                                    subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/");

                                    Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                    context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                    var listItem = subfolder.ListItemAllFields;
                                    context.Load(listItem, l => l.HasUniqueRoleAssignments);
                                    context.Load(context.Web, s => s.RoleDefinitions);
                                    await context.ExecuteQueryAsync();

                                    if (!listItem.HasUniqueRoleAssignments)
                                    {
                                        listItem.BreakRoleInheritance(true, false);
                                        await context.ExecuteQueryAsync();
                                    }

                                    RoleAssignmentCollection roleAssignments = listItem.RoleAssignments;
                                    context.Load(roleAssignments);
                                    await context.ExecuteQueryAsync();

                                    // Log existing RAs
                                    var existingRAs = new List<string>();
                                    foreach (RoleAssignment ra in roleAssignments)
                                    {
                                        context.Load(ra.Member, m => m.LoginName, m => m.Title, m => m.PrincipalType);
                                        context.Load(ra.RoleDefinitionBindings);
                                        await context.ExecuteQueryAsync();
                                        var roleNames = string.Join(", ", ra.RoleDefinitionBindings.Select(rdb => rdb.Name ?? "Null").ToList());
                                        existingRAs.Add($"LoginName: {ra.Member.LoginName}, Title: {(ra.Member.Title != null ? ra.Member.Title : "None")}, PrincipalType: {ra.Member.PrincipalType}, Roles: {(string.IsNullOrEmpty(roleNames) ? "None" : roleNames)}");
                                    }
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, null, "Subfolder",
                                        $"Pre-removal RAs for '{subfolderName}': {string.Join(" | ", existingRAs)}, Session ID: {debugSessionId}");

                                    var removedGroups = new List<string>();
                                    var removedGroupLogins = new List<string>();
                                    foreach (RoleAssignment ra in roleAssignments.ToList())
                                    {
                                        context.Load(ra.Member, m => m.LoginName, m => m.Title, m => m.PrincipalType);
                                        context.Load(ra.RoleDefinitionBindings);
                                        await context.ExecuteQueryAsync();
                                        if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup)
                                        {
                                            var groupName = ra.Member.Title;
                                            var groupLogin = ra.Member.LoginName;
                                            foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                            {
                                                context.Load(rd);
                                                ra.RoleDefinitionBindings.Remove(rd);
                                            }
                                            ra.Update();
                                            context.Load(ra, r => r.Member, r => r.RoleDefinitionBindings);
                                            listItem.Update();
                                            await context.ExecuteQueryAsync();
                                            removedGroups.Add(groupName);
                                            removedGroupLogins.Add(groupLogin);
                                        }
                                    }

                                    if (removedGroups.Any())
                                    {
                                        // Verify removal
                                        context.Load(roleAssignments, ras => ras.Include(ra => ra.Member));
                                        foreach (RoleAssignment ra in roleAssignments)
                                        {
                                            context.Load(ra.Member, m => m.LoginName);
                                        }
                                        await context.ExecuteQueryAsync();
                                        var remainingRAs = roleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, null, "Subfolder",
                                            $"Post-removal RAs for '{subfolderName}': {string.Join(" | ", remainingRAs)}, Session ID: {debugSessionId}");
                                        if (removedGroupLogins.Any(login => remainingRAs.Contains(login)))
                                        {
                                            throw new Exception("Permission removal failed to apply (groups still present after verification). Check 'Manage Permissions' rights or sharing links in SharePoint UI.");
                                        }

                                        foreach (var groupName in removedGroups)
                                        {
                                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, groupName, "Subfolder",
                                                $"Removed permissions for group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                                        }
                                        removedSubfolders.Add(subfolderName);
                                    }

                                    success = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                lastException = ex;
                                retryCount++;
                                if (retryCount < maxRetries)
                                {
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionRetry", _libraryName, null, "Subfolder",
                                        $"Retry {retryCount} for removing permissions from '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
                                    await Task.Delay(1000 * retryCount);
                                    continue;
                                }

                                UpdateUI(() =>
                                {
                                    isUpdatingDataGridView = true;
                                    MessageBox.Show($"Failed to remove permissions for '{subfolderName}' after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    isUpdatingDataGridView = false;
                                });
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                                    $"Failed to remove permissions for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                            }
                        }
                    }

                    if (removedSubfolders.Any())
                    {
                        UpdateUI(() => statusLabel.Text = $"Removed permissions for {removedSubfolders.Count} subfolder(s).");
                    }
                    else
                    {
                        UpdateUI(() => statusLabel.Text = "No permissions removed.");
                    }
                    UpdateUI(() =>
                    {
                        isUpdatingDataGridView = true;
                        LoadCurrentPermissionsAsync(debugSessionId);
                        lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                        isUpdatingDataGridView = false;
                    });
                }
                else
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select one or more subfolders or a group to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Remove cancelled: No subfolders or groups selected.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"No subfolders or groups selected, Session ID: {debugSessionId}");
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingDataGridView = true;
                    MessageBox.Show($"Failed to remove permissions: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error removing permissions.";
                    isUpdatingDataGridView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                    $"Failed to remove permissions: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() =>
                {
                    isUpdatingDataGridView = true;
                    btnRemove.Enabled = true;
                    isUpdatingDataGridView = false;
                });
            }
        }
        private async void btnBreakInheritance_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() =>
            {
                isUpdatingDataGridView = true;
                btnBreakInheritance.Enabled = false;
                isUpdatingDataGridView = false;
            });

            try
            {
                if (lvSubfolders.SelectedItems.Count != 1)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Break inheritance cancelled: Invalid subfolder selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Count: {lvSubfolders.SelectedItems.Count}, Session ID: {debugSessionId}");
                    return;
                }

                var selectedItem = lvSubfolders.SelectedItems[0];
                string subfolderName = selectedItem.Text;
                bool hasUniquePermissions = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Unique";
                if (hasUniquePermissions)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show($"Subfolder '{subfolderName}' already has unique permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Break inheritance cancelled: Already unique permissions.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' already has unique permissions, Session ID: {debugSessionId}");
                    return;
                }

                var confirm = MessageBox.Show(
                    $"Are you sure you want to break role inheritance for subfolder '{subfolderName}'? This will clear all permissions and allow unique permissions to be set.",
                    "Confirm Break Inheritance",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
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
                            context.ExecutingWebRequest += (s, ev) =>
                            {
                                ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken;
                            };

                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                            subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/");

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
                                isUpdatingDataGridView = true;
                                LoadCurrentPermissionsAsync(debugSessionId);
                                lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                                isUpdatingDataGridView = false;
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
                            isUpdatingDataGridView = true;
                            MessageBox.Show($"Failed to break inheritance after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error breaking inheritance.";
                            isUpdatingDataGridView = false;
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
                    isUpdatingDataGridView = true;
                    MessageBox.Show($"Failed to break inheritance: {ex.Message}\nInner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error breaking inheritance.";
                    isUpdatingDataGridView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "BreakSubfolderInheritanceError", _libraryName, null, "Subfolder",
                    $"Failed to break inheritance: {ex.Message}, Inner: {(ex.InnerException != null ? ex.InnerException.Message : "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() =>
                {
                    isUpdatingDataGridView = true;
                    btnBreakInheritance.Enabled = true;
                    isUpdatingDataGridView = false;
                });
            }
        }
        private async void btnChange_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            string groupName = null;
            string groupLogin = null;
            UpdateUI(() => btnChange.Enabled = false);
            try
            {
                if (lvSubfolders.SelectedItems.Count != 1)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Change cancelled: Invalid subfolder selection.";
                    });
                    return;
                }

                if (cmbPermissions.SelectedItem == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a permission level.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Change cancelled: Missing permission selection.";
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
                        statusLabel.Text = "Change cancelled: Subfolder has inherited permissions.";
                    });
                    return;
                }

                string newPermission = cmbPermissions.SelectedItem.ToString();

                UpdateUI(() => _auditLogManager.LogAction(_signedInUserId, null, "Debug", _libraryName, null, "Subfolder", $"btnChange_Click: SelectedRows count: {dgvGroupPermissions.SelectedRows.Count}").GetAwaiter().GetResult());
                if (dgvGroupPermissions.SelectedRows.Count > 0)
                {
                    var selectedRow = dgvGroupPermissions.SelectedRows[0];
                    if (selectedRow.Cells["colGroupName"]?.Value != null && selectedRow.Cells["colGroupId"]?.Value != null)
                    {
                        groupName = selectedRow.Cells["colGroupName"].Value.ToString();
                        var groupId = selectedRow.Cells["colGroupId"].Value.ToString();
                        groupLogin = $"c:0t.c|tenant|{groupId}";
                    }
                    else
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show("Selected group data is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Change cancelled: Invalid group data.";
                        });
                        return;
                    }
                }
                else
                {
                    var selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                    if (selectedGroup == null)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show("Please select a group.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            statusLabel.Text = "Change cancelled: Missing group selection.";
                        });
                        return;
                    }
                    groupName = selectedGroup.DisplayName;
                    groupLogin = $"c:0t.c|tenant|{selectedGroup.Id}";
                }

                if (newPermission == "No Direct Access")
                {
                    var confirm = MessageBox.Show(
                        $"Are you sure you want to remove permissions for '{groupName}' from subfolder '{subfolderName}'? This cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                        "Confirm Remove",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => statusLabel.Text = "Remove cancelled by user.");
                        return;
                    }

                    UpdateUI(() => statusLabel.Text = $"Removing permission for '{groupName}' from '{subfolderName}'...");
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
                                await context.ExecuteQueryAsync();
                                subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                                subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/");

                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                await context.ExecuteQueryAsync();

                                // Log existing RAs
                                var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, groupName, "Subfolder", $"Pre-removal RAs for '{subfolderName}': {string.Join(", ", existingRAs)}");

                                var raToRemove = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupLogin && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                                if (raToRemove != null)
                                {
                                    foreach (RoleDefinition rd in raToRemove.RoleDefinitionBindings.ToList())
                                    {
                                        raToRemove.RoleDefinitionBindings.Remove(rd);
                                    }
                                    raToRemove.Update();
                                    raToRemove.DeleteObject();
                                    await context.ExecuteQueryAsync();

                                    // Verify removal
                                    context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                    await context.ExecuteQueryAsync();
                                    var remainingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionDebug", _libraryName, groupName, "Subfolder", $"Post-removal RAs for '{subfolderName}': {string.Join(", ", remainingRAs)}");
                                    if (remainingRAs.Contains(groupLogin))
                                    {
                                        throw new Exception("Permission removal failed to apply (group still present after verification).");
                                    }

                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, groupName, "Subfolder", $"Removed permissions for group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}' via Change");
                                    UpdateUI(() =>
                                    {
                                        statusLabel.Text = $"Removed permissions for '{groupName}' from '{subfolderName}'.";
                                        if (dgvGroupPermissions.SelectedRows.Count > 0)
                                        {
                                            dgvGroupPermissions.Rows.Remove(dgvGroupPermissions.SelectedRows[0]);
                                        }
                                        LoadCurrentPermissionsAsync();
                                        lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
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
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }

                            UpdateUI(() =>
                            {
                                MessageBox.Show($"Failed to remove permission via Change after {maxRetries} attempts: {ex.Message}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                statusLabel.Text = "Error removing permission.";
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Failed to remove permission via Change for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, StackTrace: {ex.StackTrace}");
                        }
                    }
                    return;
                }

                UpdateUI(() => statusLabel.Text = $"Changing permission for '{groupName}' to '{newPermission}' in '{subfolderName}'...");
                var scopes2 = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts2 = await _pca.GetAccountsAsync();
                var authResult2 = await _pca.AcquireTokenSilent(scopes2, accounts2.FirstOrDefault()).ExecuteAsync();

                const int maxRetries2 = 3;
                int retryCount2 = 0;
                bool success2 = false;
                Exception lastException2 = null;
                string subfolderRelativeUrl2 = null;

                while (retryCount2 < maxRetries2 && !success2)
                {
                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, ev) =>
                            {
                                ev.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult2.AccessToken;
                            };

                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();
                            subfolderRelativeUrl2 = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                            subfolderRelativeUrl2 = subfolderRelativeUrl2.Replace("//", "/");

                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl2);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
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
                                    UpdateUI(() =>
                                    {
                                        MessageBox.Show($"Permission level '{newPermission}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        statusLabel.Text = "Error: Permission level not found.";
                                    });
                                    await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Permission level '{newPermission}' not found for subfolder '{subfolderName}'");
                                    return;
                                }

                                ra.RoleDefinitionBindings.Add(roleDefinition);
                                ra.Update();
                                await context.ExecuteQueryAsync();

                                // Verify change
                                context.Load(ra.RoleDefinitionBindings);
                                await context.ExecuteQueryAsync();
                                if (!ra.RoleDefinitionBindings.Any(rdb => rdb.Name == roleName))
                                {
                                    throw new Exception("Permission change failed to apply after execution.");
                                }

                                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermission", _libraryName, groupName, "Subfolder", $"Changed permission for group '{groupName}' to '{newPermission}' on subfolder '{subfolderName}' in library '{_libraryName}'");
                                UpdateUI(() =>
                                {
                                    statusLabel.Text = $"Changed permission for '{groupName}' to '{newPermission}' in '{subfolderName}'.";
                                    if (dgvGroupPermissions.SelectedRows.Count > 0)
                                    {
                                        dgvGroupPermissions.SelectedRows[0].Cells["colPermission"].Value = newPermission;
                                    }
                                    LoadCurrentPermissionsAsync();
                                    lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
                                });

                                success2 = true;
                            }
                            else
                            {
                                UpdateUI(() =>
                                {
                                    MessageBox.Show($"Group '{groupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    statusLabel.Text = "Change cancelled: Group not found.";
                                });
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException2 = ex;
                        retryCount2++;
                        if (retryCount2 < maxRetries2)
                        {
                            await Task.Delay(1000 * retryCount2);
                            continue;
                        }

                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Failed to change permission after {maxRetries2} attempts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error changing permission.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Failed to change permission for subfolder '{subfolderName}' at '{subfolderRelativeUrl2}': {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to change permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error changing permission.";
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"Failed to change permission: {ex.Message}");
            }
            finally
            {
                UpdateUI(() => btnChange.Enabled = true);
            }
        }
        private async void btnResetPermissions_Click(object sender, EventArgs e)
        {
            UpdateUI(() => btnResetPermissions.Enabled = false);
            try
            {
                if (lvSubfolders.SelectedItems.Count != 1)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select exactly one subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Reset permissions cancelled: Invalid subfolder selection.";
                    });
                    return;
                }

                var selectedItem = lvSubfolders.SelectedItems[0];
                string subfolderName = selectedItem.Text;
                bool hasUniquePermissions = selectedItem.SubItems.Count >= 3 && selectedItem.SubItems[2].Text == "Unique";
                if (!hasUniquePermissions)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "Reset permissions cancelled: Subfolder has inherited permissions.";
                    });
                    return;
                }

                var confirm = MessageBox.Show(
                    $"Are you sure you want to reset all permissions for subfolder '{subfolderName}'? This will remove all group permissions and cannot be undone.\n\nNote: If permissions remain, check 'Manage Permissions' rights or sharing links.",
                    "Confirm Reset Permissions",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => statusLabel.Text = "Reset permissions cancelled by user.");
                    return;
                }

                UpdateUI(() => statusLabel.Text = $"Resetting permissions for '{subfolderName}'...");
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
                            await context.ExecuteQueryAsync();
                            subfolderRelativeUrl = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}";
                            subfolderRelativeUrl = subfolderRelativeUrl.Replace("//", "/");

                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(
                                ra => ra.Member.LoginName,
                                ra => ra.PrincipalId,
                                ra => ra.Member.Title,
                                ra => ra.RoleDefinitionBindings,
                                ra => ra.Member.PrincipalType));
                            context.Load(subfolder.ListItemAllFields, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();

                            // Log existing RAs
                            var existingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => $"{ra.Member.LoginName} ({(ra.RoleDefinitionBindings.FirstOrDefault() != null ? ra.RoleDefinitionBindings.FirstOrDefault().Name : "None")})").ToList();
                            await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsDebug", _libraryName, null, "Subfolder", $"Pre-reset RAs for '{subfolderName}': {string.Join(", ", existingRAs)}");

                            var removedGroups = new List<string>();
                            var removedGroupLogins = new List<string>();
                            foreach (var ra in subfolder.ListItemAllFields.RoleAssignments.ToList())
                            {
                                if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup)
                                {
                                    var groupName = ra.Member.Title;
                                    var groupLogin = ra.Member.LoginName;
                                    foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                    {
                                        ra.RoleDefinitionBindings.Remove(rd);
                                    }
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

                                // Verify removal
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName));
                                await context.ExecuteQueryAsync();
                                var remainingRAs = subfolder.ListItemAllFields.RoleAssignments.Select(ra => ra.Member.LoginName).ToList();
                                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsDebug", _libraryName, null, "Subfolder", $"Post-reset RAs for '{subfolderName}': {string.Join(", ", remainingRAs)}");
                                if (removedGroupLogins.Any(login => remainingRAs.Contains(login)))
                                {
                                    throw new Exception("Permission reset failed to apply (groups still present after verification).");
                                }

                                foreach (var groupName in removedGroups)
                                {
                                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissions", _libraryName, groupName, "Subfolder", $"Reset permissions by removing group '{groupName}' from subfolder '{subfolderName}' in library '{_libraryName}'");
                                }
                                UpdateUI(() => statusLabel.Text = $"Reset permissions for '{subfolderName}'.");
                            }
                            else
                            {
                                UpdateUI(() => statusLabel.Text = $"No permissions to reset for '{subfolderName}'.");
                            }
                            UpdateUI(() =>
                            {
                                dgvGroupPermissions.Rows.Clear();
                                LoadCurrentPermissionsAsync();
                                lvSubfolders_SelectedIndexChanged(null, EventArgs.Empty);
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
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }

                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Failed to reset permissions after {maxRetries} attempts: {ex.Message}\n\nCheck if your account has 'Manage Permissions' rights or if sharing links exist in the SharePoint UI.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            statusLabel.Text = "Error resetting permissions.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder", $"Failed to reset permissions for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to reset permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Error resetting permissions.";
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder", $"Failed to reset permissions: {ex.Message}");
            }
            finally
            {
                UpdateUI(() => btnResetPermissions.Enabled = true);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            UpdateUI(() => this.Close());
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            UpdateUI(() => btnRefresh.Enabled = false);
            try
            {
                LoadCurrentPermissionsAsync();
            }
            finally
            {
                UpdateUI(() => btnRefresh.Enabled = true);
            }
        }
    }
}
