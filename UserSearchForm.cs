using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Identity.Client;
using System.Security.Claims;
using System.Diagnostics;

namespace EntraGroupsApp
{
    public partial class UserSearchForm : Form
    {
        private readonly GraphServiceClient _graphClient;
        private readonly Form _parentForm;
        private readonly List<User> _allResults;
        private readonly Dictionary<string, List<Group>> _userGroups;
        private readonly AuditLogManager _auditLogManager;
        private readonly ClaimsPrincipal _claimsPrincipal;
        private readonly string _signedInUserId;
        private readonly IPublicClientApplication _pca;
        private bool _isSigningOut;
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
        private int _firstNameSortState = 0; // 0: default (displayName asc), 1: FirstName asc, 2: FirstName desc
        private int _groupNameSortState = 0; // 0: default (alphabetical by DisplayName), 1: FSG then CSG
        private bool _isFormInitialized;
        private readonly object _syncLock = new object();
        private CancellationTokenSource _loadUserGroupsCts = new CancellationTokenSource();
        private DateTime _lastSearchTime = DateTime.MinValue;
        private DateTime _lastAddGroupClick = DateTime.MinValue;
        private DateTime _lastRemoveMemberClick = DateTime.MinValue;
        private DateTime _lastAddOwnerClick = DateTime.MinValue;
        private DateTime _lastRemoveOwnerClick = DateTime.MinValue;
        private DateTime _lastReplaceGroupClick = DateTime.MinValue;
        private DateTime _lastCopyGroupsClick = DateTime.MinValue;
        private DateTime _lastRefreshClick = DateTime.MinValue;

        public UserSearchForm(GraphServiceClient graphClient, Form parentForm, AuditLogManager auditLogManager, string signedInUserId, ClaimsPrincipal claimsPrincipal, IPublicClientApplication pca = null)
        {
            InitializeComponent();
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _auditLogManager = auditLogManager ?? throw new ArgumentNullException(nameof(auditLogManager));
            _signedInUserId = signedInUserId ?? throw new ArgumentNullException(nameof(signedInUserId));
            _claimsPrincipal = claimsPrincipal; // Allow null for compatibility, handle in dialog
            _pca = pca;
            _allResults = new List<User>();
            _userGroups = new Dictionary<string, List<Group>>();
            _isFormInitialized = false;

            Debug.WriteLine($"UserSearchForm: _claimsPrincipal is {(_claimsPrincipal != null ? "not null" : "null")}");
            if (_claimsPrincipal != null && _claimsPrincipal.Claims != null)
            {
                Debug.WriteLine($"UserSearchForm: Claims found: {string.Join(", ", _claimsPrincipal.Claims.Select(c => $"{c.Type}:{c.Value}"))}");
            }
            else
            {
                Debug.WriteLine("UserSearchForm: No claims available");
            }

            // Initialize lvGroups columns
            lvGroups.View = View.Details;
            lvGroups.FullRowSelect = true;
            lvGroups.Columns.Clear();
            lvGroups.Columns.Add("Group Name", 600, HorizontalAlignment.Left);
            lvGroups.Columns.Add("Is Owner", 100, HorizontalAlignment.Center);

            // Clear any initial selection
            dataGridViewUsers.ClearSelection();

            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    btnSearch_Click(null, null);
                    e.SuppressKeyPress = true;
                }
            };

