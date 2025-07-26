namespace EntraGroupsApp
{
    partial class SubfolderPermissionsEditDialog
    {
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tvSubfolders = new System.Windows.Forms.TreeView();
            this.imageListIcons = new System.Windows.Forms.ImageList(this.components);
            this.pnlSidebar = new System.Windows.Forms.Panel();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnChange = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnBreakInheritance = new System.Windows.Forms.Button();
            this.btnResetPermissions = new System.Windows.Forms.Button();
            this.cmbGroups = new System.Windows.Forms.ComboBox();
            this.cmbPermissions = new System.Windows.Forms.ComboBox();
            this.lblSelectedItem = new System.Windows.Forms.Label();
            this.statusLabel = new System.Windows.Forms.Label();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.txtSearch = new System.Windows.Forms.ToolStripTextBox();
            this.cmbView = new System.Windows.Forms.ToolStripComboBox();
            this.btnRefresh = new System.Windows.Forms.ToolStripButton();
            this.btnClose = new System.Windows.Forms.ToolStripButton();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.pnlSidebar.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // tvSubfolders
            // 
            this.tvSubfolders.ImageIndex = 0;
            this.tvSubfolders.ImageList = this.imageListIcons;
            this.tvSubfolders.Location = new System.Drawing.Point(12, 37);
            this.tvSubfolders.Name = "tvSubfolders";
            this.tvSubfolders.SelectedImageIndex = 0;
            this.tvSubfolders.Size = new System.Drawing.Size(438, 463);
            this.tvSubfolders.TabIndex = 0;
            this.tvSubfolders.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvSubfolders_AfterSelect);
            this.tvSubfolders.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.tvSubfolders_NodeMouseClick);
            this.toolTip.SetToolTip(this.tvSubfolders, "Select a subfolder or group to view or modify permissions. Right-click for more options.");
            // 
            // imageListIcons
            // 
            this.imageListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListIcons.Images.Add("Folder", System.Drawing.SystemIcons.WinLogo);
            this.imageListIcons.Images.Add("Group", System.Drawing.SystemIcons.WinLogo);
            // 
            // pnlSidebar
            // 
            this.pnlSidebar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlSidebar.Controls.Add(this.btnAdd);
            this.pnlSidebar.Controls.Add(this.btnChange);
            this.pnlSidebar.Controls.Add(this.btnRemove);
            this.pnlSidebar.Controls.Add(this.btnBreakInheritance);
            this.pnlSidebar.Controls.Add(this.btnResetPermissions);
            this.pnlSidebar.Controls.Add(this.cmbGroups);
            this.pnlSidebar.Controls.Add(this.cmbPermissions);
            this.pnlSidebar.Controls.Add(this.lblSelectedItem);
            this.pnlSidebar.Controls.Add(this.statusLabel);
            this.pnlSidebar.Location = new System.Drawing.Point(456, 37);
            this.pnlSidebar.Name = "pnlSidebar";
            this.pnlSidebar.Size = new System.Drawing.Size(200, 463);
            this.pnlSidebar.TabIndex = 1;
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(10, 180);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(180, 25);
            this.btnAdd.TabIndex = 0;
            this.btnAdd.Text = "Add Permission";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            this.toolTip.SetToolTip(this.btnAdd, "Add the selected group with the chosen permission to the subfolder.");
            // 
            // btnChange
            // 
            this.btnChange.Location = new System.Drawing.Point(10, 210);
            this.btnChange.Name = "btnChange";
            this.btnChange.Size = new System.Drawing.Size(180, 25);
            this.btnChange.TabIndex = 1;
            this.btnChange.Text = "Change Permission";
            this.btnChange.UseVisualStyleBackColor = true;
            this.btnChange.Click += new System.EventHandler(this.btnChange_Click);
            this.toolTip.SetToolTip(this.btnChange, "Change the permission type for the selected group.");
            // 
            // btnRemove
            // 
            this.btnRemove.Location = new System.Drawing.Point(10, 240);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(180, 25);
            this.btnRemove.TabIndex = 2;
            this.btnRemove.Text = "Remove Permission";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            this.toolTip.SetToolTip(this.btnRemove, "Remove the selected group's permissions or all groups from the subfolder.");
            // 
            // btnBreakInheritance
            // 
            this.btnBreakInheritance.Location = new System.Drawing.Point(10, 270);
            this.btnBreakInheritance.Name = "btnBreakInheritance";
            this.btnBreakInheritance.Size = new System.Drawing.Size(180, 25);
            this.btnBreakInheritance.TabIndex = 3;
            this.btnBreakInheritance.Text = "Break Inheritance";
            this.btnBreakInheritance.UseVisualStyleBackColor = true;
            this.btnBreakInheritance.Click += new System.EventHandler(this.btnBreakInheritance_Click);
            this.toolTip.SetToolTip(this.btnBreakInheritance, "Break inheritance to allow unique permissions (clears all existing permissions).");
            // 
            // btnResetPermissions
            // 
            this.btnResetPermissions.Location = new System.Drawing.Point(10, 300);
            this.btnResetPermissions.Name = "btnResetPermissions";
            this.btnResetPermissions.Size = new System.Drawing.Size(180, 25);
            this.btnResetPermissions.TabIndex = 4;
            this.btnResetPermissions.Text = "Reset Permissions";
            this.btnResetPermissions.UseVisualStyleBackColor = true;
            this.btnResetPermissions.Click += new System.EventHandler(this.btnResetPermissions_Click);
            this.toolTip.SetToolTip(this.btnResetPermissions, "Remove all group permissions from the selected subfolder.");
            // 
            // cmbGroups
            // 
            this.cmbGroups.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGroups.FormattingEnabled = true;
            this.cmbGroups.Location = new System.Drawing.Point(10, 110);
            this.cmbGroups.Name = "cmbGroups";
            this.cmbGroups.Size = new System.Drawing.Size(180, 21);
            this.cmbGroups.TabIndex = 5;
            this.toolTip.SetToolTip(this.cmbGroups, "Select a CSG-CLBA-MKTG group to assign permissions.");
            // 
            // cmbPermissions
            // 
            this.cmbPermissions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPermissions.FormattingEnabled = true;
            this.cmbPermissions.Items.AddRange(new object[] { "Read", "Edit", "No Direct Access" });
            this.cmbPermissions.Location = new System.Drawing.Point(10, 140);
            this.cmbPermissions.Name = "cmbPermissions";
            this.cmbPermissions.Size = new System.Drawing.Size(180, 21);
            this.cmbPermissions.TabIndex = 6;
            this.toolTip.SetToolTip(this.cmbPermissions, "Select the permission type for the group.");
            // 
            // lblSelectedItem
            // 
            this.lblSelectedItem.AutoSize = true;
            this.lblSelectedItem.Location = new System.Drawing.Point(10, 10);
            this.lblSelectedItem.Name = "lblSelectedItem";
            this.lblSelectedItem.Size = new System.Drawing.Size(113, 13);
            this.lblSelectedItem.Text = "Selected Item: None";
            this.toolTip.SetToolTip(this.lblSelectedItem, "Shows the currently selected subfolder or group.");
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = false;
            this.statusLabel.Location = new System.Drawing.Point(10, 350);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(180, 100);
            this.statusLabel.Text = "Ready";
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.toolTip.SetToolTip(this.statusLabel, "Displays the status of recent actions.");
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.txtSearch,
                this.cmbView,
                this.btnRefresh,
                this.btnClose});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(660, 25);
            this.toolStrip.TabIndex = 2;
            // 
            // txtSearch
            // 
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(200, 25);
            this.txtSearch.Text = "Search subfolders or groups...";
            this.txtSearch.ToolTipText = "Filter subfolders or groups by name.";
            // 
            // cmbView
            // 
            this.cmbView.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbView.Items.AddRange(new object[] { "All Subfolders", "Unique Permissions Only", "Inherited Permissions Only" });
            this.cmbView.Name = "cmbView";
            this.cmbView.Size = new System.Drawing.Size(150, 25);
            this.cmbView.ToolTipText = "Filter subfolders by permission type.";
            // 
            // btnRefresh
            // 
            this.btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(60, 22);
            this.btnRefresh.ToolTipText = "Refresh the list of subfolders and permissions.";
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnClose
            // 
            this.btnClose.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnClose.Text = "Close";
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(60, 22);
            this.btnClose.ToolTipText = "Close the dialog.";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 5000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 100;
            // 
            // SubfolderPermissionsEditDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(660, 510);
            this.Controls.Add(this.tvSubfolders);
            this.Controls.Add(this.pnlSidebar);
            this.Controls.Add(this.toolStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SubfolderPermissionsEditDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Subfolder Permissions";
            this.pnlSidebar.ResumeLayout(false);
            this.pnlSidebar.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.TreeView tvSubfolders;
        private System.Windows.Forms.ImageList imageListIcons;
        private System.Windows.Forms.Panel pnlSidebar;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnChange;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnBreakInheritance;
        private System.Windows.Forms.Button btnResetPermissions;
        private System.Windows.Forms.ComboBox cmbGroups;
        private System.Windows.Forms.ComboBox cmbPermissions;
        private System.Windows.Forms.Label lblSelectedItem;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripTextBox txtSearch;
        private System.Windows.Forms.ToolStripComboBox cmbView;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripButton btnClose;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
