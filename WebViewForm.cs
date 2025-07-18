using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;
using System.IO;

namespace EntraGroupsApp
{
    public partial class WebViewForm : Form
    {
        private readonly WebView2 _webView;

        public WebViewForm(string url, string title)
        {
            InitializeComponent();
            Text = title;

            // Set custom user data folder for WebView2
            string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EntraGroupsApp", "WebView2");
            try
            {
                Directory.CreateDirectory(userDataFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create WebView2 user data folder at {userDataFolder}:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(ClientSize.Width, ClientSize.Height - btnClose.Height - 10)
            };
            Controls.Add(_webView);

            // Initialize WebView2 and navigate
            InitializeWebView2Async(url, userDataFolder).GetAwaiter().GetResult();
        }

        private async Task InitializeWebView2Async(string url, string userDataFolder)
        {
            try
            {
                var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(environment);
                _webView.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void WebViewForm_Load(object sender, EventArgs e)
        {
            // Set form size to 80% of screen dimensions
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Size = new Size((int)(screen.Width * 0.8), (int)(screen.Height * 0.8));
            this.Location = new Point((screen.Width - this.Width) / 2, (screen.Height - this.Height) / 2);
        }
    }
}