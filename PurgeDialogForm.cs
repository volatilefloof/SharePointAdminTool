using System;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class PurgeDialogForm : Form
    {
        public bool PurgeAll { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public PurgeDialogForm()
        {
            InitializeComponent();
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            datePickerStart.Value = StartDate;
            datePickerEnd.Value = EndDate;
            radioPurgeRange.Checked = true;
        }

        private void BtnPurge_Click(object sender, EventArgs e)
        {
            if (radioPurgeRange.Checked)
            {
                StartDate = datePickerStart.Value.Date;
                EndDate = datePickerEnd.Value.Date.AddDays(1).AddTicks(-1); // End of day
                if (StartDate > EndDate)
                {
                    MessageBox.Show("Start date must be before or equal to end date.", "Invalid Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                PurgeAll = false;
            }
            else
            {
                PurgeAll = true;
            }

            var confirm = MessageBox.Show(
                PurgeAll ? "Are you sure you want to purge ALL audit logs? This action cannot be undone." :
                           $"Are you sure you want to purge audit logs from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}? This action cannot be undone.",
                "Confirm Purge",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void RadioPurgeRange_CheckedChanged(object sender, EventArgs e)
        {
            datePickerStart.Enabled = radioPurgeRange.Checked;
            datePickerEnd.Enabled = radioPurgeRange.Checked;
        }
    }
}