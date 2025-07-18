namespace EntraGroupsApp
{
    partial class ConfirmCopyDialog
    {
        private System.ComponentModel.Container components = null;
        private TableLayoutPanel tableLayoutPanel;
        private Label lblUsers;
        private TextBox txtUsers;
        private Label lblGroups;
        private TextBox txtGroups;
        private Panel panelButtons;
        private Button btnOK;
        private Button btnCancel;

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
            tableLayoutPanel = new TableLayoutPanel();
            lblUsers = new Label();
            txtUsers = new TextBox();
            lblGroups = new Label();
            txtGroups = new TextBox();
            panelButtons = new Panel();
            btnOK = new Button();
            btnCancel = new Button();
            tableLayoutPanel.SuspendLayout();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(lblUsers, 0, 0);
            tableLayoutPanel.Controls.Add(txtUsers, 0, 1);
            tableLayoutPanel.Controls.Add(lblGroups, 0, 2);
            tableLayoutPanel.Controls.Add(txtGroups, 0, 3);
            tableLayoutPanel.Controls.Add(panelButtons, 0, 4);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 5;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.Size = new Size(500, 400);
            tableLayoutPanel.TabIndex = 0;
            // 
            // lblUsers
            // 
            lblUsers.AutoSize = true;
            lblUsers.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblUsers.Location = new Point(3, 10);
            lblUsers.Name = "lblUsers";
            lblUsers.Size = new Size(44, 15);
            lblUsers.TabIndex = 0;
            lblUsers.Text = "Users:";
            // 
            // txtUsers
            // 
            txtUsers.Dock = DockStyle.Fill;
            txtUsers.Font = new Font("Segoe UI", 9F);
            txtUsers.Multiline = true;
            txtUsers.ReadOnly = true;
            txtUsers.ScrollBars = ScrollBars.Vertical;
            txtUsers.Location = new Point(3, 50);
            txtUsers.Name = "txtUsers";
            txtUsers.Size = new Size(494, 134);
            txtUsers.TabIndex = 1;
            // 
            // lblGroups
            // 
            lblGroups.AutoSize = true;
            lblGroups.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblGroups.Location = new Point(3, 190);
            lblGroups.Name = "lblGroups";
            lblGroups.Size = new Size(50, 15);
            lblGroups.TabIndex = 2;
            lblGroups.Text = "Groups:";
            // 
            // txtGroups
            // 
            txtGroups.Dock = DockStyle.Fill;
            txtGroups.Font = new Font("Segoe UI", 9F);
            txtGroups.Multiline = true;
            txtGroups.ReadOnly = true;
            txtGroups.ScrollBars = ScrollBars.Vertical;
            txtGroups.Location = new Point(3, 230);
            txtGroups.Name = "txtGroups";
            txtGroups.Size = new Size(494, 134);
            txtGroups.TabIndex = 3;
            // 
            // panelButtons
            // 
            panelButtons.Controls.Add(btnOK);
            panelButtons.Controls.Add(btnCancel);
            panelButtons.Dock = DockStyle.Fill;
            panelButtons.Location = new Point(3, 368);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(494, 36);
            panelButtons.TabIndex = 4;
            // 
            // btnOK
            // 
            btnOK.Anchor = AnchorStyles.Right;
            btnOK.Font = new Font("Segoe UI", 9F);
            btnOK.Location = new Point(294, 6);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(90, 24);
            btnOK.TabIndex = 0;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Right;
            btnCancel.Font = new Font("Segoe UI", 9F);
            btnCancel.Location = new Point(390, 6);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(90, 24);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // ConfirmCopyDialog
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(500, 400);
            Controls.Add(tableLayoutPanel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ConfirmCopyDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Confirm Copy Users";
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}