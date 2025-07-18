namespace EntraGroupsApp
{
    partial class AddToDepartmentGroupsDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelDepartment = new System.Windows.Forms.Label();
            this.cmbDepartments = new System.Windows.Forms.ComboBox();
            this.labelCategory = new System.Windows.Forms.Label();
            this.cmbCategory = new System.Windows.Forms.ComboBox();
            this.listGroups = new System.Windows.Forms.ListBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            // 
            // labelDepartment
            // 
            this.labelDepartment.AutoSize = true;
            this.labelDepartment.Location = new System.Drawing.Point(20, 22);
            this.labelDepartment.Name = "labelDepartment";
            this.labelDepartment.Size = new System.Drawing.Size(85, 17);
            this.labelDepartment.Text = "Department:";
            this.labelDepartment.Font = new System.Drawing.Font("Segoe UI", 10F);

            // 
            // cmbDepartments
            // 
            this.cmbDepartments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDepartments.Location = new System.Drawing.Point(130, 19);
            this.cmbDepartments.Name = "cmbDepartments";
            this.cmbDepartments.Size = new System.Drawing.Size(280, 25);
            this.cmbDepartments.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbDepartments.SelectedIndexChanged += new System.EventHandler(this.cmbDepartments_SelectedIndexChanged);

            // 
            // labelCategory
            // 
            this.labelCategory.AutoSize = true;
            this.labelCategory.Location = new System.Drawing.Point(20, 62);
            this.labelCategory.Name = "labelCategory";
            this.labelCategory.Size = new System.Drawing.Size(70, 17);
            this.labelCategory.Text = "Category:";
            this.labelCategory.Font = new System.Drawing.Font("Segoe UI", 10F);

            // 
            // cmbCategory
            // 
            this.cmbCategory.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCategory.Location = new System.Drawing.Point(130, 59);
            this.cmbCategory.Name = "cmbCategory";
            this.cmbCategory.Size = new System.Drawing.Size(280, 25);
            this.cmbCategory.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbCategory.SelectedIndexChanged += new System.EventHandler(this.cmbCategory_SelectedIndexChanged);

            // 
            // listGroups
            // 
            this.listGroups.FormattingEnabled = true;
            this.listGroups.Location = new System.Drawing.Point(20, 100);
            this.listGroups.Name = "listGroups";
            this.listGroups.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listGroups.Size = new System.Drawing.Size(390, 184);
            this.listGroups.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.listGroups.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                            | System.Windows.Forms.AnchorStyles.Left)
                                            | System.Windows.Forms.AnchorStyles.Right)));

            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(214, 300);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 32);
            this.btnOk.Text = "OK";
            this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.None;
            this.btnOk.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);

            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(314, 300);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            //
            // AddToDepartmentGroupsDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(430, 350);
            this.Controls.Add(this.labelDepartment);
            this.Controls.Add(this.cmbDepartments);
            this.Controls.Add(this.labelCategory);
            this.Controls.Add(this.cmbCategory);
            this.Controls.Add(this.listGroups);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.MinimumSize = new System.Drawing.Size(440, 380);
            this.Name = "AddToDepartmentGroupsDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select Department Groups";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ComboBox cmbDepartments;
        private System.Windows.Forms.ComboBox cmbCategory;
        private System.Windows.Forms.ListBox listGroups;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label labelDepartment;
        private System.Windows.Forms.Label labelCategory;
    }
}