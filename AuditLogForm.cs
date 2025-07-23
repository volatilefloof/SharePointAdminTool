using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using System.Drawing;
using System.Text.Json;
using System.IO;

namespace EntraGroupsApp
{
    public partial class AuditLogForm : Form
    {
        private readonly AuditLogManager _auditLogManager;
        private readonly string _userId;
        private List<AuditLogEntry> _currentLogs;
        private readonly ContextMenuStrip dataGridViewContextMenu; // Added for right-click menu

        public AuditLogForm(AuditLogManager auditLogManager, string userId)
        {
            InitializeComponent();
            _auditLogManager = auditLogManager ?? throw new ArgumentNullException(nameof(auditLogManager));
            _userId = userId ?? throw new ArgumentNullException(nameof(userId));
            _currentLogs = new List<AuditLogEntry>();

            // Initialize context menu
            dataGridViewContextMenu = new ContextMenuStrip();
            var copyDetailsMenuItem = new ToolStripMenuItem("Copy Details", null, DataGridViewContextMenu_CopyDetails_Click);
            dataGridViewContextMenu.Items.Add(copyDetailsMenuItem);
            dataGridViewLogs.ContextMenuStrip = dataGridViewContextMenu;

            datePicker.Value = DateTime.Today;
            datePicker.ValueChanged += DatePicker_ValueChanged;
            btnCopyToClipboard.Click += BtnCopyToClipboard_Click;
            btnClose.Click += BtnClose_Click;
            btnPurge.Click += BtnPurge_Click;
            btnExport.Click += BtnExport_Click;
            Load += AuditLogForm_Load;

            // Add MouseDown event to handle right-click row selection
            dataGridViewLogs.MouseDown += DataGridViewLogs_MouseDown;

            LoadLogs(datePicker.Value);
        }

