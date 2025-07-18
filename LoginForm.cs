using Microsoft.Identity.Client;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    public partial class LoginForm : Form
    {
        private readonly IPublicClientApplication _pca;
        private readonly string[] _scopes;
        private WebView2 _webView;
        public AuthenticationResult AuthenticationResult { get; private set; }

        public LoginForm(IPublicClientApplication pca, string[] scopes)
        {
            _pca = pca ?? throw new ArgumentNullException(nameof(pca));
            _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _webView = new WebView2();
            SuspendLayout();

            // WebView2 control
            _webView.Dock = DockStyle.Fill;
            _webView.Name = "webView";
            _webView.Size = new Size(600, 400);
            _webView.NavigationStarting += WebView_NavigationStarting;
            Controls.Add(_webView);

            // LoginForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "LoginForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Sign In";
            Load += LoginForm_Load;
            ResumeLayout(false);
        }

        private async void LoginForm_Load(object sender, EventArgs e)
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                await StartAuthenticationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize login: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private async Task StartAuthenticationAsync()
        {
            try
            {
                AuthenticationResult = await _pca.AcquireTokenInteractive(_scopes)
                    .ExecuteAsync();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (MsalException ex)
            {
                if (ex.ErrorCode != "authentication_canceled")
                {
                    MessageBox.Show($"Authentication failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void WebView_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith("http://localhost:57672"))
            {
                e.Cancel = false; // Let MSAL handle the redirect
            }
        }
    }
}