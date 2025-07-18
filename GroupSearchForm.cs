using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;

namespace EntraGroupsApp
{
    public partial class GroupSearchForm : Form
    {
        private GraphServiceClient _graphClient;
        private Form1 _mainForm;
        private readonly Dictionary<string, List<string>> _departmentPrefixes;
        private readonly List<string> _defaultOwnerIds = new List<string>
        {
            "8167862d-ba57-48c3-9bc5-6b06dbe85dc0",
            "d3f1bd3d-ddaf-435a-b03f-1596a8d0717a",
            "d5b40431-a7cf-4f8e-a696-79397bca15de",
            "49730f89-411a-4774-ba99-79b12564c80a",
            "ec2e7f12-9c02-4cd3-a52e-873df03dc596",
            "98623607-331d-4050-989e-9c58ddb14011",
            "246978c9-5f75-401b-a798-c9c26a44d6e2"
        };
        private List<Group>? _currentGroups;
        private bool _cleanedUp;
        private readonly AuditLogManager _auditLogManager;
        private readonly string _signedInUserId;
        private readonly ClaimsPrincipal _claimsPrincipal;
        private readonly string _adminUnitId = "d4d0b7e6-233c-41c0-939a-e271578427ca";

        public GroupSearchForm(GraphServiceClient graphClient, Form1 mainForm, AuditLogManager auditLogManager, string signedInUserId, ClaimsPrincipal claimsPrincipal)
        {
            InitializeComponent();
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _auditLogManager = auditLogManager ?? throw new ArgumentNullException(nameof(auditLogManager));
            _signedInUserId = signedInUserId ?? throw new ArgumentNullException(nameof(signedInUserId));
            _claimsPrincipal = claimsPrincipal ?? throw new ArgumentNullException(nameof(claimsPrincipal));
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
            _currentGroups = new List<Group>();
            listBoxGroups.SelectionMode = SelectionMode.MultiExtended;

            FormClosing += GroupSearchForm_FormClosing;
            listBoxGroups.SelectedIndexChanged += listBoxGroups_SelectedIndexChanged;
        }

        private void GroupSearchForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_cleanedUp)
                return;

            comboBoxDepartments.SelectedIndexChanged -= comboBoxDepartments_SelectedIndexChanged;
            comboBoxSortOrder.SelectedIndexChanged -= comboBoxSortOrder_SelectedIndexChanged;
            comboBoxGroupType.SelectedIndexChanged -= comboBoxGroupType_SelectedIndexChanged;
            btnAddGroup.Click -= btnAddGroup_Click;
            btnDeleteGroup.Click -= btnDeleteGroup_Click;
            btnManageMemberships.Click -= btnManageMemberships_Click;
            btnRefresh.Click -= btnRefresh_Click;
            btnReturn.Click -= btnReturn_Click;
            Load -= GroupSearchForm_Load;
            FormClosing -= GroupSearchForm_FormClosing;
            listBoxGroups.SelectedIndexChanged -= listBoxGroups_SelectedIndexChanged;

