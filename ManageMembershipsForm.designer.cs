namespace EntraGroupsApp
{
    partial class ManageMembershipsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            headerPanel = new Panel();
            titleLabel = new Label();
            instructionsLabel = new Label();
            mainSplitContainer = new SplitContainer();
            groupsPanel = new Panel();
            groupsListBox = new ListBox();
            groupsHeaderPanel = new Panel();
            groupsHeaderLabel = new Label();
            membershipPanel = new Panel();
            contentSplitContainer = new SplitContainer();
            membersPanel = new Panel();
            dataGridViewMembers = new DataGridView();
            contextMenuStripMembers = new ContextMenuStrip(components);
            expandGroupToolStripMenuItem = new ToolStripMenuItem();
            copyIdToolStripMenuItem = new ToolStripMenuItem();
            membersHeaderPanel = new Panel();
            membersHeaderLabel = new Label();
            memberDetailsPanel = new Panel();
            memberDetailsLabel = new Label();
            memberDetailsHeaderPanel = new Panel();
            memberDetailsHeaderLabel = new Label();
            actionsFlowPanel = new FlowLayoutPanel();
            btnBrowseAddUser = new Button();
            btnRemoveUser = new Button();
            btnReplaceUser = new Button();
            btnAddNestedGroup = new Button();
            btnRemoveNestedGroup = new Button();
            btnCopyUsers = new Button();
            btnSelectAll = new Button();
            btnExportToCsv = new Button();
            btnUndo = new Button();
            bottomPanel = new Panel();
            returnButtonsPanel = new Panel();
            btnReturn = new Button();
            btnReturnToPreviousWindow = new Button();
            headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).BeginInit();
            mainSplitContainer.Panel1.SuspendLayout();
            mainSplitContainer.Panel2.SuspendLayout();
            mainSplitContainer.SuspendLayout();
            groupsPanel.SuspendLayout();
            groupsHeaderPanel.SuspendLayout();
            membershipPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)contentSplitContainer).BeginInit();
            contentSplitContainer.Panel1.SuspendLayout();
            contentSplitContainer.Panel2.SuspendLayout();
            contentSplitContainer.SuspendLayout();
            membersPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMembers).BeginInit();
            contextMenuStripMembers.SuspendLayout();
            membersHeaderPanel.SuspendLayout();
            memberDetailsPanel.SuspendLayout();
            memberDetailsHeaderPanel.SuspendLayout();
            actionsFlowPanel.SuspendLayout();
            bottomPanel.SuspendLayout();
            returnButtonsPanel.SuspendLayout();
            SuspendLayout();
            // 
            // headerPanel
            // 
            headerPanel.BackColor = Color.White;
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(instructionsLabel);
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Location = new Point(0, 0);
            headerPanel.Name = "headerPanel";
            headerPanel.Padding = new Padding(20, 15, 20, 15);
            headerPanel.Size = new Size(1400, 70);
            headerPanel.TabIndex = 0;
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(50, 49, 48);
            titleLabel.Location = new Point(20, 15);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(238, 25);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Manage Group Memberships";
            // 
            // instructionsLabel
            // 
            instructionsLabel.AutoSize = true;
            instructionsLabel.Dock = DockStyle.Top;
            instructionsLabel.Font = new Font("Segoe UI", 9.5F);
            instructionsLabel.ForeColor = Color.FromArgb(96, 94, 92);
            instructionsLabel.Location = new Point(20, 40);
            instructionsLabel.Name = "instructionsLabel";
            instructionsLabel.Padding = new Padding(0, 5, 0, 0);
            instructionsLabel.Size = new Size(365, 22);
            instructionsLabel.TabIndex = 1;
            instructionsLabel.Text = "Select a group to view and manage its members or nested groups.";
            // 
            // mainSplitContainer
            // 
            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.FixedPanel = FixedPanel.Panel1;
            mainSplitContainer.Location = new Point(0, 70);
            mainSplitContainer.Name = "mainSplitContainer";
            mainSplitContainer.Panel1MinSize = 280;
            // 
            // mainSplitContainer.Panel1
            // 
            mainSplitContainer.Panel1.Controls.Add(groupsPanel);
            // 
            // mainSplitContainer.Panel2
            // 
            mainSplitContainer.Panel2.Controls.Add(membershipPanel);
            mainSplitContainer.Size = new Size(1400, 580);
            mainSplitContainer.SplitterDistance = 300;
            mainSplitContainer.SplitterWidth = 8;
            mainSplitContainer.TabIndex = 1;
            // 
            // groupsPanel
            // 
            groupsPanel.BackColor = Color.FromArgb(250, 250, 250);
            groupsPanel.Controls.Add(groupsListBox);
            groupsPanel.Controls.Add(groupsHeaderPanel);
            groupsPanel.Dock = DockStyle.Fill;
            groupsPanel.Location = new Point(0, 0);
            groupsPanel.Name = "groupsPanel";
            groupsPanel.Size = new Size(300, 580);
            groupsPanel.TabIndex = 0;
            // 
            // groupsListBox
            // 
            groupsListBox.BackColor = Color.FromArgb(250, 250, 250);
            groupsListBox.BorderStyle = BorderStyle.None;
            groupsListBox.Dock = DockStyle.Fill;
            groupsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            groupsListBox.Font = new Font("Segoe UI", 9.5F);
            groupsListBox.IntegralHeight = false;
            groupsListBox.ItemHeight = 45;
            groupsListBox.Location = new Point(0, 50);
            groupsListBox.Name = "groupsListBox";
            groupsListBox.Size = new Size(300, 530);
            groupsListBox.TabIndex = 0;
            // 
            // groupsHeaderPanel
            // 
            groupsHeaderPanel.BackColor = Color.FromArgb(240, 240, 240);
            groupsHeaderPanel.Controls.Add(groupsHeaderLabel);
            groupsHeaderPanel.Dock = DockStyle.Top;
            groupsHeaderPanel.Location = new Point(0, 0);
            groupsHeaderPanel.Name = "groupsHeaderPanel";
            groupsHeaderPanel.Size = new Size(300, 50);
            groupsHeaderPanel.TabIndex = 1;
            // 
            // groupsHeaderLabel
            // 
            groupsHeaderLabel.Dock = DockStyle.Fill;
            groupsHeaderLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            groupsHeaderLabel.ForeColor = Color.FromArgb(50, 49, 48);
            groupsHeaderLabel.Location = new Point(0, 0);
            groupsHeaderLabel.Name = "groupsHeaderLabel";
            groupsHeaderLabel.Padding = new Padding(20, 0, 0, 0);
            groupsHeaderLabel.Size = new Size(300, 50);
            groupsHeaderLabel.TabIndex = 0;
            groupsHeaderLabel.Text = "Groups";
            groupsHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // membershipPanel
            // 
            membershipPanel.BackColor = Color.White;
            membershipPanel.Controls.Add(contentSplitContainer);
            membershipPanel.Controls.Add(bottomPanel);
            membershipPanel.Dock = DockStyle.Fill;
            membershipPanel.Location = new Point(0, 0);
            membershipPanel.Name = "membershipPanel";
            membershipPanel.Size = new Size(1092, 580);
            membershipPanel.TabIndex = 0;
            // 
            // contentSplitContainer
            // 
            contentSplitContainer.Dock = DockStyle.Fill;
            contentSplitContainer.Location = new Point(0, 0);
            contentSplitContainer.Name = "contentSplitContainer";
            // 
            // contentSplitContainer.Panel1
            // 
            contentSplitContainer.Panel1.Controls.Add(membersPanel);
            // 
            // contentSplitContainer.Panel2
            // 
            contentSplitContainer.Panel2.Controls.Add(memberDetailsPanel);
            contentSplitContainer.Panel2MinSize = 250;
            contentSplitContainer.Size = new Size(1092, 460);
            contentSplitContainer.SplitterDistance = 750;
            contentSplitContainer.SplitterWidth = 8;
            contentSplitContainer.TabIndex = 0;
            // 
            // membersPanel
            // 
            membersPanel.BackColor = Color.FromArgb(250, 250, 250);
            membersPanel.Controls.Add(dataGridViewMembers);
            membersPanel.Controls.Add(membersHeaderPanel);
            membersPanel.Dock = DockStyle.Fill;
            membersPanel.Location = new Point(0, 0);
            membersPanel.Name = "membersPanel";
            membersPanel.Size = new Size(750, 460);
            membersPanel.TabIndex = 0;
            // 
            // dataGridViewMembers
            // 
            dataGridViewMembers.AllowUserToAddRows = false;
            dataGridViewMembers.AllowUserToDeleteRows = false;
            dataGridViewMembers.BackgroundColor = Color.FromArgb(250, 250, 250);
            dataGridViewMembers.BorderStyle = BorderStyle.None;
            dataGridViewMembers.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewMembers.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewMembers.ColumnHeadersHeight = 35;
            dataGridViewMembers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewMembers.ContextMenuStrip = contextMenuStripMembers;
            dataGridViewMembers.Dock = DockStyle.Fill;
            dataGridViewMembers.EnableHeadersVisualStyles = false;
            dataGridViewMembers.Font = new Font("Segoe UI", 9.5F);
            dataGridViewMembers.GridColor = Color.FromArgb(230, 230, 230);
            dataGridViewMembers.Location = new Point(0, 50);
            dataGridViewMembers.Name = "dataGridViewMembers";
            dataGridViewMembers.ReadOnly = true;
            dataGridViewMembers.RowHeadersVisible = false;
            dataGridViewMembers.RowHeadersWidth = 51;
            dataGridViewMembers.RowTemplate.Height = 30;
            dataGridViewMembers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMembers.Size = new Size(750, 410);
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
            // 
            // copyIdToolStripMenuItem
            // 
            copyIdToolStripMenuItem.Name = "copyIdToolStripMenuItem";
            copyIdToolStripMenuItem.Size = new Size(193, 22);
            copyIdToolStripMenuItem.Text = "Copy ID to Clipboard";
            // 
            // membersHeaderPanel
            // 
            membersHeaderPanel.BackColor = Color.FromArgb(240, 240, 240);
            membersHeaderPanel.Controls.Add(membersHeaderLabel);
            membersHeaderPanel.Dock = DockStyle.Top;
            membersHeaderPanel.Location = new Point(0, 0);
            membersHeaderPanel.Name = "membersHeaderPanel";
            membersHeaderPanel.Size = new Size(750, 50);
            membersHeaderPanel.TabIndex = 1;
            // 
            // membersHeaderLabel
            // 
            membersHeaderLabel.Dock = DockStyle.Fill;
            membersHeaderLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            membersHeaderLabel.ForeColor = Color.FromArgb(50, 49, 48);
            membersHeaderLabel.Location = new Point(0, 0);
            membersHeaderLabel.Name = "membersHeaderLabel";
            membersHeaderLabel.Padding = new Padding(20, 0, 0, 0);
            membersHeaderLabel.Size = new Size(750, 50);
            membersHeaderLabel.TabIndex = 0;
            membersHeaderLabel.Text = "Members";
            membersHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // memberDetailsPanel
            // 
            memberDetailsPanel.BackColor = Color.FromArgb(250, 250, 250);
            memberDetailsPanel.Controls.Add(memberDetailsLabel);
            memberDetailsPanel.Controls.Add(memberDetailsHeaderPanel);
            memberDetailsPanel.Dock = DockStyle.Fill;
            memberDetailsPanel.Location = new Point(0, 0);
            memberDetailsPanel.Name = "memberDetailsPanel";
            memberDetailsPanel.Size = new Size(334, 460);
            memberDetailsPanel.TabIndex = 0;
            // 
            // memberDetailsLabel
            // 
            memberDetailsLabel.Dock = DockStyle.Fill;
            memberDetailsLabel.Font = new Font("Segoe UI", 9.5F);
            memberDetailsLabel.ForeColor = Color.FromArgb(96, 94, 92);
            memberDetailsLabel.Location = new Point(0, 50);
            memberDetailsLabel.Name = "memberDetailsLabel";
            memberDetailsLabel.Padding = new Padding(20, 20, 20, 20);
            memberDetailsLabel.Size = new Size(334, 410);
            memberDetailsLabel.TabIndex = 0;
            memberDetailsLabel.Text = "Select a member to view details";
            memberDetailsLabel.TextAlign = ContentAlignment.TopLeft;
            // 
            // memberDetailsHeaderPanel
            // 
            memberDetailsHeaderPanel.BackColor = Color.FromArgb(240, 240, 240);
            memberDetailsHeaderPanel.Controls.Add(memberDetailsHeaderLabel);
            memberDetailsHeaderPanel.Dock = DockStyle.Top;
            memberDetailsHeaderPanel.Location = new Point(0, 0);
            memberDetailsHeaderPanel.Name = "memberDetailsHeaderPanel";
            memberDetailsHeaderPanel.Size = new Size(334, 50);
            memberDetailsHeaderPanel.TabIndex = 1;
            // 
            // memberDetailsHeaderLabel
            // 
            memberDetailsHeaderLabel.Dock = DockStyle.Fill;
            memberDetailsHeaderLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            memberDetailsHeaderLabel.ForeColor = Color.FromArgb(50, 49, 48);
            memberDetailsHeaderLabel.Location = new Point(0, 0);
            memberDetailsHeaderLabel.Name = "memberDetailsHeaderLabel";
            memberDetailsHeaderLabel.Padding = new Padding(20, 0, 0, 0);
            memberDetailsHeaderLabel.Size = new Size(334, 50);
            memberDetailsHeaderLabel.TabIndex = 0;
            memberDetailsHeaderLabel.Text = "Member Details";
            memberDetailsHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // actionsFlowPanel
            // 
            actionsFlowPanel.AutoSize = false;
            actionsFlowPanel.Controls.Add(btnBrowseAddUser);
            actionsFlowPanel.Controls.Add(btnRemoveUser);
            actionsFlowPanel.Controls.Add(btnReplaceUser);
            actionsFlowPanel.Controls.Add(btnAddNestedGroup);
            actionsFlowPanel.Controls.Add(btnRemoveNestedGroup);
            actionsFlowPanel.Controls.Add(btnCopyUsers);
            actionsFlowPanel.Controls.Add(btnSelectAll);
            actionsFlowPanel.Controls.Add(btnExportToCsv);
            actionsFlowPanel.Controls.Add(btnUndo);
            actionsFlowPanel.Dock = DockStyle.Left;
            actionsFlowPanel.FlowDirection = FlowDirection.LeftToRight;
            actionsFlowPanel.Location = new Point(0, 0);
            actionsFlowPanel.Name = "actionsFlowPanel";
            actionsFlowPanel.Padding = new Padding(20, 15, 20, 15);
            actionsFlowPanel.Size = new Size(750, 120);
            actionsFlowPanel.TabIndex = 0;
            actionsFlowPanel.WrapContents = true;
            // 
            // btnBrowseAddUser
            // 
            btnBrowseAddUser.BackColor = Color.FromArgb(0, 120, 212);
            btnBrowseAddUser.FlatAppearance.BorderSize = 0;
            btnBrowseAddUser.FlatStyle = FlatStyle.Flat;
            btnBrowseAddUser.Font = new Font("Segoe UI", 9F);
            btnBrowseAddUser.ForeColor = Color.White;
            btnBrowseAddUser.Location = new Point(20, 15);
            btnBrowseAddUser.Margin = new Padding(0, 5, 8, 5);
            btnBrowseAddUser.Name = "btnBrowseAddUser";
            btnBrowseAddUser.Size = new Size(100, 30);
            btnBrowseAddUser.TabIndex = 0;
            btnBrowseAddUser.Text = "Add Member(s)";
            btnBrowseAddUser.UseVisualStyleBackColor = false;
            // 
            // btnRemoveUser
            // 
            btnRemoveUser.BackColor = Color.FromArgb(0, 120, 212);
            btnRemoveUser.FlatAppearance.BorderSize = 0;
            btnRemoveUser.FlatStyle = FlatStyle.Flat;
            btnRemoveUser.Font = new Font("Segoe UI", 9F);
            btnRemoveUser.ForeColor = Color.White;
            btnRemoveUser.Location = new Point(128, 15);
            btnRemoveUser.Margin = new Padding(0, 5, 8, 5);
            btnRemoveUser.Name = "btnRemoveUser";
            btnRemoveUser.Size = new Size(115, 30);
            btnRemoveUser.TabIndex = 1;
            btnRemoveUser.Text = "Remove Member(s)";
            btnRemoveUser.UseVisualStyleBackColor = false;
            // 
            // btnReplaceUser
            // 
            btnReplaceUser.BackColor = Color.FromArgb(0, 120, 212);
            btnReplaceUser.FlatAppearance.BorderSize = 0;
            btnReplaceUser.FlatStyle = FlatStyle.Flat;
            btnReplaceUser.Font = new Font("Segoe UI", 9F);
            btnReplaceUser.ForeColor = Color.White;
            btnReplaceUser.Location = new Point(251, 15);
            btnReplaceUser.Margin = new Padding(0, 5, 8, 5);
            btnReplaceUser.Name = "btnReplaceUser";
            btnReplaceUser.Size = new Size(120, 30);
            btnReplaceUser.TabIndex = 2;
            btnReplaceUser.Text = "Replace Member(s)";
            btnReplaceUser.UseVisualStyleBackColor = false;
            // 
            // btnAddNestedGroup
            // 
            btnAddNestedGroup.BackColor = Color.FromArgb(0, 120, 212);
            btnAddNestedGroup.FlatAppearance.BorderSize = 0;
            btnAddNestedGroup.FlatStyle = FlatStyle.Flat;
            btnAddNestedGroup.Font = new Font("Segoe UI", 9F);
            btnAddNestedGroup.ForeColor = Color.White;
            btnAddNestedGroup.Location = new Point(379, 15);
            btnAddNestedGroup.Margin = new Padding(0, 5, 8, 5);
            btnAddNestedGroup.Name = "btnAddNestedGroup";
            btnAddNestedGroup.Size = new Size(110, 30);
            btnAddNestedGroup.TabIndex = 3;
            btnAddNestedGroup.Text = "Add Nested Group";
            btnAddNestedGroup.UseVisualStyleBackColor = false;
            // 
            // btnRemoveNestedGroup
            // 
            btnRemoveNestedGroup.BackColor = Color.FromArgb(0, 120, 212);
            btnRemoveNestedGroup.FlatAppearance.BorderSize = 0;
            btnRemoveNestedGroup.FlatStyle = FlatStyle.Flat;
            btnRemoveNestedGroup.Font = new Font("Segoe UI", 9F);
            btnRemoveNestedGroup.ForeColor = Color.White;
            btnRemoveNestedGroup.Location = new Point(497, 15);
            btnRemoveNestedGroup.Margin = new Padding(0, 5, 8, 5);
            btnRemoveNestedGroup.Name = "btnRemoveNestedGroup";
            btnRemoveNestedGroup.Size = new Size(135, 30);
            btnRemoveNestedGroup.TabIndex = 4;
            btnRemoveNestedGroup.Text = "Remove Nested Group";
            btnRemoveNestedGroup.UseVisualStyleBackColor = false;
            // 
            // btnCopyUsers
            // 
            btnCopyUsers.BackColor = Color.FromArgb(0, 120, 212);
            btnCopyUsers.FlatAppearance.BorderSize = 0;
            btnCopyUsers.FlatStyle = FlatStyle.Flat;
            btnCopyUsers.Font = new Font("Segoe UI", 9F);
            btnCopyUsers.ForeColor = Color.White;
            btnCopyUsers.Location = new Point(640, 15);
            btnCopyUsers.Margin = new Padding(0, 5, 8, 5);
            btnCopyUsers.Name = "btnCopyUsers";
            btnCopyUsers.Size = new Size(95, 30);
            btnCopyUsers.TabIndex = 5;
            btnCopyUsers.Text = "Copy Members";
            btnCopyUsers.UseVisualStyleBackColor = false;
            // 
            // btnSelectAll
            // 
            btnSelectAll.BackColor = Color.FromArgb(0, 120, 212);
            btnSelectAll.FlatAppearance.BorderSize = 0;
            btnSelectAll.FlatStyle = FlatStyle.Flat;
            btnSelectAll.Font = new Font("Segoe UI", 9F);
            btnSelectAll.ForeColor = Color.White;
            btnSelectAll.Location = new Point(20, 50);
            btnSelectAll.Margin = new Padding(0, 5, 8, 5);
            btnSelectAll.Name = "btnSelectAll";
            btnSelectAll.Size = new Size(95, 30);
            btnSelectAll.TabIndex = 6;
            btnSelectAll.Text = "Select All";
            btnSelectAll.UseVisualStyleBackColor = false;
            // 
            // btnExportToCsv
            // 
            btnExportToCsv.BackColor = Color.FromArgb(0, 120, 212);
            btnExportToCsv.FlatAppearance.BorderSize = 0;
            btnExportToCsv.FlatStyle = FlatStyle.Flat;
            btnExportToCsv.Font = new Font("Segoe UI", 9F);
            btnExportToCsv.ForeColor = Color.White;
            btnExportToCsv.Location = new Point(123, 50);
            btnExportToCsv.Margin = new Padding(0, 5, 8, 5);
            btnExportToCsv.Name = "btnExportToCsv";
            btnExportToCsv.Size = new Size(95, 30);
            btnExportToCsv.TabIndex = 7;
            btnExportToCsv.Text = "Export to CSV";
            btnExportToCsv.UseVisualStyleBackColor = false;
            // 
            // btnUndo
            // 
            btnUndo.BackColor = Color.FromArgb(0, 120, 212);
            btnUndo.FlatAppearance.BorderSize = 0;
            btnUndo.FlatStyle = FlatStyle.Flat;
            btnUndo.Font = new Font("Segoe UI", 9F);
            btnUndo.ForeColor = Color.White;
            btnUndo.Location = new Point(226, 50);
            btnUndo.Margin = new Padding(0, 5, 8, 5);
            btnUndo.Name = "btnUndo";
            btnUndo.Size = new Size(95, 30);
            btnUndo.TabIndex = 8;
            btnUndo.Text = "Undo Action";
            btnUndo.UseVisualStyleBackColor = false;
            // 
            // bottomPanel
            // 
            bottomPanel.BackColor = Color.FromArgb(248, 249, 250);
            bottomPanel.Controls.Add(returnButtonsPanel);
            bottomPanel.Controls.Add(actionsFlowPanel);
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Location = new Point(0, 460);
            bottomPanel.Name = "bottomPanel";
            bottomPanel.Size = new Size(1092, 120);
            bottomPanel.TabIndex = 1;
            // 
            // returnButtonsPanel
            // 
            returnButtonsPanel.Controls.Add(btnReturn);
            returnButtonsPanel.Controls.Add(btnReturnToPreviousWindow);
            returnButtonsPanel.Dock = DockStyle.Right;
            returnButtonsPanel.Location = new Point(770, 0);
            returnButtonsPanel.Margin = new Padding(0, 0, 20, 0);
            returnButtonsPanel.Name = "returnButtonsPanel";
            returnButtonsPanel.Size = new Size(322, 120);
            returnButtonsPanel.TabIndex = 1;
            // 
            // btnReturn
            // 
            btnReturn.BackColor = Color.FromArgb(108, 117, 125);
            btnReturn.FlatAppearance.BorderSize = 0;
            btnReturn.FlatStyle = FlatStyle.Flat;
            btnReturn.Font = new Font("Segoe UI", 9F);
            btnReturn.ForeColor = Color.White;
            btnReturn.Location = new Point(10, 44);
            btnReturn.Margin = new Padding(5, 5, 8, 5);
            btnReturn.Name = "btnReturn";
            btnReturn.Size = new Size(130, 32);
            btnReturn.TabIndex = 0;
            btnReturn.Text = "← Return Home";
            btnReturn.UseVisualStyleBackColor = false;
            // 
            // btnReturnToPreviousWindow
            // 
            btnReturnToPreviousWindow.BackColor = Color.FromArgb(108, 117, 125);
            btnReturnToPreviousWindow.FlatAppearance.BorderSize = 0;
            btnReturnToPreviousWindow.FlatStyle = FlatStyle.Flat;
            btnReturnToPreviousWindow.Font = new Font("Segoe UI", 9F);
            btnReturnToPreviousWindow.ForeColor = Color.White;
            btnReturnToPreviousWindow.Location = new Point(148, 44);
            btnReturnToPreviousWindow.Margin = new Padding(0, 5, 5, 5);
            btnReturnToPreviousWindow.Name = "btnReturnToPreviousWindow";
            btnReturnToPreviousWindow.Size = new Size(140, 32);
            btnReturnToPreviousWindow.TabIndex = 1;
            btnReturnToPreviousWindow.Text = "← Return Previous";
            btnReturnToPreviousWindow.UseVisualStyleBackColor = false;
            // 
            // ManageMembershipsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1400, 650);
            Controls.Add(mainSplitContainer);
            Controls.Add(headerPanel);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(1400, 650);
            Name = "ManageMembershipsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Manage Group Memberships - Entra Groups App";
            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            mainSplitContainer.Panel1.ResumeLayout(false);
            mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).EndInit();
            mainSplitContainer.ResumeLayout(false);
            groupsPanel.ResumeLayout(false);
            groupsHeaderPanel.ResumeLayout(false);
            membershipPanel.ResumeLayout(false);
            contentSplitContainer.Panel1.ResumeLayout(false);
            contentSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)contentSplitContainer).EndInit();
            contentSplitContainer.ResumeLayout(false);
            membersPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewMembers).EndInit();
            contextMenuStripMembers.ResumeLayout(false);
            membersHeaderPanel.ResumeLayout(false);
            memberDetailsPanel.ResumeLayout(false);
            memberDetailsHeaderPanel.ResumeLayout(false);
            actionsFlowPanel.ResumeLayout(false);
            bottomPanel.ResumeLayout(false);
            returnButtonsPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label instructionsLabel;
        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.Panel groupsPanel;
        private System.Windows.Forms.ListBox groupsListBox;
        private System.Windows.Forms.Panel groupsHeaderPanel;
        private System.Windows.Forms.Label groupsHeaderLabel;
        private System.Windows.Forms.Panel membershipPanel;
        private System.Windows.Forms.SplitContainer contentSplitContainer;
        private System.Windows.Forms.Panel membersPanel;
        private System.Windows.Forms.DataGridView dataGridViewMembers;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripMembers;
        private System.Windows.Forms.ToolStripMenuItem expandGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyIdToolStripMenuItem;
        private System.Windows.Forms.Panel membersHeaderPanel;
        private System.Windows.Forms.Label membersHeaderLabel;
        private System.Windows.Forms.Panel memberDetailsPanel;
        private System.Windows.Forms.Label memberDetailsLabel;
        private System.Windows.Forms.Panel memberDetailsHeaderPanel;
        private System.Windows.Forms.Label memberDetailsHeaderLabel;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.FlowLayoutPanel actionsFlowPanel;
        private System.Windows.Forms.Button btnBrowseAddUser;
        private System.Windows.Forms.Button btnRemoveUser;
        private System.Windows.Forms.Button btnReplaceUser;
        private System.Windows.Forms.Button btnAddNestedGroup;
        private System.Windows.Forms.Button btnRemoveNestedGroup;
        private System.Windows.Forms.Button btnCopyUsers;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnExportToCsv;
        private System.Windows.Forms.Button btnUndo;
        private System.Windows.Forms.Panel returnButtonsPanel;
        private System.Windows.Forms.Button btnReturn;
        private System.Windows.Forms.Button btnReturnToPreviousWindow;
    }
}
