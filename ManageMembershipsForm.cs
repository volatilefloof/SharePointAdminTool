using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace EntraGroupsApp
{
    public partial class ManageMembershipsForm : Form
    {
        private GraphServiceClient _graphClient;
        private List<Group> _selectedGroups;
        private Form1 _mainForm;
        private GroupSearchForm? _groupSearchForm;
        private Stack<ActionRecord> undoStack = new Stack<ActionRecord>();
        internal readonly AuditLogManager _auditLogManager;
        internal readonly string _signedInUserId;
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

        public class ActionRecord
        {
            public required string ActionType { get; set; }
            public required Group ParentGroup { get; set; }
            public User? User { get; set; }
            public Group? NestedGroup { get; set; }
            public Group? TargetGroup { get; set; }
        }

        public ManageMembershipsForm(List<Group> selectedGroups, GraphServiceClient graphClient, Form1 mainForm,
            GroupSearchForm? groupSearchForm, AuditLogManager auditLogManager, string signedInUserId)
        {
            InitializeComponent();
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _selectedGroups = selectedGroups ?? new List<Group>();
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _groupSearchForm = groupSearchForm;
            _auditLogManager = auditLogManager ?? throw new ArgumentNullException(nameof(auditLogManager));
            _signedInUserId = signedInUserId ?? throw new ArgumentNullException(nameof(signedInUserId));

            // Enable AutoSize for all buttons to match old behavior
            btnBrowseAddUser.AutoSize = true;
            btnRemoveUser.AutoSize = true;
            btnSelectAll.AutoSize = true;
            btnUndo.AutoSize = true;
            btnReplaceUser.AutoSize = true;
            btnExportToCsv.AutoSize = true;
            btnAddNestedGroup.AutoSize = true;
            btnRemoveNestedGroup.AutoSize = true;
            btnCopyUsers.AutoSize = true;
            btnReturn.AutoSize = true;
            btnReturnToPreviousWindow.AutoSize = true;

            listBoxGroups.DataSource = _selectedGroups;
            listBoxGroups.DisplayMember = "DisplayName";

            // Event handlers
            listBoxGroups.SelectedIndexChanged += async (s, e) => await listBoxGroups_SelectedIndexChanged(s, e);
            btnBrowseAddUser.Click += btnBrowseAddUser_Click;
            btnRemoveUser.Click += btnRemoveUser_Click;
            btnUndo.Click += btnUndo_Click;
            btnReplaceUser.Click += btnReplaceUser_Click;
            btnReturn.Click += btnReturn_Click;
            btnSelectAll.Click += btnSelectAll_Click;
            btnAddNestedGroup.Click += btnAddNestedGroup_Click;
            btnRemoveNestedGroup.Click += btnRemoveNestedGroup_Click;
            btnReturnToPreviousWindow.Click += btnReturnToPreviousWindow_Click;
            btnExportToCsv.Click += btnExportToCsv_Click;
            btnCopyUsers.Click += btnCopyUsers_Click;
            dataGridViewMembers.MouseDown += dataGridViewMembers_MouseDown;

            expandGroupToolStripMenuItem.Text = "Manage Nested Group";

            btnReturnToPreviousWindow.Visible = true;
            btnReturnToPreviousWindow.Enabled = _groupSearchForm != null;

            if (_selectedGroups.Any())
            {
                listBoxGroups.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show("No groups provided to display.");
            }
        }

        public void PushUndoAction(ActionRecord action)
        {
            undoStack.Push(action);
        }

        private async void btnBrowseAddUser_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }
            var selectedGroup = (Group)listBoxGroups.SelectedItem;

            bool usersConfirmed = false;
            List<User> validUsers = null;

            while (!usersConfirmed)
            {
                using (var browseForm = new BrowseUsersForm(_graphClient))
                {
                    if (browseForm.ShowDialog() != DialogResult.OK || !browseForm.SelectedUsers.Any())
                    {
                        return;
                    }

                    var usersToAdd = browseForm.SelectedUsers;

                    try
                    {
                        var currentMembers = new List<DirectoryObject>();
                        var membersRequest = await _graphClient.Groups[selectedGroup.Id].Members.GetAsync();
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

                        var duplicates = usersToAdd.Where(u => memberIds.Contains(u.Id)).ToList();
                        validUsers = usersToAdd.Where(u => !memberIds.Contains(u.Id)).ToList();

                        if (duplicates.Any())
                        {
                            using (var duplicateDialog = new DuplicateUsersDialog(duplicates.Select(d => d.UserPrincipalName ?? d.DisplayName ?? d.Id).ToList()))
                            {
                                duplicateDialog.ShowDialog();
                            }
                        }

                        if (!validUsers.Any())
                        {
                            MessageBox.Show("No new users to add (all selected users are already members).");
                            return;
                        }

                        using (var confirmDialog = new ConfirmUsersDialog(validUsers.Select(u => (u.UserPrincipalName ?? u.DisplayName ?? u.Id, u)).ToList()))
                        {
                            var result = confirmDialog.ShowDialog();
                            if (result == DialogResult.OK)
                            {
                                validUsers = confirmDialog.SelectedUsers;
                                if (!validUsers.Any())
                                {
                                    MessageBox.Show("No users selected to add.");
                                    return;
                                }
                                usersConfirmed = true;
                            }
                            else if (confirmDialog.EditListRequested)
                            {
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in btnBrowseAddUser_Click: {ex}");
                        MessageBox.Show($"Error adding users via browse: {ex.Message}");
                        return;
                    }
                }
            }

            var addedCount = 0;
            var failedUsers = new List<string>();
            foreach (var user in validUsers)
            {
                try
                {
                    await _graphClient.Groups[selectedGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                    {
                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{user.Id}"
                    });
                    undoStack.Push(new ActionRecord { ActionType = "AddMember", ParentGroup = selectedGroup, User = user });
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "AddMember",
                        selectedGroup.DisplayName ?? "Unknown",
                        user.DisplayName ?? user.UserPrincipalName ?? "Unknown",
                        "User",
                        user.Id ?? "",
                        $"Added user to group {selectedGroup.DisplayName} via browse").ConfigureAwait(false);
                    addedCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding user {user.DisplayName} (ID: {user.Id}): {ex.Message}");
                    failedUsers.Add(user.DisplayName ?? user.UserPrincipalName ?? user.Id);
                }
            }

            if (failedUsers.Any())
            {
                MessageBox.Show($"Failed to add {failedUsers.Count} user(s): {string.Join(", ", failedUsers)}");
            }

            if (addedCount > 0)
            {
                await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
                MessageBox.Show($"{addedCount} user(s) added successfully via browse.");
            }
        }

        private async void btnRemoveUser_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }
            var selectedGroup = (Group)listBoxGroups.SelectedItem;

            var selectedRows = dataGridViewMembers.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one member to remove.");
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to remove {selectedRows.Count} member(s) from '{selectedGroup.DisplayName}'?", "Confirm Removal", MessageBoxButtons.YesNo);
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
                        await _graphClient.Groups[selectedGroup.Id].Members[member.Id].Ref.DeleteAsync();
                        undoStack.Push(new ActionRecord { ActionType = "RemoveMember", ParentGroup = selectedGroup, User = user });
                        await _auditLogManager.LogAction(
                            _signedInUserId,
                            "RemoveMember",
                            selectedGroup.DisplayName ?? "Unknown",
                            user.DisplayName ?? user.UserPrincipalName ?? "Unknown",
                            "User",
                            user.Id ?? "",
                            $"Removed user from group {selectedGroup.DisplayName}").ConfigureAwait(false);
                    }
                    await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing members: {ex.Message}");
                }
            }
        }

        private async void btnAddNestedGroup_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }
            var selectedGroup = (Group)listBoxGroups.SelectedItem;

            try
            {
                Debug.WriteLine("Starting btnAddNestedGroup_Click");

                string? department = null;
                string? departmentPrefix = null;
                foreach (var dept in _departmentPrefixes)
                {
                    if (selectedGroup.DisplayName != null && dept.Value.Any(prefix => selectedGroup.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        department = dept.Key;
                        departmentPrefix = dept.Value.FirstOrDefault(p => p.StartsWith("CSG-CLBA-"));
                        break;
                    }
                }

                if (string.IsNullOrEmpty(department) || departmentPrefix == null)
                {
                    MessageBox.Show("Could not determine the department of the selected group.");
                    Debug.WriteLine($"Department not found for group: {selectedGroup.DisplayName}");
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
                            await _graphClient.Groups[selectedGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                            {
                                OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{nestedGroup.Id}"
                            });
                            undoStack.Push(new ActionRecord { ActionType = "AddMember", ParentGroup = selectedGroup, NestedGroup = nestedGroup });
                            await _auditLogManager.LogAction(
                                _signedInUserId,
                                "AddMember",
                                selectedGroup.DisplayName ?? "Unknown",
                                nestedGroup.DisplayName ?? "Unknown",
                                "Nested Group",
                                nestedGroup.Id ?? "",
                                $"Added nested group to {selectedGroup.DisplayName}").ConfigureAwait(false);
                            addedCount++;
                            Debug.WriteLine($"Nested group added: {nestedGroup.DisplayName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error adding nested group {nestedGroup.DisplayName}: {ex.Message}");
                            failedGroups.Add(nestedGroup.DisplayName ?? "Unknown");
                        }
                    }

                    await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);

                    if (addedCount > 0)
                    {
                        var message = $"{addedCount} nested group(s) added to '{selectedGroup.DisplayName}'.";
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

        private async void btnRemoveNestedGroup_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }
            var selectedGroup = (Group)listBoxGroups.SelectedItem;

            var selectedRows = dataGridViewMembers.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one nested group to remove.");
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to remove {selectedRows.Count} nested group(s) from '{selectedGroup.DisplayName}'?", "Confirm Removal", MessageBoxButtons.YesNo);
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

                        await _graphClient.Groups[selectedGroup.Id].Members[memberId].Ref.DeleteAsync();

                        undoStack.Push(new ActionRecord
                        {
                            ActionType = "RemoveMember",
                            ParentGroup = selectedGroup,
                            NestedGroup = group
                        });
                        await _auditLogManager.LogAction(
                            _signedInUserId,
                            "RemoveMember",
                            selectedGroup.DisplayName ?? "Unknown",
                            group.DisplayName ?? "Unknown",
                            "Nested Group",
                            group.Id ?? "",
                            $"Removed nested group from {selectedGroup.DisplayName}").ConfigureAwait(false);
                    }
                    await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing nested groups: {ex.Message}");
                }
            }
        }

        private async void btnCopyUsers_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }
            var selectedGroup = (Group)listBoxGroups.SelectedItem;

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
                if (selectedGroup.DisplayName != null && dept.Value.Any(prefix => selectedGroup.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    department = dept.Key;
                    break;
                }
            }

            if (string.IsNullOrEmpty(department))
            {
                MessageBox.Show("Could not determine the department of the selected group.");
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
                                    undoStack.Push(new ActionRecord
                                    {
                                        ActionType = "CopyGroups",
                                        ParentGroup = selectedGroup,
                                        User = new User { Id = user.Id, DisplayName = user.DisplayName },
                                        TargetGroup = targetGroup
                                    });
                                    await _auditLogManager.LogAction(
                                        _signedInUserId,
                                        "CopyGroups",
                                        targetGroup.DisplayName ?? "Unknown",
                                        user.DisplayName ?? "Unknown",
                                        "User",
                                        user.Id ?? "",
                                        $"Copied user from {selectedGroup.DisplayName} to {targetGroup.DisplayName}").ConfigureAwait(false);
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

        private async void btnExportToCsv_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }

            var selectedGroup = (Group)listBoxGroups.SelectedItem;
            try
            {
                var allMembers = new List<DirectoryObject>();
                var membersRequest = await _graphClient.Groups[selectedGroup.Id].Members.GetAsync();

                if (membersRequest?.Value == null)
                {
                    MessageBox.Show($"Group '{selectedGroup.DisplayName}' has no members.");
                    return;
                }

                var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                    .CreatePageIterator(_graphClient, membersRequest, (member) =>
                    {
                        allMembers.Add(member);
                        return true;
                    });

                await pageIterator.IterateAsync();

                if (!allMembers.Any())
                {
                    MessageBox.Show($"Group '{selectedGroup.DisplayName}' has no members.");
                    return;
                }

                var displayMembers = allMembers.Select(m => new
                {
                    GroupName = selectedGroup.DisplayName,
                    GroupId = selectedGroup.Id,
                    MemberDisplayName = m is User user1 ? user1.DisplayName : (m is Group group ? group.DisplayName : "Unknown"),
                    MemberId = m.Id,
                    MemberType = m.OdataType switch
                    {
                        "#microsoft.graph.user" => "User",
                        "#microsoft.graph.group" => "Group",
                        "#microsoft.graph.servicePrincipal" => "Service Principal",
                        _ => "Other"
                    },
                    MemberUPN = m is User user2 ? user2.UserPrincipalName ?? string.Empty : string.Empty
                }).ToList();

                var csv = new StringBuilder();
                csv.AppendLine("GroupName,GroupId,MemberDisplayName,MemberId,MemberType,MemberUPN");
                foreach (var member in displayMembers)
                {
                    var groupName = $"\"{member.GroupName?.Replace("\"", "\"\"") ?? ""}\"";
                    var memberDisplayName = $"\"{member.MemberDisplayName?.Replace("\"", "\"\"") ?? ""}\"";
                    var memberUPN = $"\"{member.MemberUPN.Replace("\"", "\"\"")}\"";
                    csv.AppendLine($"{groupName},{member.GroupId},{memberDisplayName},{member.MemberId},{member.MemberType},{memberUPN}");
                }

                using (var saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveFileDialog.Title = "Save Group Members CSV";
                    saveFileDialog.FileName = $"{selectedGroup.DisplayName}_Members.csv";
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(saveFileDialog.FileName, csv.ToString());
                        await _auditLogManager.LogAction(
                            _signedInUserId,
                            "Export_Audit",
                            selectedGroup.DisplayName ?? "Unknown",
                            saveFileDialog.FileName,
                            "File",
                            "",  // No specific targetId for export
                            $"Exported group members to CSV file {saveFileDialog.FileName}").ConfigureAwait(false);
                        MessageBox.Show($"Successfully exported to {saveFileDialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to CSV: {ex.Message}");
            }
        }

        private async void btnReplaceUser_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group.");
                return;
            }
            var selectedGroup = (Group)listBoxGroups.SelectedItem;

            var selectedRows = dataGridViewMembers.SelectedRows;
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one member to replace.");
                return;
            }

            var membersToRemove = selectedRows.Cast<DataGridViewRow>().Select(row => new
            {
                Id = ((dynamic)row.DataBoundItem).Id as string,
                Type = ((dynamic)row.DataBoundItem).Type as string,
                DisplayName = ((dynamic)row.DataBoundItem).DisplayName as string ?? "Unknown"
            }).ToList();

            using (var typeDialog = new ReplaceTypeSelectionDialog())
            {
                if (typeDialog.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    if (typeDialog.SelectedType == "NestedGroups")
                    {
                        string? department = null;
                        string? departmentPrefix = null;
                        foreach (var dept in _departmentPrefixes)
                        {
                            if (selectedGroup.DisplayName != null && dept.Value.Any(prefix => selectedGroup.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                            {
                                department = dept.Key;
                                departmentPrefix = dept.Value.FirstOrDefault(p => p.StartsWith("CSG-CLBA-"));
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(department) || departmentPrefix == null)
                        {
                            MessageBox.Show("Could not determine the department of the selected group.");
                            Debug.WriteLine($"Department not found for group: {selectedGroup.DisplayName}");
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

                        using (var selectionForm = new GroupSelectionForm(groups))
                        {
                            selectionForm.Owner = this;
                            if (selectionForm.ShowDialog() != DialogResult.OK)
                                return;

                            var skippedMembers = new List<string>();
                            var removedCount = 0;
                            foreach (var member in membersToRemove)
                            {
                                try
                                {
                                    if (member.Type == "User")
                                    {
                                        var user = await _graphClient.Users[member.Id].GetAsync();
                                        if (user == null)
                                        {
                                            skippedMembers.Add(member.DisplayName);
                                            continue;
                                        }
                                        await _graphClient.Groups[selectedGroup.Id].Members[member.Id].Ref.DeleteAsync();
                                        undoStack.Push(new ActionRecord { ActionType = "RemoveMember", ParentGroup = selectedGroup, User = user });
                                        await _auditLogManager.LogAction(
                                            _signedInUserId,
                                            "RemoveMember",
                                            selectedGroup.DisplayName ?? "Unknown",
                                            user.DisplayName ?? user.UserPrincipalName ?? "Unknown",
                                            "User",
                                            user.Id ?? "",
                                            $"Removed user during replacement in {selectedGroup.DisplayName}").ConfigureAwait(false);
                                        removedCount++;
                                    }
                                    else if (member.Type == "Group")
                                    {
                                        var group = await _graphClient.Groups[member.Id].GetAsync(config =>
                                        {
                                            config.QueryParameters.Select = new[] { "id", "displayName" };
                                        });
                                        if (group == null)
                                        {
                                            skippedMembers.Add(member.DisplayName);
                                            continue;
                                        }
                                        await _graphClient.Groups[selectedGroup.Id].Members[member.Id].Ref.DeleteAsync();
                                        undoStack.Push(new ActionRecord { ActionType = "RemoveMember", ParentGroup = selectedGroup, NestedGroup = group });
                                        await _auditLogManager.LogAction(
                                            _signedInUserId,
                                            "RemoveMember",
                                            selectedGroup.DisplayName ?? "Unknown",
                                            group.DisplayName ?? "Unknown",
                                            "Nested Group",
                                            group.Id ?? "",
                                            $"Removed nested group during replacement in {selectedGroup.DisplayName}").ConfigureAwait(false);
                                        removedCount++;
                                    }
                                    else
                                    {
                                        skippedMembers.Add(member.DisplayName);
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error removing member {member.DisplayName} (ID: {member.Id}): {ex.Message}");
                                    skippedMembers.Add(member.DisplayName);
                                    continue;
                                }
                            }

                            if (skippedMembers.Any())
                            {
                                MessageBox.Show($"Skipped {skippedMembers.Count} member(s) due to errors or non-existence: {string.Join(", ", skippedMembers)}");
                            }

                            if (removedCount == 0)
                            {
                                MessageBox.Show("No members were removed. Aborting replacement.");
                                return;
                            }

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
                                    await _graphClient.Groups[selectedGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                                    {
                                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{nestedGroup.Id}"
                                    });
                                    undoStack.Push(new ActionRecord { ActionType = "AddMember", ParentGroup = selectedGroup, NestedGroup = nestedGroup });
                                    await _auditLogManager.LogAction(
                                        _signedInUserId,
                                        "AddMember",
                                        selectedGroup.DisplayName ?? "Unknown",
                                        nestedGroup.DisplayName ?? "Unknown",
                                        "Nested Group",
                                        nestedGroup.Id ?? "",
                                        $"Added nested group during replacement in {selectedGroup.DisplayName}").ConfigureAwait(false);
                                    addedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error adding nested group {nestedGroup.DisplayName}: {ex.Message}");
                                    failedGroups.Add(nestedGroup.DisplayName ?? "Unknown");
                                }
                            }

                            await _auditLogManager.LogAction(
                                _signedInUserId,
                                "ReplaceMember",
                                selectedGroup.DisplayName ?? "Unknown",
                                null,
                                "Group",
                                "",  // No specific targetId for aggregate replace
                                $"Replaced {removedCount} member(s) with {addedCount} nested group(s) in {selectedGroup.DisplayName}").ConfigureAwait(false);

                            await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
                            var message = $"Replaced {removedCount} member(s) with {addedCount} nested group(s) in '{selectedGroup.DisplayName}'.";
                            if (failedGroups.Any())
                            {
                                message += $"\nFailed to add {failedGroups.Count} group(s): {string.Join(", ", failedGroups)}.";
                            }
                            MessageBox.Show(message);
                        }
                    }
                    else
                    {
                        bool usersConfirmed = false;
                        List<User> validUsers = null;

                        while (!usersConfirmed)
                        {
                            using (var browseForm = new BrowseUsersForm(_graphClient))
                            {
                                if (browseForm.ShowDialog() != DialogResult.OK || !browseForm.SelectedUsers.Any())
                                {
                                    return;
                                }

                                var usersToAdd = browseForm.SelectedUsers;

                                var currentMembers = new List<DirectoryObject>();
                                var membersRequest = await _graphClient.Groups[selectedGroup.Id].Members.GetAsync();
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

                                var duplicates = usersToAdd.Where(u => memberIds.Contains(u.Id)).ToList();
                                validUsers = usersToAdd.Where(u => !memberIds.Contains(u.Id)).ToList();

                                if (duplicates.Any())
                                {
                                    using (var duplicateDialog = new DuplicateUsersDialog(duplicates.Select(d => d.UserPrincipalName ?? d.DisplayName ?? d.Id).ToList()))
                                    {
                                        duplicateDialog.ShowDialog();
                                    }
                                }

                                if (!validUsers.Any())
                                {
                                    MessageBox.Show("No new users to add.");
                                    return;
                                }

                                using (var confirmDialog = new ConfirmUsersDialog(validUsers.Select(u => (u.UserPrincipalName ?? u.DisplayName ?? u.Id, u)).ToList()))
                                {
                                    var result = confirmDialog.ShowDialog();
                                    if (result == DialogResult.OK)
                                    {
                                        validUsers = confirmDialog.SelectedUsers;
                                        if (!validUsers.Any())
                                        {
                                            MessageBox.Show("No users selected to add.");
                                            return;
                                        }
                                        usersConfirmed = true;
                                    }
                                    else if (confirmDialog.EditListRequested)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                            }
                        }

                        var skippedMembers = new List<string>();
                        var removedCount = 0;
                        foreach (var member in membersToRemove)
                        {
                            try
                            {
                                if (member.Type == "User")
                                {
                                    var user = await _graphClient.Users[member.Id].GetAsync();
                                    if (user == null)
                                    {
                                        skippedMembers.Add(member.DisplayName);
                                        continue;
                                    }
                                    await _graphClient.Groups[selectedGroup.Id].Members[member.Id].Ref.DeleteAsync();
                                    undoStack.Push(new ActionRecord { ActionType = "RemoveMember", ParentGroup = selectedGroup, User = user });
                                    await _auditLogManager.LogAction(
                                        _signedInUserId,
                                        "RemoveMember",
                                        selectedGroup.DisplayName ?? "Unknown",
                                        user.DisplayName ?? user.UserPrincipalName ?? "Unknown",
                                        "User",
                                        user.Id ?? "",
                                        $"Removed user during replacement in {selectedGroup.DisplayName}").ConfigureAwait(false);
                                    removedCount++;
                                }
                                else if (member.Type == "Group")
                                {
                                    var group = await _graphClient.Groups[member.Id].GetAsync(config =>
                                    {
                                        config.QueryParameters.Select = new[] { "id", "displayName" };
                                    });
                                    if (group == null)
                                    {
                                        skippedMembers.Add(member.DisplayName);
                                        continue;
                                    }
                                    await _graphClient.Groups[selectedGroup.Id].Members[member.Id].Ref.DeleteAsync();
                                    undoStack.Push(new ActionRecord { ActionType = "RemoveMember", ParentGroup = selectedGroup, NestedGroup = group });
                                    await _auditLogManager.LogAction(
                                        _signedInUserId,
                                        "RemoveMember",
                                        selectedGroup.DisplayName ?? "Unknown",
                                        group.DisplayName ?? "Unknown",
                                        "Nested Group",
                                        group.Id ?? "",
                                        $"Removed nested group during replacement in {selectedGroup.DisplayName}").ConfigureAwait(false);
                                    removedCount++;
                                }
                                else
                                {
                                    skippedMembers.Add(member.DisplayName);
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error removing member {member.DisplayName} (ID: {member.Id}): {ex.Message}");
                                skippedMembers.Add(member.DisplayName);
                                continue;
                            }
                        }

                        if (skippedMembers.Any())
                        {
                            MessageBox.Show($"Skipped {skippedMembers.Count} member(s) due to errors or non-existence: {string.Join(", ", skippedMembers)}");
                        }

                        if (removedCount == 0)
                        {
                            MessageBox.Show("No members were removed. Aborting replacement.");
                            return;
                        }

                        var addedCount = 0;
                        foreach (var userToAdd in validUsers)
                        {
                            try
                            {
                                await _graphClient.Groups[selectedGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                                {
                                    OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{userToAdd.Id}"
                                });
                                undoStack.Push(new ActionRecord { ActionType = "AddMember", ParentGroup = selectedGroup, User = userToAdd });
                                await _auditLogManager.LogAction(
                                    _signedInUserId,
                                    "AddMember",
                                    selectedGroup.DisplayName ?? "Unknown",
                                    userToAdd.DisplayName ?? userToAdd.UserPrincipalName ?? "Unknown",
                                    "User",
                                    userToAdd.Id ?? "",
                                    $"Added user during replacement in {selectedGroup.DisplayName}").ConfigureAwait(false);
                                addedCount++;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error adding user {userToAdd.DisplayName} (ID: {userToAdd.Id}): {ex.Message}");
                                MessageBox.Show($"Failed to add user {userToAdd.DisplayName}: {ex.Message}");
                            }
                        }

                        await _auditLogManager.LogAction(
                            _signedInUserId,
                            "ReplaceMember",
                            selectedGroup.DisplayName ?? "Unknown",
                            null,
                            "Group",
                            "",  // No specific targetId for aggregate replace
                            $"Replaced {removedCount} member(s) with {addedCount} user(s) in {selectedGroup.DisplayName}").ConfigureAwait(false);

                        await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
                        MessageBox.Show($"Replaced {removedCount} member(s) with {addedCount} user(s) in '{selectedGroup.DisplayName}'.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in btnReplaceUser_Click: {ex}");
                    MessageBox.Show($"Error replacing members: {ex.Message}");
                }
            }
        }

        private async void btnUndo_Click(object sender, EventArgs e)
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("No actions to undo.");
                return;
            }

            var lastAction = undoStack.Pop();
            try
            {
                if (lastAction.ActionType == "AddMember" && lastAction.User != null)
                {
                    await _graphClient.Groups[lastAction.ParentGroup.Id].Members[lastAction.User.Id].Ref.DeleteAsync();
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "RemoveMember",
                        lastAction.ParentGroup.DisplayName ?? "Unknown",
                        lastAction.User.DisplayName ?? lastAction.User.UserPrincipalName ?? "Unknown",
                        "User",
                        lastAction.User.Id ?? "",
                        $"Undid adding user to {lastAction.ParentGroup.DisplayName}").ConfigureAwait(false);
                }
                else if (lastAction.ActionType == "RemoveMember" && lastAction.User != null)
                {
                    await _graphClient.Groups[lastAction.ParentGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                    {
                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{lastAction.User.Id}"
                    });
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "AddMember",
                        lastAction.ParentGroup.DisplayName ?? "Unknown",
                        lastAction.User.DisplayName ?? lastAction.User.UserPrincipalName ?? "Unknown",
                        "User",
                        lastAction.User.Id ?? "",
                        $"Undid removing user from {lastAction.ParentGroup.DisplayName}").ConfigureAwait(false);
                }
                else if (lastAction.ActionType == "AddMember" && lastAction.NestedGroup != null)
                {
                    await _graphClient.Groups[lastAction.ParentGroup.Id].Members[lastAction.NestedGroup.Id].Ref.DeleteAsync();
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "RemoveMember",
                        lastAction.ParentGroup.DisplayName ?? "Unknown",
                        lastAction.NestedGroup.DisplayName ?? "Unknown",
                        "Nested Group",
                        lastAction.NestedGroup.Id ?? "",
                        $"Undid adding nested group to {lastAction.ParentGroup.DisplayName}").ConfigureAwait(false);
                }
                else if (lastAction.ActionType == "RemoveMember" && lastAction.NestedGroup != null)
                {
                    await _graphClient.Groups[lastAction.ParentGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                    {
                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{lastAction.NestedGroup.Id}"
                    });
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "AddMember",
                        lastAction.ParentGroup.DisplayName ?? "Unknown",
                        lastAction.NestedGroup.DisplayName ?? "Unknown",
                        "Nested Group",
                        lastAction.NestedGroup.Id ?? "",
                        $"Undid removing nested group from {lastAction.ParentGroup.DisplayName}").ConfigureAwait(false);
                }
                else if (lastAction.ActionType == "CopyGroups")
                {
                    await _graphClient.Groups[lastAction.TargetGroup!.Id].Members[lastAction.User!.Id].Ref.DeleteAsync();
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "RemoveMember",
                        lastAction.TargetGroup.DisplayName ?? "Unknown",
                        lastAction.User.DisplayName ?? lastAction.User.UserPrincipalName ?? "Unknown",
                        "User",
                        lastAction.User.Id ?? "",
                        $"Undid copying user to {lastAction.TargetGroup.DisplayName}").ConfigureAwait(false);
                }

                if (listBoxGroups.SelectedItem == lastAction.ParentGroup)
                {
                    await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error undoing action: {ex.Message}");
            }
        }

        private async Task listBoxGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                dataGridViewMembers.DataSource = null;
                MessageBox.Show("No group selected.");
                return;
            }

            var selectedGroup = (Group)listBoxGroups.SelectedItem;
            try
            {
                var allMembers = new List<DirectoryObject>();
                var membersRequest = await _graphClient.Groups[selectedGroup.Id].Members.GetAsync();

                if (membersRequest?.Value == null)
                {
                    Debug.WriteLine($"No members returned for group '{selectedGroup.DisplayName}' (ID: {selectedGroup.Id})");
                    dataGridViewMembers.DataSource = null;
                    MessageBox.Show($"Group '{selectedGroup.DisplayName}' has no members.");
                    return;
                }

                Debug.WriteLine($"Initial members count: {membersRequest.Value.Count}");
                var pageIterator = PageIterator<DirectoryObject, DirectoryObjectCollectionResponse>
                    .CreatePageIterator(_graphClient, membersRequest, (member) =>
                    {
                        allMembers.Add(member);
                        return true;
                    });

                await pageIterator.IterateAsync();
                Debug.WriteLine($"Total members fetched: {allMembers.Count}");

                if (!allMembers.Any())
                {
                    Debug.WriteLine($"No members found after iteration for group '{selectedGroup.DisplayName}'");
                    dataGridViewMembers.DataSource = null;
                    MessageBox.Show($"Group '{selectedGroup.DisplayName}' has no members.");
                    return;
                }

                var displayMembers = allMembers.Select(m => new
                {
                    DisplayName = m is User user1 ? user1.DisplayName : (m is Group group ? group.DisplayName : "Unknown"),
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

                Debug.WriteLine($"Display members count: {displayMembers.Count}");
                dataGridViewMembers.DataSource = displayMembers;

                dataGridViewMembers.AutoResizeColumn(dataGridViewMembers.Columns["DisplayName"].Index, DataGridViewAutoSizeColumnMode.AllCells);
                dataGridViewMembers.AutoResizeColumn(dataGridViewMembers.Columns["UPN"].Index, DataGridViewAutoSizeColumnMode.AllCells);

                dataGridViewMembers.Columns["DisplayName"].MinimumWidth = 200;
                dataGridViewMembers.Columns["UPN"].MinimumWidth = 250;

                dataGridViewMembers.Refresh();
                MessageBox.Show($"Loaded {displayMembers.Count} member(s) for '{selectedGroup.DisplayName}'.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in listBoxGroups_SelectedIndexChanged for group '{selectedGroup.DisplayName}': {ex}");
                dataGridViewMembers.DataSource = null;
                MessageBox.Show($"Error loading members: {ex.Message}");
            }
        }

        private void btnReturn_Click(object sender, EventArgs e)
        {
            _mainForm.Show();
            this.Close();
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            dataGridViewMembers.SelectAll();
        }

        private void btnReturnToPreviousWindow_Click(object sender, EventArgs e)
        {
            if (_groupSearchForm != null)
            {
                _groupSearchForm.Show();
            }
            else
            {
                _mainForm.Show();
            }
            this.Close();
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

                    expandGroupToolStripMenuItem.Visible = member.Type == "Group";
                    copyIdToolStripMenuItem.Visible = columnName == "Id";

                    if (expandGroupToolStripMenuItem.Visible || copyIdToolStripMenuItem.Visible)
                    {
                        contextMenuStripMembers.Show(dataGridViewMembers, e.Location);
                    }
                }
            }
        }

        private async void expandGroupToolStripMenuItem_Click(object sender, EventArgs e)
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

            string groupId = member.Id;
            var parentGroup = (Group?)listBoxGroups.SelectedItem;
            if (parentGroup == null)
            {
                MessageBox.Show("No parent group selected.");
                return;
            }

            try
            {
                var nestedMembers = new List<DirectoryObject>();
                var membersRequest = await _graphClient.Groups[groupId].Members.GetAsync();

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

                var nestedGroupForm = new NestedGroupForm(nestedMembers, groupId, parentGroup, _graphClient, _auditLogManager, _signedInUserId);
                nestedGroupForm.Owner = this;
                nestedGroupForm.ShowDialog();
                await listBoxGroups_SelectedIndexChanged(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error managing nested group: {ex.Message}");
            }
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

        private void btnExportToCsv_Click_1(object sender, EventArgs e)
        {
        }
    }
}