using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class BrowseUsersForm : Form
    {
        private readonly GraphServiceClient _graphClient;
        private readonly bool _singleSelection;
        private List<User> _allResults = new List<User>();
        private List<UserDisplayItem> _currentDisplayItems = new List<UserDisplayItem>();
        private List<User> _selectedUsers = new List<User>();
        public User SelectedUser { get; private set; }
        public List<User> SelectedUsers => _selectedUsers;
        private int _firstNameSortState = 0;

        public BrowseUsersForm(GraphServiceClient graphClient, bool singleSelection = false)
        {
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _singleSelection = singleSelection;
            InitializeComponent();
        }

        private Label lblNote;
        private TextBox txtSearch;
        private Button btnSearch;
        private DataGridView dataGridViewUsers;
        private Label lblSelected;
        private Button btnOK;
        private Button btnCancel;

        private void InitializeComponent()
        {
            lblNote = new Label { Location = new Point(12, 5), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Italic) };
            lblNote.Text = "Search by name (full or partial) or NetID (@tamu.edu optional)";

            txtSearch = new TextBox { Location = new Point(12, 30), Size = new Size(300, 20), Font = new Font("Segoe UI", 10F) };
            txtSearch.KeyDown += txtSearch_KeyDown;

            btnSearch = new Button { Location = new Point(320, 28), Size = new Size(80, 25), Text = "Search" };
            btnSearch.Click += btnSearch_Click;

            dataGridViewUsers = new DataGridView
            {
                Location = new Point(12, 60),
                Size = new Size(760, 300),
                AllowUserToAddRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = !_singleSelection,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dataGridViewUsers.CellValueChanged += dataGridViewUsers_CellValueChanged;
            dataGridViewUsers.CurrentCellDirtyStateChanged += dataGridViewUsers_CurrentCellDirtyStateChanged;
            dataGridViewUsers.KeyDown += dataGridViewUsers_KeyDown;
            dataGridViewUsers.ColumnHeaderMouseClick += dataGridViewUsers_ColumnHeaderMouseClick;

            lblSelected = new Label { Location = new Point(12, 370), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            lblSelected.Text = _singleSelection ? "No user selected" : "No users selected";

            btnOK = new Button { Location = new Point(600, 400), Size = new Size(80, 30), Text = "OK" };
            btnOK.Click += btnOK_Click;

            btnCancel = new Button { Location = new Point(690, 400), Size = new Size(80, 30), Text = "Cancel" };
            btnCancel.Click += btnCancel_Click;

            Controls.AddRange(new Control[] { lblNote, txtSearch, btnSearch, dataGridViewUsers, lblSelected, btnOK, btnCancel });
            ClientSize = new Size(780, 450);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Text = _singleSelection ? "Browse and Select User" : "Browse and Select Users";
            StartPosition = FormStartPosition.CenterParent;
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSearch.PerformClick();
                e.SuppressKeyPress = true;
            }
        }

        private void dataGridViewUsers_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && dataGridViewUsers.CurrentRow != null)
            {
                int rowIndex = dataGridViewUsers.CurrentRow.Index;
                var displayItem = _currentDisplayItems[rowIndex];
                bool newValue = !displayItem.Select;

                if (_singleSelection)
                {
                    foreach (var item in _currentDisplayItems)
                    {
                        item.Select = false;
                    }
                }
                displayItem.Select = newValue;

                var user = _allResults.FirstOrDefault(u => u.Id == displayItem.UserId);
                if (_singleSelection)
                {
                    _selectedUsers.Clear();
                    if (newValue && user != null)
                    {
                        _selectedUsers.Add(user);
                    }
                    lblSelected.Text = user != null && newValue ? $"{user.DisplayName} ({user.UserPrincipalName})" : "No user selected";
                }
                else
                {
                    if (newValue && user != null && !_selectedUsers.Any(u => u.Id == user.Id))
                    {
                        _selectedUsers.Add(user);
                    }
                    else if (!newValue)
                    {
                        _selectedUsers.RemoveAll(u => u.Id == user.Id);
                    }
                    lblSelected.Text = _selectedUsers.Any() ? string.Join(", ", _selectedUsers.Select(u => $"{u.DisplayName} ({u.UserPrincipalName})")) : "No users selected";
                }

                dataGridViewUsers.Refresh();
                e.Handled = true;
            }
        }

        private void dataGridViewUsers_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dataGridViewUsers.Columns[e.ColumnIndex].Name == "FirstName")
            {
                _firstNameSortState = (_firstNameSortState + 1) % 3;

                if (_firstNameSortState == 1)
                {
                    _currentDisplayItems = _currentDisplayItems.OrderBy(item => item.FirstName).ToList();
                }
                else if (_firstNameSortState == 2)
                {
                    _currentDisplayItems = _currentDisplayItems.OrderByDescending(item => item.FirstName).ToList();
                }
                else
                {
                    _currentDisplayItems = _currentDisplayItems.OrderBy(item => item.DisplayName).ToList();
                }

                dataGridViewUsers.DataSource = null;
                dataGridViewUsers.DataSource = _currentDisplayItems;

                if (dataGridViewUsers.Columns["UserId"] != null)
                {
                    dataGridViewUsers.Columns["UserId"].Visible = false;
                }

                if (dataGridViewUsers.Rows.Count > 0)
                {
                    dataGridViewUsers.CurrentCell = dataGridViewUsers.Rows[0].Cells[0];
                    dataGridViewUsers.Rows[0].Selected = true;
                    dataGridViewUsers.Focus();
                }
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            await LoadUsersAsync(txtSearch.Text.Trim());
        }

        private async Task LoadUsersAsync(string query)
        {
            try
            {
                _allResults.Clear();
                _currentDisplayItems.Clear();
                dataGridViewUsers.DataSource = null;
                _firstNameSortState = 0;
                _selectedUsers.Clear();
                lblSelected.Text = _singleSelection ? "No user selected" : "No users selected";

                if (string.IsNullOrWhiteSpace(query))
                {
                    MessageBox.Show("Please enter a search term.");
                    return;
                }

                string filter = string.Empty;

                if (query.Contains("@"))
                {
                    filter = $"mail eq '{query}' or userPrincipalName eq '{query}'";
                }
                else
                {
                    if (query.Contains(","))
                    {
                        var parts = query.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                        if (parts.Length >= 2)
                        {
                            filter = $"startswith(surname, '{parts[0]}') and startswith(givenName, '{parts[1]}')";
                        }
                    }
                    else if (query.Contains(" "))
                    {
                        var parts = query.Split(' ').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                        if (parts.Length == 2)
                        {
                            filter = $"(startswith(givenName, '{parts[0]}') and startswith(surname, '{parts[1]}')) or " +
                                     $"(startswith(surname, '{parts[0]}') and startswith(givenName, '{parts[1]}'))";
                        }
                        else if (parts.Length > 2)
                        {
                            filter = $"startswith(givenName, '{parts[0]}') and startswith(surname, '{parts[parts.Length - 1]}')";
                        }
                    }

                    if (string.IsNullOrEmpty(filter))
                    {
                        filter = $"startswith(displayName, '{query}') or " +
                                 $"startswith(givenName, '{query}') or " +
                                 $"startswith(surname, '{query}') or " +
                                 $"startswith(userPrincipalName, '{query}') or " +
                                 $"startswith(mail, '{query}') or " +
                                 $"startswith(mailNickname, '{query}')";
                    }
                }

                var requestConfig = new Action<Microsoft.Kiota.Abstractions.RequestConfiguration<Microsoft.Graph.Users.UsersRequestBuilder.UsersRequestBuilderGetQueryParameters>>(config =>
                {
                    config.QueryParameters.Select = new[] { "id", "displayName", "givenName", "surname", "userPrincipalName", "mail", "jobTitle", "department" };
                    config.QueryParameters.Top = 999;
                    config.QueryParameters.Count = true;
                    config.Headers.Add("ConsistencyLevel", "eventual");
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Orderby = new[] { "displayName" };
                });

                var response = await _graphClient.Users.GetAsync(requestConfig);
                if (response?.Value != null)
                {
                    var pageIterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(_graphClient, response, user =>
                    {
                        _allResults.Add(user);
                        return true;
                    });

                    await pageIterator.IterateAsync();
                }

                _currentDisplayItems = _allResults.Select(u => new UserDisplayItem
                {
                    Select = _selectedUsers.Any(s => s.Id == u.Id),
                    FirstName = u.GivenName,
                    LastName = u.Surname,
                    DisplayName = u.DisplayName,
                    Email = u.Mail ?? u.UserPrincipalName,
                    JobTitle = u.JobTitle,
                    Department = u.Department,
                    UserId = u.Id
                }).ToList();

                dataGridViewUsers.DataSource = null;
                dataGridViewUsers.DataSource = _currentDisplayItems;

                if (dataGridViewUsers.Columns["Select"] == null)
                {
                    var checkBoxColumn = new DataGridViewCheckBoxColumn
                    {
                        Name = "Select",
                        HeaderText = "Select",
                        DataPropertyName = "Select"
                    };
                    dataGridViewUsers.Columns.Insert(0, checkBoxColumn);
                }

                if (dataGridViewUsers.Columns["UserId"] != null)
                {
                    dataGridViewUsers.Columns["UserId"].Visible = false;
                }

                dataGridViewUsers.Columns["FirstName"].HeaderText = "First Name";
                dataGridViewUsers.Columns["LastName"].HeaderText = "Last Name";
                dataGridViewUsers.Columns["DisplayName"].HeaderText = "Display Name";
                dataGridViewUsers.Columns["Email"].HeaderText = "Email";
                dataGridViewUsers.Columns["JobTitle"].HeaderText = "Job Title";
                dataGridViewUsers.Columns["Department"].HeaderText = "Department";

                if (dataGridViewUsers.Rows.Count > 0)
                {
                    dataGridViewUsers.CurrentCell = dataGridViewUsers.Rows[0].Cells[0];
                    dataGridViewUsers.Rows[0].Selected = true;
                    dataGridViewUsers.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridViewUsers_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewUsers.IsCurrentCellDirty)
                dataGridViewUsers.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridViewUsers_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dataGridViewUsers.Columns["Select"].Index && e.RowIndex >= 0)
            {
                var displayItem = _currentDisplayItems[e.RowIndex];
                bool newValue = displayItem.Select;
                var user = _allResults.FirstOrDefault(u => u.Id == displayItem.UserId);

                if (_singleSelection)
                {
                    foreach (var item in _currentDisplayItems)
                    {
                        item.Select = false;
                    }
                    displayItem.Select = newValue;
                    _selectedUsers.Clear();
                    if (newValue && user != null)
                    {
                        _selectedUsers.Add(user);
                    }
                    lblSelected.Text = user != null && newValue ? $"{user.DisplayName} ({user.UserPrincipalName})" : "No user selected";
                }
                else
                {
                    if (newValue && user != null && !_selectedUsers.Any(u => u.Id == user.Id))
                    {
                        _selectedUsers.Add(user);
                    }
                    else if (!newValue)
                    {
                        _selectedUsers.RemoveAll(u => u.Id == user.Id);
                    }
                    lblSelected.Text = _selectedUsers.Any() ? string.Join(", ", _selectedUsers.Select(u => $"{u.DisplayName} ({u.UserPrincipalName})")) : "No users selected";
                }
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (_singleSelection)
            {
                SelectedUser = _selectedUsers.FirstOrDefault();
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private class UserDisplayItem
        {
            public bool Select { get; set; }
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