            _graphClient?.Dispose();
            _cleanedUp = true;
        }

        private async void GroupSearchForm_Load(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine($"GroupSearchForm_Load: SignedInUserId={_signedInUserId}");
                Debug.WriteLine($"GroupSearchForm_Load: ClaimsPrincipal is {(_claimsPrincipal != null ? "not null" : "null")}");

                // Check for department-specific roles
                string roleClaim = _claimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "roles")?.Value;
                Debug.WriteLine($"GroupSearchForm_Load: roleClaim={roleClaim}, Claims={(_claimsPrincipal != null ? string.Join(", ", _claimsPrincipal.Claims.Select(c => $"{c.Type}:{c.Value}")) : "No claims")}");

                switch (roleClaim)
                {
                    case "CLBA_ACCT":
                        await LoadAccountingRestrictedForm();
                        return;
                    case "CLBA_FINC":
                        await LoadFinanceRestrictedForm();
                        return;
                    case "CLBA_MGMT":
                        await LoadManagementRestrictedForm();
                        return;
                    case "CLBA_BizGrad":
                        await LoadBizGradRestrictedForm();
                        return;
                    case "CLBA_UAO":
                        await LoadUAORestrictedForm();
                        return;
                    case "CLBA_INFO":
                        await LoadINFORestrictedForm();
                        return;
                    case "CLBA_DEAN":
                        await LoadDeanRestrictedForm();
                        return;
                    case "CLBA_BUSP":
                        await LoadBUSPRestrictedForm();
                        return;
                    case "CLBA_COMM":
                        await LoadCOMMRestrictedForm();
                        return;
                    case "CLBA_CIBS":
                        await LoadCIBSRestrictedForm();
                        return;
                    case "CLBA_CED":
                        await LoadCEDRestrictedForm();
                        return;
                    case "CLBA_UAVS":
                        await LoadUAVSRestrictedForm();
                        return;
                    case "CLBA_MKTG":
                        await LoadMarketingRestrictedForm();
                        return;
                }

                // Original logic for users without specific department role
                string jsonFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EntraGroupsApp",
                    "WebView2",
                    "UserDepartmentAssignments.json");
                string jsonContent = File.Exists(jsonFilePath) ? File.ReadAllText(jsonFilePath) : "File not found";
                Debug.WriteLine($"GroupSearchForm_Load: JSON file contents={jsonContent}");

                string assignedDepartment = UserDepartmentManager.GetAssignedDepartment(_signedInUserId);
                Debug.WriteLine($"GroupSearchForm_Load: AssignedDepartment={assignedDepartment}");

                if (!string.IsNullOrEmpty(assignedDepartment) && _departmentPrefixes.ContainsKey(assignedDepartment))
                {
                    comboBoxDepartments.Items.Clear();
                    comboBoxDepartments.Items.Add(assignedDepartment);
                    comboBoxDepartments.SelectedItem = assignedDepartment;
                    comboBoxDepartments.Enabled = false;
                    lblStatus.Text = $"Department restricted to: {assignedDepartment}";
                    Debug.WriteLine($"GroupSearchForm_Load: Restricting to department {assignedDepartment}");
                    await PerformGroupSearch(assignedDepartment);
                }
                else
                {
                    comboBoxDepartments.Items.Clear();
                    comboBoxDepartments.Items.AddRange(_departmentPrefixes.Keys.ToArray());
                    comboBoxDepartments.SelectedIndex = -1;
                    lblStatus.Text = "Please select a department.";
                    Debug.WriteLine($"GroupSearchForm_Load: No valid department assigned, showing all departments");
                    await _auditLogManager.LogAction(_signedInUserId, null, "NoDepartmentAssigned", null, null, null, $"No valid department found for user {_signedInUserId}");
                }

                comboBoxSortOrder.Items.Clear();
                comboBoxSortOrder.Items.Add("A to Z (Ascending)");
                comboBoxSortOrder.Items.Add("Z to A (Descending)");
                comboBoxSortOrder.SelectedIndex = 0;

                comboBoxGroupType.Items.Clear();
                comboBoxGroupType.Items.Add("All");
                comboBoxGroupType.Items.Add("Folder Group");
                comboBoxGroupType.Items.Add("Subfolder Group");
                comboBoxGroupType.Items.Add("User Group");
                comboBoxGroupType.Items.Add("Root SharePoint Site");
                comboBoxGroupType.SelectedIndex = 0;

                btnManageMemberships.Enabled = false;
                btnDeleteGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading form: {ex.Message}";
                Debug.WriteLine($"GroupSearchForm_Load: Error={ex.Message}, StackTrace={ex.StackTrace}");
                await _auditLogManager.LogAction(_signedInUserId, null, "GroupSearchFormLoadError", null, null, null, $"Failed to load GroupSearchForm: {ex.Message}");
                MessageBox.Show($"Failed to load Group Search Form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadAccountingRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadAccountingRestrictedForm: Restricting to Accounting (ACCT) due to CLBA_ACCT role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Accounting (ACCT)");
                comboBoxDepartments.SelectedItem = "Accounting (ACCT)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Accounting (ACCT) (via CLBA_ACCT role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Accounting (ACCT) due to CLBA_ACCT role");
                await PerformGroupSearch("Accounting (ACCT)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Accounting", ex);
            }
        }

        private async Task LoadFinanceRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadFinanceRestrictedForm: Restricting to Finance (FINC) due to CLBA_FINC role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Finance (FINC)");
                comboBoxDepartments.SelectedItem = "Finance (FINC)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Finance (FINC) (via CLBA_FINC role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Finance (FINC) due to CLBA_FINC role");
                await PerformGroupSearch("Finance (FINC)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Finance", ex);
            }
        }

        private async Task LoadManagementRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadManagementRestrictedForm: Restricting to Management (MGMT) due to CLBA_MGMT role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Management (MGMT)");
                comboBoxDepartments.SelectedItem = "Management (MGMT)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Management (MGMT) (via CLBA_MGMT role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Management (MGMT) due to CLBA_MGMT role");
                await PerformGroupSearch("Management (MGMT)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Management", ex);
            }
        }

        private async Task LoadBizGradRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadBizGradRestrictedForm: Restricting to MBA Programs (BizGrad) due to CLBA_BizGrad role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("MBA Programs (BizGrad)");
                comboBoxDepartments.SelectedItem = "MBA Programs (BizGrad)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: MBA Programs (BizGrad) (via CLBA_BizGrad role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to MBA Programs (BizGrad) due to CLBA_BizGrad role");
                await PerformGroupSearch("MBA Programs (BizGrad)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("MBA Programs", ex);
            }
        }

        private async Task LoadUAORestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadUAORestrictedForm: Restricting to Business Undergraduate Advising Office (UAO) due to CLBA_UAO role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Business Undergraduate Advising Office (UAO)");
                comboBoxDepartments.SelectedItem = "Business Undergraduate Advising Office (UAO)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Business Undergraduate Advising Office (UAO) (via CLBA_UAO role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Business Undergraduate Advising Office (UAO) due to CLBA_UAO role");
                await PerformGroupSearch("Business Undergraduate Advising Office (UAO)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Business Undergraduate Advising Office", ex);
            }
        }

        private async Task LoadINFORestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadINFORestrictedForm: Restricting to Information & Operations Management (INFO) due to CLBA_INFO role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Information & Operations Management (INFO)");
                comboBoxDepartments.SelectedItem = "Information & Operations Management (INFO)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Information & Operations Management (INFO) (via CLBA_INFO role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Information & Operations Management (INFO) due to CLBA_INFO role");
                await PerformGroupSearch("Information & Operations Management (INFO)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Information & Operations Management", ex);
            }
        }

        private async Task LoadDeanRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadDeanRestrictedForm: Restricting to Dean's Office (DEAN) due to CLBA_DEAN role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Dean's Office (DEAN)");
                comboBoxDepartments.SelectedItem = "Dean's Office (DEAN)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Dean's Office (DEAN) (via CLBA_DEAN role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Dean's Office (DEAN) due to CLBA_DEAN role");
                await PerformGroupSearch("Dean's Office (DEAN)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Dean's Office", ex);
            }
        }

        private async Task LoadBUSPRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadBUSPRestrictedForm: Restricting to Business Undergraduate Special Programs (BUSP) due to CLBA_BUSP role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Business Undergraduate Special Programs (BUSP)");
                comboBoxDepartments.SelectedItem = "Business Undergraduate Special Programs (BUSP)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Business Undergraduate Special Programs (BUSP) (via CLBA_BUSP role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Business Undergraduate Special Programs (BUSP) due to CLBA_BUSP role");
                await PerformGroupSearch("Business Undergraduate Special Programs (BUSP)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Business Undergraduate Special Programs", ex);
            }
        }

        private async Task LoadCOMMRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadCOMMRestrictedForm: Restricting to Marcomm & Experience Team (COMM) due to CLBA_COMM role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Marcomm & Experience Team (COMM)");
                comboBoxDepartments.SelectedItem = "Marcomm & Experience Team (COMM)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Marcomm & Experience Team (COMM) (via CLBA_COMM role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Marcomm & Experience Team (COMM) due to CLBA_COMM role");
                await PerformGroupSearch("Marcomm & Experience Team (COMM)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Marcomm & Experience Team", ex);
            }
        }

        private async Task LoadCIBSRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadCIBSRestrictedForm: Restricting to Center for International Business Studies (CIBS) due to CLBA_CIBS role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Center for International Business Studies (CIBS)");
                comboBoxDepartments.SelectedItem = "Center for International Business Studies (CIBS)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Center for International Business Studies (CIBS) (via CLBA_CIBS role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Center for International Business Studies (CIBS) due to CLBA_CIBS role");
                await PerformGroupSearch("Center for International Business Studies (CIBS)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Center for International Business Studies", ex);
            }
        }

        private async Task LoadCEDRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadCEDRestrictedForm: Restricting to Center for Executive Development (CED) due to CLBA_CED role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Center for Executive Development (CED)");
                comboBoxDepartments.SelectedItem = "Center for Executive Development (CED)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Center for Executive Development (CED) (via CLBA_CED role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Center for Executive Development (CED) due to CLBA_CED role");
                await PerformGroupSearch("Center for Executive Development (CED)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Center for Executive Development", ex);
            }
        }

        private async Task LoadUAVSRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadUAVSRestrictedForm: Restricting to Media Office (UAVS) due to CLBA_UAVS role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Media Office (UAVS)");
                comboBoxDepartments.SelectedItem = "Media Office (UAVS)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Media Office (UAVS) (via CLBA_UAVS role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Media Office (UAVS) due to CLBA_UAVS role");
                await PerformGroupSearch("Media Office (UAVS)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Media Office", ex);
            }
        }

        private async Task LoadMarketingRestrictedForm()
        {
            try
            {
                Debug.WriteLine("LoadMarketingRestrictedForm: Restricting to Marketing (MKTG) due to CLBA_MKTG role");
                comboBoxDepartments.Items.Clear();
                comboBoxDepartments.Items.Add("Marketing (MKTG)");
                comboBoxDepartments.SelectedItem = "Marketing (MKTG)";
                comboBoxDepartments.Enabled = false;
                lblStatus.Text = "Department restricted to: Marketing (MKTG) (via CLBA_MKTG role)";
                await _auditLogManager.LogAction(_signedInUserId, null, "RoleBasedDepartmentRestriction", null, null, null, "Restricted to Marketing (MKTG) due to CLBA_MKTG role");
                await PerformGroupSearch("Marketing (MKTG)");

                SetupCommonControls();
                btnAddGroup.Enabled = false;
            }
            catch (Exception ex)
            {
                HandleRestrictedFormError("Marketing", ex);
            }
        }

        private void SetupCommonControls()
        {
            comboBoxSortOrder.Items.Clear();
            comboBoxSortOrder.Items.Add("A to Z (Ascending)");
            comboBoxSortOrder.Items.Add("Z to A (Descending)");
            comboBoxSortOrder.SelectedIndex = 0;

            comboBoxGroupType.Items.Clear();
            comboBoxGroupType.Items.Add("All");
            comboBoxGroupType.Items.Add("Folder Group");
            comboBoxGroupType.Items.Add("Subfolder Group");
            comboBoxGroupType.Items.Add("User Group");
            comboBoxGroupType.Items.Add("Root SharePoint Site");
            comboBoxGroupType.SelectedIndex = 0;
        }

        private void HandleRestrictedFormError(string department, Exception ex)
        {
            lblStatus.Text = $"Error loading restricted form for {department}: {ex.Message}";
            Debug.WriteLine($"Load{department.Replace(" ", "")}RestrictedForm: Error={ex.Message}, StackTrace={ex.StackTrace}");
            _auditLogManager.LogAction(_signedInUserId, null, $"Load{department.Replace(" ", "")}RestrictedFormError", null, null, null, $"Failed to load restricted form for {department}: {ex.Message}");
            MessageBox.Show($"Failed to load restricted Group Search Form for {department}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private async void comboBoxDepartments_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxDepartments.SelectedIndex == -1)
            {
                listBoxGroups.DataSource = null;
                btnManageMemberships.Enabled = false;
                btnDeleteGroup.Enabled = false;
                lblStatus.Text = "Please select a department.";
                _currentGroups = null;
                comboBoxGroupType.SelectedIndex = 0;
                return;
            }

            string selectedDept = comboBoxDepartments.SelectedItem?.ToString();
            if (selectedDept != null)
            {
                await PerformGroupSearch(selectedDept);
            }
        }

        private async Task PerformGroupSearch(string selectedDept)
        {
            try
            {
                List<Group> allGroups = new List<Group>();
                var prefixes = _departmentPrefixes[selectedDept];

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
                btnManageMemberships.Enabled = true;
                lblStatus.Text = "Groups loaded.";
                Debug.WriteLine($"PerformGroupSearch: Loaded {allGroups.Count} groups for department {selectedDept}");
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading groups: {ex.Message}";
                Debug.WriteLine($"PerformGroupSearch: Error={ex.Message}, StackTrace={ex.StackTrace}");
                await _auditLogManager.LogAction(_signedInUserId, null, "PerformGroupSearchError", null, selectedDept, "Department", $"Failed to load groups: {ex.Message}");
                MessageBox.Show($"Failed to load groups for {selectedDept}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplySortOrderAndTypeFilter()
        {
            if (_currentGroups == null || _currentGroups.Count == 0)
            {
                listBoxGroups.DataSource = null;
                return;
            }

            var filteredGroups = _currentGroups;
            string selectedDept = comboBoxDepartments.SelectedItem?.ToString();
            string deptCode = _departmentPrefixes.TryGetValue(selectedDept ?? string.Empty, out var prefixes)
                ? prefixes.FirstOrDefault(p => p.StartsWith("CSG-CLBA-"))?.Replace("CSG-CLBA-", "") ?? string.Empty
                : string.Empty;
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

        private async void comboBoxSortOrder_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySortOrderAndTypeFilter();
        }

        private async void comboBoxGroupType_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySortOrderAndTypeFilter();
        }

        private async void btnAddGroup_Click(object sender, EventArgs e)
        {
            await _auditLogManager.LogAction(
                _signedInUserId,
                "System",
                "CreateGroups",
                null,
                null,
                "Group",
                "Initiated group creation via PowerShell script").ConfigureAwait(false);

            await LaunchPowerShellGroupCreator();
        }

        private async void btnDeleteGroup_Click(object sender, EventArgs e)
        {
            if (listBoxGroups.SelectedItem == null)
            {
                MessageBox.Show("Please select a group to delete.");
                return;
            }

            var selectedGroup = (Group)listBoxGroups.SelectedItem;
            var result = MessageBox.Show(
                $"Are you sure you want to delete '{selectedGroup.DisplayName}'?\n" +
                "WARNING: This will delete all access to files/folders for users assigned to this group.",
                "Confirm Group Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    await _graphClient.Groups[selectedGroup.Id].DeleteAsync().ConfigureAwait(false);
                    lblStatus.Text = $"Group '{selectedGroup.DisplayName}' deleted.";

                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "System",
                        "DeleteGroups",
                        selectedGroup.DisplayName ?? "Unknown",
                        null,
                        "Group",
                        $"Deleted group {selectedGroup.DisplayName}").ConfigureAwait(false);

                    if (comboBoxDepartments.SelectedIndex != -1)
                    {
                        string selectedDept = comboBoxDepartments.SelectedItem?.ToString();
                        if (selectedDept != null)
                        {
                            await PerformGroupSearch(selectedDept).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting group: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Console.WriteLine($"Error in btnDeleteGroup_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                }
            }
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            if (comboBoxDepartments.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a department to refresh.");
                return;
            }

            string selectedDept = comboBoxDepartments.SelectedItem?.ToString();
            if (selectedDept != null)
            {
                try
                {
                    await PerformGroupSearch(selectedDept).ConfigureAwait(false);
                    await _auditLogManager.LogAction(
                        _signedInUserId,
                        "System",
                        "RefreshGroups",
                        null,
                        selectedDept,
                        "Department",
                        $"Refreshed group list for department {selectedDept}").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error refreshing groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Console.WriteLine($"Error in btnRefresh_Click: {ex.Message}\nStack Trace: {ex.StackTrace}");
                }
            }
        }

        private void btnManageMemberships_Click(object sender, EventArgs e)
        {
            var selectedGroups = listBoxGroups.SelectedItems.Cast<Group>().ToList();
            if (selectedGroups.Count == 0)
            {
                MessageBox.Show("Please select at least one group.");
                return;
            }
            var manageForm = new ManageMembershipsForm(selectedGroups, _graphClient, _mainForm, this, _auditLogManager, _signedInUserId);
            manageForm.Show();
            this.Hide();
        }

        private void btnReturn_Click(object sender, EventArgs e)
        {
            _mainForm.Show();
            this.Close();
        }

        private void OnGroupsSelected(List<Group> selected)
        {
            if (_mainForm is Form1 form1)
            {
                form1.SetSelectedGroups(selected);
                var manageForm = new ManageMembershipsForm(selected, _graphClient, form1, this, _auditLogManager, _signedInUserId);
                manageForm.Show();
                this.Hide();
            }
        }

        private async Task LaunchPowerShellGroupCreator()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "createGroupWithGraph.ps1");

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"PowerShell script not found at:\n{scriptPath}", "Script Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string pwshPath = FindPwshExecutable();
            if (string.IsNullOrEmpty(pwshPath))
            {
                MessageBox.Show("PowerShell 7 is required to run this script. Please download and install it from https://aka.ms/powershell.", "PowerShell 7 Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string zoneIdentifierPath = $"{scriptPath}:Zone.Identifier";
                if (File.Exists(zoneIdentifierPath))
                {
                    File.Delete(zoneIdentifierPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove Mark of the Web from script:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try
            {
                var checkModulePsi = new ProcessStartInfo
                {
                    FileName = pwshPath,
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"if (Get-Module -ListAvailable -Name Microsoft.Graph.Authentication) { exit 0 } else { exit 1 }\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var checkModuleProcess = System.Diagnostics.Process.Start(checkModulePsi))
                {
                    if (checkModuleProcess == null)
                    {
                        MessageBox.Show("Failed to start PowerShell process to check module.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    await Task.Run(() => checkModuleProcess.WaitForExit());

                    if (checkModuleProcess.ExitCode != 0)
                    {
                        var installModulePsi = new ProcessStartInfo
                        {
                            FileName = pwshPath,
                            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Install-Module -Name Microsoft.Graph.Authentication -Scope CurrentUser -Force -AllowClobber\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using (var installModuleProcess = System.Diagnostics.Process.Start(installModulePsi))
                        {
                            if (installModuleProcess == null)
                            {
                                MessageBox.Show("Failed to start PowerShell process to install module.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            string installOutput = await installModuleProcess.StandardOutput.ReadToEndAsync();
                            string installError = await installModuleProcess.StandardError.ReadToEndAsync();
                            await Task.Run(() => installModuleProcess.WaitForExit());

                            if (installModuleProcess.ExitCode != 0)
                            {
                                MessageBox.Show($"Failed to install Microsoft.Graph.Authentication module:\nError: {installError}\nOutput: {installOutput}", "Module Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking or installing Microsoft.Graph.Authentication module:\n{ex.Message}", "Module Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = pwshPath,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            try
            {
                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    if (process == null)
                    {
                        MessageBox.Show("Failed to start PowerShell process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    await Task.Run(() => process.WaitForExit());

                    this.Activate();
                    this.WindowState = FormWindowState.Normal;
                    if (comboBoxDepartments.SelectedIndex != -1)
                    {
                        string selectedDept = comboBoxDepartments.SelectedItem?.ToString();
                        if (selectedDept != null)
                        {
                            await PerformGroupSearch(selectedDept);
                        }
                    }
                    MessageBox.Show("Script execution completed.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing PowerShell script:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Activate();
                this.WindowState = FormWindowState.Normal;
            }
        }

        private string FindPwshExecutable()
        {
            string[] possiblePaths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PowerShell", "7", "pwsh.exe"),
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    string fullPath = Path.Combine(dir, "pwsh.exe");
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return string.Empty;
        }

        private void listBoxGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnDeleteGroup.Enabled = listBoxGroups.SelectedItems.Count > 0;
        }
    }
}