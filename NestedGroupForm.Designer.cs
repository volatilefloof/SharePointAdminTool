namespace EntraGroupsApp
{
    partial class NestedGroupForm
    {
        private System.ComponentModel.IContainer components = null;
        private ContextMenuStrip contextMenuStripMembers;
        private ToolStripMenuItem manageNestedGroupToolStripMenuItem;
        private ToolStripMenuItem copyIdToolStripMenuItem;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NestedGroupForm));
            contextMenuStripMembers = new ContextMenuStrip(components);
            manageNestedGroupToolStripMenuItem = new ToolStripMenuItem();
            copyIdToolStripMenuItem = new ToolStripMenuItem();
            membersPanel = new Panel();
            dataGridViewMembers = new DataGridView();
            actionsPanel = new Panel();
            btnAddUsers = new Button();
            btnRemoveUsers = new Button();
            btnCopyUsers = new Button();
            btnExportToCsv = new Button();
            btnAddNestedGroup = new Button();
            btnRemoveNestedGroup = new Button();
            btnClose = new Button();
            contextMenuStripMembers.SuspendLayout();
            membersPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMembers).BeginInit();
            actionsPanel.SuspendLayout();
            SuspendLayout();
            // 
            // contextMenuStripMembers
            // 
            contextMenuStripMembers.ImageScalingSize = new Size(32, 32);
            contextMenuStripMembers.Items.AddRange(new ToolStripItem[] { manageNestedGroupToolStripMenuItem, copyIdToolStripMenuItem });
            contextMenuStripMembers.Name = "contextMenuStripMembers";
            contextMenuStripMembers.Size = new Size(194, 48);
            // 
            // manageNestedGroupToolStripMenuItem
            // 
            manageNestedGroupToolStripMenuItem.Name = "manageNestedGroupToolStripMenuItem";
            manageNestedGroupToolStripMenuItem.Size = new Size(193, 22);
            manageNestedGroupToolStripMenuItem.Text = "Manage Nested Group";
            manageNestedGroupToolStripMenuItem.Click += manageNestedGroupToolStripMenuItem_Click;
            // 
            // copyIdToolStripMenuItem
            // 
            copyIdToolStripMenuItem.Name = "copyIdToolStripMenuItem";
            copyIdToolStripMenuItem.Size = new Size(193, 22);
            copyIdToolStripMenuItem.Text = "Copy ID to Clipboard";
            copyIdToolStripMenuItem.Click += copyIdToolStripMenuItem_Click;
            // 
            // membersPanel
            // 
            membersPanel.Controls.Add(dataGridViewMembers);
            membersPanel.Dock = DockStyle.Fill;
            membersPanel.Location = new Point(0, 0);
            membersPanel.Name = "membersPanel";
            membersPanel.Padding = new Padding(10, 10, 10, 10);
            membersPanel.Size = new Size(800, 397);
            membersPanel.TabIndex = 0;
            // 
            // dataGridViewMembers
            // 
            dataGridViewMembers.AllowUserToAddRows = false;
            dataGridViewMembers.ColumnHeadersHeight = 46;
            dataGridViewMembers.ContextMenuStrip = contextMenuStripMembers;
            dataGridViewMembers.Dock = DockStyle.Fill;
            dataGridViewMembers.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewMembers.Location = new Point(10, 10);
            dataGridViewMembers.Name = "dataGridViewMembers";
            dataGridViewMembers.ReadOnly = true;
            dataGridViewMembers.RowHeadersWidth = 82;
            dataGridViewMembers.RowTemplate.Height = 28;
            dataGridViewMembers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMembers.Size = new Size(780, 377);
            dataGridViewMembers.TabIndex = 0;
            // 
            // actionsPanel
            // 
            actionsPanel.Controls.Add(btnAddUsers);
            actionsPanel.Controls.Add(btnRemoveUsers);
            actionsPanel.Controls.Add(btnCopyUsers);
            actionsPanel.Controls.Add(btnExportToCsv);
            actionsPanel.Controls.Add(btnAddNestedGroup);
            actionsPanel.Controls.Add(btnRemoveNestedGroup);
            actionsPanel.Controls.Add(btnClose);
            actionsPanel.Dock = DockStyle.Bottom;
            actionsPanel.Location = new Point(0, 397);
            actionsPanel.Name = "actionsPanel";
            actionsPanel.Padding = new Padding(10, 10, 10, 10);
            actionsPanel.Size = new Size(800, 100);
            actionsPanel.TabIndex = 1;
            // 
            // btnAddUsers
            // 
            btnAddUsers.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAddUsers.Location = new Point(10, 10);
            btnAddUsers.Name = "btnAddUsers";
            btnAddUsers.Size = new Size(120, 43);
            btnAddUsers.TabIndex = 0;
            btnAddUsers.Text = "Add User(s) to Group";
            btnAddUsers.UseVisualStyleBackColor = true;
            btnAddUsers.Click += btnAddUsers_Click;
            // 
            // btnRemoveUsers
            // 
            btnRemoveUsers.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRemoveUsers.Location = new Point(140, 10);
            btnRemoveUsers.Name = "btnRemoveUsers";
            btnRemoveUsers.Size = new Size(120, 43);
            btnRemoveUsers.TabIndex = 1;
            btnRemoveUsers.Text = "Remove User(s) from Group";
            btnRemoveUsers.UseVisualStyleBackColor = true;
            btnRemoveUsers.Click += btnRemoveUsers_Click;
            // 
            // btnCopyUsers
            // 
            btnCopyUsers.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCopyUsers.Location = new Point(270, 10);
            btnCopyUsers.Name = "btnCopyUsers";
            btnCopyUsers.Size = new Size(120, 43);
            btnCopyUsers.TabIndex = 2;
            btnCopyUsers.Text = "Copy User(s) to Group";
            btnCopyUsers.UseVisualStyleBackColor = true;
            btnCopyUsers.Click += btnCopyUsers_Click;
            // 
            // btnExportToCsv
            // 
            btnExportToCsv.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnExportToCsv.Location = new Point(400, 10);
            btnExportToCsv.Name = "btnExportToCsv";
            btnExportToCsv.Size = new Size(120, 43);
            btnExportToCsv.TabIndex = 3;
            btnExportToCsv.Text = "Export Group to CSV";
            btnExportToCsv.UseVisualStyleBackColor = true;
            // 
            // btnAddNestedGroup
            // 
            btnAddNestedGroup.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAddNestedGroup.Location = new Point(530, 10);
            btnAddNestedGroup.Name = "btnAddNestedGroup";
            btnAddNestedGroup.Size = new Size(120, 43);
            btnAddNestedGroup.TabIndex = 4;
            btnAddNestedGroup.Text = "Add Nested User Group";
            btnAddNestedGroup.UseVisualStyleBackColor = true;
            btnAddNestedGroup.Click += btnAddNestedGroup_Click;
            // 
            // btnRemoveNestedGroup
            // 
            btnRemoveNestedGroup.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRemoveNestedGroup.Location = new Point(660, 10);
            btnRemoveNestedGroup.Name = "btnRemoveNestedGroup";
            btnRemoveNestedGroup.Size = new Size(120, 43);
            btnRemoveNestedGroup.TabIndex = 5;
            btnRemoveNestedGroup.Text = "Remove Nested User Group";
            btnRemoveNestedGroup.UseVisualStyleBackColor = true;
            btnRemoveNestedGroup.Click += btnRemoveNestedGroup_Click;
            // 
            // btnClose
            // 
            btnClose.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnClose.Location = new Point(670, 60);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(120, 30);
            btnClose.TabIndex = 6;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // NestedGroupForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 497);
            Controls.Add(membersPanel);
            Controls.Add(actionsPanel);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "NestedGroupForm";
            Text = "Manage Nested Group Members";
            contextMenuStripMembers.ResumeLayout(false);
            membersPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewMembers).EndInit();
            actionsPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel membersPanel;
        private DataGridView dataGridViewMembers;
        private Panel actionsPanel;
        private Button btnAddUsers;
        private Button btnRemoveUsers;
        private Button btnCopyUsers;
        private Button btnExportToCsv;
        private Button btnAddNestedGroup;
        private Button btnRemoveNestedGroup;
        private Button btnClose;
    }
}