            lvGroups.ColumnClick += lvGroups_ColumnClick;
            dataGridViewUsers.SelectionChanged += dataGridViewUsers_SelectionChanged;
            Load += (s, e) =>
            {
                _isFormInitialized = true;
                var workingArea = Screen.PrimaryScreen.WorkingArea;
                ClientSize = new Size(Math.Min(workingArea.Width - 40, 1200), Math.Min(workingArea.Height - 40, 600));
                Location = new Point((workingArea.Width - ClientSize.Width) / 2, (workingArea.Height - ClientSize.Height) / 2);
            };
        }
        public void PromptClipboardOnSignOut(DateTime sessionStartTime)
        {
            _isSigningOut = true;
            if (!string.IsNullOrEmpty(_signedInUserId))
            {
                var sessionLogs = _auditLogManager.GetLogsByUserAndDate(_signedInUserId, null)
                    .Where(l => l.Timestamp >= sessionStartTime.AddSeconds(-1) &&
                                new[] { "AddMember", "RemoveMember", "AddOwner", "RemoveOwner", "ReplaceGroup", "CopyGroups" }
                                .Contains(l.ActionType))
                    .ToList();

                Console.WriteLine($"SignOut: Found {sessionLogs.Count} modification logs for user {_signedInUserId}");
                foreach (var log in sessionLogs)
                {
                    Console.WriteLine($"Log: ActionType={log.ActionType}, GroupName={log.GroupName}, TargetName={log.TargetName}, Timestamp={log.Timestamp}, Details={log.Details}");
                }

                if (sessionLogs.Any())
                {
                    var result = MessageBox.Show(
                        "Would you like to copy your current session's audit logs for user or group modifications to the clipboard before signing out?",
                        "Export Session Modification Logs",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            var output = sessionLogs.Select(l =>
                                $"Modification applied to {l.GroupName} for \"{l.TargetName}\" ({l.TargetType}) on {l.Timestamp:yyyy-MM-dd HH:mm:ss}. Details: {l.Details}")
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
                        return;
                    }
                }
            }
        }
        public void SetSigningOut(bool isSigningOut)
        {
            _isSigningOut = isSigningOut;
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastSearchTime).TotalMilliseconds < 500)
                return;
            _lastSearchTime = DateTime.Now;

            string input = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Please enter at least one search term.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsDisposed || !IsHandleCreated)
            {
                Console.WriteLine("Search canceled: Form is disposed or handle not created.");
                return;
            }

            await SearchUsers(input).ConfigureAwait(false);
        }
        private async Task SearchUsers(string input)
        {
            try
            {
                input = input.Trim();
                if (string.IsNullOrEmpty(input))
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            MessageBox.Show("Please enter at least one search term.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            Cursor = Cursors.Default;
                            lblStatus.Visible = false;
                        }));
                    }
                    return;
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.WaitCursor;
                        lblStatus.Text = "Searching...";
                        lblStatus.Visible = true;
                        dataGridViewUsers.DataSource = null;
                        lvGroups.Items.Clear();
                    }));
                }

                lock (_syncLock)
                {
                    _userGroups.Clear();
                    _allResults.Clear();
                }

                List<User> newResults = new List<User>();
                string filter = string.Empty;

                input = input.Replace("'", "''"); // Escape single quotes

                // Smart filter logic
                if (input.Contains("@"))
                {
                    filter = $"mail eq '{input}' or userPrincipalName eq '{input}'";
                }
                else if (input.Contains(","))
                {
                    var parts = input.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length >= 2)
                        filter = $"startswith(surname, '{parts[0]}') and startswith(givenName, '{parts[1]}')";
                }
                else if (input.Contains(" "))
                {
                    var parts = input.Split(' ').Select(p => p.Trim()).ToArray();
                    if (parts.Length == 2)
                    {
                        filter = $"(startswith(givenName, '{parts[0]}') and startswith(surname, '{parts[1]}')) or " +
                                 $"(startswith(surname, '{parts[0]}') and startswith(givenName, '{parts[1]}'))";
                    }
                    else if (parts.Length > 2)
                    {
                        filter = $"startswith(givenName, '{parts[0]}') and startswith(surname, '{parts[^1]}')";
                    }
                }
                else
                {
                    filter = $"startswith(surname, '{input}') or startswith(displayName, '{input}') or " +
                             $"startswith(givenName, '{input}') or startswith(userPrincipalName, '{input}') or " +
                             $"startswith(mail, '{input}') or startswith(mailNickname, '{input}')";
                }

                Console.WriteLine($"Starting user search with filter: {filter}");

                // Graph query
                var requestConfig = new Action<Microsoft.Kiota.Abstractions.RequestConfiguration<Microsoft.Graph.Users.UsersRequestBuilder.UsersRequestBuilderGetQueryParameters>>(config =>
                {
                    config.QueryParameters.Select = new[] { "id", "displayName", "givenName", "surname", "userPrincipalName", "mail", "jobTitle", "department" };
                    config.QueryParameters.Top = 50;
                    config.QueryParameters.Count = true;
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Orderby = new[] { "displayName" };
                    config.Headers.Add("ConsistencyLevel", "eventual");
                });

                int maxRetries = 3;
                int retryDelayMs = 1500;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                        var response = await _graphClient.Users.GetAsync(requestConfig, cts.Token).ConfigureAwait(false);

                        if (response?.Value != null)
                        {
                            var iterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(_graphClient, response, user =>
                            {
                                lock (_syncLock)
                                {
                                    newResults.Add(user);
                                }
                                return true;
                            });

                            await iterator.IterateAsync(cts.Token).ConfigureAwait(false);
                            break;
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        Console.WriteLine($"Search attempt {attempt} timed out: {ex.Message}");
                        if (attempt == maxRetries && !IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                MessageBox.Show("Search timed out after multiple attempts. Please try again.", "Timeout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }));
                        }
                        await Task.Delay(retryDelayMs).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Graph error attempt {attempt}: {ex.Message}");
                        if (attempt == maxRetries && !IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                MessageBox.Show($"Error searching users:\n{ex.Message}", "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }));
                        }
                        await Task.Delay(retryDelayMs).ConfigureAwait(false);
                    }
                }

                if (newResults.Count == 0)
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            MessageBox.Show($"No users found for '{input}'.", "Search Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            lblName.Text = "";
                            lblEmail.Text = "";
                            lblJobTitle.Text = "";
                            lblDepartment.Text = "";
                        }));
                    }
                    return;
                }

                lock (_syncLock)
                {
                    _allResults.AddRange(newResults);
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        dataGridViewUsers.DataSource = _allResults.Select(user => new UserDisplayItem
                        {
                            FirstName = user.GivenName,
                            LastName = user.Surname,
                            DisplayName = user.DisplayName,
                            Email = user.Mail ?? user.UserPrincipalName,
                            JobTitle = user.JobTitle,
                            Department = user.Department,
                            UserId = user.Id
                        }).ToList();

                        if (dataGridViewUsers.Columns["UserId"] != null)
                            dataGridViewUsers.Columns["UserId"].Visible = false;

                        if (dataGridViewUsers.Rows.Count > 0)
                        {
                            dataGridViewUsers.CurrentCell = dataGridViewUsers.Rows[0].Cells[0];
                            dataGridViewUsers.Rows[0].Selected = true;
                            dataGridViewUsers.Focus();
                        }
                    }));
                }

                await _auditLogManager.LogAction(
                    _signedInUserId,
                    "System",
                    "SearchUsers",
                    null,
                    txtSearch.Text.Trim(),
                    "SearchQuery",
                    $"Searched for users with query '{txtSearch.Text.Trim()}', found {_allResults.Count} results").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in SearchUsers: {ex.Message}");
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show($"Unexpected error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.Default;
                        lblStatus.Text = "Ready";
                        lblStatus.Visible = false;
                    }));
                }
            }
        }
        private void DisplayUserDetails(User user)
        {
            BeginInvoke((Action)(() =>
            {
                lblName.Text = $"Name: {user.DisplayName ?? "N/A"}";
                lblEmail.Text = $"Email: {user.Mail ?? "N/A"}";
                lblJobTitle.Text = $"Job Title: {user.JobTitle ?? "N/A"}";
                lblDepartment.Text = $"Department: {user.Department ?? "N/A"}";
            }));
        }

        private async Task LoadUserGroups(List<User> users = null, CancellationToken cancellationToken = default)
        {
            users = users ?? _allResults;
            var usersCopy = users.ToList(); // Avoid enumeration issues

            foreach (var user in usersCopy)
            {
                if (user?.Id == null)
                {
                    Console.WriteLine($"Invalid user ID for group loading: {user?.DisplayName ?? "Unknown"}");
                    continue;
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Console.WriteLine($"Fetching groups for user {user.DisplayName ?? "Unknown"} (ID: {user.Id})");

                    int maxRetries = 3;
                    int retryDelayMs = 1500;
                    List<Group> groupList = null;

                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(
                                cancellationToken,
                                new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token))
                            {
                                Console.WriteLine($"Attempt {attempt}/{maxRetries} for loading groups for user {user.Id}");

                                // Simplified group loading logic with CSG-CLBA and FSG-CLBA filter
                                var memberOf = await _graphClient.Users[user.Id].MemberOf.GetAsync(request =>
                                {
                                    request.QueryParameters.Select = new[] { "id", "displayName" };
                                    request.Headers.Add("ConsistencyLevel", "eventual");
                                }, cts.Token).ConfigureAwait(false);

                                groupList = memberOf?.Value?.OfType<Group>()
                                    .Where(g => g.Id != null && g.DisplayName != null &&
                                        (g.DisplayName.Contains("CSG-CLBA", StringComparison.OrdinalIgnoreCase) ||
                                         g.DisplayName.Contains("FSG-CLBA", StringComparison.OrdinalIgnoreCase)))
                                    .ToList() ?? new List<Group>();

                                break; // Success
                            }
                        }
                        catch (OperationCanceledException ex)
                        {
                            Console.WriteLine($"[Suppressed] Group loading canceled for {user.DisplayName ?? "Unknown"} (ID: {user.Id}), attempt {attempt}/{maxRetries}: {ex.Message}");
                            if (attempt == maxRetries)
                                continue;
                            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading groups for user {user.Id}, attempt {attempt}/{maxRetries}: {ex.Message}");
                            if (attempt == maxRetries)
                            {
                                if (!IsDisposed && IsHandleCreated)
                                {
                                    BeginInvoke((Action)(() =>
                                    {
                                        MessageBox.Show(
                                            $"Failed to load groups for {user.DisplayName ?? "Unknown"}: {ex.Message}",
                                            "Group Load Error",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                    }));
                                }
                                continue;
                            }
                            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    lock (_syncLock)
                    {
                        _userGroups[user.Id] = groupList;
                    }

                    Console.WriteLine($"Loaded {groupList.Count} CSG-CLBA/FSG-CLBA groups for user {user.DisplayName ?? "Unknown"} (ID: {user.Id}): {string.Join(", ", groupList.Select(g => g.DisplayName ?? "Null"))}");
                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine($"[Suppressed] Group loading canceled for user {user.DisplayName ?? "Unknown"} (ID: {user.Id}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading groups for user {user.DisplayName ?? "Unknown"} (ID: {user.Id}): {ex.Message}");
                    lock (_syncLock)
                    {
                        _userGroups[user.Id] = new List<Group>();
                    }
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            MessageBox.Show(
                                $"Failed to load groups for {user.DisplayName ?? "Unknown"}: {ex.Message}",
                                "Group Load Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }));
                    }
                }
            }
        }
        private async void dataGridViewUsers_SelectionChanged(object sender, EventArgs e)
        {
            if (!_isFormInitialized)
            {
                Console.WriteLine("Skipping dataGridViewUsers_SelectionChanged: Form not yet initialized.");
                return;
            }

            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke((Action)(() =>
                {
                    lvGroups.Items.Clear();
                    if (dataGridViewUsers.SelectedRows.Count == 0)
                    {
                        lblName.Text = "";
                        lblEmail.Text = "";
                        lblJobTitle.Text = "";
                        lblDepartment.Text = "";
                        lvGroups.Items.Add(new ListViewItem(new[] { "No user selected", "N/A" }));
                        Console.WriteLine("No user selected in dataGridViewUsers.");
                        return;
                    }

                    var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
                    if (selectedRow == null)
                    {
                        lblName.Text = "";
                        lblEmail.Text = "";
                        lblJobTitle.Text = "";
                        lblDepartment.Text = "";
                        lvGroups.Items.Add(new ListViewItem(new[] { "Invalid user selection", "N/A" }));
                        Console.WriteLine("Invalid user selection in dataGridViewUsers.");
                        MessageBox.Show("Failed to get the selected user.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
                    if (selectedUser == null)
                    {
                        lblName.Text = "";
                        lblEmail.Text = "";
                        lblJobTitle.Text = "";
                        lblDepartment.Text = "";
                        lvGroups.Items.Add(new ListViewItem(new[] { "Invalid user selection", "N/A" }));
                        Console.WriteLine($"Selected UserId {selectedRow.UserId} not found in _allResults.");
                        MessageBox.Show("Failed to get the selected user.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    Console.WriteLine($"User selected: {selectedUser.DisplayName ?? "Unknown"} (ID: {selectedUser.Id})");
                    DisplayUserDetails(selectedUser);
                }));
            }

            if (dataGridViewUsers.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
                if (selectedRow != null && !string.IsNullOrEmpty(selectedRow.UserId))
                {
                    User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
                    if (selectedUser != null)
                    {
                        // Cancel any ongoing group loading operations
                        lock (_syncLock)
                        {
                            _loadUserGroupsCts.Cancel();
                            _loadUserGroupsCts = new CancellationTokenSource();
                        }

                        if (!_userGroups.ContainsKey(selectedUser.Id))
                        {
                            Console.WriteLine($"No groups loaded for user {selectedUser.DisplayName ?? "Unknown"} (ID: {selectedUser.Id}). Loading now...");
                            try
                            {
                                await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);
                                Console.WriteLine($"Triggering PopulateGroupListView for user {selectedUser.DisplayName ?? "Unknown"} (ID: {selectedUser.Id})");
                                await PopulateGroupListView(selectedUser).ConfigureAwait(false);
                                if (!IsDisposed && IsHandleCreated)
                                {
                                    BeginInvoke((Action)(() => lvGroups.Refresh())); // Force UI refresh
                                }
                            }
                            catch (OperationCanceledException ex)
                            {
                                Console.WriteLine($"Group loading canceled in SelectionChanged for user {selectedUser.DisplayName ?? "Unknown"} (ID: {selectedUser.Id}): {ex.Message}\nStack Trace: {ex.StackTrace}");
                                if (!IsDisposed && IsHandleCreated)
                                {
                                    BeginInvoke((Action)(() =>
                                    {
                                        MessageBox.Show($"Group loading timed out for {selectedUser.DisplayName ?? "Unknown"}. Please try refreshing.", "Timeout Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error loading groups in SelectionChanged for user {selectedUser.DisplayName ?? "Unknown"} (ID: {selectedUser.Id}): {ex.Message}\nStack Trace: {ex.StackTrace}");
                                if (!IsDisposed && IsHandleCreated)
                                {
                                    BeginInvoke((Action)(() =>
                                    {
                                        MessageBox.Show($"Failed to load groups for {selectedUser.DisplayName ?? "Unknown"}: {ex.Message}", "Group Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }));
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Triggering PopulateGroupListView for user {selectedUser.DisplayName ?? "Unknown"} (ID: {selectedUser.Id}) with cached groups");
                            await PopulateGroupListView(selectedUser).ConfigureAwait(false);
                            if (!IsDisposed && IsHandleCreated)
                            {
                                BeginInvoke((Action)(() => lvGroups.Refresh())); // Force UI refresh
                            }
                        }
                    }
                }
            }
        }
        private async Task<List<string>> GetOwnerGroups(string userId)
        {
            try
            {
                var groups = await _graphClient.Users[userId].OwnedObjects.GetAsync(request =>
                {
                    request.QueryParameters.Select = new[] { "id", "displayName" };
                    request.Headers.Add("ConsistencyLevel", "eventual");
                }).ConfigureAwait(false);

                var ownedGroupIds = groups?.Value?.OfType<Group>()
                    .Where(g => g.Id != null && g.DisplayName != null &&
                                (g.DisplayName.StartsWith("CSG-CLBA", StringComparison.OrdinalIgnoreCase) ||
                                 g.DisplayName.StartsWith("FSG-CLBA", StringComparison.OrdinalIgnoreCase)))
                    .Select(g => g.Id)
                    .ToList() ?? new List<string>();

                Console.WriteLine($"User ID {userId} owns {ownedGroupIds.Count} CSG-CLBA/FSG-CLBA groups: {string.Join(", ", ownedGroupIds)}");
                return ownedGroupIds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading owned groups for user ID {userId}: {ex.Message}");
                return new List<string>();
            }
        }
        private async Task PopulateGroupListView(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Id) || IsDisposed || !IsHandleCreated)
                return;

            if (!_userGroups.TryGetValue(user.Id, out var groups))
            {
                Console.WriteLine($"No groups loaded for user {user.DisplayName ?? "Unknown"} (ID: {user.Id})");
                BeginInvoke((Action)(() =>
                {
                    lvGroups.BeginUpdate();
                    lvGroups.Items.Clear();
                    lvGroups.Items.Add(new ListViewItem(new[] { "No groups loaded", "N/A" }));
                    lvGroups.EndUpdate();
                }));
                return;
            }

            var sortedGroups = _groupNameSortState switch
            {
                1 => groups.OrderBy(g => g.DisplayName.Contains("FSG-CLBA", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                           .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase),
                2 => groups.OrderBy(g => g.DisplayName.Contains("CSG-CLBA", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                           .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase),
                _ => groups.OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            };

            // Load group items
            BeginInvoke((Action)(() =>
            {
                lvGroups.BeginUpdate();
                lvGroups.Items.Clear();

                if (lvGroups.Columns.Count == 0)
                {
                    lvGroups.Columns.Add("Group Name", -2, HorizontalAlignment.Left);
                    lvGroups.Columns.Add("Is Owner", -2, HorizontalAlignment.Left);
                }

                foreach (var group in sortedGroups)
                {
                    if (group?.DisplayName != null)
                    {
                        var item = new ListViewItem(new[] { group.DisplayName, "" });
                        item.Tag = group;
                        lvGroups.Items.Add(item);
                    }
                }

                foreach (ColumnHeader column in lvGroups.Columns)
                    column.Width = -2;

                lvGroups.EndUpdate();
                lvGroups.Refresh();
                Console.WriteLine($"Displayed {lvGroups.Items.Count} groups for user {user.DisplayName ?? "Unknown"} (ID: {user.Id})");
            }));

            // Now update ownership
            var ownerGroups = await GetOwnerGroups(user.Id).ConfigureAwait(false);
            BeginInvoke((Action)(() =>
            {
                lvGroups.BeginUpdate();
                foreach (ListViewItem item in lvGroups.Items)
                {
                    var group = item.Tag as Group;
                    string ownerText = (group?.Id != null && ownerGroups.Contains(group.Id)) ? "Yes" : "No";

                    if (item.SubItems.Count > 1)
                        item.SubItems[1].Text = ownerText;
                    else
                        item.SubItems.Add(ownerText);
                }

                lvGroups.EndUpdate();
                lvGroups.Refresh();
                Console.WriteLine($"Updated ownership status for {lvGroups.Items.Count} groups for user {user.DisplayName ?? "Unknown"} (ID: {user.Id})");
            }));
        }
        private async void btnRemoveOwner_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastRemoveOwnerClick).TotalMilliseconds < 500)
                return;
            _lastRemoveOwnerClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0 || lvGroups.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a user and at least one group.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.UserId))
            {
                MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (selectedUser == null)
            {
                MessageBox.Show("No valid user selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedGroups = lvGroups.SelectedItems.Cast<ListViewItem>().Select(item => (Group)item.Tag).ToList();
            bool allGroupsSelected = lvGroups.SelectedItems.Count == lvGroups.Items.Count;

            var confirm = MessageBox.Show($"Are you sure you want to remove {selectedUser.DisplayName} as owner from {selectedGroups.Count} group(s)?", "Confirm Removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.WaitCursor;
                        lblStatus.Text = "Removing ownership...";
                        lblStatus.Visible = true;
                        btnRemoveOwner.Enabled = false;
                        btnAddOwner.Enabled = false;
                        btnRemoveMember.Enabled = false;
                        btnReplaceGroup.Enabled = false;
                        btnCopyGroups.Enabled = false;
                        btnAddToDepartmentGroups.Enabled = false;
                        btnRefresh.Enabled = false;
                        btnBack.Enabled = false;
                    }));
                }

                List<string> removedOwnerGroups = new List<string>();
                List<string> errors = new List<string>();
                foreach (var group in selectedGroups)
                {
                    try
                    {
                        if (group?.Id == null)
                        {
                            Console.WriteLine($"Invalid group ID for group {group?.DisplayName ?? "Unknown"}");
                            errors.Add($"{group?.DisplayName ?? "Unknown"}: Invalid group ID");
                            continue;
                        }

                        // Check if user is an owner
                        var owners = await _graphClient.Groups[group.Id].Owners.GetAsync().ConfigureAwait(false);
                        if (owners?.Value?.Any(o => o.Id == selectedUser.Id) != true)
                        {
                            Console.WriteLine($"User {selectedUser.DisplayName} (ID: {selectedUser.Id}) is not an owner of group {group.DisplayName} (ID: {group.Id})");
                            continue;
                        }

                        Console.WriteLine($"Removing user {selectedUser.DisplayName} (ID: {selectedUser.Id}) as owner from group {group.DisplayName} (ID: {group.Id})");
                        await _graphClient.Groups[group.Id].Owners[selectedUser.Id].Ref.DeleteAsync().ConfigureAwait(false);
                        removedOwnerGroups.Add(group.DisplayName ?? "Unknown");
                        await _auditLogManager.LogAction(
                            _signedInUserId,
                            "System",
                            "RemoveOwner",
                            group.DisplayName ?? "Unknown",
                            selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                            "User",
                            $"Removed user as owner from group {group.DisplayName}").ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{group.DisplayName}: {ex.Message}");
                        Console.WriteLine($"Error removing user as owner from group {group.DisplayName}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                    }
                }

                if (allGroupsSelected && removedOwnerGroups.Count > 0)
                {
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "System",
                        "RemoveOwner",
                        "All Selected Groups",
                        selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                        "User",
                        $"Removed user as owner from {removedOwnerGroups.Count} selected groups").ConfigureAwait(false);
                }

                // Cancel any ongoing group loading operations
                lock (_syncLock)
                {
                    _loadUserGroupsCts.Cancel();
                    _loadUserGroupsCts = new CancellationTokenSource();
                }

                // Reload groups for the selected user
                Console.WriteLine($"Reloading groups for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);

                // Refresh the group list
                Console.WriteLine($"Populating group list for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                await PopulateGroupListView(selectedUser).ConfigureAwait(false);

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        lvGroups.Refresh(); // Force UI refresh
                        if (errors.Count == 0)
                        {
                            MessageBox.Show(
                                $"Removed {selectedUser.DisplayName} as owner from {removedOwnerGroups.Count} group(s).",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Some groups failed to remove ownership:\n{string.Join("\n", errors)}",
                                "Partial Failure",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in btnRemoveOwner_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show(
                            $"Error removing owner:\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.Default;
                        lblStatus.Text = "Ready";
                        lblStatus.Visible = false;
                        btnRemoveOwner.Enabled = true;
                        btnAddOwner.Enabled = true;
                        btnRemoveMember.Enabled = true;
                        btnReplaceGroup.Enabled = true;
                        btnCopyGroups.Enabled = true;
                        btnAddToDepartmentGroups.Enabled = true;
                        btnRefresh.Enabled = true;
                        btnBack.Enabled = true;
                        lvGroups.Refresh(); // Ensure UI is updated
                        Console.WriteLine("UI state reset after removing owners");
                    }));
                }
            }
        }
        private async void lvGroups_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == 0) // "Group Name" column
            {
                _groupNameSortState = (_groupNameSortState + 1) % 3; // Cycle through 0, 1, 2
                Console.WriteLine($"Group Name column clicked, sort state: {_groupNameSortState} (0=Alphabetical, 1=FSG then CSG, 2=CSG then FSG)");

                if (dataGridViewUsers.SelectedRows.Count > 0)
                {
                    var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
                    if (selectedRow != null && !string.IsNullOrEmpty(selectedRow.UserId))
                    {
                        User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
                        if (selectedUser != null)
                        {
                            await PopulateGroupListView(selectedUser).ConfigureAwait(false);
                            if (!IsDisposed && IsHandleCreated)
                            {
                                BeginInvoke((Action)(() => lvGroups.Refresh())); // Force UI refresh
                            }
                        }
                        else
                        {
                            Console.WriteLine("No valid user selected for sorting groups.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid user selection for sorting groups.");
                    }
                }
                else
                {
                    Console.WriteLine("No user selected in dataGridViewUsers for sorting groups.");
                }
            }
        }
        private void btnBack_Click(object sender, EventArgs e)
        {
            _parentForm.Show();
            this.Close();
        }

        private async void btnAddToDepartmentGroups_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastAddGroupClick).TotalMilliseconds < 500)
                return;
            _lastAddGroupClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.UserId))
            {
                MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (selectedUser == null)
            {
                MessageBox.Show("No valid user selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                BeginInvoke(() =>
                {
                    Cursor = Cursors.WaitCursor;
                    lblStatus.Text = "Adding to groups...";
                    lblStatus.Visible = true;
                    btnAddToDepartmentGroups.Enabled = false;
                    btnAddOwner.Enabled = false;
                    btnRemoveOwner.Enabled = false;
                    btnRemoveMember.Enabled = false;
                    btnReplaceGroup.Enabled = false;
                    btnCopyGroups.Enabled = false;
                    btnRefresh.Enabled = false;
                    btnBack.Enabled = false;
                });

                Debug.WriteLine($"UserSearchForm.btnAddToDepartmentGroups_Click: _claimsPrincipal is {(_claimsPrincipal != null ? "not null" : "null")}");
                if (_claimsPrincipal != null && _claimsPrincipal.Claims != null)
                {
                    Debug.WriteLine($"UserSearchForm.btnAddToDepartmentGroups_Click: Claims found: {string.Join(", ", _claimsPrincipal.Claims.Select(c => $"{c.Type}:{c.Value}"))}");
                }
                else
                {
                    Debug.WriteLine("UserSearchForm.btnAddToDepartmentGroups_Click: No claims available");
                }

                using var dialog = new AddToDepartmentGroupsDialog(_graphClient, _departmentPrefixes, _claimsPrincipal, null);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var selectedGroups = dialog.SelectedGroups;
                    var errors = new List<string>();
                    var addedGroups = new List<string>();
                    var skippedGroups = new List<string>();

                    foreach (var grp in selectedGroups)
                    {
                        try
                        {
                            var members = await _graphClient.Groups[grp.Id].Members.GetAsync().ConfigureAwait(false);
                            if (members?.Value?.Any(m => m.Id == selectedUser.Id) == true)
                            {
                                Console.WriteLine($"User already in group: {grp.DisplayName}");
                                skippedGroups.Add(grp.DisplayName ?? "Unknown");
                                continue;
                            }

                            Console.WriteLine($"Adding user to group: {grp.DisplayName}");
                            await _graphClient.Groups[grp.Id].Members.Ref.PostAsync(new ReferenceCreate
                            {
                                OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{selectedUser.Id}"
                            }).ConfigureAwait(false);

                            addedGroups.Add(grp.DisplayName ?? "Unknown");

                            await _auditLogManager.LogAction(
                                _signedInUserId,
                                "System",
                                "AddMember",
                                grp.DisplayName ?? "Unknown",
                                selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                                "User",
                                $"Added user to group {grp.DisplayName}").ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{grp.DisplayName}: {ex.Message}");
                            Console.WriteLine($"Error adding to group {grp.DisplayName}: {ex.Message}");
                        }
                    }

                    // Cancel any ongoing group loading operations
                    lock (_syncLock)
                    {
                        _loadUserGroupsCts.Cancel();
                        _loadUserGroupsCts = new CancellationTokenSource();
                        _userGroups.Remove(selectedUser.Id); // Ensure it gets reloaded
                    }

                    await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);
                    await PopulateGroupListView(selectedUser).ConfigureAwait(false);

                    BeginInvoke(() =>
                    {
                        lvGroups.Refresh();
                        string message = $"User added to {addedGroups.Count} group(s).";
                        if (skippedGroups.Count > 0)
                        {
                            message += $"\nSkipped {skippedGroups.Count} group(s) (user already a member): {string.Join(", ", skippedGroups)}.";
                        }
                        if (errors.Count > 0)
                        {
                            message += $"\nSome groups failed:\n{string.Join("\n", errors)}";
                            MessageBox.Show(message, "Partial Success", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                BeginInvoke(() =>
                {
                    MessageBox.Show($"Error adding user to groups:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                BeginInvoke(() =>
                {
                    Cursor = Cursors.Default;
                    lblStatus.Text = "Ready";
                    lblStatus.Visible = false;
                    btnAddToDepartmentGroups.Enabled = true;
                    btnAddOwner.Enabled = true;
                    btnRemoveOwner.Enabled = true;
                    btnRemoveMember.Enabled = true;
                    btnReplaceGroup.Enabled = true;
                    btnCopyGroups.Enabled = true;
                    btnRefresh.Enabled = true;
                    btnBack.Enabled = true;
                    lvGroups.Refresh();
                    Console.WriteLine("UI reset after adding to groups");
                });
            }
        }
        private async void btnRemoveMember_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastRemoveMemberClick).TotalMilliseconds < 500)
                return;
            _lastRemoveMemberClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0 || lvGroups.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a user and at least one group.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.UserId))
            {
                MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (selectedUser == null)
            {
                MessageBox.Show("No valid user selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedGroups = lvGroups.SelectedItems.Cast<ListViewItem>().Select(item => (Group)item.Tag).ToList();
            bool allGroupsSelected = lvGroups.SelectedItems.Count == lvGroups.Items.Count;

            var confirm = MessageBox.Show(
                $"Are you sure you want to remove {selectedUser.DisplayName} from {selectedGroups.Count} group(s)? The user will lose access to all files/folders in these groups.",
                "Confirm Removal",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.WaitCursor;
                        lblStatus.Text = "Removing membership...";
                        lblStatus.Visible = true;
                        btnRemoveMember.Enabled = false;
                        btnAddOwner.Enabled = false;
                        btnRemoveOwner.Enabled = false;
                        btnReplaceGroup.Enabled = false;
                        btnCopyGroups.Enabled = false;
                        btnAddToDepartmentGroups.Enabled = false;
                        btnRefresh.Enabled = false;
                        btnBack.Enabled = false;
                    }));
                }

                List<string> removedGroups = new List<string>();
                List<string> errors = new List<string>();
                int maxRetries = 3;
                int retryDelayMs = 1500;
                int batchSize = 20;
                int batchDelayMs = 2000; // Delay between batches to avoid throttling

                // Split groups into batches of 20
                var groupBatches = selectedGroups
                    .Select((group, index) => new { group, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.group).ToList())
                    .ToList();

                for (int batchIndex = 0; batchIndex < groupBatches.Count; batchIndex++)
                {
                    var batch = groupBatches[batchIndex];
                    Console.WriteLine($"Processing batch {batchIndex + 1} of {groupBatches.Count} with {batch.Count} groups");

                    foreach (var group in batch)
                    {
                        if (group?.Id == null)
                        {
                            Console.WriteLine($"Invalid group ID for group {group?.DisplayName ?? "Unknown"}");
                            errors.Add($"{group?.DisplayName ?? "Unknown"}: Invalid group ID");
                            continue;
                        }

                        bool removed = false;
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                Console.WriteLine($"Attempt {attempt}/{maxRetries} to remove user {selectedUser.DisplayName} (ID: {selectedUser.Id}) from group {group.DisplayName} (ID: {group.Id})");
                                await _graphClient.Groups[group.Id].Members[selectedUser.Id].Ref.DeleteAsync().ConfigureAwait(false);
                                removedGroups.Add(group.DisplayName ?? "Unknown");
                                await _auditLogManager.LogAction(
                                    _signedInUserId,
                                    "System",
                                    "RemoveMember",
                                    group.DisplayName ?? "Unknown",
                                    selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                                    "User",
                                    $"Removed user from group {group.DisplayName}").ConfigureAwait(false);
                                removed = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error removing user from group {group.DisplayName}, attempt {attempt}/{maxRetries}: {ex.Message}");
                                if (attempt == maxRetries)
                                {
                                    errors.Add($"{group.DisplayName}: {ex.Message}");
                                }
                                await Task.Delay(retryDelayMs).ConfigureAwait(false);
                            }
                        }

                        if (removed)
                        {
                            Console.WriteLine($"Successfully removed user {selectedUser.DisplayName} from group {group.DisplayName}");
                        }
                    }

                    // Add delay between batches (except for the last batch)
                    if (batchIndex < groupBatches.Count - 1)
                    {
                        Console.WriteLine($"Waiting {batchDelayMs}ms before processing next batch");
                        await Task.Delay(batchDelayMs).ConfigureAwait(false);
                    }
                }

                if (allGroupsSelected && removedGroups.Count > 0)
                {
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "System",
                        "RemoveMember",
                        "All Selected Groups",
                        selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                        "User",
                        $"Removed user from {removedGroups.Count} selected groups").ConfigureAwait(false);
                }

                // Cancel any ongoing group loading operations
                lock (_syncLock)
                {
                    _loadUserGroupsCts.Cancel();
                    _loadUserGroupsCts = new CancellationTokenSource();
                }

                // Reload groups for the selected user
                Console.WriteLine($"Reloading groups for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);

                // Refresh the group list
                Console.WriteLine($"Populating group list for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                await PopulateGroupListView(selectedUser).ConfigureAwait(false);

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        lvGroups.Refresh(); // Force UI refresh
                        if (errors.Count == 0)
                        {
                            MessageBox.Show(
                                $"Removed {selectedUser.DisplayName} from {removedGroups.Count} group(s).",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Removed {selectedUser.DisplayName} from {removedGroups.Count} group(s).\nSome groups failed to remove:\n{string.Join("\n", errors)}",
                                "Partial Failure",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in btnRemoveMember_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show(
                            $"Error removing member:\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.Default;
                        lblStatus.Text = "Ready";
                        lblStatus.Visible = false;
                        btnRemoveMember.Enabled = true;
                        btnAddOwner.Enabled = true;
                        btnRemoveOwner.Enabled = true;
                        btnReplaceGroup.Enabled = true;
                        btnCopyGroups.Enabled = true;
                        btnAddToDepartmentGroups.Enabled = true;
                        btnRefresh.Enabled = true;
                        btnBack.Enabled = true;
                        lvGroups.Refresh(); // Ensure UI is updated
                        Console.WriteLine("UI state reset after removing members");
                    }));
                }
            }
        }
        private async void btnAddOwner_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastAddOwnerClick).TotalMilliseconds < 500)
                return;
            _lastAddOwnerClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0 || lvGroups.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a user and at least one group.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.UserId))
            {
                MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (selectedUser == null)
            {
                MessageBox.Show("No valid user selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedGroups = lvGroups.SelectedItems.Cast<ListViewItem>().Select(item => (Group)item.Tag).ToList();
            bool allGroupsSelected = lvGroups.SelectedItems.Count == lvGroups.Items.Count;

            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.WaitCursor;
                        lblStatus.Text = "Adding ownership...";
                        lblStatus.Visible = true;
                        btnAddOwner.Enabled = false;
                        btnRemoveMember.Enabled = false;
                        btnRemoveOwner.Enabled = false;
                        btnReplaceGroup.Enabled = false;
                        btnCopyGroups.Enabled = false;
                        btnAddToDepartmentGroups.Enabled = false;
                        btnRefresh.Enabled = false;
                        btnBack.Enabled = false;
                    }));
                }

                List<string> addedOwnerGroups = new List<string>();
                List<string> errors = new List<string>();
                foreach (var group in selectedGroups)
                {
                    try
                    {
                        if (group?.Id == null)
                        {
                            Console.WriteLine($"Invalid group ID for group {group?.DisplayName ?? "Unknown"}");
                            errors.Add($"{group?.DisplayName ?? "Unknown"}: Invalid group ID");
                            continue;
                        }

                        // Check if user is already an owner
                        var owners = await _graphClient.Groups[group.Id].Owners.GetAsync().ConfigureAwait(false);
                        if (owners?.Value?.Any(o => o.Id == selectedUser.Id) == true)
                        {
                            Console.WriteLine($"User {selectedUser.DisplayName} (ID: {selectedUser.Id}) is already an owner of group {group.DisplayName} (ID: {group.Id})");
                            continue;
                        }

                        Console.WriteLine($"Adding user {selectedUser.DisplayName} (ID: {selectedUser.Id}) as owner to group {group.DisplayName} (ID: {group.Id})");
                        await _graphClient.Groups[group.Id].Owners.Ref.PostAsync(new ReferenceCreate
                        {
                            OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{selectedUser.Id}"
                        }).ConfigureAwait(false);
                        addedOwnerGroups.Add(group.DisplayName ?? "Unknown");
                        await _auditLogManager.LogAction(
                            _signedInUserId,
                            "System",
                            "AddOwner",
                            group.DisplayName ?? "Unknown",
                            selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                            "User",
                            $"Added user as owner to group {group.DisplayName}").ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{group.DisplayName}: {ex.Message}");
                        Console.WriteLine($"Error adding user as owner to group {group.DisplayName}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                    }
                }

                if (allGroupsSelected && addedOwnerGroups.Count > 0)
                {
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "System",
                        "AddOwner",
                        "All Selected Groups",
                        selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                        "User",
                        $"Added user as owner to {addedOwnerGroups.Count} selected groups").ConfigureAwait(false);
                }

                // Cancel any ongoing group loading operations
                lock (_syncLock)
                {
                    _loadUserGroupsCts.Cancel();
                    _loadUserGroupsCts = new CancellationTokenSource();
                }

                // Reload groups for the selected user
                Console.WriteLine($"Reloading groups for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);

                // Refresh the group list
                Console.WriteLine($"Populating group list for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                await PopulateGroupListView(selectedUser).ConfigureAwait(false);

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        lvGroups.Refresh(); // Force UI refresh
                        if (errors.Count == 0)
                        {
                            MessageBox.Show(
                                $"Added {selectedUser.DisplayName} as owner to {addedOwnerGroups.Count} group(s).",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Some groups failed to add ownership:\n{string.Join("\n", errors)}",
                                "Partial Failure",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in btnAddOwner_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show(
                            $"Error adding owner:\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.Default;
                        lblStatus.Text = "Ready";
                        lblStatus.Visible = false;
                        btnAddOwner.Enabled = true;
                        btnRemoveMember.Enabled = true;
                        btnRemoveOwner.Enabled = true;
                        btnReplaceGroup.Enabled = true;
                        btnCopyGroups.Enabled = true;
                        btnAddToDepartmentGroups.Enabled = true;
                        btnRefresh.Enabled = true;
                        btnBack.Enabled = true;
                        lvGroups.Refresh(); // Ensure UI is updated
                        Console.WriteLine("UI state reset after adding owners");
                    }));
                }
            }
        }



        private async void btnReplaceGroup_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastReplaceGroupClick).TotalMilliseconds < 500)
                return;
            _lastReplaceGroupClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0 || lvGroups.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a user and at least one group.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null)
            {
                MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (selectedUser == null)
            {
                MessageBox.Show("No valid user selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedGroups = lvGroups.SelectedItems.Cast<ListViewItem>().Select(item => (Group)item.Tag).ToList();
            bool allGroupsSelected = lvGroups.SelectedItems.Count == lvGroups.Items.Count;

            try
            {
                BeginInvoke((Action)(() =>
                {
                    Cursor = Cursors.WaitCursor;
                    lblStatus.Text = "Replacing groups...";
                    lblStatus.Visible = true;
                    btnReplaceGroup.Enabled = false;
                    btnAddOwner.Enabled = false;
                    btnRemoveOwner.Enabled = false;
                    btnRemoveMember.Enabled = false;
                    btnCopyGroups.Enabled = false;
                    btnAddToDepartmentGroups.Enabled = false;
                    btnRefresh.Enabled = false;
                    btnBack.Enabled = false;
                }));

                Debug.WriteLine($"UserSearchForm.btnReplaceGroup_Click: _claimsPrincipal is {(_claimsPrincipal != null ? "not null" : "null")}");
                if (_claimsPrincipal != null && _claimsPrincipal.Claims != null)
                {
                    Debug.WriteLine($"UserSearchForm.btnReplaceGroup_Click: Claims found: {string.Join(", ", _claimsPrincipal.Claims.Select(c => $"{c.Type}:{c.Value}"))}");
                }
                else
                {
                    Debug.WriteLine("UserSearchForm.btnReplaceGroup_Click: No claims available");
                }

                using (var replaceDialog = new ReplaceGroupDialog(_graphClient, selectedGroups, _claimsPrincipal))
                {
                    if (replaceDialog.ShowDialog() == DialogResult.OK)
                    {
                        var newGroup = replaceDialog.SelectedGroup;
                        List<string> replacedGroups = new List<string>();
                        List<string> errors = new List<string>();

                        // Check if user is already a member of the new group
                        var newGroupMembers = await _graphClient.Groups[newGroup.Id].Members.GetAsync().ConfigureAwait(false);
                        bool isAlreadyMember = newGroupMembers?.Value?.Any(m => m.Id == selectedUser.Id) == true;

                        foreach (var oldGroup in selectedGroups)
                        {
                            try
                            {
                                // Check if user is a member of the old group
                                var oldGroupMembers = await _graphClient.Groups[oldGroup.Id].Members.GetAsync().ConfigureAwait(false);
                                if (oldGroupMembers?.Value?.Any(m => m.Id == selectedUser.Id) != true)
                                {
                                    Console.WriteLine($"User {selectedUser.DisplayName} (ID: {selectedUser.Id}) is not a member of group {oldGroup.DisplayName} (ID: {oldGroup.Id})");
                                    continue;
                                }

                                Console.WriteLine($"Replacing group {oldGroup.DisplayName} (ID: {oldGroup.Id}) with {newGroup.DisplayName} (ID: {newGroup.Id}) for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                                await _graphClient.Groups[oldGroup.Id].Members[selectedUser.Id].Ref.DeleteAsync().ConfigureAwait(false);
                                if (!isAlreadyMember)
                                {
                                    await _graphClient.Groups[newGroup.Id].Members.Ref.PostAsync(new ReferenceCreate
                                    {
                                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{selectedUser.Id}"
                                    }).ConfigureAwait(false);
                                    isAlreadyMember = true; // Avoid redundant additions
                                }
                                replacedGroups.Add(oldGroup.DisplayName ?? "Unknown");
                                await _auditLogManager.LogAction(
                                    _signedInUserId,
                                    "System",
                                    "ReplaceGroup",
                                    oldGroup.DisplayName ?? "Unknown",
                                    selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                                    "User",
                                    $"Replaced group {oldGroup.DisplayName} with {newGroup.DisplayName} for user").ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"{oldGroup.DisplayName}: {ex.Message}");
                                Console.WriteLine($"Error replacing group {oldGroup.DisplayName} with {newGroup.DisplayName}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                            }
                        }

                        if (allGroupsSelected && replacedGroups.Count > 0)
                        {
                            await _auditLogManager.LogAction(
                                _signedInUserId,
                                "System",
                                "ReplaceGroup",
                                "All Selected Groups",
                                selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                                "User",
                                $"Replaced {replacedGroups.Count} selected groups with {newGroup.DisplayName} for user").ConfigureAwait(false);
                        }

                        // Cancel any ongoing group loading operations
                        lock (_syncLock)
                        {
                            _loadUserGroupsCts.Cancel();
                            _loadUserGroupsCts = new CancellationTokenSource();
                        }

                        // Reload groups for the selected user
                        Console.WriteLine($"Reloading groups for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                        await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);

                        // Refresh the group list
                        Console.WriteLine($"Populating group list for user {selectedUser.DisplayName} (ID: {selectedUser.Id})");
                        await PopulateGroupListView(selectedUser).ConfigureAwait(false);

                        BeginInvoke((Action)(() =>
                        {
                            lvGroups.Refresh(); // Force UI refresh
                            if (errors.Count == 0)
                            {
                                MessageBox.Show(
                                    $"Replaced {replacedGroups.Count} group(s) with {newGroup.DisplayName} for {selectedUser.DisplayName}.",
                                    "Success",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Some groups failed to replace:\n{string.Join("\n", errors)}",
                                    "Partial Failure",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in btnReplaceGroup_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(
                        $"Error replacing group:\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }));
            }
            finally
            {
                BeginInvoke((Action)(() =>
                {
                    Cursor = Cursors.Default;
                    lblStatus.Text = "Ready";
                    lblStatus.Visible = false;
                    btnReplaceGroup.Enabled = true;
                    btnAddOwner.Enabled = true;
                    btnRemoveOwner.Enabled = true;
                    btnRemoveMember.Enabled = true;
                    btnCopyGroups.Enabled = true;
                    btnAddToDepartmentGroups.Enabled = true;
                    btnRefresh.Enabled = true;
                    btnBack.Enabled = true;
                    lvGroups.Refresh(); // Ensure UI is updated
                    Console.WriteLine("UI state reset after replacing groups");
                }));
            }
        }
        private async void btnCopyGroups_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastCopyGroupsClick).TotalMilliseconds < 500)
                return;
            _lastCopyGroupsClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to copy groups from.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.UserId))
            {
                MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            User sourceUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (sourceUser == null || !_userGroups.ContainsKey(sourceUser.Id) || !_userGroups[sourceUser.Id].Any())
            {
                MessageBox.Show("The selected user has no groups to copy.", "No Groups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.WaitCursor;
                        lblStatus.Text = "Copying groups...";
                        lblStatus.Visible = true;
                        btnCopyGroups.Enabled = false;
                        btnAddOwner.Enabled = false;
                        btnRemoveOwner.Enabled = false;
                        btnRemoveMember.Enabled = false;
                        btnReplaceGroup.Enabled = false;
                        btnAddToDepartmentGroups.Enabled = false;
                        btnRefresh.Enabled = false;
                        btnBack.Enabled = false;
                    }));
                }

                using (var selectUserDialog = new SelectUserDialog(_graphClient))
                {
                    if (selectUserDialog.ShowDialog() == DialogResult.OK)
                    {
                        var targetUser = selectUserDialog.SelectedUser;
                        if (targetUser == null || string.IsNullOrEmpty(targetUser.Id))
                        {
                            if (!IsDisposed && IsHandleCreated)
                            {
                                BeginInvoke((Action)(() =>
                                {
                                    MessageBox.Show("No valid target user selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }));
                            }
                            return;
                        }

                        if (targetUser.Id == sourceUser.Id)
                        {
                            if (!IsDisposed && IsHandleCreated)
                            {
                                BeginInvoke((Action)(() =>
                                {
                                    MessageBox.Show("Cannot copy groups to the same user.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }));
                            }
                            return;
                        }

                        var sourceGroups = _userGroups[sourceUser.Id];
                        var sourceOwnerGroups = await GetOwnerGroups(sourceUser.Id).ConfigureAwait(false);
                        List<string> addedGroups = new List<string>();
                        List<string> addedOwnerGroups = new List<string>();
                        List<string> skippedGroups = new List<string>();  // NEW: Track skipped memberships
                        List<string> skippedOwners = new List<string>();  // NEW: Track skipped ownerships
                        List<string> errors = new List<string>();

                        foreach (var group in sourceGroups)
                        {
                            try
                            {
                                if (group?.Id == null)
                                {
                                    Console.WriteLine($"Invalid group ID for group {group?.DisplayName ?? "Unknown"}");
                                    errors.Add($"{group?.DisplayName ?? "Unknown"}: Invalid group ID");
                                    continue;
                                }

                                // Check if user is already a member
                                var members = await _graphClient.Groups[group.Id].Members.GetAsync().ConfigureAwait(false);
                                if (members?.Value?.Any(m => m.Id == targetUser.Id) == true)
                                {
                                    Console.WriteLine($"User {targetUser.DisplayName} (ID: {targetUser.Id}) is already a member of group {group.DisplayName} (ID: {group.Id})");
                                    skippedGroups.Add(group.DisplayName ?? "Unknown");
                                }
                                else
                                {
                                    Console.WriteLine($"Copying group {group.DisplayName} (ID: {group.Id}) to user {targetUser.DisplayName} (ID: {targetUser.Id})");
                                    await _graphClient.Groups[group.Id].Members.Ref.PostAsync(new ReferenceCreate
                                    {
                                        OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{targetUser.Id}"
                                    }).ConfigureAwait(false);
                                    addedGroups.Add(group.DisplayName ?? "Unknown");
                                    await _auditLogManager.LogAction(
                                        _signedInUserId,
                                        "System",
                                        "AddMember",
                                        group.DisplayName ?? "Unknown",
                                        targetUser.DisplayName ?? targetUser.UserPrincipalName ?? "Unknown",
                                        "User",
                                        $"Copied group membership {group.DisplayName} from {sourceUser.DisplayName ?? sourceUser.UserPrincipalName ?? "Unknown"} to {targetUser.DisplayName ?? targetUser.UserPrincipalName ?? "Unknown"}").ConfigureAwait(false);
                                }

                                if (sourceOwnerGroups.Contains(group.Id))
                                {
                                    var owners = await _graphClient.Groups[group.Id].Owners.GetAsync().ConfigureAwait(false);
                                    if (owners?.Value?.Any(o => o.Id == targetUser.Id) == true)
                                    {
                                        Console.WriteLine($"User {targetUser.DisplayName} (ID: {targetUser.Id}) is already an owner of group {group.DisplayName} (ID: {group.Id})");
                                        skippedOwners.Add(group.DisplayName ?? "Unknown");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Copying ownership of group {group.DisplayName} (ID: {group.Id}) to user {targetUser.DisplayName} (ID: {targetUser.Id})");
                                        await _graphClient.Groups[group.Id].Owners.Ref.PostAsync(new ReferenceCreate
                                        {
                                            OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{targetUser.Id}"
                                        }).ConfigureAwait(false);
                                        addedOwnerGroups.Add(group.DisplayName ?? "Unknown");
                                        await _auditLogManager.LogAction(
                                            _signedInUserId,
                                            "System",
                                            "AddOwner",
                                            group.DisplayName ?? "Unknown",
                                            targetUser.DisplayName ?? targetUser.UserPrincipalName ?? "Unknown",
                                            "User",
                                            $"Copied group ownership {group.DisplayName} from {sourceUser.DisplayName ?? sourceUser.UserPrincipalName ?? "Unknown"} to {targetUser.DisplayName ?? targetUser.UserPrincipalName ?? "Unknown"}").ConfigureAwait(false);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"{group.DisplayName}: {ex.Message}");
                                Console.WriteLine($"Error copying group {group.DisplayName} to user {targetUser.DisplayName}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                            }
                        }

                        if (addedGroups.Count > 0 || addedOwnerGroups.Count > 0)
                        {
                            await _auditLogManager.LogAction(
                                _signedInUserId,
                                "System",
                                "CopyGroups",
                                "All Groups",
                                targetUser.DisplayName ?? targetUser.UserPrincipalName ?? "Unknown",
                                "User",
                                $"Copied {addedGroups.Count} group memberships and {addedOwnerGroups.Count} owner roles from {sourceUser.DisplayName ?? sourceUser.UserPrincipalName ?? "Unknown"} to {targetUser.DisplayName ?? targetUser.UserPrincipalName ?? "Unknown"}").ConfigureAwait(false);
                        }

                        // Cancel any ongoing group loading operations
                        lock (_syncLock)
                        {
                            _loadUserGroupsCts.Cancel();
                            _loadUserGroupsCts = new CancellationTokenSource();
                            _userGroups.Remove(targetUser.Id);  // Ensure fresh load
                        }

                        // Reload groups for the target user
                        Console.WriteLine($"Reloading groups for target user {targetUser.DisplayName} (ID: {targetUser.Id})");
                        await LoadUserGroups(new List<User> { targetUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);

                        // Add target to results if not present
                        lock (_syncLock)
                        {
                            if (!_allResults.Any(u => u.Id == targetUser.Id))
                            {
                                _allResults.Add(targetUser);
                            }
                        }

                        // Rebind DataGridView with sorted list (respect _firstNameSortState)
                        var sortedList = _allResults.AsEnumerable();
                        switch (_firstNameSortState)
                        {
                            case 1: sortedList = sortedList.OrderBy(u => u.GivenName ?? ""); break;
                            case 2: sortedList = sortedList.OrderByDescending(u => u.GivenName ?? ""); break;
                            default: sortedList = sortedList.OrderBy(u => u.DisplayName ?? ""); break;
                        }

                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                dataGridViewUsers.DataSource = sortedList.Select(user => new UserDisplayItem
                                {
                                    FirstName = user.GivenName,
                                    LastName = user.Surname,
                                    DisplayName = user.DisplayName,
                                    Email = user.Mail ?? user.UserPrincipalName,
                                    JobTitle = user.JobTitle,
                                    Department = user.Department,
                                    UserId = user.Id
                                }).ToList();

                                if (dataGridViewUsers.Columns["UserId"] != null)
                                    dataGridViewUsers.Columns["UserId"].Visible = false;

                                // Select the target row
                                var targetRow = dataGridViewUsers.Rows.Cast<DataGridViewRow>()
                                    .FirstOrDefault(r => (r.DataBoundItem as UserDisplayItem)?.UserId == targetUser.Id);
                                if (targetRow != null)
                                {
                                    dataGridViewUsers.ClearSelection();
                                    targetRow.Selected = true;
                                    dataGridViewUsers.CurrentCell = targetRow.Cells[0];
                                    dataGridViewUsers.Focus();  // Triggers SelectionChanged
                                }
                            }));
                        }

                        // If the target user is selected in the DataGridView, refresh the group list
                        if (dataGridViewUsers.SelectedRows.Count > 0)
                        {
                            var selectedItem = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
                            if (selectedItem != null && selectedItem.UserId == targetUser.Id)
                            {
                                Console.WriteLine($"Populating group list for target user {targetUser.DisplayName} (ID: {targetUser.Id})");
                                await PopulateGroupListView(targetUser).ConfigureAwait(false);
                            }
                        }

                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke((Action)(() =>
                            {
                                lvGroups.Refresh(); // Force UI refresh
                                string message = $"Copied {addedGroups.Count} group(s) and {addedOwnerGroups.Count} owner role(s) from {sourceUser.DisplayName} to {targetUser.DisplayName}.";
                                if (skippedGroups.Count > 0)
                                {
                                    message += $"\nSkipped {skippedGroups.Count} membership(s) (already a member): {string.Join(", ", skippedGroups)}.";
                                }
                                if (skippedOwners.Count > 0)
                                {
                                    message += $"\nSkipped {skippedOwners.Count} ownership(s) (already an owner): {string.Join(", ", skippedOwners)}.";
                                }
                                if (errors.Count > 0)
                                {
                                    message += $"\nSome groups or ownerships failed to copy:\n{string.Join("\n", errors)}";
                                    MessageBox.Show(message, "Partial Success", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                                else
                                {
                                    MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error in btnCopyGroups_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show(
                            $"Error copying groups:\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.Default;
                        lblStatus.Text = "Ready";
                        lblStatus.Visible = false;
                        btnCopyGroups.Enabled = true;
                        btnAddOwner.Enabled = true;
                        btnRemoveOwner.Enabled = true;
                        btnRemoveMember.Enabled = true;
                        btnReplaceGroup.Enabled = true;
                        btnAddToDepartmentGroups.Enabled = true;
                        btnRefresh.Enabled = true;
                        btnBack.Enabled = true;
                        lvGroups.Refresh(); // Ensure UI is updated
                        Console.WriteLine("UI state reset after copying groups");
                    }));
                }
            }
        }
        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastRefreshClick).TotalMilliseconds < 500)
                return;
            _lastRefreshClick = DateTime.Now;

            if (dataGridViewUsers.SelectedRows.Count == 0)
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show("Please select a user to refresh group memberships.", "No User Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
                return;
            }

            var selectedRow = dataGridViewUsers.SelectedRows[0].DataBoundItem as UserDisplayItem;
            if (selectedRow == null)
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show("Invalid user selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                return;
            }

            User selectedUser = _allResults.FirstOrDefault(u => u.Id == selectedRow.UserId);
            if (selectedUser == null)
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show("Unable to resolve user object.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                return;
            }

            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.WaitCursor;
                        lblStatus.Text = $"Refreshing group memberships for {selectedUser.DisplayName ?? selectedUser.UserPrincipalName}";
                        lblStatus.Visible = true;
                        btnRefresh.Enabled = false;
                        btnAddOwner.Enabled = false;
                        btnRemoveOwner.Enabled = false;
                        btnRemoveMember.Enabled = false;
                        btnReplaceGroup.Enabled = false;
                        btnCopyGroups.Enabled = false;
                        btnAddToDepartmentGroups.Enabled = false;
                        btnBack.Enabled = false;
                    }));
                }

                lock (_syncLock)
                {
                    _loadUserGroupsCts.Cancel();
                    _loadUserGroupsCts = new CancellationTokenSource();
                    _userGroups.Remove(selectedUser.Id); // Force refresh
                }

                await LoadUserGroups(new List<User> { selectedUser }, _loadUserGroupsCts.Token).ConfigureAwait(false);

                if (!IsDisposed && IsHandleCreated)
                {
                    await PopulateGroupListView(selectedUser).ConfigureAwait(false);
                    BeginInvoke((Action)(() =>
                    {
                        lvGroups.Refresh();
                        MessageBox.Show(
                            $"Group memberships refreshed for {selectedUser.DisplayName}.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }));
                }

                await _auditLogManager.LogAction(
                    _signedInUserId,
                    "System",
                    "RefreshGroups",
                    null,
                    selectedUser.DisplayName ?? selectedUser.UserPrincipalName ?? "Unknown",
                    "User",
                    $"Refreshed group memberships for user").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing group list: {ex.Message}\nStack Trace: {ex.StackTrace}");
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        MessageBox.Show(
                            $"Failed to refresh group list: {ex.Message}",
                            "Refresh Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Cursor = Cursors.Default;
                        lblStatus.Text = "Ready";
                        lblStatus.Visible = false;
                        btnRefresh.Enabled = true;
                        btnAddOwner.Enabled = true;
                        btnRemoveOwner.Enabled = true;
                        btnRemoveMember.Enabled = true;
                        btnReplaceGroup.Enabled = true;
                        btnCopyGroups.Enabled = true;
                        btnAddToDepartmentGroups.Enabled = true;
                        btnBack.Enabled = true;
                        lvGroups.Refresh();
                        Console.WriteLine("UI state reset after refreshing groups");
                    }));
                }
            }
        }
        private void btnSelectAllGroups_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvGroups.Items)
            {
                item.Selected = true;
            }
        }



        private void dataGridViewUsers_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dataGridViewUsers.Columns[e.ColumnIndex].Name == "FirstName")
            {
                _firstNameSortState = (_firstNameSortState + 1) % 3;

                var sortedList = _allResults.AsEnumerable();
                switch (_firstNameSortState)
                {
                    case 1:
                        sortedList = sortedList.OrderBy(u => u.GivenName ?? "");
                        break;
                    case 2:
                        sortedList = sortedList.OrderByDescending(u => u.GivenName ?? "");
                        break;
                    default:
                        sortedList = sortedList.OrderBy(u => u.DisplayName ?? "");
                        break;
                }

                dataGridViewUsers.DataSource = sortedList.Select(user => new UserDisplayItem
                {
                    FirstName = user.GivenName,
                    LastName = user.Surname,
                    DisplayName = user.DisplayName,
                    Email = user.Mail ?? user.UserPrincipalName,
                    JobTitle = user.JobTitle,
                    Department = user.Department,
                    UserId = user.Id
                }).ToList();

                if (dataGridViewUsers.Columns["UserId"] != null)
                {
                    dataGridViewUsers.Columns["UserId"].Visible = false;
                }
            }
        }

        public class UserDisplayItem
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string DisplayName { get; set; }
            public string Email { get; set; }
            public string JobTitle { get; set; }
            public string Department { get; set; }
            public string UserId { get; set; }
        }
    }
}