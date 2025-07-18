namespace EntraGroupsApp
{
    partial class AuditLogForm
    {
        private System.ComponentModel.IContainer components = null;

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
            dataGridViewLogs = new DataGridView();
            datePicker = new DateTimePicker();
            lblDate = new Label();
            comboBoxActionType = new ComboBox();
            lblActionType = new Label();
            btnCopyToClipboard = new Button();
            btnPurge = new Button();
            btnExport = new Button();
            btnClose = new Button();
            lblStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewLogs).BeginInit();
            SuspendLayout();
            //
            // dataGridViewLogs
            //
            dataGridViewLogs.AllowUserToAddRows = false;
            dataGridViewLogs.AllowUserToDeleteRows = false;
            dataGridViewLogs.ReadOnly = true;
            dataGridViewLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewLogs.MultiSelect = true;
            dataGridViewLogs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            dataGridViewLogs.Location = new Point(10, 40);
            dataGridViewLogs.Name = "dataGridViewLogs";
            dataGridViewLogs.Size = new Size(780, 340);
            dataGridViewLogs.TabIndex = 0;
            dataGridViewLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //
            // datePicker
            //
            datePicker.Location = new Point(100, 10);
            datePicker.Name = "datePicker";
            datePicker.Size = new Size(200, 23);
            datePicker.TabIndex = 1;
            //
            // lblDate
            //
            lblDate.AutoSize = true;
            lblDate.Location = new Point(10, 12);
            lblDate.Name = "lblDate";
            lblDate.Size = new Size(80, 15);
            lblDate.TabIndex = 2;
            lblDate.Text = "Select Date:";
            //
            // comboBoxActionType
            //
            comboBoxActionType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxActionType.Location = new Point(390, 10);
            comboBoxActionType.Name = "comboBoxActionType";
            comboBoxActionType.Size = new Size(150, 23);
            comboBoxActionType.TabIndex = 3;
            //
            // lblActionType
            //
            lblActionType.AutoSize = true;
            lblActionType.Location = new Point(310, 12);
            lblActionType.Name = "lblActionType";
            lblActionType.Size = new Size(80, 15);
            lblActionType.TabIndex = 4;
            lblActionType.Text = "Action Type:";
            //
            // btnCopyToClipboard
            //
            btnCopyToClipboard.Location = new Point(550, 10);
            btnCopyToClipboard.Name = "btnCopyToClipboard";
            btnCopyToClipboard.Size = new Size(120, 25);
            btnCopyToClipboard.TabIndex = 5;
            btnCopyToClipboard.Text = "Copy to Clipboard";
            btnCopyToClipboard.UseVisualStyleBackColor = true;
            //
            // btnPurge
            //
            btnPurge.Location = new Point(680, 10);
            btnPurge.Name = "btnPurge";
            btnPurge.Size = new Size(120, 25);
            btnPurge.TabIndex = 6;
            btnPurge.Text = "Purge Logs";
            btnPurge.UseVisualStyleBackColor = true;
            //
            // btnExport
            //
            btnExport.Location = new Point(810, 10);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(120, 25);
            btnExport.TabIndex = 7;
            btnExport.Text = "Export Logs";
            btnExport.UseVisualStyleBackColor = true;
            //
            // btnClose
            //
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(650, 390);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(120, 25);
            btnClose.TabIndex = 8;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            //
            // lblStatus
            //
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(10, 390);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(100, 15);
            lblStatus.TabIndex = 9;
            lblStatus.Text = "Ready";
            //
            // AuditLogForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(dataGridViewLogs);
            Controls.Add(datePicker);
            Controls.Add(lblDate);
            Controls.Add(comboBoxActionType);
            Controls.Add(lblActionType);
            Controls.Add(btnCopyToClipboard);
            Controls.Add(btnPurge);
            Controls.Add(btnExport);
            Controls.Add(btnClose);
            Controls.Add(lblStatus);
            Name = "AuditLogForm";
            Text = "Review Recent Changes";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)dataGridViewLogs).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private DataGridView dataGridViewLogs;
        private DateTimePicker datePicker;
        private Label lblDate;
        private ComboBox comboBoxActionType;
        private Label lblActionType;
        private Button btnCopyToClipboard;
        private Button btnPurge;
        private Button btnExport;
        private Button btnClose;
        private Label lblStatus;
    }
}