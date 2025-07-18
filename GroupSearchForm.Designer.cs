namespace EntraGroupsApp
{
    partial class GroupSearchForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label lblSelectDepartment;
        private System.Windows.Forms.ComboBox comboBoxDepartments;
        private System.Windows.Forms.Label lblSortOrder;
        private System.Windows.Forms.ComboBox comboBoxSortOrder;
        private System.Windows.Forms.Label lblGroupType;
        private System.Windows.Forms.ComboBox comboBoxGroupType;
        private System.Windows.Forms.ListBox listBoxGroups;
        private System.Windows.Forms.Panel panelGroupToolbox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelButtons;
        private System.Windows.Forms.Button btnAddGroup;
        private System.Windows.Forms.Button btnDeleteGroup;
        private System.Windows.Forms.Button btnManageMemberships;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnReturn;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GroupSearchForm));
            tableLayoutPanel1 = new TableLayoutPanel();
            lblSelectDepartment = new Label();
            comboBoxDepartments = new ComboBox();
            lblSortOrder = new Label();
            comboBoxSortOrder = new ComboBox();
            lblGroupType = new Label();
            comboBoxGroupType = new ComboBox();
            listBoxGroups = new ListBox();
            panelGroupToolbox = new Panel();
            tableLayoutPanelButtons = new TableLayoutPanel();
            btnAddGroup = new Button();
            btnDeleteGroup = new Button();
            btnManageMemberships = new Button();
            btnRefresh = new Button();
            btnReturn = new Button();
            lblStatus = new Label();
            tableLayoutPanel1.SuspendLayout();
            panelGroupToolbox.SuspendLayout();
            tableLayoutPanelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.Controls.Add(lblSelectDepartment, 0, 0);
            tableLayoutPanel1.Controls.Add(comboBoxDepartments, 0, 1);
            tableLayoutPanel1.Controls.Add(lblSortOrder, 1, 0);
            tableLayoutPanel1.Controls.Add(comboBoxSortOrder, 1, 1);
            tableLayoutPanel1.Controls.Add(lblGroupType, 2, 0);
            tableLayoutPanel1.Controls.Add(comboBoxGroupType, 2, 1);
            tableLayoutPanel1.Controls.Add(listBoxGroups, 0, 2);
            tableLayoutPanel1.Controls.Add(panelGroupToolbox, 0, 3);
            tableLayoutPanel1.Controls.Add(btnReturn, 0, 4);
            tableLayoutPanel1.Controls.Add(lblStatus, 0, 5);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(13, 12, 13, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 6;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 14.2657347F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 8.671329F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 9.93007F));
            tableLayoutPanel1.Size = new Size(889, 447);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // lblSelectDepartment
            // 
            lblSelectDepartment.AutoSize = true;
            lblSelectDepartment.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSelectDepartment.Location = new Point(13, 12);
            lblSelectDepartment.Margin = new Padding(13, 12, 13, 12);
            lblSelectDepartment.Name = "lblSelectDepartment";
            lblSelectDepartment.Size = new Size(139, 20);
            lblSelectDepartment.TabIndex = 0;
            lblSelectDepartment.Text = "Select Department";
            // 
            // comboBoxDepartments
            // 
            comboBoxDepartments.Dock = DockStyle.Fill;
            comboBoxDepartments.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxDepartments.FlatStyle = FlatStyle.Flat;
            comboBoxDepartments.Font = new Font("Segoe UI", 9F);
            comboBoxDepartments.Location = new Point(13, 56);
            comboBoxDepartments.Margin = new Padding(13, 12, 13, 12);
            comboBoxDepartments.Name = "comboBoxDepartments";
            comboBoxDepartments.Size = new Size(507, 28);
            comboBoxDepartments.TabIndex = 1;
            comboBoxDepartments.SelectedIndexChanged += comboBoxDepartments_SelectedIndexChanged;
            // 
            // lblSortOrder
            // 
            lblSortOrder.AutoSize = true;
            lblSortOrder.Location = new Point(546, 12);
            lblSortOrder.Margin = new Padding(13, 12, 13, 12);
            lblSortOrder.Name = "lblSortOrder";
            lblSortOrder.Size = new Size(78, 20);
            lblSortOrder.TabIndex = 2;
            lblSortOrder.Text = "Sort Order";
            // 
            // comboBoxSortOrder
            // 
            comboBoxSortOrder.Dock = DockStyle.Fill;
            comboBoxSortOrder.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSortOrder.FlatStyle = FlatStyle.Flat;
            comboBoxSortOrder.Font = new Font("Segoe UI", 9F);
            comboBoxSortOrder.Location = new Point(546, 56);
            comboBoxSortOrder.Margin = new Padding(13, 12, 13, 12);
            comboBoxSortOrder.Name = "comboBoxSortOrder";
            comboBoxSortOrder.Size = new Size(151, 28);
            comboBoxSortOrder.TabIndex = 3;
            comboBoxSortOrder.SelectedIndexChanged += comboBoxSortOrder_SelectedIndexChanged;
            // 
            // lblGroupType
            // 
            lblGroupType.AutoSize = true;
            lblGroupType.Location = new Point(723, 12);
            lblGroupType.Margin = new Padding(13, 12, 13, 12);
            lblGroupType.Name = "lblGroupType";
            lblGroupType.Size = new Size(85, 20);
            lblGroupType.TabIndex = 8;
            lblGroupType.Text = "Group Type";
            // 
            // comboBoxGroupType
            // 
            comboBoxGroupType.Dock = DockStyle.Fill;
            comboBoxGroupType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxGroupType.FlatStyle = FlatStyle.Flat;
            comboBoxGroupType.Font = new Font("Segoe UI", 9F);
            comboBoxGroupType.Location = new Point(723, 56);
            comboBoxGroupType.Margin = new Padding(13, 12, 13, 12);
            comboBoxGroupType.Name = "comboBoxGroupType";
            comboBoxGroupType.Size = new Size(153, 28);
            comboBoxGroupType.TabIndex = 9;
            comboBoxGroupType.SelectedIndexChanged += comboBoxGroupType_SelectedIndexChanged;
            // 
            // listBoxGroups
            // 
            tableLayoutPanel1.SetColumnSpan(listBoxGroups, 3);
            listBoxGroups.Dock = DockStyle.Fill;
            listBoxGroups.FormattingEnabled = true;
            listBoxGroups.HorizontalScrollbar = true;
            listBoxGroups.Location = new Point(13, 109);
            listBoxGroups.Margin = new Padding(13, 12, 13, 12);
            listBoxGroups.Name = "listBoxGroups";
            listBoxGroups.Size = new Size(863, 179);
            listBoxGroups.TabIndex = 4;
            // 
            // panelGroupToolbox
            // 
            tableLayoutPanel1.SetColumnSpan(panelGroupToolbox, 3);
            panelGroupToolbox.Controls.Add(tableLayoutPanelButtons);
            panelGroupToolbox.Dock = DockStyle.Fill;
            panelGroupToolbox.Location = new Point(13, 312);
            panelGroupToolbox.Margin = new Padding(13, 12, 13, 12);
            panelGroupToolbox.Name = "panelGroupToolbox";
            panelGroupToolbox.Size = new Size(863, 39);
            panelGroupToolbox.TabIndex = 5;
            // 
            // tableLayoutPanelButtons
            // 
            tableLayoutPanelButtons.ColumnCount = 4;
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanelButtons.Controls.Add(btnAddGroup, 0, 0);
            tableLayoutPanelButtons.Controls.Add(btnDeleteGroup, 1, 0);
            tableLayoutPanelButtons.Controls.Add(btnManageMemberships, 2, 0);
            tableLayoutPanelButtons.Controls.Add(btnRefresh, 3, 0);
            tableLayoutPanelButtons.Dock = DockStyle.Fill;
            tableLayoutPanelButtons.Location = new Point(0, 0);
            tableLayoutPanelButtons.Margin = new Padding(6, 7, 6, 7);
            tableLayoutPanelButtons.Name = "tableLayoutPanelButtons";
            tableLayoutPanelButtons.RowCount = 1;
            tableLayoutPanelButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanelButtons.Size = new Size(863, 39);
            tableLayoutPanelButtons.TabIndex = 0;
            // 
            // btnAddGroup
            // 
            btnAddGroup.Dock = DockStyle.Fill;
            btnAddGroup.Font = new Font("Segoe UI", 9F);
            btnAddGroup.Location = new Point(2, 1);
            btnAddGroup.Margin = new Padding(2, 1, 2, 1);
            btnAddGroup.Name = "btnAddGroup";
            btnAddGroup.Size = new Size(211, 37);
            btnAddGroup.TabIndex = 0;
            btnAddGroup.Text = "Create Group";
            btnAddGroup.UseVisualStyleBackColor = true;
            btnAddGroup.Click += btnAddGroup_Click;
            // 
            // btnDeleteGroup
            // 
            btnDeleteGroup.Dock = DockStyle.Fill;
            btnDeleteGroup.Font = new Font("Segoe UI", 9F);
            btnDeleteGroup.Location = new Point(217, 1);
            btnDeleteGroup.Margin = new Padding(2, 1, 2, 1);
            btnDeleteGroup.Name = "btnDeleteGroup";
            btnDeleteGroup.Size = new Size(211, 37);
            btnDeleteGroup.TabIndex = 1;
            btnDeleteGroup.Text = "Delete Group";
            btnDeleteGroup.UseVisualStyleBackColor = true;
            btnDeleteGroup.Click += btnDeleteGroup_Click;
            // 
            // btnManageMemberships
            // 
            btnManageMemberships.Dock = DockStyle.Fill;
            btnManageMemberships.Font = new Font("Segoe UI", 9F);
            btnManageMemberships.Location = new Point(432, 1);
            btnManageMemberships.Margin = new Padding(2, 1, 2, 1);
            btnManageMemberships.Name = "btnManageMemberships";
            btnManageMemberships.Size = new Size(211, 37);
            btnManageMemberships.TabIndex = 2;
            btnManageMemberships.Text = "Manage Memberships";
            btnManageMemberships.UseVisualStyleBackColor = true;
            btnManageMemberships.Click += btnManageMemberships_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Dock = DockStyle.Fill;
            btnRefresh.Font = new Font("Segoe UI", 9F);
            btnRefresh.Location = new Point(647, 1);
            btnRefresh.Margin = new Padding(2, 1, 2, 1);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(214, 37);
            btnRefresh.TabIndex = 3;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnReturn
            // 
            btnReturn.Font = new Font("Segoe UI", 9F);
            btnReturn.Location = new Point(13, 370);
            btnReturn.Margin = new Padding(13, 7, 13, 0);
            btnReturn.Name = "btnReturn";
            btnReturn.Size = new Size(106, 31);
            btnReturn.TabIndex = 7;
            btnReturn.Text = "Return";
            btnReturn.UseVisualStyleBackColor = true;
            btnReturn.Click += btnReturn_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.Location = new Point(13, 413);
            lblStatus.Margin = new Padding(13, 12, 13, 12);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(119, 20);
            lblStatus.TabIndex = 6;
            lblStatus.Text = "Ready for search";
            // 
            // GroupSearchForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(889, 447);
            Controls.Add(tableLayoutPanel1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(13, 12, 13, 12);
            Name = "GroupSearchForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Group Search";
            Load += GroupSearchForm_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            panelGroupToolbox.ResumeLayout(false);
            tableLayoutPanelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
