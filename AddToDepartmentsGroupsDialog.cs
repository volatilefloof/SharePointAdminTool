using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace EntraGroupsApp
{
    public partial class AddToDepartmentGroupsDialog : Form
    {
        private readonly GraphServiceClient _graphClient;
        private readonly Dictionary<string, List<string>> _departmentPrefixes;
        private readonly string _restrictedDepartment;
        private readonly ClaimsPrincipal _claimsPrincipal;
        public List<Group> SelectedGroups { get; private set; } = new List<Group>();
        private List<Group> _currentGroups = new List<Group>();

        public AddToDepartmentGroupsDialog(GraphServiceClient graphClient, Dictionary<string, List<string>> departmentPrefixes, ClaimsPrincipal claimsPrincipal, string restrictedDepartment = null)
        {
            try
            {
                InitializeComponent();
                _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
                _departmentPrefixes = departmentPrefixes ?? throw new ArgumentNullException(nameof(departmentPrefixes));
                _restrictedDepartment = restrictedDepartment;
                _claimsPrincipal = claimsPrincipal; // Allow null, handle below

                Debug.WriteLine($"AddToDepartmentGroupsDialog: claimsPrincipal is {(_claimsPrincipal != null ? "not null" : "null")}");

                // If a restricted department is specified, limit the dropdown to that department
                if (!string.IsNullOrEmpty(_restrictedDepartment) && _departmentPrefixes.ContainsKey(_restrictedDepartment))
                {
                    Debug.WriteLine($"AddToDepartmentGroupsDialog: Restricted to department {_restrictedDepartment}");
                    cmbDepartments.Items.Clear();
                    cmbDepartments.Items.Add(_restrictedDepartment);
                    cmbDepartments.SelectedItem = _restrictedDepartment;
                    cmbDepartments.Enabled = false;
                    // Trigger group loading immediately
                    cmbDepartments_SelectedIndexChanged(null, EventArgs.Empty);
                }
                else
                {
                    // Check for department-specific roles
                    string roleClaim = _claimsPrincipal?.Claims?.FirstOrDefault(c => c.Type == "roles")?.Value;
                    Debug.WriteLine($"AddToDepartmentGroupsDialog: roleClaim={roleClaim ?? "null"}");
                    string selectedDepartment = null;

                    if (roleClaim != null)
                    {
                        switch (roleClaim)
                        {
                            case "CLBA_ACCT":
                                selectedDepartment = "Accounting (ACCT)";
                                break;
                            case "CLBA_FINC":
                                selectedDepartment = "Finance (FINC)";
                                break;
                            case "CLBA_MGMT":
                                selectedDepartment = "Management (MGMT)";
                                break;
                            case "CLBA_BizGrad":
                                selectedDepartment = "MBA Programs (BizGrad)";
                                break;
                            case "CLBA_UAO":
                                selectedDepartment = "Business Undergraduate Advising Office (UAO)";
                                break;
                            case "CLBA_INFO":
                                selectedDepartment = "Information & Operations Management (INFO)";
                                break;
                            case "CLBA_DEAN":
                                selectedDepartment = "Dean's Office (DEAN)";
                                break;
                            case "CLBA_BUSP":
                                selectedDepartment = "Business Undergraduate Special Programs (BUSP)";
                                break;
                            case "CLBA_COMM":
                                selectedDepartment = "Marcomm & Experience Team (COMM)";
                                break;
                            case "CLBA_CIBS":
                                selectedDepartment = "Center for International Business Studies (CIBS)";
                                break;
                            case "CLBA_CED":
                                selectedDepartment = "Center for Executive Development (CED)";
                                break;
                            case "CLBA_UAVS":
                                selectedDepartment = "Media Office (UAVS)";
                                break;
                            case "CLBA_MKTG":
                                selectedDepartment = "Marketing (MKTG)";
                                break;
                            default:
                                Debug.WriteLine($"AddToDepartmentGroupsDialog: Unknown role claim {roleClaim}");
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(selectedDepartment) && _departmentPrefixes.ContainsKey(selectedDepartment))
                    {
                        Debug.WriteLine($"AddToDepartmentGroupsDialog: Restricting to department {selectedDepartment} based on role {roleClaim}");
                        cmbDepartments.Items.Clear();
                        cmbDepartments.Items.Add(selectedDepartment);
                        cmbDepartments.SelectedItem = selectedDepartment;
                        cmbDepartments.Enabled = false;
                        // Trigger group loading immediately
                        cmbDepartments_SelectedIndexChanged(null, EventArgs.Empty);
                    }
                    else
                    {
                        Debug.WriteLine("AddToDepartmentGroupsDialog: No restricted department or valid role claim, showing all departments");
                        cmbDepartments.Items.AddRange(_departmentPrefixes.Keys.ToArray());
                        cmbDepartments.SelectedIndex = -1;
                    }
                }

                cmbCategory.Items.AddRange(new object[]
                {
                    "All", "Folder Group", "Subfolder Group", "User Group", "Root SharePoint Site"
                });
                cmbCategory.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddToDepartmentGroupsDialog: Constructor error: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to initialize dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private void cmbDepartments_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                listGroups.DataSource = null;
                if (cmbDepartments.SelectedIndex == -1) return;

                // Get matching groups
                string deptName = cmbDepartments.SelectedItem.ToString();
                var prefixes = _departmentPrefixes[deptName];
                List<Group> allGroups = new List<Group>();

                foreach (var prefix in prefixes)
                {
                    var groupsPage = _graphClient.Groups.GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Filter = $"startswith(displayName,'{prefix}')";
                        cfg.QueryParameters.Select = new[] { "id", "displayName" };
                    }).GetAwaiter().GetResult();
                    if (groupsPage?.Value != null)
                        allGroups.AddRange(groupsPage.Value);
                }

                _currentGroups = allGroups;
                ApplyCategoryFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"cmbDepartments_SelectedIndexChanged: Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to load groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyCategoryFilter();
        }

        private void ApplyCategoryFilter()
        {
            try
            {
                if (_currentGroups == null) return;
                string selectedDept = cmbDepartments.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrEmpty(selectedDept) || !_departmentPrefixes.ContainsKey(selectedDept))
                {
                    listGroups.DataSource = null;
                    return;
                }
                var prefixes = _departmentPrefixes[selectedDept];
                string fsgPrefix = prefixes.FirstOrDefault(p => p.StartsWith("FSG-", StringComparison.OrdinalIgnoreCase)) ?? "";
                string csgPrefix = prefixes.FirstOrDefault(p => p.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase)) ?? "";
                var filtered = _currentGroups;

                switch (cmbCategory.SelectedItem?.ToString())
                {
                    case "Folder Group":
                        filtered = filtered.Where(g => g.DisplayName != null && g.DisplayName.StartsWith(fsgPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Subfolder Group":
                        filtered = filtered.Where(g => g.DisplayName != null && g.DisplayName.StartsWith(csgPrefix, StringComparison.OrdinalIgnoreCase) && !g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase)).ToList();
                        break;
                    case "User Group":
                        filtered = filtered.Where(g => g.DisplayName != null && g.DisplayName.StartsWith(csgPrefix, StringComparison.OrdinalIgnoreCase) && g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase) && !g.DisplayName.Contains("ReadOnly SharePoint Site (Limited)", StringComparison.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Root SharePoint Site":
                        filtered = filtered.Where(g => g.DisplayName != null && g.DisplayName.StartsWith($"{csgPrefix}-mays-group", StringComparison.OrdinalIgnoreCase) && g.DisplayName.Contains("ReadOnly SharePoint Site (Limited)", StringComparison.OrdinalIgnoreCase)).ToList();
                        break;
                    default:
                        break; // show all
                }

                listGroups.DataSource = filtered;
                listGroups.DisplayMember = "DisplayName";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyCategoryFilter: Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to filter groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                SelectedGroups = listGroups.SelectedItems.Cast<Group>().ToList();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"btnOk_Click: Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to select groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}