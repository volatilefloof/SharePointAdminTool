namespace EntraGroupsApp
{
    partial class UserSearchForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.DataGridView dataGridViewUsers;
        private System.Windows.Forms.Label lblUsers;
        private System.Windows.Forms.ListView lvGroups;
        private System.Windows.Forms.Label lblGroups;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.Label lblEmail;
        private System.Windows.Forms.Label lblJobTitle;
        private System.Windows.Forms.Label lblDepartment;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelButtons;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnAddToDepartmentGroups;
        private System.Windows.Forms.Button btnAddOwner;
        private System.Windows.Forms.Button btnRemoveOwner;
        private System.Windows.Forms.Button btnSelectAllGroups;
        private System.Windows.Forms.Button btnRemoveMember;
        private System.Windows.Forms.Button btnReplaceGroup;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnCopyGroups;
        private System.Windows.Forms.Label lblStatus;

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
            tableLayoutPanel1 = new TableLayoutPanel();
            lblInstructions = new Label();
            txtSearch = new TextBox();
            btnSearch = new Button();
            splitContainer = new SplitContainer();
            dataGridViewUsers = new DataGridView();
            lblUsers = new Label();
            lvGroups = new ListView();
            lblGroups = new Label();
            lblName = new Label();
            lblEmail = new Label();
            lblJobTitle = new Label();
            lblDepartment = new Label();
            panelButtons = new Panel();
            tableLayoutPanelButtons = new TableLayoutPanel();
            btnBack = new Button();
            btnAddToDepartmentGroups = new Button();
            btnAddOwner = new Button();
            btnRemoveOwner = new Button();
            btnSelectAllGroups = new Button();
            btnRemoveMember = new Button();
            btnReplaceGroup = new Button();
            btnCopyGroups = new Button();
            btnRefresh = new Button();
            lblStatus = new Label();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewUsers).BeginInit();
            panelButtons.SuspendLayout();
            tableLayoutPanelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel1.Controls.Add(lblInstructions, 0, 0);
            tableLayoutPanel1.Controls.Add(txtSearch, 0, 1);
            tableLayoutPanel1.Controls.Add(btnSearch, 1, 1);
            tableLayoutPanel1.Controls.Add(splitContainer, 0, 2);
            tableLayoutPanel1.Controls.Add(panelButtons, 0, 3);
            tableLayoutPanel1.Controls.Add(lblStatus, 0, 4);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(10, 12, 10, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 5;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel1.Size = new Size(1183, 574);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // lblInstructions
            // 
            lblInstructions.AutoSize = true;
            lblInstructions.Font = new Font("Segoe UI", 10F, FontStyle.Italic);
            lblInstructions.Location = new Point(4, 0);
            lblInstructions.Margin = new Padding(4, 0, 4, 0);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new Size(469, 23);
            lblInstructions.TabIndex = 0;
            lblInstructions.Text = "Search by name (full or partial) or NetID (@tamu.edu optional)";
            // 
            // txtSearch
            // 
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.Font = new Font("Segoe UI", 10F);
            txtSearch.Location = new Point(4, 61);
            txtSearch.Margin = new Padding(4);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(938, 30);
            txtSearch.TabIndex = 1;
            // 
            // btnSearch
            // 
            btnSearch.Dock = DockStyle.Fill;
            btnSearch.Font = new Font("Segoe UI", 10F);
            btnSearch.Location = new Point(950, 61);
            btnSearch.Margin = new Padding(4);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new Size(229, 49);
            btnSearch.TabIndex = 2;
            btnSearch.Text = "Search";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += btnSearch_Click;
            // 
            // splitContainer
            // 
            tableLayoutPanel1.SetColumnSpan(splitContainer, 2);
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(4, 118);
            splitContainer.Margin = new Padding(4);
            splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(dataGridViewUsers);
            splitContainer.Panel1.Controls.Add(lblUsers);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(lvGroups);
            splitContainer.Panel2.Controls.Add(lblGroups);
            splitContainer.Panel2.Controls.Add(lblName);
            splitContainer.Panel2.Controls.Add(lblEmail);
            splitContainer.Panel2.Controls.Add(lblJobTitle);
            splitContainer.Panel2.Controls.Add(lblDepartment);
            splitContainer.Size = new Size(1175, 336);
            splitContainer.SplitterDistance = 553;
            splitContainer.TabIndex = 3;
            // 
            // dataGridViewUsers
            // 
            dataGridViewUsers.AllowUserToAddRows = false;
            dataGridViewUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewUsers.ColumnHeadersHeight = 46;
            dataGridViewUsers.Dock = DockStyle.Fill;
            dataGridViewUsers.Font = new Font("Segoe UI", 10F);
            dataGridViewUsers.Location = new Point(0, 0);
            dataGridViewUsers.Margin = new Padding(4);
            dataGridViewUsers.MultiSelect = false;
            dataGridViewUsers.Name = "dataGridViewUsers";
            dataGridViewUsers.ReadOnly = true;
            dataGridViewUsers.RowHeadersWidth = 82;
            dataGridViewUsers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewUsers.Size = new Size(553, 336);
            dataGridViewUsers.TabIndex = 1;
            dataGridViewUsers.ColumnHeaderMouseClick += dataGridViewUsers_ColumnHeaderMouseClick;
            dataGridViewUsers.SelectionChanged += dataGridViewUsers_SelectionChanged;
            // 
            // lblUsers
            // 
            lblUsers.AutoSize = true;
            lblUsers.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblUsers.Location = new Point(0, 0);
            lblUsers.Margin = new Padding(4, 0, 4, 0);
            lblUsers.Name = "lblUsers";
            lblUsers.Size = new Size(107, 23);
            lblUsers.TabIndex = 0;
            lblUsers.Text = "Users Found";
            // 
            // lvGroups
            // 
            lvGroups.Dock = DockStyle.Fill;
            lvGroups.Font = new Font("Segoe UI", 10F);
            lvGroups.FullRowSelect = true;
            lvGroups.Location = new Point(0, 0);
            lvGroups.Margin = new Padding(4);
            lvGroups.Name = "lvGroups";
            lvGroups.Size = new Size(618, 336);
            lvGroups.TabIndex = 1;
            lvGroups.UseCompatibleStateImageBehavior = false;
            lvGroups.View = View.Details;
            lvGroups.ColumnClick += lvGroups_ColumnClick;
            // 
            // lblGroups
            // 
            lblGroups.AutoSize = true;
            lblGroups.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblGroups.Location = new Point(4, 71);
            lblGroups.Margin = new Padding(4, 0, 4, 0);
            lblGroups.Name = "lblGroups";
            lblGroups.Size = new Size(173, 23);
            lblGroups.TabIndex = 0;
            lblGroups.Text = "Group Memberships";
            // 
            // lblName
            // 
            lblName.AutoSize = true;
            lblName.Font = new Font("Segoe UI", 10F);
            lblName.Location = new Point(4, 0);
            lblName.Margin = new Padding(4, 0, 4, 0);
            lblName.Name = "lblName";
            lblName.Size = new Size(60, 23);
            lblName.TabIndex = 2;
            lblName.Text = "Name:";
            // 
            // lblEmail
            // 
            lblEmail.AutoSize = true;
            lblEmail.Font = new Font("Segoe UI", 10F);
            lblEmail.Location = new Point(4, 23);
            lblEmail.Margin = new Padding(4, 0, 4, 0);
            lblEmail.Name = "lblEmail";
            lblEmail.Size = new Size(55, 23);
            lblEmail.TabIndex = 3;
            lblEmail.Text = "Email:";
            // 
            // lblJobTitle
            // 
            lblJobTitle.AutoSize = true;
            lblJobTitle.Font = new Font("Segoe UI", 10F);
            lblJobTitle.Location = new Point(4, 46);
            lblJobTitle.Margin = new Padding(4, 0, 4, 0);
            lblJobTitle.Name = "lblJobTitle";
            lblJobTitle.Size = new Size(77, 23);
            lblJobTitle.TabIndex = 4;
            lblJobTitle.Text = "Job Title:";
            // 
            // lblDepartment
            // 
            lblDepartment.AutoSize = true;
            lblDepartment.Font = new Font("Segoe UI", 10F);
            lblDepartment.Location = new Point(4, 69);
            lblDepartment.Margin = new Padding(4, 0, 4, 0);
            lblDepartment.Name = "lblDepartment";
            lblDepartment.Size = new Size(106, 23);
            lblDepartment.TabIndex = 5;
            lblDepartment.Text = "Department:";
            // 
            // panelButtons
            // 
            tableLayoutPanel1.SetColumnSpan(panelButtons, 2);
            panelButtons.Controls.Add(tableLayoutPanelButtons);
            panelButtons.Dock = DockStyle.Fill;
            panelButtons.Location = new Point(4, 462);
            panelButtons.Margin = new Padding(4);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(1175, 49);
            panelButtons.TabIndex = 4;
            // 
            // tableLayoutPanelButtons
            // 
            tableLayoutPanelButtons.ColumnCount = 9;
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));
            tableLayoutPanelButtons.Controls.Add(btnBack, 0, 0);
            tableLayoutPanelButtons.Controls.Add(btnAddToDepartmentGroups, 1, 0);
            tableLayoutPanelButtons.Controls.Add(btnAddOwner, 2, 0);
            tableLayoutPanelButtons.Controls.Add(btnRemoveOwner, 3, 0);
            tableLayoutPanelButtons.Controls.Add(btnSelectAllGroups, 4, 0);
            tableLayoutPanelButtons.Controls.Add(btnRemoveMember, 5, 0);
            tableLayoutPanelButtons.Controls.Add(btnReplaceGroup, 6, 0);
            tableLayoutPanelButtons.Controls.Add(btnCopyGroups, 7, 0);
            tableLayoutPanelButtons.Controls.Add(btnRefresh, 8, 0);
            tableLayoutPanelButtons.Dock = DockStyle.Fill;
            tableLayoutPanelButtons.Location = new Point(0, 0);
            tableLayoutPanelButtons.Margin = new Padding(2);
            tableLayoutPanelButtons.Name = "tableLayoutPanelButtons";
            tableLayoutPanelButtons.RowCount = 1;
            tableLayoutPanelButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanelButtons.Size = new Size(1175, 49);
            tableLayoutPanelButtons.TabIndex = 0;
            // 
            // btnBack
            // 
            btnBack.Dock = DockStyle.Fill;
            btnBack.Font = new Font("Segoe UI", 9F);
            btnBack.Location = new Point(2, 2);
            btnBack.Margin = new Padding(2);
            btnBack.Name = "btnBack";
            btnBack.Size = new Size(126, 45);
            btnBack.TabIndex = 6;
            btnBack.Text = "Back";
            btnBack.UseVisualStyleBackColor = true;
            btnBack.Click += btnBack_Click;
            // 
            // btnAddToDepartmentGroups
            // 
            btnAddToDepartmentGroups.Dock = DockStyle.Fill;
            btnAddToDepartmentGroups.Font = new Font("Segoe UI", 9F);
            btnAddToDepartmentGroups.Location = new Point(132, 2);
            btnAddToDepartmentGroups.Margin = new Padding(2);
            btnAddToDepartmentGroups.Name = "btnAddToDepartmentGroups";
            btnAddToDepartmentGroups.Size = new Size(126, 45);
            btnAddToDepartmentGroups.TabIndex = 7;
            btnAddToDepartmentGroups.Text = "Add to Group";
            btnAddToDepartmentGroups.UseVisualStyleBackColor = true;
            btnAddToDepartmentGroups.Click += btnAddToDepartmentGroups_Click;
            // 
            // btnAddOwner
            // 
            btnAddOwner.Dock = DockStyle.Fill;
            btnAddOwner.Font = new Font("Segoe UI", 9F);
            btnAddOwner.Location = new Point(262, 2);
            btnAddOwner.Margin = new Padding(2);
            btnAddOwner.Name = "btnAddOwner";
            btnAddOwner.Size = new Size(126, 45);
            btnAddOwner.TabIndex = 9;
            btnAddOwner.Text = "Add Group Ownership";
            btnAddOwner.UseVisualStyleBackColor = true;
            btnAddOwner.Click += btnAddOwner_Click;
            // 
            // btnRemoveOwner
            // 
            btnRemoveOwner.Dock = DockStyle.Fill;
            btnRemoveOwner.Font = new Font("Segoe UI", 9F);
            btnRemoveOwner.Location = new Point(392, 2);
            btnRemoveOwner.Margin = new Padding(2);
            btnRemoveOwner.Name = "btnRemoveOwner";
            btnRemoveOwner.Size = new Size(126, 45);
            btnRemoveOwner.TabIndex = 8;
            btnRemoveOwner.Text = "Remove Group Ownership";
            btnRemoveOwner.UseVisualStyleBackColor = true;
            btnRemoveOwner.Click += btnRemoveOwner_Click;
            // 
            // btnSelectAllGroups
            // 
            btnSelectAllGroups.Dock = DockStyle.Fill;
            btnSelectAllGroups.Font = new Font("Segoe UI", 9F);
            btnSelectAllGroups.Location = new Point(522, 2);
            btnSelectAllGroups.Margin = new Padding(2);
            btnSelectAllGroups.Name = "btnSelectAllGroups";
            btnSelectAllGroups.Size = new Size(126, 45);
            btnSelectAllGroups.TabIndex = 10;
            btnSelectAllGroups.Text = "Select All Groups";
            btnSelectAllGroups.UseVisualStyleBackColor = true;
            btnSelectAllGroups.Click += btnSelectAllGroups_Click;
            // 
            // btnRemoveMember
            // 
            btnRemoveMember.Dock = DockStyle.Fill;
            btnRemoveMember.Font = new Font("Segoe UI", 9F);
            btnRemoveMember.Location = new Point(652, 2);
            btnRemoveMember.Margin = new Padding(2);
            btnRemoveMember.Name = "btnRemoveMember";
            btnRemoveMember.Size = new Size(126, 45);
            btnRemoveMember.TabIndex = 11;
            btnRemoveMember.Text = "Remove Group Membership";
            btnRemoveMember.UseVisualStyleBackColor = true;
            btnRemoveMember.Click += btnRemoveMember_Click;
            // 
            // btnReplaceGroup
            // 
            btnReplaceGroup.Dock = DockStyle.Fill;
            btnReplaceGroup.Font = new Font("Segoe UI", 9F);
            btnReplaceGroup.Location = new Point(782, 2);
            btnReplaceGroup.Margin = new Padding(2);
            btnReplaceGroup.Name = "btnReplaceGroup";
            btnReplaceGroup.Size = new Size(126, 45);
            btnReplaceGroup.TabIndex = 12;
            btnReplaceGroup.Text = "Swap Group Membership";
            btnReplaceGroup.UseVisualStyleBackColor = true;
            btnReplaceGroup.Click += btnReplaceGroup_Click;
            // 
            // btnCopyGroups
            // 
            btnCopyGroups.Dock = DockStyle.Fill;
            btnCopyGroups.Font = new Font("Segoe UI", 9F);
            btnCopyGroups.Location = new Point(912, 2);
            btnCopyGroups.Margin = new Padding(2);
            btnCopyGroups.Name = "btnCopyGroups";
            btnCopyGroups.Size = new Size(126, 45);
            btnCopyGroups.TabIndex = 14;
            btnCopyGroups.Text = "Mirror All Groups to Other User";
            btnCopyGroups.UseVisualStyleBackColor = true;
            btnCopyGroups.Click += btnCopyGroups_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Dock = DockStyle.Fill;
            btnRefresh.Font = new Font("Segoe UI", 9F);
            btnRefresh.Location = new Point(1042, 2);
            btnRefresh.Margin = new Padding(2);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(131, 45);
            btnRefresh.TabIndex = 13;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.Location = new Point(4, 515);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(119, 20);
            lblStatus.TabIndex = 15;
            lblStatus.Text = "Ready for search";
            // 
            // UserSearchForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1183, 574);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4);
            MaximizeBox = false;
            Name = "UserSearchForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Manage Users and Groups";
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel1.PerformLayout();
            splitContainer.Panel2.ResumeLayout(false);
            splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewUsers).EndInit();
            panelButtons.ResumeLayout(false);
            tableLayoutPanelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}