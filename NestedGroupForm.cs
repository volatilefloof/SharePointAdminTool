using Microsoft.Graph.Models;
using Microsoft.Graph;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace EntraGroupsApp
{
    public partial class NestedGroupForm : Form
    {
        private readonly List<DirectoryObject> _nestedMembers;
        private readonly string _groupId;
        private readonly Group _parentGroup;
        private readonly GraphServiceClient _graphClient;
        private readonly Stack<ManageMembershipsForm.ActionRecord> _undoStack;
        private bool _clipboardPrompted;
        private readonly DateTime _sessionStartTime;
        private readonly AuditLogManager _auditLogManager;
        private readonly string _signedInUserId;
        private readonly Dictionary<string, List<string>> _departmentPrefixes = new Dictionary<string, List<string>>
        {
            { "Accounting (ACCT)", new List<string> { "CSG-CLBA-ACCT", "FSG-CLBA-ACCT" } },
            { "Finance (FINC)", new List<string> { "CSG-CLBA-FINC", "FSG-CLBA-FINC" } },
            { "Management (MGMT)", new List<string> { "CSG-CLBA-MGMT", "FSG-CLBA-MGMT" } },
            { "MBA Programs (BizGrad)", new List<string> { "CSG-CLBA-BizGrad", "FSG-CLBA-BizGrad" } },
            { "Business Undergraduate Advising Office (UAO)", new List<string> { "CSG-CLBA-UAO", "FSG-CLBA-UAO" } },
            { "Information & Operations Management (INFO)", new List<string> { "CSG-CLBA-INFO", "FSG-CLBA-INFO" } },
            { "Dean's Office (DEAN)", new List<string> { "CSG-CLBA-DEAN", "FSG-CLBA-DEAN" } },
            { "Business Undergraduate Special Programs (BUSP)", new List<string> { "CSG-CLBA-BUSP", "FSG-CLBA-BUSP" } },
            { "Marcomm & Experience Team (COMM)", new List<string> { "CSG-CLBA-COMM", "FSG-CLBA-COMM" } },
            { "Center for International Business Studies (CIBS)", new List<string> { "CSG-CLBA-CIBS", "FSG-CLBA-CIBS" } },
            { "Center for Executive Development (CED)", new List<string> { "CSG-CLBA-CED", "FSG-CLBA-CED" } },
            { "Media Office (UAVS)", new List<string> { "CSG-CLBA-MEDIA", "FSG-CLBA-MEDIA" } },
            { "Marketing (MKTG)", new List<string> { "CSG-CLBA-MKTG", "FSG-CLBA-MKTG" } }
        };

        public NestedGroupForm(List<DirectoryObject> nestedMembers, string groupId, Group parentGroup, GraphServiceClient graphClient, AuditLogManager auditLogManager, string signedInUserId)
        {
            InitializeComponent();
            _nestedMembers = nestedMembers ?? throw new ArgumentNullException(nameof(nestedMembers));
            _groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            _parentGroup = parentGroup ?? throw new ArgumentNullException(nameof(parentGroup));
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _auditLogManager = auditLogManager ?? throw new ArgumentNullException(nameof(auditLogManager));
            _signedInUserId = signedInUserId ?? throw new ArgumentNullException(nameof(signedInUserId));
            _undoStack = new Stack<ManageMembershipsForm.ActionRecord>();
            _sessionStartTime = DateTime.Now;
            FormClosing += NestedGroupForm_FormClosing;

            contextMenuStripMembers.Opening += ContextMenuStripMembers_Opening;
            dataGridViewMembers.MouseDown += dataGridViewMembers_MouseDown;
            btnExportToCsv.Click += btnExportToCsv_Click;

            LoadMembers();
        }

        // NEW: Public method to allow pushing undo actions from child forms
        public void PushUndoAction(ManageMembershipsForm.ActionRecord record)
        {
            _undoStack.Push(record);
        }

        private void NestedGroupForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_clipboardPrompted)
            {
                var sessionLogs = _auditLogManager.GetLogsByUserAndDate(_signedInUserId, null)
                    .Where(l => l.Timestamp >= _sessionStartTime.AddSeconds(-1) &&
                                new[] { "AddMember", "RemoveMember", "AddOwner", "RemoveOwner", "ReplaceGroup", "CopyGroups", "AddToGroups", "AddNestedGroup", "RemoveNestedGroup", "CopyUser" }
                                .Contains(l.ActionType))
                    .ToList();

                Console.WriteLine($"FormClosing: Found {sessionLogs.Count} modification logs for user {_signedInUserId}");
                foreach (var log in sessionLogs)
                {
                    Console.WriteLine($"Log: ActionType={log.ActionType}, GroupName={log.GroupName}, TargetName={log.TargetName}, Timestamp={log.Timestamp}, Details={log.Details}");
                }

                if (sessionLogs.Any())
                {
                    var result = MessageBox.Show(
                        "Would you like to copy your current session's audit logs for user or group modifications to the clipboard before closing?",
                        "Export Session Modification Logs",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            var output = sessionLogs.Select(log =>
                                $"Modification applied to {log.GroupName} for \"{log.TargetName}\" ({log.TargetType}) on {log.Timestamp:yyyy-MM-dd HH:mm:ss}. Details: {log.Details}")
                                .Aggregate((a, b) => a + "\n" + b);

                            Clipboard.SetText(output);
                            MessageBox.Show("Session modification audit logs copied to clipboard.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error copying session logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }

                    _clipboardPrompted = true;
                }
            }
        }

        private void dataGridViewMembers_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTestInfo = dataGridViewMembers.HitTest(e.X, e.Y);
                if (hitTestInfo.Type == DataGridViewHitTestType.Cell && hitTestInfo.RowIndex >= 0)
                {
                    if (!dataGridViewMembers.Rows[hitTestInfo.RowIndex].Selected)
                    {
                        dataGridViewMembers.ClearSelection();
                        dataGridViewMembers.Rows[hitTestInfo.RowIndex].Selected = true;
                    }

                    var columnName = dataGridViewMembers.Columns[hitTestInfo.ColumnIndex].Name;
                    dynamic member = dataGridViewMembers.Rows[hitTestInfo.RowIndex].DataBoundItem;

                    manageNestedGroupToolStripMenuItem.Visible = member.Type == "Group" &&
                        member.DisplayName != null &&
                        member.DisplayName.ToString().StartsWith("CSG", StringComparison.OrdinalIgnoreCase);
                    copyIdToolStripMenuItem.Visible = columnName == "Id";

                    if (manageNestedGroupToolStripMenuItem.Visible || copyIdToolStripMenuItem.Visible)
                    {
                        contextMenuStripMembers.Show(dataGridViewMembers, e.Location);
                    }
                }
            }
        }

        private void ContextMenuStripMembers_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Logic moved to dataGridViewMembers_MouseDown
        }

        private void copyIdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridViewMembers.SelectedRows.Count == 1)
            {
                dynamic member = dataGridViewMembers.SelectedRows[0].DataBoundItem;
                string id = member.Id as string;
                if (!string.IsNullOrEmpty(id))
                {
                    Clipboard.SetText(id);
                    MessageBox.Show("ID copied to clipboard.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No valid ID to copy.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadMembers()
        {
            var displayMembers = _nestedMembers.Select(m => new
            {
                DisplayName = m is User user ? user.DisplayName : (m is Group group ? group.DisplayName : "Unknown"),
                Id = m.Id,
                Type = m.OdataType switch
                {
                    "#microsoft.graph.user" => "User",
                    "#microsoft.graph.group" => "Group",
                    "#microsoft.graph.servicePrincipal" => "Service Principal",
                    _ => "Other"
                },
                UPN = m is User user2 ? user2.UserPrincipalName ?? string.Empty : string.Empty
            }).ToList();

            dataGridViewMembers.DataSource = displayMembers;
            dataGridViewMembers.AutoResizeColumn(dataGridViewMembers.Columns["DisplayName"].Index, DataGridViewAutoSizeColumnMode.AllCells);
            dataGridViewMembers.AutoResizeColumn(dataGridViewMembers.Columns["UPN"].Index, DataGridViewAutoSizeColumnMode.AllCells);
            dataGridViewMembers.Columns["DisplayName"].MinimumWidth = 200;
            dataGridViewMembers.Columns["UPN"].MinimumWidth = 250;
            dataGridViewMembers.Refresh();
        }

        private async void btnAddUsers_Click(object? sender, EventArgs e)
        {
            List<User> previousSelectedUsers = new List<User>();

            while (true)
            {
                using (var browseForm = new BrowseUsersForm(_graphClient, false))
                {
                    if (browseForm.ShowDialog() != DialogResult.OK)
                        return;

                    var selectedUsers = browseForm.SelectedUsers;
                    if (!selectedUsers.Any())
                    {
                        MessageBox.Show("No users selected.");
                        continue;
                    }

                    try
                    {
                        var currentMembers = new List<DirectoryObject>();
                        var membersRequest = await _graphClient.Groups[_groupId].Members.GetAsync();
                        if (membersRequest?.Value != null)
                        {
                            var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                                .CreatePageIterator(_graphClient, membersRequest, (member) =>
                                {
                                    currentMembers.Add(member);
                                    return true;
                                });
                            await pageIterator.IterateAsync();
                        }

                        var memberIds = currentMembers
                            .Select(m => m.Id)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToHashSet();

                        var matchedUsers = new List<(string Input, User User)>();
                        var duplicates = new List<string>();

                        foreach (var user in selectedUsers)
                        {
                            string input = user.UserPrincipalName ?? user.Mail ?? user.DisplayName ?? user.Id;
                            if (memberIds.Contains(user.Id))
                            {
                                duplicates.Add(input);
                            }
                            else
                            {
                                matchedUsers.Add((input, user));
                            }
                        }

                        if (duplicates.Any())
                        {
                            using (var duplicateDialog = new DuplicateUsersDialog(duplicates))
                            {
                                duplicateDialog.ShowDialog();
                            }
                        }

                        var validUsers = matchedUsers;

                        while (true)
                        {
                            using (var confirmDialog = new ConfirmUsersDialog(validUsers))
                            {
                                var result = confirmDialog.ShowDialog();
                                if (result != DialogResult.OK)
                                {
                                    if (confirmDialog.EditListRequested)
                                    {
                                        previousSelectedUsers = confirmDialog.SelectedUsers;
                                        break;
                                    }
                                    return;
                                }

                                var usersToAdd = confirmDialog.SelectedUsers;
                                if (!usersToAdd.Any())
                                {
                                    MessageBox.Show("No users selected to add.");
                                    return;
                                }

                                var addedCount = 0;
                                var duplicateUsers = new List<string>();
                                foreach (var user in usersToAdd)
                                {
                                    try
                                    {
                                        await _graphClient.Groups[_groupId].Members.Ref.PostAsync(new ReferenceCreate
                                        {
                                            OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{user.Id}"
                                        });
                                        _undoStack.Push(new ManageMembershipsForm.ActionRecord
                                        {
                                            ActionType = "AddMember",
                                            ParentGroup = new Group { Id = _groupId },
                                            User = user
                                        });
                                        LogAction("AddMember", _parentGroup.DisplayName, user.DisplayName ?? user.UserPrincipalName ?? user.Id, "User", user.Id, $"Added user to group {_groupId}");
                                        addedCount++;
                                    }
                                    catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 409)
                                    {
                                        duplicateUsers.Add(user.DisplayName ?? user.UserPrincipalName ?? user.Id);
                                        Debug.WriteLine($"Duplicate user detected: {user.DisplayName} (ID: {user.Id})");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error adding user {user.DisplayName}: {ex.Message}");
                                        MessageBox.Show($"Failed to add user {user.DisplayName}: {ex.Message}");
                                    }
                                }

                                if (duplicateUsers.Any())
                                {
                                    using (var duplicateDialog = new DuplicateUsersDialog(duplicateUsers))
                                    {
                                        duplicateDialog.ShowDialog();
                                    }
                                }

                                if (addedCount > 0)
                                {
                                    await RefreshMembers();
                                    MessageBox.Show($"{addedCount} user(s) added successfully.");
                                }
                                else if (!duplicateUsers.Any())
                                {
                                    MessageBox.Show("No users were added.");
                                }

                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in btnAddUsers_Click: {ex}");
                        MessageBox.Show($"Error adding users: {ex.Message}");
                    }
                }
            }
        }

        private async void btnRemoveUsers_Click(object sender, EventArgs e)
        {
            var selectedRows = dataGridViewMembers.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one user to remove.");
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to remove {selectedRows.Count} user(s) from this nested group?", "Confirm Removal", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.Yes)
            {
                try
                {
                    foreach (DataGridViewRow row in selectedRows)
                    {
                        dynamic member = row.DataBoundItem;
                        if (member.Type != "User")
                        {
                            MessageBox.Show($"Skipping non-user member: {member.DisplayName}");
                            continue;
                        }
                        var user = await _graphClient.Users[member.Id].GetAsync();
                        await _graphClient.Groups[_groupId].Members[member.Id].Ref.DeleteAsync();
                        _undoStack.Push(new ManageMembershipsForm.ActionRecord
                        {
                            ActionType = "RemoveMember",
                            ParentGroup = new Group { Id = _groupId },
                            User = user
                        });
                        LogAction("RemoveMember", _parentGroup.DisplayName, user.DisplayName ?? user.UserPrincipalName ?? user.Id, "User", user.Id, $"Removed user from group {_groupId}");
                    }
                    await RefreshMembers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing users: {ex.Message}");
                    Debug.WriteLine($"Error in btnRemoveUsers_Click: {ex}");
                }
            }
        }

        private async void btnCopyUsers_Click(object? sender, EventArgs e)
        {
            var selectedRows = dataGridViewMembers.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one user to copy.");
                return;
            }

            var usersToCopy = new List<(string Id, string DisplayName)>();
            foreach (DataGridViewRow row in selectedRows)
            {
                dynamic member = row.DataBoundItem;
                if (member.Type != "User")
                {
                    MessageBox.Show($"Skipping non-user member: {member.DisplayName}");
                    continue;
                }
                usersToCopy.Add((member.Id, member.DisplayName));
            }

            if (!usersToCopy.Any())
            {
                MessageBox.Show("No valid users selected to copy.");
                return;
            }

            string? department = null;
            foreach (var dept in _departmentPrefixes)
            {
                if (_parentGroup.DisplayName != null && dept.Value.Any(prefix => _parentGroup.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    department = dept.Key;
                    break;
                }
            }

            if (string.IsNullOrEmpty(department))
            {
                MessageBox.Show("Could not determine the department of the nested group.");
                return;
            }

            try
            {
                using (var copyForm = new CopyToGroupForm(department, _graphClient, this))
                {
                    if (copyForm.ShowDialog() != DialogResult.OK || !copyForm.SelectedGroups.Any())
                    {
                        return;
                    }

                    var targetGroups = copyForm.SelectedGroups;

                    using (var confirmDialog = new ConfirmCopyDialog(usersToCopy, targetGroups))
                    {
                        if (confirmDialog.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }

                        var addedCount = 0;
                        foreach (var targetGroup in targetGroups)
                        {
                            var currentMembers = new List<DirectoryObject>();
                            var membersRequest = await _graphClient.Groups[targetGroup.Id].Members.GetAsync();
                            if (membersRequest?.Value != null)
                            {
                                var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                                    .CreatePageIterator(_graphClient, membersRequest, (member) =>
                                    {
                                        currentMembers.Add(member);
                                        return true;
                                    });
                                await pageIterator.IterateAsync();
                            }

                            var memberIds = currentMembers.Select(m => m.Id).ToHashSet();

                            foreach (var user in usersToCopy)
                            {
                                if (memberIds.Contains(user.Id))
                                {
                                    Debug.WriteLine($"User {user.DisplayName} already in group {targetGroup.DisplayName}");
                                    continue;
                                }

                                try
                                {
                                    await _graphClient.Groups[targetGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                                    {
                                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{user.Id}"
                                    });
                                    _undoStack.Push(new ManageMembershipsForm.ActionRecord
                                    {
                                        ActionType = "CopyUser",
                                        ParentGroup = _parentGroup,
                                        User = new User { Id = user.Id, DisplayName = user.DisplayName },
                                        TargetGroup = targetGroup
                                    });
                                    LogAction("CopyUser", _parentGroup.DisplayName, user.DisplayName, "User", user.Id, $"Copied user to group {targetGroup.DisplayName} (ID: {targetGroup.Id})");
                                    addedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error copying user {user.DisplayName} to group {targetGroup.DisplayName}: {ex.Message}");
                                    MessageBox.Show($"Failed to copy user {user.DisplayName} to group {targetGroup.DisplayName}: {ex.Message}");
                                }
                            }
                        }

                        if (addedCount > 0)
                        {
                            MessageBox.Show($"Copied {addedCount} user(s) to {targetGroups.Count} group(s).");
                        }
                        else
                        {
                            MessageBox.Show("No users were copied (all users may already exist in target groups).");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in btnCopyUsers_Click: {ex}");
                MessageBox.Show($"Error copying users: {ex.Message}");
            }
        }

        private void btnExportToCsv_Click(object sender, EventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "CSV files (*.csv)|*.csv";
                    saveDialog.Title = "Export Group to CSV";
                    saveDialog.FileName = $"{_parentGroup.DisplayName}_Members_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var csv = new StringBuilder();
                        csv.AppendLine("DisplayName,Id,Type,UPN");

                        foreach (var m in _nestedMembers)
                        {
                            string displayName = (m is User user ? user.DisplayName : (m is Group group ? group.DisplayName : "Unknown")) ?? "";
                            string id = m.Id ?? "";
                            string type = m.OdataType switch
                            {
                                "#microsoft.graph.user" => "User",
                                "#microsoft.graph.group" => "Group",
                                "#microsoft.graph.servicePrincipal" => "Service Principal",
                                _ => "Other"
                            };
                            string upn = m is User user2 ? user2.UserPrincipalName ?? "" : "";

                            csv.AppendLine($"\"{displayName.Replace("\"", "\"\"")}\",\"{id.Replace("\"", "\"\"")}\",\"{type.Replace("\"", "\"\"")}\",\"{upn.Replace("\"", "\"\"")}\"");
                        }

                        File.WriteAllText(saveDialog.FileName, csv.ToString());
                        MessageBox.Show("Group members exported to CSV successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LogAction("ExportCsv", _parentGroup.DisplayName, "", "", "", $"Exported group {_groupId} members to CSV file: {saveDialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnAddNestedGroup_Click(object? sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Starting btnAddNestedGroup_Click");

                string? department = null;
                string? departmentPrefix = null;
                foreach (var dept in _departmentPrefixes)
                {
                    if (_parentGroup.DisplayName != null && dept.Value.Any(prefix => _parentGroup.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        department = dept.Key;
                        departmentPrefix = dept.Value.FirstOrDefault(p => p.StartsWith("CSG-CLBA-"));
                        break;
                    }
                }

                if (string.IsNullOrEmpty(department) || departmentPrefix == null)
                {
                    MessageBox.Show("Could not determine the department of the selected group.");
                    Debug.WriteLine($"Department not found for group: {_parentGroup.DisplayName}");
                    return;
                }

                Debug.WriteLine($"Current department: {department}, Prefix: {departmentPrefix}");

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var allGroups = new List<Microsoft.Graph.Models.Group>();
                Debug.WriteLine($"Fetching groups with prefix: {departmentPrefix}");

                var request = _graphClient.Groups.GetAsync(r =>
                {
                    r.QueryParameters.Select = new[] { "id", "displayName" };
                    r.QueryParameters.Filter = $"startswith(displayName, '{departmentPrefix}')";
                    r.QueryParameters.Top = 999;
                }, cts.Token);

                var response = await request;
                if (response?.Value != null)
                {
                    var pageIterator = PageIterator<Group, GroupCollectionResponse>
                        .CreatePageIterator(_graphClient, response, (g) =>
                        {
                            if (g.DisplayName != null)
                            {
                                allGroups.Add(g);
                            }
                            return true;
                        });

                    await pageIterator.IterateAsync();
                }
                Debug.WriteLine($"Fetched {allGroups.Count} groups for department {department}");

                var groups = allGroups
                    .Where(g => g.DisplayName != null &&
                                g.DisplayName.StartsWith(departmentPrefix, StringComparison.OrdinalIgnoreCase) &&
                                g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!groups.Any())
                {
                    MessageBox.Show($"No {department} user groups (containing 'mays-group') found.");
                    Debug.WriteLine($"No mays-group groups found for department {department}");
                    return;
                }

                Debug.WriteLine($"Total groups after filtering: {groups.Count}");
                foreach (var group in groups)
                {
                    Debug.WriteLine($"Filtered group: {group.DisplayName} (ID: {group.Id})");
                }

                Debug.WriteLine("Opening GroupSelectionForm");
                using (var selectionForm = new GroupSelectionForm(groups))
                {
                    selectionForm.Owner = this;
                    if (selectionForm.ShowDialog() != DialogResult.OK)
                    {
                        Debug.WriteLine("Group selection cancelled");
                        return;
                    }

                    Debug.WriteLine($"Selected {selectionForm.SelectedGroupNames.Count} groups: {string.Join(", ", selectionForm.SelectedGroupNames)}");
                    var addedCount = 0;
                    var failedGroups = new List<string>();

                    for (int i = 0; i < selectionForm.SelectedGroupIds.Count; i++)
                    {
                        var nestedGroup = new Microsoft.Graph.Models.Group
                        {
                            Id = selectionForm.SelectedGroupIds[i],
                            DisplayName = selectionForm.SelectedGroupNames[i]
                        };

                        try
                        {
                            Debug.WriteLine($"Adding nested group {nestedGroup.DisplayName}");
                            await _graphClient.Groups[_groupId].Members.Ref.PostAsync(new ReferenceCreate
                            {
                                OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{nestedGroup.Id}"
                            });
                            _undoStack.Push(new ManageMembershipsForm.ActionRecord { ActionType = "AddNestedGroup", ParentGroup = new Group { Id = _groupId }, NestedGroup = nestedGroup });
                            LogAction("AddNestedGroup", _parentGroup.DisplayName, nestedGroup.DisplayName, "Group", nestedGroup.Id, $"Added nested group to parent group {_groupId}");
                            addedCount++;
                            Debug.WriteLine($"Nested group added: {nestedGroup.DisplayName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error adding nested group {nestedGroup.DisplayName}: {ex.Message}");
                            failedGroups.Add(nestedGroup.DisplayName ?? "Unknown");
                        }
                    }

                    await RefreshMembers();

                    if (addedCount > 0)
                    {
                        var message = $"{addedCount} nested group(s) added to '{_parentGroup.DisplayName}'.";
                        if (failedGroups.Any())
                        {
                            message += $"\nFailed to add {failedGroups.Count} group(s): {string.Join(", ", failedGroups)}.";
                        }
                        MessageBox.Show(message);
                    }
                    else
                    {
                        MessageBox.Show($"No groups were added. Errors: {string.Join(", ", failedGroups)}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding nested groups: {ex.Message}");
                Debug.WriteLine($"Error in btnAddNestedGroup_Click: {ex}");
            }
        }

        private async void btnRemoveNestedGroup_Click(object? sender, EventArgs e)
        {
            var selectedRows = dataGridViewMembers.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one nested group to remove.");
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to remove {selectedRows.Count} nested group(s) from '{_parentGroup.DisplayName}'?", "Confirm Removal", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.Yes)
            {
                try
                {
                    foreach (DataGridViewRow row in selectedRows)
                    {
                        dynamic member = row.DataBoundItem;
                        string memberType = member.Type;
                        string memberDisplayName = member.DisplayName;
                        string memberId = member.Id;

                        if (memberType != "Group" || !memberDisplayName.Contains("CSG-CLBA", StringComparison.OrdinalIgnoreCase) || !memberDisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show($"Skipping non-CSG-CLBA or non-mays-group group: {memberDisplayName}");
                            continue;
                        }

                        var group = await _graphClient.Groups[memberId].GetAsync(config =>
                        {
                            config.QueryParameters.Select = new[] { "id", "displayName" };
                        });

                        await _graphClient.Groups[_groupId].Members[memberId].Ref.DeleteAsync();

                        _undoStack.Push(new ManageMembershipsForm.ActionRecord
                        {
                            ActionType = "RemoveNestedGroup",
                            ParentGroup = new Group { Id = _groupId },
                            NestedGroup = group
                        });
                        LogAction("RemoveNestedGroup", _parentGroup.DisplayName, group.DisplayName, "Group", group.Id, $"Removed nested group from parent group {_groupId}");
                    }
                    await RefreshMembers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing nested groups: {ex.Message}");
                }
            }
        }

        private async void manageNestedGroupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (dataGridViewMembers.SelectedRows.Count != 1)
            {
                MessageBox.Show("Please select exactly one group to manage.");
                return;
            }

            dynamic member = dataGridViewMembers.SelectedRows[0].DataBoundItem;
            if (member.Type != "Group")
            {
                MessageBox.Show("Please select a group to manage.");
                return;
            }

            string nestedGroupId = member.Id;
            try
            {
                var nestedMembers = new List<DirectoryObject>();
                var membersRequest = await _graphClient.Groups[nestedGroupId].Members.GetAsync();

                if (membersRequest == null)
                {
                    MessageBox.Show("Failed to retrieve members.");
                    return;
                }

                var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                    .CreatePageIterator(
                        _graphClient,
                        membersRequest,
                        m =>
                        {
                            nestedMembers.Add(m);
                            return true;
                        });

                await pageIterator.IterateAsync();

                var nestedGroupForm = new NestedGroupForm(nestedMembers, nestedGroupId, new Group { Id = nestedGroupId, DisplayName = member.DisplayName }, _graphClient, _auditLogManager, _signedInUserId);
                nestedGroupForm.Owner = this;
                nestedGroupForm.ShowDialog();
                await RefreshMembers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error managing nested group: {ex.Message}");
            }
        }

        private async Task RefreshMembers()
        {
            _nestedMembers.Clear();
            var membersRequest = await _graphClient.Groups[_groupId].Members.GetAsync();
            if (membersRequest?.Value != null)
            {
                var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                    .CreatePageIterator(_graphClient, membersRequest, (member) =>
                    {
                        _nestedMembers.Add(member);
                        return true;
                    });
                await pageIterator.IterateAsync();
            }
            LoadMembers();
        }

        private void LogAction(string actionType, string groupName, string targetName, string targetType, string targetId, string details)
        {
            _auditLogManager.LogAction(_signedInUserId, actionType, groupName, targetName, targetType, targetId, details);
        }

        private void btnClose_Click(object? sender, EventArgs e)
        {
            // UPDATED: Handle both ManageMembershipsForm and NestedGroupForm as Owner for undo propagation
            if (Owner is ManageMembershipsForm parentForm)
            {
                while (_undoStack.Count > 0)
                {
                    parentForm.PushUndoAction(_undoStack.Pop());
                }
            }
            else if (Owner is NestedGroupForm nestedParent)
            {
                while (_undoStack.Count > 0)
                {
                    nestedParent.PushUndoAction(_undoStack.Pop());
                }
            }
            this.Close();
        }
    }
}