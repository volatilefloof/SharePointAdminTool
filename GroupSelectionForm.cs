using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;

namespace EntraGroupsApp
{
    public partial class GroupSelectionForm : Form, IDisposable
    {
        private ListBox? listBoxGroups;
        private Button? btnOK;
        private Button? btnCancel;
        private readonly List<Microsoft.Graph.Models.Group> _groups;
        public List<string> SelectedGroupIds { get; private set; }
        public List<string> SelectedGroupNames { get; private set; }
        private bool _disposed;

        public GroupSelectionForm(List<Microsoft.Graph.Models.Group> groups)
        {
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            SelectedGroupIds = new List<string>();
            SelectedGroupNames = new List<string>();
            Debug.WriteLine("GroupSelectionForm constructor started");
            InitializeComponent();
            PopulateListBox();
            Debug.WriteLine("GroupSelectionForm constructor completed");
        }

        private void InitializeComponent()
        {
            Debug.WriteLine("InitializeComponent started");
            listBoxGroups = new ListBox();
            btnOK = new Button();
            btnCancel = new Button();

            // listBoxGroups
            listBoxGroups.Location = new System.Drawing.Point(12, 12);
            listBoxGroups.Size = new System.Drawing.Size(360, 200);
            listBoxGroups.SelectionMode = SelectionMode.MultiExtended;
            listBoxGroups.Font = new System.Drawing.Font("Segoe UI", 9F);
            listBoxGroups.HorizontalScrollbar = true;

            // btnOK
            btnOK.Location = new System.Drawing.Point(216, 220);
            btnOK.Size = new System.Drawing.Size(75, 30);
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += (s, e) => BtnOK_Click(s!, e);

            // btnCancel
            btnCancel.Location = new System.Drawing.Point(297, 220);
            btnCancel.Size = new System.Drawing.Size(75, 30);
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += (s, e) => BtnCancel_Click(s!, e);

            // Form settings
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 270);
            this.Controls.Add(listBoxGroups);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GroupSelectionForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Select Nested Groups";
            this.Load += (s, e) => GroupSelectionForm_Load(s!, e);
            Debug.WriteLine("InitializeComponent completed");
        }

        private void GroupSelectionForm_Load(object sender, EventArgs e)
        {
            Debug.WriteLine("GroupSelectionForm_Load started");
            this.Location = new System.Drawing.Point(
                (Screen.PrimaryScreen.WorkingArea.Width - this.Width) / 2,
                (Screen.PrimaryScreen.WorkingArea.Height - this.Height) / 2);
            Debug.WriteLine($"GroupSelectionForm loaded at position: {this.Location}");
        }

        private void PopulateListBox()
        {
            if (listBoxGroups == null) return;
            Debug.WriteLine("PopulateListBox started");

            listBoxGroups.Items.Clear();
            var filteredGroups = _groups
                .Where(g => g.DisplayName != null &&
                            g.DisplayName.StartsWith("CSG-CLBA-", StringComparison.OrdinalIgnoreCase) &&
                            g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.DisplayName)
                .ToList();

            listBoxGroups.DataSource = filteredGroups;
            listBoxGroups.DisplayMember = "DisplayName";

            Debug.WriteLine($"Populated ListBox with {listBoxGroups.Items.Count} groups");
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("BtnOK_Click started");
            if (listBoxGroups?.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one group.");
                return;
            }

            SelectedGroupIds.Clear();
            SelectedGroupNames.Clear();

            foreach (var item in listBoxGroups.SelectedItems.Cast<Microsoft.Graph.Models.Group>())
            {
                if (item.Id != null && item.DisplayName != null)
                {
                    SelectedGroupIds.Add(item.Id);
                    SelectedGroupNames.Add(item.DisplayName);
                }
            }

            DialogResult = DialogResult.OK;
            Close();
            Debug.WriteLine($"Selected {SelectedGroupNames.Count} groups: {string.Join(", ", SelectedGroupNames)}");
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("BtnCancel_Click started");
            DialogResult = DialogResult.Cancel;
            Close();
            Debug.WriteLine("Group selection cancelled");
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (btnOK != null) btnOK.Click -= BtnOK_Click;
                if (btnCancel != null) btnCancel.Click -= BtnCancel_Click;
                Load -= GroupSelectionForm_Load;

                listBoxGroups?.Dispose();
                btnOK?.Dispose();
                btnCancel?.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        ~GroupSelectionForm()
        {
            Dispose(false);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}