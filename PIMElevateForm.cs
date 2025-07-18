using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class PIMElevateForm : Form
    {
        private WebView2 _webView;
        private const string PIM_URL = "https://entra.microsoft.com/#view/Microsoft_Azure_PIMCommon/ActivationMenuBlade/~/aadmigratedroles";

        public PIMElevateForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _webView = new WebView2();
            SuspendLayout();

            // Calculate dynamic size based on screen resolution
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            int formWidth = (int)(workingArea.Width * 0.7); // 70% of screen width
            int formHeight = (int)(workingArea.Height * 0.6); // 60% of screen height

            // Apply bounds
            formWidth = Math.Max(600, Math.Min(1200, formWidth)); // Min 600, Max 1200
            formHeight = Math.Max(400, Math.Min(800, formHeight)); // Min 400, Max 800

            // WebView2 control
            _webView.Dock = DockStyle.Fill;
            _webView.Name = "webView";
            _webView.Size = new Size(formWidth, formHeight);
            Controls.Add(_webView);

            // PIMElevateForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(formWidth, formHeight);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PIMElevateForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "PIM Role Elevation";
            Load += PIMElevateForm_Load;
            ResumeLayout(false);
        }

        private async void PIMElevateForm_Load(object sender, EventArgs e)
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.CoreWebView2.Navigate(PIM_URL);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load PIM page: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }
    }
}