        private void DataGridViewLogs_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = dataGridViewLogs.HitTest(e.X, e.Y);
                if (hitTest.Type == DataGridViewHitTestType.Cell)
                {
                    // Select the row under the cursor
                    dataGridViewLogs.ClearSelection();
                    dataGridViewLogs.Rows[hitTest.RowIndex].Selected = true;
                }
            }
        }

        private void DataGridViewContextMenu_CopyDetails_Click(object sender, EventArgs e)
        {
            if (dataGridViewLogs.SelectedRows.Count == 0)
            {
                MessageBox.Show("No log entry selected.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var selectedLog = _currentLogs[dataGridViewLogs.SelectedRows[0].Index];
                var output = $"Modification applied to {selectedLog.GroupName} for \"{selectedLog.TargetName}\" ({selectedLog.TargetType}) on {selectedLog.Timestamp:yyyy-MM-dd HH:mm:ss}. Details: {selectedLog.Details}";
                Clipboard.SetText(output);
                MessageBox.Show("Log entry details copied to clipboard.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AuditLogForm_Load(object sender, EventArgs e)
        {
            AdjustControlPositions();
        }

        private void LoadLogs(DateTime? date = null)
        {
            try
            {
                _currentLogs = _auditLogManager.GetLogsByUserAndDate(_userId, date)
                    .Where(l => !l.ActionType.StartsWith("Open"))
                    .ToList();

                dataGridViewLogs.DataSource = _currentLogs.Select(l => new
                {
                    Timestamp = l.Timestamp,
                    Action = l.ActionType,
                    Group = l.GroupName,
                    Target = l.TargetName,
                    TargetType = l.TargetType,
                    Details = l.Details
                }).ToList();

                if (_currentLogs.Any())
                {
                    dataGridViewLogs.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                    dataGridViewLogs.Columns["Timestamp"].MinimumWidth = 150;
                    dataGridViewLogs.Columns["Details"].MinimumWidth = 300;
                    dataGridViewLogs.Columns["Timestamp"].Width = Math.Max(dataGridViewLogs.Columns["Timestamp"].Width, 150);
                    dataGridViewLogs.Columns["Details"].Width = Math.Max(dataGridViewLogs.Columns["Details"].Width, 300);
                }
                lblStatus.Text = $"Loaded {_currentLogs.Count} log entries.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error loading logs.";
            }
        }

        private void AdjustControlPositions()
        {
            // Ensure DataGridView fills available space
            dataGridViewLogs.Location = new Point(10, 40);
            dataGridViewLogs.Size = new Size(ClientSize.Width - 20, ClientSize.Height - 100);

            // Center top controls
            int spacing = 10;
            int topPanelWidth = lblDate.Width + datePicker.Width +
                               btnCopyToClipboard.Width + btnPurge.Width + btnExport.Width + (spacing * 4);
            int topPanelX = (ClientSize.Width - topPanelWidth) / 2;
            lblDate.Location = new Point(topPanelX, 10);
            datePicker.Location = new Point(topPanelX + lblDate.Width + spacing, 10);
            btnCopyToClipboard.Location = new Point(topPanelX + lblDate.Width + datePicker.Width + (spacing * 2), 10);
            btnPurge.Location = new Point(topPanelX + lblDate.Width + datePicker.Width + btnCopyToClipboard.Width + (spacing * 3), 10);
            btnExport.Location = new Point(topPanelX + lblDate.Width + datePicker.Width + btnCopyToClipboard.Width + btnPurge.Width + (spacing * 4), 10);

            // Position status label below DataGridView
            lblStatus.Location = new Point(10, dataGridViewLogs.Bottom + 10);

            // Position close button at bottom right
            btnClose.Location = new Point(ClientSize.Width - btnClose.Width - 10, ClientSize.Height - btnClose.Height - 10);
        }

        private void DatePicker_ValueChanged(object sender, EventArgs e)
        {
            LoadLogs(datePicker.Value);
        }

        private void BtnCopyToClipboard_Click(object sender, EventArgs e)
        {
            if (dataGridViewLogs.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one log entry to copy.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var selectedLogs = dataGridViewLogs.SelectedRows.Cast<DataGridViewRow>()
                    .Select(r => _currentLogs[r.Index])
                    .ToList();

                if (!selectedLogs.Any())
                {
                    MessageBox.Show("No valid log entries selected.", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var output = selectedLogs.Select(l =>
                    $"Modification applied to {l.GroupName} for \"{l.TargetName}\" ({l.TargetType}) on {l.Timestamp:yyyy-MM-dd HH:mm:ss}. Details: {l.Details}")
                    .Aggregate((a, b) => a + "\n" + b);

                Clipboard.SetText(output);
                MessageBox.Show($"Successfully copied {selectedLogs.Count} log entr{(selectedLogs.Count == 1 ? "y" : "ies")} to clipboard.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnPurge_Click(object sender, EventArgs e)
        {
            using (var purgeDialog = new PurgeDialogForm())
            {
                if (purgeDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        int purgedCount;
                        if (purgeDialog.PurgeAll)
                        {
                            purgedCount = _auditLogManager.PurgeLogsByUser(_userId);
                            MessageBox.Show($"Successfully purged {purgedCount} log entries.", "Purge Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            purgedCount = _auditLogManager.PurgeLogsByUserAndDateRange(_userId, purgeDialog.StartDate, purgeDialog.EndDate);
                            MessageBox.Show($"Successfully purged {purgedCount} log entries from {purgeDialog.StartDate:yyyy-MM-dd} to {purgeDialog.EndDate:yyyy-MM-dd}.", "Purge Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        LoadLogs(datePicker.Value);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error purging logs: {ex.Message}", "Purge Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (var exportDialog = new ExportDialogForm())
            {
                if (exportDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        List<AuditLogEntry> logsToExport;
                        if (exportDialog.ExportAll)
                        {
                            logsToExport = _auditLogManager.GetLogsByUserAndDate(_userId, null)
                                .Where(l => !l.ActionType.StartsWith("Open"))
                                .ToList();
                        }
                        else
                        {
                            logsToExport = _auditLogManager.GetLogsByUserAndDateRange(_userId, exportDialog.StartDate, exportDialog.EndDate)
                                .Where(l => !l.ActionType.StartsWith("Open"))
                                .ToList();
                        }

                        if (!logsToExport.Any())
                        {
                            MessageBox.Show("No logs found to export.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        using (var saveFileDialog = new SaveFileDialog())
                        {
                            saveFileDialog.Filter = "JSON files (*.json)|*.json";
                            saveFileDialog.Title = "Export Audit Logs";
                            saveFileDialog.FileName = $"AuditLogs_{_userId}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                var json = JsonSerializer.Serialize(logsToExport, new JsonSerializerOptions { WriteIndented = true });
                                File.WriteAllText(saveFileDialog.FileName, json);
                                MessageBox.Show($"Successfully exported {logsToExport.Count} log entries to {saveFileDialog.FileName}.", "Export Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exporting logs: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}
