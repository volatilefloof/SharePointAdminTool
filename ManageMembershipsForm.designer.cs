namespace EntraGroupsApp
{
    partial class ManageMembershipsForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripMembers;
        private System.Windows.Forms.ToolStripMenuItem expandGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyIdToolStripMenuItem;
        private System.Windows.Forms.Panel groupsPanel;
        private System.Windows.Forms.ListBox listBoxGroups;
        private System.Windows.Forms.Panel membersPanel;
        private System.Windows.Forms.DataGridView dataGridViewMembers;
        private System.Windows.Forms.Panel actionsPanel;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelActions;
        private System.Windows.Forms.Button btnBrowseAddUser;
        private System.Windows.Forms.Button btnRemoveUser;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnUndo;
        private System.Windows.Forms.Button btnReplaceUser;
        private System.Windows.Forms.Button btnExportToCsv;
        private System.Windows.Forms.Button btnAddNestedGroup;
        private System.Windows.Forms.Button btnRemoveNestedGroup;
        private System.Windows.Forms.Button btnCopyUsers;
        private System.Windows.Forms.Button btnReturn;
        private System.Windows.Forms.Button btnReturnToPreviousWindow;

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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ManageMembershipsForm));
            groupsPanel = new Panel();
            listBoxGroups = new ListBox();
            membersPanel = new Panel();
            dataGridViewMembers = new DataGridView();
            contextMenuStripMembers = new ContextMenuStrip(components);
            expandGroupToolStripMenuItem = new ToolStripMenuItem();
            copyIdToolStripMenuItem = new ToolStripMenuItem();
            actionsPanel = new Panel();
            tableLayoutPanelActions = new TableLayoutPanel();
            btnBrowseAddUser = new Button();
            btnRemoveUser = new Button();
            btnSelectAll = new Button();
            btnUndo = new Button();
            btnReplaceUser = new Button();
            btnAddNestedGroup = new Button();
            btnRemoveNestedGroup = new Button();
            btnCopyUsers = new Button();
            btnExportToCsv = new Button();
            btnReturn = new Button();
            btnReturnToPreviousWindow = new Button();
            groupsPanel.SuspendLayout();
            membersPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMembers).BeginInit();
            contextMenuStripMembers.SuspendLayout();
            actionsPanel.SuspendLayout();
            tableLayoutPanelActions.SuspendLayout();
            SuspendLayout();
            // 
            // groupsPanel
            // 
            groupsPanel.Controls.Add(listBoxGroups);
            groupsPanel.Dock = DockStyle.Top;
            groupsPanel.Location = new Point(0, 0);
            groupsPanel.Name = "groupsPanel";
            groupsPanel.Padding = new Padding(10);
            groupsPanel.Size = new Size(1200, 200);
            groupsPanel.TabIndex = 2;
            // 
            // listBoxGroups
            // 
            listBoxGroups.Dock = DockStyle.Fill;
            listBoxGroups.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            listBoxGroups.HorizontalScrollbar = true;
            listBoxGroups.ItemHeight = 17;
            listBoxGroups.Location = new Point(10, 10);
            listBoxGroups.Name = "listBoxGroups";
            listBoxGroups.Size = new Size(1180, 180);
            listBoxGroups.TabIndex = 0;
            // 
            // membersPanel
            // 
            membersPanel.Controls.Add(dataGridViewMembers);
            membersPanel.Dock = DockStyle.Fill;
            membersPanel.Location = new Point(0, 200);
            membersPanel.Name = "membersPanel";
            membersPanel.Padding = new Padding(10);
            membersPanel.Size = new Size(1200, 400);
            membersPanel.TabIndex = 0;
            // 
            // dataGridViewMembers
            // 
            dataGridViewMembers.AllowUserToAddRows = false;
            dataGridViewMembers.ContextMenuStrip = contextMenuStripMembers;
            dataGridViewMembers.Dock = DockStyle.Fill;
            dataGridViewMembers.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewMembers.Location = new Point(10, 10);
            dataGridViewMembers.Name = "dataGridViewMembers";
            dataGridViewMembers.ReadOnly = true;
            dataGridViewMembers.RowTemplate.Height = 28;
            dataGridViewMembers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMembers.Size = new Size(1180, 380);
            dataGridViewMembers.TabIndex = 0;
            // 
            // contextMenuStripMembers
            // 
            contextMenuStripMembers.Items.AddRange(new ToolStripItem[] { expandGroupToolStripMenuItem, copyIdToolStripMenuItem });
            contextMenuStripMembers.Name = "contextMenuStripMembers";
            contextMenuStripMembers.Size = new Size(194, 48);
            // 
            // expandGroupToolStripMenuItem
            // 
            expandGroupToolStripMenuItem.Name = "expandGroupToolStripMenuItem";
            expandGroupToolStripMenuItem.Size = new Size(193, 22);
            expandGroupToolStripMenuItem.Text = "Manage Nested Group";
            expandGroupToolStripMenuItem.Click += expandGroupToolStripMenuItem_Click;
            // 
            // copyIdToolStripMenuItem
            // 
            copyIdToolStripMenuItem.Name = "copyIdToolStripMenuItem";
            copyIdToolStripMenuItem.Size = new Size(193, 22);
            copyIdToolStripMenuItem.Text = "Copy ID to Clipboard";
            copyIdToolStripMenuItem.Click += copyIdToolStripMenuItem_Click;
            // 
            // actionsPanel
            // 
            actionsPanel.Controls.Add(tableLayoutPanelActions);
            actionsPanel.Controls.Add(btnReturn);
            actionsPanel.Controls.Add(btnReturnToPreviousWindow);
            actionsPanel.Dock = DockStyle.Bottom;
            actionsPanel.Location = new Point(0, 600);
            actionsPanel.Name = "actionsPanel";
            actionsPanel.Padding = new Padding(10);
            actionsPanel.Size = new Size(1200, 100);
            actionsPanel.TabIndex = 1;
            // 
            // tableLayoutPanelActions
            // 
            tableLayoutPanelActions.ColumnCount = 9;
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 131F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 127F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 127F));
            tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 166F));
            tableLayoutPanelActions.Controls.Add(btnBrowseAddUser, 0, 0);
            tableLayoutPanelActions.Controls.Add(btnRemoveUser, 1, 0);
            tableLayoutPanelActions.Controls.Add(btnSelectAll, 2, 0);
            tableLayoutPanelActions.Controls.Add(btnUndo, 3, 0);
            tableLayoutPanelActions.Controls.Add(btnReplaceUser, 4, 0);
            tableLayoutPanelActions.Controls.Add(btnAddNestedGroup, 6, 0);
            tableLayoutPanelActions.Controls.Add(btnRemoveNestedGroup, 7, 0);
            tableLayoutPanelActions.Controls.Add(btnCopyUsers, 8, 0);
            tableLayoutPanelActions.Controls.Add(btnExportToCsv, 5, 0);
            tableLayoutPanelActions.Dock = DockStyle.Top;
            tableLayoutPanelActions.Location = new Point(10, 10);
            tableLayoutPanelActions.Name = "tableLayoutPanelActions";
            tableLayoutPanelActions.RowCount = 1;
            tableLayoutPanelActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanelActions.Size = new Size(1180, 50);
            tableLayoutPanelActions.TabIndex = 0;
            // 
            // btnBrowseAddUser
            // 
            btnBrowseAddUser.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnBrowseAddUser.Location = new Point(3, 3);
            btnBrowseAddUser.Name = "btnBrowseAddUser";
            btnBrowseAddUser.Size = new Size(125, 44);
            btnBrowseAddUser.TabIndex = 1;
            btnBrowseAddUser.Text = "Add Member(s) to Group";
            btnBrowseAddUser.UseVisualStyleBackColor = true;
            // 
            // btnRemoveUser
            // 
            btnRemoveUser.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRemoveUser.Location = new Point(134, 3);
            btnRemoveUser.Name = "btnRemoveUser";
            btnRemoveUser.Size = new Size(119, 44);
            btnRemoveUser.TabIndex = 2;
            btnRemoveUser.Text = "Remove Member(s) from Group";
            btnRemoveUser.UseVisualStyleBackColor = true;
            // 
            // btnSelectAll
            // 
            btnSelectAll.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnSelectAll.Location = new Point(259, 3);
            btnSelectAll.Name = "btnSelectAll";
            btnSelectAll.Size = new Size(119, 44);
            btnSelectAll.TabIndex = 3;
            btnSelectAll.Text = "Select All Members";
            btnSelectAll.UseVisualStyleBackColor = true;
            // 
            // btnUndo
            // 
            btnUndo.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnUndo.Location = new Point(384, 3);
            btnUndo.Name = "btnUndo";
            btnUndo.Size = new Size(120, 44);
            btnUndo.TabIndex = 4;
            btnUndo.Text = "Undo Action";
            btnUndo.UseVisualStyleBackColor = true;
            // 
            // btnReplaceUser
            // 
            btnReplaceUser.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnReplaceUser.Location = new Point(511, 3);
            btnReplaceUser.Name = "btnReplaceUser";
            btnReplaceUser.Size = new Size(120, 44);
            btnReplaceUser.TabIndex = 5;
            btnReplaceUser.Text = "Replace Member(s) in Group";
            btnReplaceUser.UseVisualStyleBackColor = true;
            // 
            // btnAddNestedGroup
            // 
            btnAddNestedGroup.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAddNestedGroup.Location = new Point(765, 3);
            btnAddNestedGroup.Name = "btnAddNestedGroup";
            btnAddNestedGroup.Size = new Size(118, 44);
            btnAddNestedGroup.TabIndex = 7;
            btnAddNestedGroup.Text = "Add Nested User Group";
            btnAddNestedGroup.UseVisualStyleBackColor = true;
            // 
            // btnRemoveNestedGroup
            // 
            btnRemoveNestedGroup.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRemoveNestedGroup.Location = new Point(889, 3);
            btnRemoveNestedGroup.Name = "btnRemoveNestedGroup";
            btnRemoveNestedGroup.Size = new Size(120, 44);
            btnRemoveNestedGroup.TabIndex = 8;
            btnRemoveNestedGroup.Text = "Remove Nested User Group";
            btnRemoveNestedGroup.UseVisualStyleBackColor = true;
            // 
            // btnCopyUsers
            // 
            btnCopyUsers.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCopyUsers.Location = new Point(1016, 3);
            btnCopyUsers.Name = "btnCopyUsers";
            btnCopyUsers.Size = new Size(120, 44);
            btnCopyUsers.TabIndex = 9;
            btnCopyUsers.Text = "Copy Users to Other Group";
            btnCopyUsers.UseVisualStyleBackColor = true;
            // 
            // btnExportToCsv
            // 
            btnExportToCsv.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnExportToCsv.Location = new Point(639, 3);
            btnExportToCsv.Name = "btnExportToCsv";
            btnExportToCsv.Size = new Size(120, 44);
            btnExportToCsv.TabIndex = 6;
            btnExportToCsv.Text = "Export to CSV";
            btnExportToCsv.UseVisualStyleBackColor = true;
            btnExportToCsv.Click += btnExportToCsv_Click_1;
            // 
            // btnReturn
            // 
            btnReturn.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnReturn.Location = new Point(10, 70);
            btnReturn.Name = "btnReturn";
            btnReturn.Size = new Size(120, 25);
            btnReturn.TabIndex = 10;
            btnReturn.Text = "Return home";
            btnReturn.UseVisualStyleBackColor = true;
            // 
            // btnReturnToPreviousWindow
            // 
            btnReturnToPreviousWindow.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnReturnToPreviousWindow.Location = new Point(140, 70);
            btnReturnToPreviousWindow.Name = "btnReturnToPreviousWindow";
            btnReturnToPreviousWindow.Size = new Size(150, 25);
            btnReturnToPreviousWindow.TabIndex = 11;
            btnReturnToPreviousWindow.Text = "Return to Group Search";
            btnReturnToPreviousWindow.UseVisualStyleBackColor = true;
            // 
            // ManageMembershipsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 700);
            Controls.Add(membersPanel);
            Controls.Add(actionsPanel);
            Controls.Add(groupsPanel);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "ManageMembershipsForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Manage Group Memberships";
            groupsPanel.ResumeLayout(false);
            membersPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewMembers).EndInit();
            contextMenuStripMembers.ResumeLayout(false);
            actionsPanel.ResumeLayout(false);
            tableLayoutPanelActions.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}