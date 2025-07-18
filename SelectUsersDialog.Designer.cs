namespace EntraGroupsApp
{
    partial class SelectUserDialog
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;

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
            lblInstructions = new System.Windows.Forms.Label();
            txtSearch = new System.Windows.Forms.TextBox();
            btnSearch = new System.Windows.Forms.Button();
            lblResult = new System.Windows.Forms.Label();
            lblStatus = new System.Windows.Forms.Label();
            btnOK = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            SuspendLayout();
            //
            // lblInstructions
            //
            lblInstructions.AutoSize = true;
            lblInstructions.Location = new System.Drawing.Point(12, 12);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new System.Drawing.Size(200, 15);
            lblInstructions.TabIndex = 0;
            lblInstructions.Text = "Enter NetID or email of target user";
            lblInstructions.Font = new System.Drawing.Font("Segoe UI", 10F);
            //
            // txtSearch
            //
            txtSearch.Location = new System.Drawing.Point(12, 35);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new System.Drawing.Size(250, 23);
            txtSearch.TabIndex = 1;
            txtSearch.Font = new System.Drawing.Font("Segoe UI", 10F);
            //
            // btnSearch
            //
            btnSearch.Location = new System.Drawing.Point(268, 35);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new System.Drawing.Size(80, 23);
            btnSearch.TabIndex = 2;
            btnSearch.Text = "Search";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += btnSearch_Click;
            //
            // lblResult
            //
            lblResult.AutoSize = true;
            lblResult.Location = new System.Drawing.Point(12, 65);
            lblResult.Name = "lblResult";
            lblResult.Size = new System.Drawing.Size(0, 15);
            lblResult.TabIndex = 3;
            lblResult.Visible = false;
            //
            // lblStatus
            //
            lblStatus.AutoSize = true;
            lblStatus.Location = new System.Drawing.Point(12, 90);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(0, 15);
            lblStatus.TabIndex = 4;
            lblStatus.Visible = false;
            //
            // btnOK
            //
            btnOK.Location = new System.Drawing.Point(192, 115);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(75, 30);
            btnOK.TabIndex = 5;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Enabled = false;
            btnOK.Click += btnOK_Click;
            //
            // btnCancel
            //
            btnCancel.Location = new System.Drawing.Point(273, 115);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 30);
            btnCancel.TabIndex = 6;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            //
            // SelectUserDialog
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(360, 160);
            Controls.Add(lblInstructions);
            Controls.Add(txtSearch);
            Controls.Add(btnSearch);
            Controls.Add(lblResult);
            Controls.Add(lblStatus);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SelectUserDialog";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Select Target User";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}