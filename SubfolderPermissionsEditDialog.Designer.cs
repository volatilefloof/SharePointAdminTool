namespace EntraGroupsApp
{
    partial class SubfolderPermissionsEditDialog
    {
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tvSubfolders = new System.Windows.Forms.TreeView();
            this.treeViewImageList = new System.Windows.Forms.ImageList(this.components);
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnChange = new System.Windows.Forms.Button();
            this.btnBreakInheritance = new System.Windows.Forms.Button();
            this.btnResetPermissions = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.cmbView = new System.Windows.Forms.ComboBox();
            this.cmbGroups = new System.Windows.Forms.ComboBox();  // Fixed: Changed from CheckedListBox (clbGroups) to ComboBox (cmbGroups) for single-select consistency.
            this.lblSelectedItem = new System.Windows.Forms.Label();
            this.chkRead = new System.Windows.Forms.CheckBox();
            this.chkEdit = new System.Windows.Forms.CheckBox();
            this.chkNoAccess = new System.Windows.Forms.CheckBox();
            this.toolTipGroups = new System.Windows.Forms.ToolTip(this.components);  // Added: Initialized toolTipGroups from your class declarations; can be used for group hover tips.

            // treeViewImageList
            this.treeViewImageList.ImageSize = new System.Drawing.Size(12, 12);
            this.treeViewImageList.Images.Add("Folder", System.Drawing.SystemIcons.Application.ToBitmap());
            this.treeViewImageList.Images.Add("Group", System.Drawing.SystemIcons.WinLogo.ToBitmap());

            // tvSubfolders
            this.tvSubfolders.ImageList = this.treeViewImageList;
            this.tvSubfolders.Location = new System.Drawing.Point(12, 12);
            this.tvSubfolders.Name = "tvSubfolders";
            this.tvSubfolders.Size = new System.Drawing.Size(400, 400);
            this.tvSubfolders.TabIndex = 0;
            this.tvSubfolders.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvSubfolders_AfterSelect);

            // btnAdd
            this.btnAdd.Location = new System.Drawing.Point(420, 12);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(460, 30);
            this.btnAdd.TabIndex = 1;
            this.btnAdd.Text = "Add Permission";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);

            // btnRemove
            this.btnRemove.Location = new System.Drawing.Point(420, 50);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(460, 30);
            this.btnRemove.TabIndex = 2;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);

            // btnChange
            this.btnChange.Location = new System.Drawing.Point(420, 88);
            this.btnChange.Name = "btnChange";
            this.btnChange.Size = new System.Drawing.Size(460, 30);
            this.btnChange.TabIndex = 3;
            this.btnChange.Text = "Change";
            this.btnChange.UseVisualStyleBackColor = true;
            this.btnChange.Visible = false;

            // btnBreakInheritance
            this.btnBreakInheritance.Location = new System.Drawing.Point(420, 126);
            this.btnBreakInheritance.Name = "btnBreakInheritance";
            this.btnBreakInheritance.Size = new System.Drawing.Size(460, 30);
            this.btnBreakInheritance.TabIndex = 4;
            this.btnBreakInheritance.Text = "Break Inheritance";
            this.btnBreakInheritance.UseVisualStyleBackColor = true;
            this.btnBreakInheritance.Click += new System.EventHandler(this.btnBreakInheritance_Click);

            // btnResetPermissions
            this.btnResetPermissions.Location = new System.Drawing.Point(420, 164);
            this.btnResetPermissions.Name = "btnResetPermissions";
            this.btnResetPermissions.Size = new System.Drawing.Size(460, 30);
            this.btnResetPermissions.TabIndex = 5;
            this.btnResetPermissions.Text = "Reset Permissions";
            this.btnResetPermissions.UseVisualStyleBackColor = true;
            this.btnResetPermissions.Click += new System.EventHandler(this.btnResetPermissions_Click);

            // btnRefresh
            this.btnRefresh.Location = new System.Drawing.Point(420, 202);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(460, 30);
            this.btnRefresh.TabIndex = 6;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

            // statusLabel
            this.statusLabel.Location = new System.Drawing.Point(12, 420);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(400, 23);
            this.statusLabel.TabIndex = 8;
            this.statusLabel.Text = "Ready";

            // txtSearch
            this.txtSearch.Location = new System.Drawing.Point(12, 480);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(200, 20);
            this.txtSearch.TabIndex = 10;

            // cmbView
            this.cmbView.Location = new System.Drawing.Point(220, 480);
            this.cmbView.Name = "cmbView";
            this.cmbView.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbView.Items.AddRange(new object[] { "All Subfolders", "Unique Permissions Only", "Inherited Permissions Only" });
            this.cmbView.Size = new System.Drawing.Size(192, 21);
            this.cmbView.TabIndex = 11;

            // cmbGroups (Fixed: Replaced clbGroups with cmbGroups; set DropDownStyle to DropDownList for selection consistency)
            this.cmbGroups.Location = new System.Drawing.Point(420, 240);
            this.cmbGroups.Name = "cmbGroups";
            this.cmbGroups.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGroups.Size = new System.Drawing.Size(460, 100);
            this.cmbGroups.TabIndex = 12;
            // Added: Optional toolTip for groups (e.g., show group ID or description on hover).
            this.toolTipGroups.SetToolTip(this.cmbGroups, "Select a group to assign permissions.");

            // lblSelectedItem
            this.lblSelectedItem.Location = new System.Drawing.Point(420, 350);
            this.lblSelectedItem.Name = "lblSelectedItem";
            this.lblSelectedItem.Size = new System.Drawing.Size(460, 23);
            this.lblSelectedItem.TabIndex = 13;
            this.lblSelectedItem.Text = "Selected Item: None";

            // chkRead
            this.chkRead.Location = new System.Drawing.Point(420, 380);
            this.chkRead.Name = "chkRead";
            this.chkRead.Size = new System.Drawing.Size(150, 24);
            this.chkRead.TabIndex = 14;
            this.chkRead.Text = "Read";
            this.chkRead.UseVisualStyleBackColor = true;
            this.chkRead.CheckedChanged += new System.EventHandler(this.chkPermission_CheckedChanged);

            // chkEdit
            this.chkEdit.Location = new System.Drawing.Point(570, 380);
            this.chkEdit.Name = "chkEdit";
            this.chkEdit.Size = new System.Drawing.Size(150, 24);
            this.chkEdit.TabIndex = 15;
            this.chkEdit.Text = "Edit";
            this.chkEdit.UseVisualStyleBackColor = true;
            this.chkEdit.CheckedChanged += new System.EventHandler(this.chkPermission_CheckedChanged);

            // chkNoAccess
            this.chkNoAccess.Location = new System.Drawing.Point(720, 380);
            this.chkNoAccess.Name = "chkNoAccess";
            this.chkNoAccess.Size = new System.Drawing.Size(160, 24);
            this.chkNoAccess.TabIndex = 16;
            this.chkNoAccess.Text = "No Access";
            this.chkNoAccess.UseVisualStyleBackColor = true;
            this.chkNoAccess.CheckedChanged += new System.EventHandler(this.chkPermission_CheckedChanged);

            // SubfolderPermissionsEditDialog
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Controls.Add(this.chkNoAccess);
            this.Controls.Add(this.chkEdit);
            this.Controls.Add(this.chkRead);
            this.Controls.Add(this.lblSelectedItem);
            this.Controls.Add(this.cmbGroups);  // Fixed: Added cmbGroups to controls (was clbGroups).
            this.Controls.Add(this.cmbView);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.btnResetPermissions);
            this.Controls.Add(this.btnBreakInheritance);
            this.Controls.Add(this.btnChange);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.tvSubfolders);
            this.Name = "SubfolderPermissionsEditDialog";
            this.Text = "Edit Subfolder Permissions";
            this.ResumeLayout(false);
        }
        private System.Windows.Forms.TreeView tvSubfolders;
        private System.Windows.Forms.ImageList treeViewImageList;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnChange;
        private System.Windows.Forms.Button btnBreakInheritance;
        private System.Windows.Forms.Button btnResetPermissions;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ComboBox cmbView;
        private System.Windows.Forms.CheckedListBox clbGroups;
        private System.Windows.Forms.Label lblSelectedItem;
        private System.Windows.Forms.CheckBox chkRead;
        private System.Windows.Forms.CheckBox chkEdit;
        private System.Windows.Forms.CheckBox chkNoAccess;
    }
}
