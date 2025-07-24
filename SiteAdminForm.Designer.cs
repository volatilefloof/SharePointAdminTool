namespace EntraGroupsApp
{
    partial class SiteAdminForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Panel panelDepartment;
        private System.Windows.Forms.Label lblDepartments;
        private System.Windows.Forms.ComboBox cmbDepartments;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.Button btnReturn;
        private System.Windows.Forms.ListView lstLibraries;
        private System.Windows.Forms.ColumnHeader colLibraryName;
        private System.Windows.Forms.ColumnHeader colPermissions;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button btnCreateLibrary;
        private System.Windows.Forms.Button btnApplyGroup;
        private System.Windows.Forms.Button btnDeleteLibrary;
        private System.Windows.Forms.Button btnAddToNavigation;
        private System.Windows.Forms.Button btnRemoveFromNavigation;
        private System.Windows.Forms.Button btnExportPermissions;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnViewSubfolderPermissions;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ContextMenuStrip contextMenuLibraries;
        private System.Windows.Forms.ToolStripMenuItem menuItemViewSubfolderPermissions;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.panelDepartment = new System.Windows.Forms.Panel();
            this.lblDepartments = new System.Windows.Forms.Label();
            this.cmbDepartments = new System.Windows.Forms.ComboBox();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnReturn = new System.Windows.Forms.Button();
            this.lstLibraries = new System.Windows.Forms.ListView();
            this.colLibraryName = new System.Windows.Forms.ColumnHeader();
            this.colPermissions = new System.Windows.Forms.ColumnHeader();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCreateLibrary = new System.Windows.Forms.Button();
            this.btnApplyGroup = new System.Windows.Forms.Button();
            this.btnDeleteLibrary = new System.Windows.Forms.Button();
            this.btnAddToNavigation = new System.Windows.Forms.Button();
            this.btnRemoveFromNavigation = new System.Windows.Forms.Button();
            this.btnExportPermissions = new System.Windows.Forms.Button();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.btnViewSubfolderPermissions = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.contextMenuLibraries = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemViewSubfolderPermissions = new System.Windows.Forms.ToolStripMenuItem();

            // contextMenuLibraries
            this.contextMenuLibraries.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuItemViewSubfolderPermissions
            });
            this.contextMenuLibraries.Name = "contextMenuLibraries";
            this.contextMenuLibraries.Size = new System.Drawing.Size(230, 26);

            // menuItemViewSubfolderPermissions
            this.menuItemViewSubfolderPermissions.Name = "menuItemViewSubfolderPermissions";
            this.menuItemViewSubfolderPermissions.Size = new System.Drawing.Size(229, 22);
            this.menuItemViewSubfolderPermissions.Text = "View Subfolder Permissions";
            this.menuItemViewSubfolderPermissions.Click += new System.EventHandler(this.menuItemViewSubfolderPermissions_Click);

            // tableLayoutPanel
            this.tableLayoutPanel.ColumnCount = 1;
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.RowCount = 5;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.Size = new System.Drawing.Size(1300, 700);
            this.tableLayoutPanel.TabIndex = 0;

            // panelDepartment
            this.panelDepartment.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDepartment.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);

            // lblDepartments
            this.lblDepartments.AutoSize = true;
            this.lblDepartments.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular);
            this.lblDepartments.Location = new System.Drawing.Point(0, 15);
            this.lblDepartments.Name = "lblDepartments";
            this.lblDepartments.Size = new System.Drawing.Size(110, 23);
            this.lblDepartments.TabIndex = 1;
            this.lblDepartments.Text = "Department:";

            // cmbDepartments
            this.cmbDepartments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDepartments.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbDepartments.Location = new System.Drawing.Point(120, 12);
            this.cmbDepartments.Name = "cmbDepartments";
            this.cmbDepartments.Size = new System.Drawing.Size(300, 30);
            this.cmbDepartments.TabIndex = 2;
            this.cmbDepartments.SelectedIndexChanged += new System.EventHandler(this.cmbDepartments_SelectedIndexChanged);

            // btnLoad
            this.btnLoad.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnLoad.Location = new System.Drawing.Point(430, 10);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(80, 35);
            this.btnLoad.TabIndex = 3;
            this.btnLoad.Text = "Load";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Enabled = false;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);

            // btnReturn
            this.btnReturn.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnReturn.Location = new System.Drawing.Point(530, 10);
            this.btnReturn.Name = "btnReturn";
            this.btnReturn.Size = new System.Drawing.Size(100, 35);
            this.btnReturn.TabIndex = 4;
            this.btnReturn.Text = "Return";
            this.btnReturn.UseVisualStyleBackColor = true;
            this.btnReturn.Click += new System.EventHandler(this.btnReturn_Click);

            this.panelDepartment.Controls.Add(this.lblDepartments);
            this.panelDepartment.Controls.Add(this.cmbDepartments);
            this.panelDepartment.Controls.Add(this.btnLoad);
            this.panelDepartment.Controls.Add(this.btnReturn);

            // lstLibraries
            this.lstLibraries.FullRowSelect = true;
            this.lstLibraries.GridLines = true;
            this.lstLibraries.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colLibraryName, this.colPermissions });
            this.lstLibraries.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lstLibraries.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstLibraries.Name = "lstLibraries";
            this.lstLibraries.TabIndex = 5;
            this.lstLibraries.View = System.Windows.Forms.View.Details;
            this.lstLibraries.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lstLibraries.MultiSelect = true;
            this.lstLibraries.ContextMenuStrip = this.contextMenuLibraries;
            this.lstLibraries.MouseUp += new System.Windows.Forms.MouseEventHandler(this.lstLibraries_MouseUp);

            // colLibraryName
            this.colLibraryName.Text = "Document Library";
            this.colLibraryName.Width = 350;

            // colPermissions
            this.colPermissions.Text = "Permissions";
            this.colPermissions.Width = 900;

            // flowLayoutPanelButtons
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flowLayoutPanelButtons.WrapContents = false;
            this.flowLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(10, 10, 10, 10);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.TabIndex = 6;
            this.flowLayoutPanelButtons.AutoScroll = true;

            // btnCreateLibrary
            this.btnCreateLibrary.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnCreateLibrary.AutoSize = true;
            this.btnCreateLibrary.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnCreateLibrary.Margin = new System.Windows.Forms.Padding(5);
            this.btnCreateLibrary.TabIndex = 7;
            this.btnCreateLibrary.Text = "Create Library";
            this.btnCreateLibrary.UseVisualStyleBackColor = true;
            this.btnCreateLibrary.Click += new System.EventHandler(this.btnCreateLibrary_Click);

            // btnApplyGroup
            this.btnApplyGroup.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnApplyGroup.AutoSize = true;
            this.btnApplyGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnApplyGroup.Margin = new System.Windows.Forms.Padding(5);
            this.btnApplyGroup.TabIndex = 8;
            this.btnApplyGroup.Text = "Apply Group";
            this.btnApplyGroup.UseVisualStyleBackColor = true;
            this.btnApplyGroup.Click += new System.EventHandler(this.btnApplyGroup_Click);

            // btnDeleteLibrary
            this.btnDeleteLibrary.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnDeleteLibrary.AutoSize = true;
            this.btnDeleteLibrary.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnDeleteLibrary.Margin = new System.Windows.Forms.Padding(5);
            this.btnDeleteLibrary.TabIndex = 9;
            this.btnDeleteLibrary.Text = "Delete Library";
            this.btnDeleteLibrary.UseVisualStyleBackColor = true;
            this.btnDeleteLibrary.Click += new System.EventHandler(this.btnDeleteLibrary_Click);

            // btnAddToNavigation
            this.btnAddToNavigation.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnAddToNavigation.AutoSize = true;
            this.btnAddToNavigation.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAddToNavigation.Margin = new System.Windows.Forms.Padding(5);
            this.btnAddToNavigation.TabIndex = 10;
            this.btnAddToNavigation.Text = "Add to Navigation";
            this.btnAddToNavigation.UseVisualStyleBackColor = true;
            this.btnAddToNavigation.Click += new System.EventHandler(this.btnAddToNavigation_Click);

            // btnRemoveFromNavigation
            this.btnRemoveFromNavigation.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnRemoveFromNavigation.AutoSize = true;
            this.btnRemoveFromNavigation.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnRemoveFromNavigation.Margin = new System.Windows.Forms.Padding(5);
            this.btnRemoveFromNavigation.TabIndex = 11;
            this.btnRemoveFromNavigation.Text = "Remove from Navigation";
            this.btnRemoveFromNavigation.UseVisualStyleBackColor = true;
            this.btnRemoveFromNavigation.Click += new System.EventHandler(this.btnRemoveFromNavigation_Click);

            // btnExportPermissions
            this.btnExportPermissions.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnExportPermissions.AutoSize = true;
            this.btnExportPermissions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnExportPermissions.Margin = new System.Windows.Forms.Padding(5);
            this.btnExportPermissions.TabIndex = 12;
            this.btnExportPermissions.Text = "Export Permissions";
            this.btnExportPermissions.UseVisualStyleBackColor = true;
            this.btnExportPermissions.Click += new System.EventHandler(this.btnExportPermissions_Click);

            // btnSelectAll
            this.btnSelectAll.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnSelectAll.AutoSize = true;
            this.btnSelectAll.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSelectAll.Margin = new System.Windows.Forms.Padding(5);
            this.btnSelectAll.TabIndex = 13;
            this.btnSelectAll.Text = "Select All";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);

            // btnViewSubfolderPermissions
            this.btnViewSubfolderPermissions.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnViewSubfolderPermissions.AutoSize = true;
            this.btnViewSubfolderPermissions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnViewSubfolderPermissions.Margin = new System.Windows.Forms.Padding(5);
            this.btnViewSubfolderPermissions.TabIndex = 14;
            this.btnViewSubfolderPermissions.Text = "View Subfolder Permissions";
            this.btnViewSubfolderPermissions.UseVisualStyleBackColor = true;
            this.btnViewSubfolderPermissions.Click += new System.EventHandler(this.btnViewSubfolderPermissions_Click);

            // btnClose
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnClose.AutoSize = true;
            this.btnClose.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnClose.Margin = new System.Windows.Forms.Padding(5);
            this.btnClose.TabIndex = 15;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            // Add buttons to flowLayoutPanel
            this.flowLayoutPanelButtons.Controls.Add(this.btnCreateLibrary);
            this.flowLayoutPanelButtons.Controls.Add(this.btnApplyGroup);
            this.flowLayoutPanelButtons.Controls.Add(this.btnDeleteLibrary);
            this.flowLayoutPanelButtons.Controls.Add(this.btnAddToNavigation);
            this.flowLayoutPanelButtons.Controls.Add(this.btnRemoveFromNavigation);
            this.flowLayoutPanelButtons.Controls.Add(this.btnExportPermissions);
            this.flowLayoutPanelButtons.Controls.Add(this.btnSelectAll);
            this.flowLayoutPanelButtons.Controls.Add(this.btnViewSubfolderPermissions);
            this.flowLayoutPanelButtons.Controls.Add(this.btnClose);

            // progressBar
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressBar.Name = "progressBar";
            this.progressBar.TabIndex = 16;
            this.progressBar.Visible = false;

            // lblStatus
            this.lblStatus.AutoSize = false;
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.TabIndex = 17;
            this.lblStatus.Text = "Select a department";

            // Add controls to tableLayoutPanel
            this.tableLayoutPanel.Controls.Add(this.panelDepartment, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.lstLibraries, 0, 1);
            this.tableLayoutPanel.Controls.Add(this.flowLayoutPanelButtons, 0, 2);
            this.tableLayoutPanel.Controls.Add(this.progressBar, 0, 3);
            this.tableLayoutPanel.Controls.Add(this.lblStatus, 0, 4);

            // SiteAdminForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1300, 700);
            this.Controls.Add(this.tableLayoutPanel);
            this.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "SiteAdminForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Direct Site Administration";
            this.ResumeLayout(false);
        }
    }

    public partial class SubfolderPermissionsDialog : Form
    {
        public SubfolderPermissionsDialog(string libraryName, List<SubfolderPermissionInfo> subfolderPermissions)
        {
            InitializeComponent();
            this.Text = $"Subfolder Permissions - {libraryName}";
            lblLibraryName.Text = libraryName;

            foreach (var info in subfolderPermissions)
            {
                var item = new ListViewItem(info.SubfolderName);
                item.SubItems.Add(info.IsInherited ? "Inherited" : string.Join("; ", info.Permissions));
                lvPermissions.Items.Add(item);
            }
        }

        private void InitializeComponent()
        {
            this.lblLibraryName = new System.Windows.Forms.Label();
            this.lvPermissions = new System.Windows.Forms.ListView();
            this.colSubfolder = new System.Windows.Forms.ColumnHeader();
            this.colPermissions = new System.Windows.Forms.ColumnHeader();
            this.btnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // lblLibraryName
            this.lblLibraryName.AutoSize = true;
            this.lblLibraryName.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblLibraryName.Location = new System.Drawing.Point(12, 9);
            this.lblLibraryName.Name = "lblLibraryName";
            this.lblLibraryName.Size = new System.Drawing.Size(52, 23);
            this.lblLibraryName.TabIndex = 0;
            this.lblLibraryName.Text = "Library";

            // lvPermissions
            this.lvPermissions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colSubfolder,
                this.colPermissions});
            this.lvPermissions.FullRowSelect = true;
            this.lvPermissions.GridLines = true;
            this.lvPermissions.Location = new System.Drawing.Point(16, 40);
            this.lvPermissions.Size = new System.Drawing.Size(600, 300);
            this.lvPermissions.Name = "lvPermissions";
            this.lvPermissions.TabIndex = 1;
            this.lvPermissions.View = System.Windows.Forms.View.Details;

            // colSubfolder
            this.colSubfolder.Text = "Subfolder";
            this.colSubfolder.Width = 200;

            // colPermissions
            this.colPermissions.Text = "Permissions";
            this.colPermissions.Width = 380;

            // btnClose
            this.btnClose.Location = new System.Drawing.Point(541, 350);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 30);
            this.btnClose.TabIndex = 2;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += (s, e) => this.Close();

            // SubfolderPermissionsDialog
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 391);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lvPermissions);
            this.Controls.Add(this.lblLibraryName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SubfolderPermissionsDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Subfolder Permissions";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblLibraryName;
        private System.Windows.Forms.ListView lvPermissions;
        private System.Windows.Forms.ColumnHeader colSubfolder;
        private System.Windows.Forms.ColumnHeader colPermissions;
        private System.Windows.Forms.Button btnClose;
    }

    public class SubfolderPermissionInfo
    {
        public string SubfolderName { get; set; }
        public bool IsInherited { get; set; }
        public List<string> Permissions { get; set; }
    }
}