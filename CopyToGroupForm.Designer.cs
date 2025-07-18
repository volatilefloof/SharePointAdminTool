namespace EntraGroupsApp
{
    partial class CopyToGroupForm
    {
        private System.ComponentModel.Container components = null;
        private TableLayoutPanel tableLayoutPanel;
        private Label lblDepartment;
        private Label lblGroupType;
        private ComboBox comboBoxGroupType;
        private Label lblSortOrder;
        private ComboBox comboBoxSortOrder;
        private ListBox listBoxGroups;
        private Panel panelButtons;
        private Button btnOK;
        private Button btnCancel;
        private Label lblStatus;

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
            lblDepartment = new Label();
            lblGroupType = new Label();
            comboBoxGroupType = new ComboBox();
            lblSortOrder = new Label();
            comboBoxSortOrder = new ComboBox();
            listBoxGroups = new ListBox();
            panelButtons = new Panel();
            btnOK = new Button();
            btnCancel = new Button();
            lblStatus = new Label();
            tableLayoutPanel.SuspendLayout();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.AutoSize = true;
            tableLayoutPanel.ColumnCount = 2;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel.Controls.Add(lblDepartment, 0, 0);
            tableLayoutPanel.Controls.Add(lblGroupType, 0, 1);
            tableLayoutPanel.Controls.Add(comboBoxGroupType, 0, 2);
            tableLayoutPanel.Controls.Add(lblSortOrder, 1, 1);
            tableLayoutPanel.Controls.Add(comboBoxSortOrder, 1, 2);
            tableLayoutPanel.Controls.Add(listBoxGroups, 0, 3);
            tableLayoutPanel.Controls.Add(panelButtons, 0, 4);
            tableLayoutPanel.Controls.Add(lblStatus, 0, 5);
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Location = new Point(0, 0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 6;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            tableLayoutPanel.Size = new Size(600, 400);
            tableLayoutPanel.TabIndex = 0;
            // 
            // lblDepartment
            // 
            lblDepartment.AutoSize = true;
            lblDepartment.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblDepartment.Location = new Point(3, 10);
            lblDepartment.Name = "lblDepartment";
            lblDepartment.Size = new Size(100, 15);
            lblDepartment.TabIndex = 0;
            lblDepartment.Text = "Department: ";
            // 
            // lblGroupType
            // 
            lblGroupType.AutoSize = true;
            lblGroupType.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblGroupType.Location = new Point(3, 50);
            lblGroupType.Name = "lblGroupType";
            lblGroupType.Size = new Size(71, 15);
            lblGroupType.TabIndex = 1;
            lblGroupType.Text = "Group Type";
            // 
            // comboBoxGroupType
            // 
            comboBoxGroupType.Dock = DockStyle.Fill;
            comboBoxGroupType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxGroupType.Font = new Font("Segoe UI", 9F);
            comboBoxGroupType.Location = new Point(3, 90);
            comboBoxGroupType.Name = "comboBoxGroupType";
            comboBoxGroupType.Size = new Size(294, 23);
            comboBoxGroupType.TabIndex = 2;
            comboBoxGroupType.SelectedIndexChanged += comboBoxGroupType_SelectedIndexChanged;
            // 
            // lblSortOrder
            // 
            lblSortOrder.AutoSize = true;
            lblSortOrder.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSortOrder.Location = new Point(303, 50);
            lblSortOrder.Name = "lblSortOrder";
            lblSortOrder.Size = new Size(67, 15);
            lblSortOrder.TabIndex = 3;
            lblSortOrder.Text = "Sort Order";
            // 
            // comboBoxSortOrder
            // 
            comboBoxSortOrder.Dock = DockStyle.Fill;
            comboBoxSortOrder.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSortOrder.Font = new Font("Segoe UI", 9F);
            comboBoxSortOrder.Location = new Point(303, 90);
            comboBoxSortOrder.Name = "comboBoxSortOrder";
            comboBoxSortOrder.Size = new Size(294, 23);
            comboBoxSortOrder.TabIndex = 4;
            comboBoxSortOrder.SelectedIndexChanged += comboBoxSortOrder_SelectedIndexChanged;
            // 
            // listBoxGroups
            // 
            tableLayoutPanel.SetColumnSpan(listBoxGroups, 2);
            listBoxGroups.Dock = DockStyle.Fill;
            listBoxGroups.Font = new Font("Segoe UI", 9F);
            listBoxGroups.HorizontalScrollbar = true;
            listBoxGroups.Location = new Point(3, 130);
            listBoxGroups.Name = "listBoxGroups";
            listBoxGroups.Size = new Size(594, 192);
            listBoxGroups.TabIndex = 5;
            // 
            // panelButtons
            // 
            tableLayoutPanel.SetColumnSpan(panelButtons, 2);
            panelButtons.Controls.Add(btnOK);
            panelButtons.Controls.Add(btnCancel);
            panelButtons.Dock = DockStyle.Fill;
            panelButtons.Location = new Point(3, 328);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(594, 36);
            panelButtons.TabIndex = 6;
            // 
            // btnOK
            // 
            btnOK.Anchor = AnchorStyles.Right;
            btnOK.Font = new Font("Segoe UI", 9F);
            btnOK.Location = new Point(394, 6);
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
            btnCancel.Location = new Point(490, 6);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(90, 24);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // lblStatus
            // 
            tableLayoutPanel.SetColumnSpan(lblStatus, 2);
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.Location = new Point(3, 368);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(94, 15);
            lblStatus.TabIndex = 7;
            lblStatus.Text = "Ready for search";
            // 
            // CopyToGroupForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 400);
            Controls.Add(tableLayoutPanel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CopyToGroupForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Select Groups to Copy Users";
            Load += CopyToGroupForm_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}