using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class MsalAccessTokenProvider : IAccessTokenProvider
{
    private readonly IPublicClientApplication _pca;
    private readonly string[] _scopes;

    public MsalAccessTokenProvider(IPublicClientApplication pca, string[] scopes)
    {
        _pca = pca ?? throw new ArgumentNullException(nameof(pca));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object> additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Get existing accounts
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account != null)
        {
            try
            {
                // Attempt silent token acquisition
                var result = await _pca.AcquireTokenSilent(_scopes, account)
                    .ExecuteAsync(cancellationToken);
                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // Silent acquisition failed; proceed to interactive
            }
        }

        // Fall back to interactive sign-in if no account or silent fails
        var authResult = await _pca.AcquireTokenInteractive(_scopes)
            .ExecuteAsync(cancellationToken);
        return authResult.AccessToken;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();
}