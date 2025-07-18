namespace EntraGroupsApp
{
    partial class ExportDialogForm
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
            radioExportRange = new RadioButton();
            radioExportAll = new RadioButton();
            datePickerStart = new DateTimePicker();
            datePickerEnd = new DateTimePicker();
            lblStartDate = new Label();
            lblEndDate = new Label();
            btnExport = new Button();
            btnCancel = new Button();
            SuspendLayout();
            //
            // radioExportRange
            //
            radioExportRange.AutoSize = true;
            radioExportRange.Location = new Point(20, 20);
            radioExportRange.Name = "radioExportRange";
            radioExportRange.Size = new Size(150, 19);
            radioExportRange.TabIndex = 0;
            radioExportRange.Text = "Export logs in date range";
            radioExportRange.Checked = true;
            radioExportRange.CheckedChanged += RadioExportRange_CheckedChanged;
            //
            // radioExportAll
            //
            radioExportAll.AutoSize = true;
            radioExportAll.Location = new Point(20, 50);
            radioExportAll.Name = "radioExportAll";
            radioExportAll.Size = new Size(150, 19);
            radioExportAll.TabIndex = 1;
            radioExportAll.Text = "Export all logs";
            //
            // datePickerStart
            //
            datePickerStart.Location = new Point(150, 80);
            datePickerStart.Name = "datePickerStart";
            datePickerStart.Size = new Size(200, 23);
            datePickerStart.TabIndex = 2;
            //
            // datePickerEnd
            //
            datePickerEnd.Location = new Point(150, 110);
            datePickerEnd.Name = "datePickerEnd";
            datePickerEnd.Size = new Size(200, 23);
            datePickerEnd.TabIndex = 3;
            //
            // lblStartDate
            //
            lblStartDate.AutoSize = true;
            lblStartDate.Location = new Point(20, 80);
            lblStartDate.Name = "lblStartDate";
            lblStartDate.Size = new Size(100, 15);
            lblStartDate.TabIndex = 4;
            lblStartDate.Text = "Start Date:";
            //
            // lblEndDate
            //
            lblEndDate.AutoSize = true;
            lblEndDate.Location = new Point(20, 110);
            lblEndDate.Name = "lblEndDate";
            lblEndDate.Size = new Size(100, 15);
            lblEndDate.TabIndex = 5;
            lblEndDate.Text = "End Date:";
            //
            // btnExport
            //
            btnExport.Location = new Point(150, 150);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(100, 30);
            btnExport.TabIndex = 6;
            btnExport.Text = "Export";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += BtnExport_Click;
            //
            // btnCancel
            //
            btnCancel.Location = new Point(260, 150);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(100, 30);
            btnCancel.TabIndex = 7;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            //
            // ExportDialogForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 200);
            Controls.Add(radioExportRange);
            Controls.Add(radioExportAll);
            Controls.Add(datePickerStart);
            Controls.Add(datePickerEnd);
            Controls.Add(lblStartDate);
            Controls.Add(lblEndDate);
            Controls.Add(btnExport);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ExportDialogForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Export Audit Logs";
            ResumeLayout(false);
            PerformLayout();
        }

        private RadioButton radioExportRange;
        private RadioButton radioExportAll;
        private DateTimePicker datePickerStart;
        private DateTimePicker datePickerEnd;
        private Label lblStartDate;
        private Label lblEndDate;
        private Button btnExport;
        private Button btnCancel;
    }
}