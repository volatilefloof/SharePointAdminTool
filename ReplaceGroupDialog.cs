using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;

namespace EntraGroupsApp
{
    public partial class ReplaceGroupDialog : Form
    {
        private readonly GraphServiceClient _graphClient;
        private readonly List<Group> _groups;
        private readonly List<Group> _excludedGroups;
        private readonly ClaimsPrincipal _claimsPrincipal;
        private readonly Dictionary<string, List<string>> _departmentPrefixes;

        public Group SelectedGroup { get; private set; }

        private Label lblInstructions;
        private ComboBox cmbGroups;
        private Button btnOK;
        private Button btnCancel;

        public ReplaceGroupDialog(GraphServiceClient graphClient)
            : this(graphClient, new List<Group>(), null) { }

        public ReplaceGroupDialog(GraphServiceClient graphClient, List<Group> excludedGroups)
            : this(graphClient, excludedGroups, null) { }

        public ReplaceGroupDialog(GraphServiceClient graphClient, List<Group> excludedGroups, ClaimsPrincipal claimsPrincipal)
        {
            try
            {
                _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
                _excludedGroups = excludedGroups ?? throw new ArgumentNullException(nameof(excludedGroups));
                _claimsPrincipal = claimsPrincipal;
                _groups = new List<Group>();
                SelectedGroup = null;

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

                InitializeComponent();
                LoadGroupsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReplaceGroupDialog: Constructor error: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to initialize dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void InitializeComponent()
        {
            lblInstructions = new Label();
            cmbGroups = new ComboBox();
            btnOK = new Button();
            btnCancel = new Button();
            SuspendLayout();

            lblInstructions.AutoSize = true;
            lblInstructions.Location = new Point(12, 12);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new Size(350, 20);
            lblInstructions.TabIndex = 0;
            lblInstructions.Text = "Select a group to replace the current group:";
            lblInstructions.Font = new Font("Segoe UI", 10F);
            lblInstructions.ForeColor = Color.Black;

            cmbGroups.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbGroups.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbGroups.Font = new Font("Segoe UI", 10F);
            cmbGroups.FormattingEnabled = true;
            cmbGroups.Location = new Point(12, 40);
            cmbGroups.Name = "cmbGroups";
            cmbGroups.Size = new Size(360, 28);
            cmbGroups.TabIndex = 1;

            btnOK.Location = new Point(212, 80);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(80, 30);
            btnOK.TabIndex = 2;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;

            btnCancel.Location = new Point(298, 80);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(80, 30);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(390, 120);
            Controls.Add(lblInstructions);
            Controls.Add(cmbGroups);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ReplaceGroupDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Select Replacement Group";
            ResumeLayout(false);
            PerformLayout();
        }

        private async void LoadGroupsAsync()
        {
            try
            {
                var filteredGroups = new List<Group>();
                string roleClaim = _claimsPrincipal?.Claims?.FirstOrDefault(c => c.Type.Equals("roles", StringComparison.OrdinalIgnoreCase))?.Value;

                string selectedDepartment = null;
                List<string> allowedPrefixes = null;

                if (!string.IsNullOrEmpty(roleClaim))
                {
                    switch (roleClaim.ToUpperInvariant())
                    {
                        case "CLBA_ACCT": selectedDepartment = "Accounting (ACCT)"; break;
                        case "CLBA_FINC": selectedDepartment = "Finance (FINC)"; break;
                        case "CLBA_MGMT": selectedDepartment = "Management (MGMT)"; break;
                        case "CLBA_BIZGRAD": selectedDepartment = "MBA Programs (BizGrad)"; break;
                        case "CLBA_UAO": selectedDepartment = "Business Undergraduate Advising Office (UAO)"; break;
                        case "CLBA_INFO": selectedDepartment = "Information & Operations Management (INFO)"; break;
                        case "CLBA_DEAN": selectedDepartment = "Dean's Office (DEAN)"; break;
                        case "CLBA_BUSP": selectedDepartment = "Business Undergraduate Special Programs (BUSP)"; break;
                        case "CLBA_COMM": selectedDepartment = "Marcomm & Experience Team (COMM)"; break;
                        case "CLBA_CIBS": selectedDepartment = "Center for International Business Studies (CIBS)"; break;
                        case "CLBA_CED": selectedDepartment = "Center for Executive Development (CED)"; break;
                        case "CLBA_UAVS": selectedDepartment = "Media Office (UAVS)"; break;
                        case "CLBA_MKTG": selectedDepartment = "Marketing (MKTG)"; break;
                        default: break;
                    }

                    if (!string.IsNullOrEmpty(selectedDepartment) && _departmentPrefixes.ContainsKey(selectedDepartment))
                        allowedPrefixes = _departmentPrefixes[selectedDepartment];
                }

                if (allowedPrefixes == null)
                    allowedPrefixes = _departmentPrefixes.Values.SelectMany(p => p).ToList();

                foreach (var prefix in allowedPrefixes)
                {
                    var groupsRequest = await _graphClient.Groups.GetAsync(request =>
                    {
                        request.QueryParameters.Select = new[] { "id", "displayName" };
                        request.QueryParameters.Filter = $"startswith(displayName, '{prefix}')";
                    });

                    if (groupsRequest?.Value != null)
                        filteredGroups.AddRange(groupsRequest.Value);
                }

                filteredGroups = filteredGroups
                    .Where(g => g.DisplayName != null && !_excludedGroups.Any(eg => eg.Id == g.Id))
                    .OrderBy(g => g.DisplayName)
                    .ToList();

                if (!filteredGroups.Any())
                {
                    BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show($"No groups available for department {selectedDepartment ?? "all departments"}.", "No Groups Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    }));
                    return;
                }

                BeginInvoke(new Action(() =>
                {
                    _groups.Clear();
                    _groups.AddRange(filteredGroups);
                    cmbGroups.DataSource = null;
                    cmbGroups.DataSource = _groups;
                    cmbGroups.DisplayMember = "DisplayName";
                    cmbGroups.ValueMember = "Id";
                    cmbGroups.Refresh();
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReplaceGroupDialog.LoadGroupsAsync: Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                BeginInvoke(new Action(() =>
                {
                    MessageBox.Show($"Error loading groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }));
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbGroups.SelectedItem is Group selectedGroup)
                {
                    SelectedGroup = selectedGroup;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Please select a group.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReplaceGroupDialog.btnOK_Click: Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Error selecting group: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
