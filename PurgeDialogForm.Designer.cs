namespace EntraGroupsApp
{
    partial class PurgeDialogForm
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
            radioPurgeRange = new RadioButton();
            radioPurgeAll = new RadioButton();
            datePickerStart = new DateTimePicker();
            datePickerEnd = new DateTimePicker();
            lblStartDate = new Label();
            lblEndDate = new Label();
            btnPurge = new Button();
            btnCancel = new Button();
            SuspendLayout();
            //
            // radioPurgeRange
            //
            radioPurgeRange.AutoSize = true;
            radioPurgeRange.Location = new Point(20, 20);
            radioPurgeRange.Name = "radioPurgeRange";
            radioPurgeRange.Size = new Size(150, 19);
            radioPurgeRange.TabIndex = 0;
            radioPurgeRange.Text = "Purge logs in date range";
            radioPurgeRange.Checked = true;
            radioPurgeRange.CheckedChanged += RadioPurgeRange_CheckedChanged;
            //
            // radioPurgeAll
            //
            radioPurgeAll.AutoSize = true;
            radioPurgeAll.Location = new Point(20, 50);
            radioPurgeAll.Name = "radioPurgeAll";
            radioPurgeAll.Size = new Size(150, 19);
            radioPurgeAll.TabIndex = 1;
            radioPurgeAll.Text = "Purge all logs";
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
            // btnPurge
            //
            btnPurge.Location = new Point(150, 150);
            btnPurge.Name = "btnPurge";
            btnPurge.Size = new Size(100, 30);
            btnPurge.TabIndex = 6;
            btnPurge.Text = "Purge";
            btnPurge.UseVisualStyleBackColor = true;
            btnPurge.Click += BtnPurge_Click;
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
            // PurgeDialogForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 200);
            Controls.Add(radioPurgeRange);
            Controls.Add(radioPurgeAll);
            Controls.Add(datePickerStart);
            Controls.Add(datePickerEnd);
            Controls.Add(lblStartDate);
            Controls.Add(lblEndDate);
            Controls.Add(btnPurge);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PurgeDialogForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Purge Audit Logs";
            ResumeLayout(false);
            PerformLayout();
        }

        private RadioButton radioPurgeRange;
        private RadioButton radioPurgeAll;
        private DateTimePicker datePickerStart;
        private DateTimePicker datePickerEnd;
        private Label lblStartDate;
        private Label lblEndDate;
        private Button btnPurge;
        private Button btnCancel;
    }
}