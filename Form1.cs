using Microsoft.Identity.Client;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Identity.Client.Desktop;
using System.Text;
using System.IO;
using System.Security.Claims;

namespace EntraGroupsApp
{
    public partial class Form1 : Form
    {
        private IPublicClientApplication _pca;
        private GraphServiceClient _graphClient;
        private List<Microsoft.Graph.Models.Group> _selectedGroups;
        private bool _cleanedUp;
        private string _signedInUserId;
        private string _signedInUsername;
        private string _lastSignedInUserId;
        private AuditLogManager _auditLogManager; // Ensure AuditLogManager class is defined in the project
        private DateTime _lastSignOutClick = DateTime.MinValue;
        private DateTime _sessionStartTime;
        private bool _silentAuthAttempted;
        private bool _isSigningIn;
        private bool _clipboardPrompted;
        private AuthenticationResult _lastAuthResult;

        public Form1()
        {
            InitializeComponent();
            _sessionStartTime = DateTime.Now;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            Initialize();
        }

        private async void Initialize()
        {
            try
            {
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EntraGroupsApp", "WebView2");
                try
                {
                    Directory.CreateDirectory(userDataFolder);
                    Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create WebView2 user data folder at {userDataFolder}:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _pca = PublicClientApplicationBuilder.Create("54cd22c5-ac3f-4f6e-9037-822150861e61")
                    .WithAuthority("https://login.microsoftonline.com/68f381e3-46da-47b9-ba57-6f322b8f0da1")
                    .WithRedirectUri("http://localhost:57672")
                    .WithWindowsEmbeddedBrowserSupport()
                    .Build();

                var existingAccounts = await _pca.GetAccountsAsync();
                foreach (var account in existingAccounts)
                {
                    await _pca.RemoveAsync(account);
                }

                var scopes = new[] { "User.Read.All", "GroupMember.ReadWrite.All", "Sites.ReadWrite.All" };
                var accessTokenProvider = new MsalAccessTokenProvider(_pca, scopes);
                var authProvider = new BaseBearerTokenAuthenticationProvider(accessTokenProvider);
                _graphClient = new GraphServiceClient(authProvider);
                _auditLogManager = new AuditLogManager();

                _silentAuthAttempted = true;
                var accounts = await _pca.GetAccountsAsync();
                if (accounts.Any())
                {
                    try
                    {
                        var result = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();
                        _signedInUserId = result.Account.HomeAccountId.Identifier;
                        _signedInUsername = result.Account.Username;
                        _lastSignedInUserId = _signedInUserId;
                        _lastAuthResult = result;
                        SetStatusText($"Signed in as: {_signedInUsername}");

                        // Debug claims
                        DebugClaims(result.ClaimsPrincipal);

                        // Update button state based on ADMIN claim
                        UpdatePIMElevateButtonState(result.ClaimsPrincipal);

                        btnSignIn.Visible = false;
                        flowLayoutPanelButtons.Visible = true;
                        btnReviewChanges.Visible = true;
                        panelReviewChanges.Visible = true;
                        CenterControls();
                        this.Invalidate();
                        this.Update();
                        await LogAuditActionAsync(_signedInUserId, result.Account.Username, "SilentSignIn", null, null, null, "Silent authentication successful");
                    }
                    catch (MsalUiRequiredException ex)
                    {
                        Console.WriteLine($"Silent sign-in failed: {ex.Message}");
                        SetStatusText("Please sign in to continue");
                    }
                }
                else
                {
                    SetStatusText("Please sign in to continue");
                }

                btnSearchUser.Click += btnSearchUser_Click;
                btnSearchGroup.Click += btnSearchGroup_Click;
                btnPIMElevate.Click += btnPIMElevate_Click;
                btnReviewChanges.Click += btnReviewChanges_Click;
                btnSignIn.Click += btnSignIn_Click;
                btnSignOut.Click += btnSignOut_Click;

                FormClosing += Form1_FormClosing;
                Shown += Form1_Shown; // Add handler to recheck claims when form is shown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initialization error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to initialize application: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _auditLogManager = new AuditLogManager();
                await LogAuditActionAsync(null, null, "InitializationError", null, null, null, $"Initialization failed: {ex.Message}");
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            // Recheck claims when form is shown
            if (_lastAuthResult != null && _lastAuthResult.ClaimsPrincipal != null)
            {
                UpdatePIMElevateButtonState(_lastAuthResult.ClaimsPrincipal);
            }
        }

        private void DebugClaims(ClaimsPrincipal claimsPrincipal)
        {
            if (claimsPrincipal == null)
            {
                Console.WriteLine("ClaimsPrincipal is null");
                return;
            }

            var claims = claimsPrincipal.Claims.Select(c => $"Type: {c.Type}, Value: {c.Value}");
            Console.WriteLine($"Claims found: {string.Join("; ", claims)}");
            bool hasAdminClaim = claimsPrincipal.Claims.Any(c => c.Type == "roles" && c.Value.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"Has ADMIN claim: {hasAdminClaim}");
        }

        private void UpdatePIMElevateButtonState(ClaimsPrincipal claimsPrincipal)
        {
            bool isAdmin = claimsPrincipal?.Claims.Any(c => c.Type == "roles" && c.Value.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)) ?? false;
            btnPIMElevate.Enabled = isAdmin;
            Console.WriteLine($"PIM Elevate button enabled: {btnPIMElevate.Enabled}");
            this.Invalidate();
            this.Update();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_cleanedUp)
            {
                string userIdToUse = _signedInUserId ?? _lastSignedInUserId;

                if (!string.IsNullOrEmpty(userIdToUse) && !_clipboardPrompted)
                {
                    var sessionLogs = _auditLogManager.GetLogsByUserAndDate(userIdToUse, null)
                        .Where(l => l.Timestamp >= _sessionStartTime.AddSeconds(-1) &&
                                    new[] { "AddMember", "RemoveMember", "AddOwner", "RemoveOwner", "ReplaceGroup", "CopyGroups", "AddToGroups" }
                                    .Contains(l.ActionType))
                        .ToList();

                    Console.WriteLine($"FormClosing: Found {sessionLogs.Count} modification logs for user {userIdToUse}");
                    foreach (var log in sessionLogs)
                    {
                        Console.WriteLine($"Log: ActionType={log.ActionType}, GroupName={log.GroupName}, TargetName={log.TargetName}, Timestamp={log.Timestamp}, Details={log.Details}");
                    }

                    if (sessionLogs.Any())
                    {
                        var result = MessageBox.Show(
                            "Would you like to copy your current session's audit logs for user or group modifications to the clipboard before closing?",
                            "Export Session Modification Logs",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            try
                            {
                                var output = sessionLogs.Select(l =>
                                    $"Modification applied to {l.GroupName} for \"{l.TargetName}\" ({l.TargetType}) on {l.Timestamp:yyyy-MM-dd HH:mm:ss}. Details: {l.Details}")
                                    .Aggregate((a, b) => a + "\n" + b);

                                Clipboard.SetText(output);
                                MessageBox.Show("Session modification audit logs copied to clipboard.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error copying session logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else if (result == DialogResult.Cancel)
                        {
                            e.Cancel = true;
                            return;
                        }

                        _clipboardPrompted = true;
                    }
                }

                CleanupResources();
            }
        }

        private void CleanupResources()
        {
            if (_cleanedUp)
                return;

            btnSearchUser.Click -= btnSearchUser_Click;
            btnSearchGroup.Click -= btnSearchGroup_Click;
            btnPIMElevate.Click -= btnPIMElevate_Click;
            btnSignIn.Click -= btnSignIn_Click;
            btnSignOut.Click -= btnSignOut_Click;
            btnReviewChanges.Click -= btnReviewChanges_Click;
            FormClosing -= Form1_FormClosing;
            Shown -= Form1_Shown;

            _graphClient?.Dispose();
            _cleanedUp = true;
        }

        private void CenterControls()
        {
            lblStatus.Location = new Point((ClientSize.Width - lblStatus.Width) / 2, lblStatus.Location.Y);

            flowLayoutPanelButtons.PerformLayout();
            int panelWidth = flowLayoutPanelButtons.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Left + c.Margin.Right) + flowLayoutPanelButtons.Padding.Left + flowLayoutPanelButtons.Padding.Right;
            flowLayoutPanelButtons.Width = panelWidth;
            flowLayoutPanelButtons.Location = new Point((ClientSize.Width - flowLayoutPanelButtons.Width) / 2, flowLayoutPanelButtons.Location.Y);

            panelReviewChanges.Location = new Point((ClientSize.Width - panelReviewChanges.Width) / 2, flowLayoutPanelButtons.Bottom + 10);
            btnReviewChanges.Location = new Point((panelReviewChanges.Width - btnReviewChanges.Width) / 2, 5);
        }

        private void SetStatusText(string text)
        {
            lblStatus.Text = text;
            CenterControls();
            this.Invalidate();
            this.Update();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            CenterControls();
        }

        private async void btnSignIn_Click(object sender, EventArgs e)
        {
            if (_isSigningIn)
            {
                Console.WriteLine("Sign-in already in progress, ignoring click.");
                return;
            }

            _isSigningIn = true;
            btnSignIn.Enabled = false;
            try
            {
                if (!string.IsNullOrEmpty(_signedInUserId))
                {
                    Console.WriteLine($"Already signed in as: {_signedInUsername}");
                    SetStatusText($"Signed in as: {_signedInUsername}");
                    return;
                }

                using (var loginForm = new LoginForm(_pca, new[] { "User.Read.All", "Group.ReadWrite.All" }))
                {
                    var dialogResult = loginForm.ShowDialog();
                    Console.WriteLine($"LoginForm dialog result: {dialogResult}");
                    if (dialogResult == DialogResult.OK && loginForm.AuthenticationResult != null)
                    {
                        _signedInUserId = loginForm.AuthenticationResult.Account.HomeAccountId.Identifier;
                        _signedInUsername = loginForm.AuthenticationResult.Account.Username;
                        _lastSignedInUserId = _signedInUserId;
                        _lastAuthResult = loginForm.AuthenticationResult;
                        Console.WriteLine($"Sign-in successful: UserId={_signedInUserId}, Username={loginForm.AuthenticationResult.Account.Username}");
                        Console.WriteLine($"Acquired scopes: {string.Join(", ", loginForm.AuthenticationResult.Scopes)}");
                        Console.WriteLine($"Access Token: {loginForm.AuthenticationResult.AccessToken.Substring(0, 20)}...");

                        // Debug claims
                        DebugClaims(loginForm.AuthenticationResult.ClaimsPrincipal);

                        // Update button state based on ADMIN claim
                        UpdatePIMElevateButtonState(loginForm.AuthenticationResult.ClaimsPrincipal);

                        SetStatusText($"Signed in as: {_signedInUsername}");
                        btnSignIn.Visible = false;
                        flowLayoutPanelButtons.Visible = true;
                        btnReviewChanges.Visible = true;
                        panelReviewChanges.Visible = true;
                        CenterControls();
                        this.Invalidate();
                        this.Update();

                        await LogAuditActionAsync(_signedInUserId, loginForm.AuthenticationResult.Account.Username, "InteractiveSignIn", null, null, null, $"Interactive authentication successful with scopes: {string.Join(", ", loginForm.AuthenticationResult.Scopes)}");
                    }
                    else
                    {
                        Console.WriteLine("LoginForm returned non-OK result or null AuthenticationResult.");
                        SetStatusText("Sign-in cancelled or failed.");
                    }
                }
            }
            catch (MsalException ex)
            {
                var errorDetails = $"Sign-in failed (MsalException): {ex.Message}\nStackTrace: {ex.StackTrace}";
                Console.WriteLine(errorDetails);
                MessageBox.Show($"Sign-in failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "SignInError", null, null, null, errorDetails);
                SetStatusText("Sign-in failed.");
            }
            catch (Exception ex)
            {
                var errorDetails = $"Unexpected sign-in error: {ex.Message}\nStackTrace: {ex.StackTrace}";
                Console.WriteLine(errorDetails);
                MessageBox.Show($"Unexpected error during sign-in: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "SignInError", null, null, null, errorDetails);
                SetStatusText("Sign-in failed.");
            }
            finally
            {
                _isSigningIn = false;
                btnSignIn.Enabled = true;
            }
        }

        private async Task LogAuditActionAsync(string userId, string userName, string actionType, string groupName, string targetName, string targetType, string details)
        {
            try
            {
                // Ensure AuditLogManager has a LogAction method with this signature
                await _auditLogManager.LogAction(userId, userName, actionType, groupName, targetName, targetType, details);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log audit action: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        private async void btnSignOut_Click(object sender, EventArgs e)
        {
            try
            {
                if ((DateTime.Now - _lastSignOutClick).TotalMilliseconds < 500)
                {
                    await LogAuditActionAsync(_signedInUserId, null, "SignOutDebounced", null, null, null, "Sign-out click ignored due to debouncing");
                    return;
                }
                _lastSignOutClick = DateTime.Now;

                if (string.IsNullOrEmpty(_signedInUserId))
                {
                    await LogAuditActionAsync(_signedInUserId, null, "SignOutSkipped", null, null, null, "Sign-out skipped: Already signed out");
                    return;
                }

                var sessionLogs = _auditLogManager.GetLogsByUserAndDate(_signedInUserId, null)
                    .Where(l => l.Timestamp >= _sessionStartTime.AddSeconds(-1) &&
                                new[] { "AddMember", "RemoveMember", "AddOwner", "RemoveOwner", "ReplaceGroup", "CopyGroups", "AddToGroups" }
                                .Contains(l.ActionType))
                    .ToList();

                Console.WriteLine($"SignOut: Found {sessionLogs.Count} modification logs for user {_signedInUserId}");
                foreach (var log in sessionLogs)
                {
                    Console.WriteLine($"Log: ActionType={log.ActionType}, GroupName={log.GroupName}, TargetName={log.TargetName}, Timestamp={log.Timestamp}, Details={log.Details}");
                }

                if (sessionLogs.Any())
                {
                    var result = MessageBox.Show(
                        "Would you like to copy your current session's audit logs to the clipboard before signing out?",
                        "Export Session Modification Logs",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            var output = sessionLogs.Select(l =>
                                $"Modification applied to {l.GroupName} for \"{l.TargetName}\" ({l.TargetType}) on {l.Timestamp:yyyy-MM-dd HH:mm:ss}. Details: {l.Details}")
                                .Aggregate((a, b) => a + "\n" + b);

                            Clipboard.SetText(output);
                            MessageBox.Show("Session modification audit logs copied to clipboard.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error copying session logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    _clipboardPrompted = true;
                }

                var accounts = await _pca.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    await _pca.RemoveAsync(account);
                }
                _lastSignedInUserId = _signedInUserId;
                _signedInUsername = null;
                _signedInUserId = null;
                _lastAuthResult = null;
                btnSignIn.Visible = true;
                flowLayoutPanelButtons.Visible = false;
                btnReviewChanges.Visible = false;
                panelReviewChanges.Visible = false;
                btnPIMElevate.Enabled = false; // Ensure button is disabled on sign-out
                SetStatusText("Not signed in");
                CenterControls();
                this.Invalidate();
                this.Update();
                await LogAuditActionAsync(_lastSignedInUserId, null, "SignOut", null, null, null, "User signed out");
                MessageBox.Show(
                    "You have successfully signed out.\nYou can now close the application.",
                    "SharePoint Administrator Signout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sign-out error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Sign-out failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "SignOutError", null, null, null, $"Sign-out failed: {ex.Message}");
            }
        }

        private async void btnSearchUser_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastAuthResult == null || _lastAuthResult.ClaimsPrincipal == null)
                {
                    MessageBox.Show("Please sign in to access the User Search form.", "Authentication Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await LogAuditActionAsync(_signedInUserId, null, "OpenUserSearchFormError", null, null, null, "Attempted to open User Search form without signing in.");
                    return;
                }

                var userSearchForm = new UserSearchForm(_graphClient, this, _auditLogManager, _signedInUserId, _lastAuthResult.ClaimsPrincipal, _pca);
                userSearchForm.Show();
                this.Hide();
                await LogAuditActionAsync(_signedInUserId, null, "OpenUserSearchForm", null, null, null, "Opened User Search Form");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open User Search form error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to open User Search form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "OpenUserSearchFormError", null, null, null, $"Failed to open User Search form: {ex.Message}");
            }
        }

        private async void btnSearchGroup_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastAuthResult == null || _lastAuthResult.ClaimsPrincipal == null)
                {
                    MessageBox.Show("Please sign in to access the Group Search form.", "Authentication Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await LogAuditActionAsync(_signedInUserId, null, "OpenGroupSearchFormError", null, null, null, "Attempted to open Group Search form without signing in.");
                    return;
                }

                var groupSearchForm = new GroupSearchForm(_graphClient, this, _auditLogManager, _signedInUserId, _lastAuthResult.ClaimsPrincipal);
                groupSearchForm.Show();
                this.Hide();
                await LogAuditActionAsync(_signedInUserId, null, "OpenGroupSearchForm", null, null, null, "Opened Group Search Form");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open Group Search form error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to open Group Search form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "OpenGroupSearchFormError", null, null, null, $"Failed to open Group Search form: {ex.Message}");
            }
        }

        private async void btnPIMElevate_Click(object sender, EventArgs e)
        {
            try
            {
                using (var pimElevateForm = new PIMElevateForm())
                {
                    pimElevateForm.ShowDialog();
                    await LogAuditActionAsync(_signedInUserId, null, "OpenPIMElevateForm", null, null, null, "Opened PIM Elevate Form");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open PIM Elevate form error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to open PIM Elevate form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "OpenPIMElevateFormError", null, null, null, $"Failed to open PIM Elevate form: {ex.Message}");
            }
        }

        private async void btnReviewChanges_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_signedInUserId))
                {
                    MessageBox.Show("Please sign in to view audit logs.", "Authentication Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var auditForm = new AuditLogForm(_auditLogManager, _signedInUserId);
                auditForm.ShowDialog();
                await LogAuditActionAsync(_signedInUserId, null, "OpenAuditLogForm", null, null, null, "Opened Audit Log Form");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open Audit Log form error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Failed to open Audit Log form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await LogAuditActionAsync(_signedInUserId, null, "OpenAuditLogFormError", null, null, null, $"Failed to open Audit Log form: {ex.Message}");
            }
        }

        private void OpenManageMembershipsForm()
        {
            var manageForm = new ManageMembershipsForm(_selectedGroups, _graphClient, this, null, _auditLogManager, _signedInUserId);
            manageForm.Show();
            this.Hide();
        }

        public void SetSelectedGroups(List<Microsoft.Graph.Models.Group> groups)
        {
            _selectedGroups = groups ?? new List<Microsoft.Graph.Models.Group>();
        }
    }
}