using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace EntraGroupsApp
{
    public partial class CopyToGroupForm : Form
    {
        private readonly GraphServiceClient _graphClient;
        private readonly string _department;
        private readonly Dictionary<string, List<string>> _departmentPrefixes;
        private List<Group> _currentGroups;
        public List<Group> SelectedGroups { get; private set; }

        public CopyToGroupForm(string department, GraphServiceClient graphClient, Form owner)
        {
            InitializeComponent();
            _department = department ?? throw new ArgumentNullException(nameof(department));
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            Owner = owner;
            _currentGroups = new List<Group>();
            SelectedGroups = new List<Group>();
            _departmentPrefixes = new Dictionary<string, List<string>>
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

            listBoxGroups.SelectionMode = SelectionMode.MultiExtended;
        }

        private async void CopyToGroupForm_Load(object sender, EventArgs e)
        {
            lblDepartment.Text = $"Department: {_department}";
            comboBoxGroupType.Items.AddRange(new[] { "All", "Folder Group", "Subfolder Group", "User Group", "Root SharePoint Site" });
            comboBoxGroupType.SelectedIndex = 0;
            comboBoxSortOrder.Items.AddRange(new[] { "A to Z (Ascending)", "Z to A (Descending)" });
            comboBoxSortOrder.SelectedIndex = 0;

            await LoadGroups();
        }

        private async Task LoadGroups()
        {
            try
            {
                List<Group> allGroups = new List<Group>();
                var prefixes = _departmentPrefixes[_department];

                foreach (var prefix in prefixes)
                {
                    var groups = await _graphClient.Groups.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"startswith(displayName,'{prefix}')";
                        requestConfiguration.QueryParameters.Select = new[] { "id", "displayName" };
                    });

                    if (groups?.Value != null)
                    {
                        allGroups.AddRange(groups.Value);
                    }
                }

                _currentGroups = allGroups;
                ApplySortOrderAndTypeFilter();
                lblStatus.Text = "Groups loaded.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading groups: {ex.Message}");
                lblStatus.Text = "Error loading groups.";
                _currentGroups = new List<Group>();
            }
        }

        private void ApplySortOrderAndTypeFilter()
        {
            if (!_currentGroups.Any())
            {
                listBoxGroups.DataSource = null;
                return;
            }

            var filteredGroups = _currentGroups;
            string deptCode = _departmentPrefixes[_department].FirstOrDefault(p => p.StartsWith("CSG-CLBA-"))?.Replace("CSG-CLBA-", "") ?? string.Empty;
            string csgPrefix = $"CSG-CLBA-{deptCode}";
            string fsgPrefix = $"FSG-CLBA-{deptCode}";

            if (comboBoxGroupType.SelectedIndex > 0)
            {
                string groupType = comboBoxGroupType.SelectedItem?.ToString() ?? string.Empty;
                filteredGroups = groupType switch
                {
                    "Folder Group" => filteredGroups.Where(g => g.DisplayName != null && g.DisplayName.StartsWith(fsgPrefix, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "Subfolder Group" => filteredGroups.Where(g => g.DisplayName != null && g.DisplayName.StartsWith(csgPrefix, StringComparison.OrdinalIgnoreCase) && !g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase)).ToList(),
                    "User Group" => filteredGroups.Where(g => g.DisplayName != null && g.DisplayName.StartsWith(csgPrefix, StringComparison.OrdinalIgnoreCase) && g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase) && !g.DisplayName.Contains("ReadOnly SharePoint Site (Limited)", StringComparison.OrdinalIgnoreCase)).ToList(),
                    "Root SharePoint Site" => filteredGroups.Where(g => g.DisplayName != null && g.DisplayName.StartsWith($"{csgPrefix}-mays-group", StringComparison.OrdinalIgnoreCase) && g.DisplayName.Contains("ReadOnly SharePoint Site (Limited)", StringComparison.OrdinalIgnoreCase)).ToList(),
                    _ => filteredGroups
                };
            }

            var sortedGroups = comboBoxSortOrder.SelectedIndex == 0
                ? filteredGroups.OrderBy(g => g.DisplayName).ToList()
                : filteredGroups.OrderByDescending(g => g.DisplayName).ToList();

            listBoxGroups.DataSource = sortedGroups;
            listBoxGroups.DisplayMember = "DisplayName";
        }

        private void comboBoxGroupType_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySortOrderAndTypeFilter();
        }

        private void comboBoxSortOrder_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySortOrderAndTypeFilter();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SelectedGroups = listBoxGroups.SelectedItems.Cast<Group>().ToList();
            if (!SelectedGroups.Any())
            {
                MessageBox.Show("Please select at least one group.");
                return;
            }
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