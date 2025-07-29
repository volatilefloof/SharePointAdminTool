namespace EntraGroupsApp
{
    partial class SubfolderPermissionsEditDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblLibrary = new System.Windows.Forms.Label();
            this.lblInstructions = new System.Windows.Forms.Label();
            this.tvSubfolders = new System.Windows.Forms.TreeView();
            this.treeViewImageList = new System.Windows.Forms.ImageList(this.components);
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnPreview = new System.Windows.Forms.Button();
            this.btnBreakInheritance = new System.Windows.Forms.Button();
            this.btnRestoreInheritance = new System.Windows.Forms.Button();
            this.btnResetPermissions = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnViewSubfolders = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.cmbView = new System.Windows.Forms.ComboBox();
            this.cmbGroups = new System.Windows.Forms.ComboBox();
            this.lblSelectedItem = new System.Windows.Forms.Label();
            this.radioRead = new System.Windows.Forms.RadioButton();
            this.radioEdit = new System.Windows.Forms.RadioButton();
            this.radioNoAccess = new System.Windows.Forms.RadioButton();
            this.lblValidation = new System.Windows.Forms.Label();
            this.toolTipGroups = new System.Windows.Forms.ToolTip(this.components);
            this.grpSubfolders = new System.Windows.Forms.GroupBox();
            this.lblSearchFilter = new System.Windows.Forms.Label();
            this.lblViewFilter = new System.Windows.Forms.Label();
            this.grpPermissions = new System.Windows.Forms.GroupBox();
            this.lblGroups = new System.Windows.Forms.Label();
            this.lblPermissionLevel = new System.Windows.Forms.Label();
            this.grpActions = new System.Windows.Forms.GroupBox();
            this.lblNestingInfo = new System.Windows.Forms.Label();
            this.btnExpandAll = new System.Windows.Forms.Button();
            this.btnCollapseAll = new System.Windows.Forms.Button();

            this.grpSubfolders.SuspendLayout();
            this.grpPermissions.SuspendLayout();
            this.grpActions.SuspendLayout();
            this.SuspendLayout();

            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.Location = new System.Drawing.Point(16, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(259, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Edit Nested Subfolder Permissions";

            // 
            // lblLibrary
            // 
            this.lblLibrary.AutoSize = true;
            this.lblLibrary.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLibrary.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lblLibrary.Location = new System.Drawing.Point(17, 45);
            this.lblLibrary.Name = "lblLibrary";
            this.lblLibrary.Size = new System.Drawing.Size(54, 15);
            this.lblLibrary.TabIndex = 1;
            this.lblLibrary.Text = "Library: ";

            // 
            // lblInstructions
            // 
            this.lblInstructions.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblInstructions.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lblInstructions.Location = new System.Drawing.Point(17, 70);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(950, 32);
            this.lblInstructions.TabIndex = 2;
            this.lblInstructions.Text = "Select a subfolder from the hierarchical list to edit its permissions. Expand folders or use 'View Subfolders' to load nested folders. Use the controls on the right to manage permissions.";

            // 
            // grpSubfolders
            // 
            this.grpSubfolders.Controls.Add(this.lblSearchFilter);
            this.grpSubfolders.Controls.Add(this.txtSearch);
            this.grpSubfolders.Controls.Add(this.lblViewFilter);
            this.grpSubfolders.Controls.Add(this.cmbView);
            this.grpSubfolders.Controls.Add(this.btnExpandAll);
            this.grpSubfolders.Controls.Add(this.btnCollapseAll);
            this.grpSubfolders.Controls.Add(this.tvSubfolders);
            this.grpSubfolders.Controls.Add(this.lblNestingInfo);
            this.grpSubfolders.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpSubfolders.Location = new System.Drawing.Point(20, 110);
            this.grpSubfolders.Name = "grpSubfolders";
            this.grpSubfolders.Size = new System.Drawing.Size(520, 500);
            this.grpSubfolders.TabIndex = 3;
            this.grpSubfolders.TabStop = false;
            this.grpSubfolders.Text = "Nested Subfolders";

            // 
            // lblSearchFilter
            // 
            this.lblSearchFilter.AutoSize = true;
            this.lblSearchFilter.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSearchFilter.Location = new System.Drawing.Point(13, 20);
            this.lblSearchFilter.Name = "lblSearchFilter";
            this.lblSearchFilter.Size = new System.Drawing.Size(47, 15);
            this.lblSearchFilter.TabIndex = 4;
            this.lblSearchFilter.Text = "Search:";

            // 
            // txtSearch
            // 
            this.txtSearch.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSearch.Location = new System.Drawing.Point(16, 40);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(200, 21);
            this.txtSearch.TabIndex = 5;

            // 
            // lblViewFilter
            // 
            this.lblViewFilter.AutoSize = true;
            this.lblViewFilter.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblViewFilter.Location = new System.Drawing.Point(225, 20);
            this.lblViewFilter.Name = "lblViewFilter";
            this.lblViewFilter.Size = new System.Drawing.Size(35, 15);
            this.lblViewFilter.TabIndex = 6;
            this.lblViewFilter.Text = "View:";

            // 
            // cmbView
            // 
            this.cmbView.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbView.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbView.Items.AddRange(new object[] {
    "All Subfolders",
    "Unique Permissions Only",
    "Inherited Permissions Only"});
            this.cmbView.Location = new System.Drawing.Point(228, 40);
            this.cmbView.Name = "cmbView";
            this.cmbView.Size = new System.Drawing.Size(160, 21);
            this.cmbView.TabIndex = 7;

            // 
            // btnExpandAll
            // 
            this.btnExpandAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnExpandAll.Location = new System.Drawing.Point(400, 40);
            this.btnExpandAll.Name = "btnExpandAll";
            this.btnExpandAll.Size = new System.Drawing.Size(50, 21);
            this.btnExpandAll.TabIndex = 8;
            this.btnExpandAll.Text = "Expand";
            this.btnExpandAll.UseVisualStyleBackColor = true;
            this.btnExpandAll.Click += new System.EventHandler(this.btnExpandAll_Click);

            // 
            // btnCollapseAll
            // 
            this.btnCollapseAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCollapseAll.Location = new System.Drawing.Point(455, 40);
            this.btnCollapseAll.Name = "btnCollapseAll";
            this.btnCollapseAll.Size = new System.Drawing.Size(55, 21);
            this.btnCollapseAll.TabIndex = 9;
            this.btnCollapseAll.Text = "Collapse";
            this.btnCollapseAll.UseVisualStyleBackColor = true;
            this.btnCollapseAll.Click += new System.EventHandler(this.btnCollapseAll_Click);

            // 
            // tvSubfolders
            // 
            this.tvSubfolders.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tvSubfolders.ImageIndex = 0;
            this.tvSubfolders.ImageList = this.treeViewImageList;
            this.tvSubfolders.Location = new System.Drawing.Point(16, 75);
            this.tvSubfolders.Name = "tvSubfolders";
            this.tvSubfolders.Size = new System.Drawing.Size(492, 380);
            this.tvSubfolders.TabIndex = 10;
            this.tvSubfolders.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvSubfolders_AfterSelect);
            this.tvSubfolders.BeforeSelect += new System.Windows.Forms.TreeViewCancelEventHandler(this.tvSubfolders_BeforeSelect);
            this.tvSubfolders.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.tvSubfolders_BeforeExpand);
            this.tvSubfolders.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.tvSubfolders_NodeMouseClick);

            // 
            // treeViewImageList
            // 
            this.treeViewImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            this.treeViewImageList.ImageSize = new System.Drawing.Size(16, 16);
            this.treeViewImageList.TransparentColor = System.Drawing.Color.Transparent;

            // 
            // lblNestingInfo
            // 
            this.lblNestingInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblNestingInfo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(128)))));
            this.lblNestingInfo.Location = new System.Drawing.Point(16, 465);
            this.lblNestingInfo.Name = "lblNestingInfo";
            this.lblNestingInfo.Size = new System.Drawing.Size(492, 20);
            this.lblNestingInfo.TabIndex = 11;
            this.lblNestingInfo.Text = "Nested folders are loaded up to 3 levels deep. Use right-click for additional options.";

            // 
            // grpPermissions
            // 
            this.grpPermissions.Controls.Add(this.lblSelectedItem);
            this.grpPermissions.Controls.Add(this.lblGroups);
            this.grpPermissions.Controls.Add(this.cmbGroups);
            this.grpPermissions.Controls.Add(this.lblPermissionLevel);
            this.grpPermissions.Controls.Add(this.radioRead);
            this.grpPermissions.Controls.Add(this.radioEdit);
            this.grpPermissions.Controls.Add(this.radioNoAccess);
            this.grpPermissions.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpPermissions.Location = new System.Drawing.Point(560, 110);
            this.grpPermissions.Name = "grpPermissions";
            this.grpPermissions.Size = new System.Drawing.Size(420, 200);
            this.grpPermissions.TabIndex = 12;
            this.grpPermissions.TabStop = false;
            this.grpPermissions.Text = "Permission Settings";

            // 
            // lblSelectedItem
            // 
            this.lblSelectedItem.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSelectedItem.Location = new System.Drawing.Point(17, 20);
            this.lblSelectedItem.Name = "lblSelectedItem";
            this.lblSelectedItem.Size = new System.Drawing.Size(386, 20);
            this.lblSelectedItem.TabIndex = 13;
            this.lblSelectedItem.Text = "Selected Item: None";

            // 
            // lblGroups
            // 
            this.lblGroups.AutoSize = true;
            this.lblGroups.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblGroups.Location = new System.Drawing.Point(17, 50);
            this.lblGroups.Name = "lblGroups";
            this.lblGroups.Size = new System.Drawing.Size(43, 15);
            this.lblGroups.TabIndex = 14;
            this.lblGroups.Text = "Group:";

            // 
            // cmbGroups
            // 
            this.cmbGroups.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGroups.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbGroups.Location = new System.Drawing.Point(20, 70);
            this.cmbGroups.Name = "cmbGroups";
            this.cmbGroups.Size = new System.Drawing.Size(383, 21);
            this.cmbGroups.TabIndex = 15;
            this.toolTipGroups.SetToolTip(this.cmbGroups, "Select a group to assign permissions.");
            this.cmbGroups.SelectedIndexChanged += new System.EventHandler(this.cmbGroups_SelectedIndexChanged);

            // 
            // lblPermissionLevel
            // 
            this.lblPermissionLevel.AutoSize = true;
            this.lblPermissionLevel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPermissionLevel.Location = new System.Drawing.Point(17, 105);
            this.lblPermissionLevel.Name = "lblPermissionLevel";
            this.lblPermissionLevel.Size = new System.Drawing.Size(102, 15);
            this.lblPermissionLevel.TabIndex = 16;
            this.lblPermissionLevel.Text = "Permission Level:";

            // 
            // radioRead
            // 
            this.radioRead.AutoSize = true;
            this.radioRead.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radioRead.Location = new System.Drawing.Point(20, 130);
            this.radioRead.Name = "radioRead";
            this.radioRead.Size = new System.Drawing.Size(55, 19);
            this.radioRead.TabIndex = 17;
            this.radioRead.Text = "Read";
            this.radioRead.UseVisualStyleBackColor = true;
            this.radioRead.CheckedChanged += new System.EventHandler(this.radioPermission_CheckedChanged);

            // 
            // radioEdit
            // 
            this.radioEdit.AutoSize = true;
            this.radioEdit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radioEdit.Location = new System.Drawing.Point(90, 130);
            this.radioEdit.Name = "radioEdit";
            this.radioEdit.Size = new System.Drawing.Size(49, 19);
            this.radioEdit.TabIndex = 18;
            this.radioEdit.Text = "Edit";
            this.radioEdit.UseVisualStyleBackColor = true;
            this.radioEdit.CheckedChanged += new System.EventHandler(this.radioPermission_CheckedChanged);

            // 
            // radioNoAccess
            // 
            this.radioNoAccess.AutoSize = true;
            this.radioNoAccess.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radioNoAccess.Location = new System.Drawing.Point(155, 130);
            this.radioNoAccess.Name = "radioNoAccess";
            this.radioNoAccess.Size = new System.Drawing.Size(117, 19);
            this.radioNoAccess.TabIndex = 19;
            this.radioNoAccess.Text = "No Direct Access";
            this.radioNoAccess.UseVisualStyleBackColor = true;
            this.radioNoAccess.CheckedChanged += new System.EventHandler(this.radioPermission_CheckedChanged);

            // 
            // grpActions
            // 
            this.grpActions.Controls.Add(this.btnPreview);
            this.grpActions.Controls.Add(this.btnAdd);
            this.grpActions.Controls.Add(this.btnRemove);
            this.grpActions.Controls.Add(this.btnBreakInheritance);
            this.grpActions.Controls.Add(this.btnRestoreInheritance);
            this.grpActions.Controls.Add(this.btnResetPermissions);
            this.grpActions.Controls.Add(this.btnViewSubfolders);
            this.grpActions.Controls.Add(this.btnRefresh);
            this.grpActions.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpActions.Location = new System.Drawing.Point(560, 330);
            this.grpActions.Name = "grpActions";
            this.grpActions.Size = new System.Drawing.Size(420, 280);
            this.grpActions.TabIndex = 20;
            this.grpActions.TabStop = false;
            this.grpActions.Text = "Actions";

            // 
            // btnPreview
            // 
            this.btnPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnPreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPreview.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnPreview.ForeColor = System.Drawing.Color.White;
            this.btnPreview.Location = new System.Drawing.Point(20, 20);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(383, 30);
            this.btnPreview.TabIndex = 21;
            this.btnPreview.Text = "Preview Permission";
            this.btnPreview.UseVisualStyleBackColor = false;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);

            // 
            // btnAdd
            // 
            this.btnAdd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAdd.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAdd.ForeColor = System.Drawing.Color.White;
            this.btnAdd.Location = new System.Drawing.Point(20, 55);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(185, 30);
            this.btnAdd.TabIndex = 22;
            this.btnAdd.Text = "Add Permission";
            this.btnAdd.UseVisualStyleBackColor = false;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);

            // 
            // btnRemove
            // 
            this.btnRemove.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnRemove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRemove.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemove.ForeColor = System.Drawing.Color.White;
            this.btnRemove.Location = new System.Drawing.Point(218, 55);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(185, 30);
            this.btnRemove.TabIndex = 23;
            this.btnRemove.Text = "Remove Permission";
            this.btnRemove.UseVisualStyleBackColor = false;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);

            // 
            // btnBreakInheritance
            // 
            this.btnBreakInheritance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnBreakInheritance.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBreakInheritance.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnBreakInheritance.ForeColor = System.Drawing.Color.White;
            this.btnBreakInheritance.Location = new System.Drawing.Point(20, 95);
            this.btnBreakInheritance.Name = "btnBreakInheritance";
            this.btnBreakInheritance.Size = new System.Drawing.Size(185, 30);
            this.btnBreakInheritance.TabIndex = 24;
            this.btnBreakInheritance.Text = "Break Inheritance";
            this.btnBreakInheritance.UseVisualStyleBackColor = false;
            this.btnBreakInheritance.Click += new System.EventHandler(this.btnBreakInheritance_Click);

            // 
            // btnRestoreInheritance
            // 
            this.btnRestoreInheritance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnRestoreInheritance.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRestoreInheritance.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRestoreInheritance.ForeColor = System.Drawing.Color.White;
            this.btnRestoreInheritance.Location = new System.Drawing.Point(218, 95);
            this.btnRestoreInheritance.Name = "btnRestoreInheritance";
            this.btnRestoreInheritance.Size = new System.Drawing.Size(185, 30);
            this.btnRestoreInheritance.TabIndex = 25;
            this.btnRestoreInheritance.Text = "Restore Inheritance";
            this.btnRestoreInheritance.UseVisualStyleBackColor = false;
            this.btnRestoreInheritance.Click += new System.EventHandler(this.btnRestoreInheritance_Click);

            // 
            // btnResetPermissions
            // 
            this.btnResetPermissions.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnResetPermissions.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResetPermissions.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnResetPermissions.ForeColor = System.Drawing.Color.White;
            this.btnResetPermissions.Location = new System.Drawing.Point(20, 135);
            this.btnResetPermissions.Name = "btnResetPermissions";
            this.btnResetPermissions.Size = new System.Drawing.Size(185, 30);
            this.btnResetPermissions.TabIndex = 26;
            this.btnResetPermissions.Text = "Reset Permissions";
            this.btnResetPermissions.UseVisualStyleBackColor = false;
            this.btnResetPermissions.Click += new System.EventHandler(this.btnResetPermissions_Click);

            // 
            // btnViewSubfolders
            // 
            this.btnViewSubfolders.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnViewSubfolders.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnViewSubfolders.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnViewSubfolders.ForeColor = System.Drawing.Color.White;
            this.btnViewSubfolders.Location = new System.Drawing.Point(218, 135);
            this.btnViewSubfolders.Name = "btnViewSubfolders";
            this.btnViewSubfolders.Size = new System.Drawing.Size(185, 30);
            this.btnViewSubfolders.TabIndex = 27;
            this.btnViewSubfolders.Text = "View Subfolders";
            this.btnViewSubfolders.UseVisualStyleBackColor = false;
            this.btnViewSubfolders.Click += new System.EventHandler(this.btnViewSubfolders_Click);

            // 
            // btnRefresh
            // 
            this.btnRefresh.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRefresh.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRefresh.ForeColor = System.Drawing.Color.White;
            this.btnRefresh.Location = new System.Drawing.Point(20, 175);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(185, 30);
            this.btnRefresh.TabIndex = 28;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = false;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(36, 620);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(492, 15);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 29;
            this.progressBar.Visible = false;

            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.lblStatus.Location = new System.Drawing.Point(20, 650);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(960, 30);
            this.lblStatus.TabIndex = 30;
            this.lblStatus.Text = "Ready to edit nested subfolder permissions...";

            // 
            // btnClose
            // 
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClose.Location = new System.Drawing.Point(905, 690);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 30);
            this.btnClose.TabIndex = 31;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            // 
            // lblValidation
            // 
            this.lblValidation.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblValidation.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(128)))));
            this.lblValidation.Location = new System.Drawing.Point(20, 690);
            this.lblValidation.Name = "lblValidation";
            this.lblValidation.Size = new System.Drawing.Size(870, 30);
            this.lblValidation.TabIndex = 32;
            this.lblValidation.Text = "Note: Changes to permissions may take time to propagate across nested folders. Use the Refresh button to update the display after making changes.";

            // 
            // SubfolderPermissionsEditDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(1000, 680);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblLibrary);
            this.Controls.Add(this.lblInstructions);
            this.Controls.Add(this.grpSubfolders);
            this.Controls.Add(this.grpPermissions);
            this.Controls.Add(this.grpActions);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblValidation);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SubfolderPermissionsEditDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Nested Subfolder Permissions";
            this.grpSubfolders.ResumeLayout(false);
            this.grpSubfolders.PerformLayout();
            this.grpPermissions.ResumeLayout(false);
            this.grpPermissions.PerformLayout();
            this.grpActions.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // Add the missing event handlers for the new buttons
        private void btnExpandAll_Click(object sender, System.EventArgs e)
        {
            if (tvSubfolders != null)
            {
                tvSubfolders.ExpandAll();
                lblStatus.Text = "Expanded all visible nodes.";
            }
        }

        private void btnCollapseAll_Click(object sender, System.EventArgs e)
        {
            if (tvSubfolders != null)
            {
                tvSubfolders.CollapseAll();
                lblStatus.Text = "Collapsed all nodes.";
            }
        }
        // Add the missing event handlers for the new buttons

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblLibrary;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.TreeView tvSubfolders;
        private System.Windows.Forms.ImageList treeViewImageList;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnPreview;
        private System.Windows.Forms.Button btnBreakInheritance;
        private System.Windows.Forms.Button btnRestoreInheritance;
        private System.Windows.Forms.Button btnResetPermissions;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnViewSubfolders;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ComboBox cmbView;
        private System.Windows.Forms.ComboBox cmbGroups;
        private System.Windows.Forms.Label lblSelectedItem;
        private System.Windows.Forms.RadioButton radioRead;
        private System.Windows.Forms.RadioButton radioEdit;
        private System.Windows.Forms.RadioButton radioNoAccess;
        private System.Windows.Forms.Label lblValidation;
        private System.Windows.Forms.ToolTip toolTipGroups;
        private System.Windows.Forms.GroupBox grpSubfolders;
        private System.Windows.Forms.Label lblSearchFilter;
        private System.Windows.Forms.Label lblViewFilter;
        private System.Windows.Forms.GroupBox grpPermissions;
        private System.Windows.Forms.Label lblGroups;
        private System.Windows.Forms.Label lblPermissionLevel;
        private System.Windows.Forms.GroupBox grpActions;
        private System.Windows.Forms.Label lblNestingInfo;
        private System.Windows.Forms.Button btnExpandAll;
        private System.Windows.Forms.Button btnCollapseAll;
    }
}
