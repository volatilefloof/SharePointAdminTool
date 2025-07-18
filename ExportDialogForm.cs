using System;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class ExportDialogForm : Form
    {
        public bool ExportAll { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public ExportDialogForm()
        {
            InitializeComponent();
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            datePickerStart.Value = StartDate;
            datePickerEnd.Value = EndDate;
            radioExportRange.Checked = true;
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (radioExportRange.Checked)
            {
                StartDate = datePickerStart.Value.Date;
                EndDate = datePickerEnd.Value.Date.AddDays(1).AddTicks(-1); // End of day
                if (StartDate > EndDate)
                {
                    MessageBox.Show("Start date must be before or equal to end date.", "Invalid Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ExportAll = false;
            }
            else
            {
                ExportAll = true;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void RadioExportRange_CheckedChanged(object sender, EventArgs e)
        {
            datePickerStart.Enabled = radioExportRange.Checked;
            datePickerEnd.Enabled = radioExportRange.Checked;
        }
    }
}