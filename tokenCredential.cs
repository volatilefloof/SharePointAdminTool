using Azure.Core;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class MsalTokenCredential : TokenCredential
{
    private readonly IPublicClientApplication _pca;
    private readonly string[] _scopes;

    public MsalTokenCredential(IPublicClientApplication pca, string[] scopes)
    {
        _pca = pca ?? throw new ArgumentNullException(nameof(pca));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        // Synchronous method not implemented; use async instead
        throw new NotImplementedException("Use GetTokenAsync for asynchronous token acquisition.");
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        // Get existing accounts
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account != null)
        {
            try
            {
                // Try to acquire token silently
                var result = await _pca.AcquireTokenSilent(_scopes, account)
                    .ExecuteAsync(cancellationToken);
                return new AccessToken(result.AccessToken, result.ExpiresOn);
            }
            catch (MsalUiRequiredException)
            {
                // Silent acquisition failed; proceed to interactive
            }
        }

        // If no account or silent fails, use interactive sign-in
        var authResult = await _pca.AcquireTokenInteractive(_scopes)
            .ExecuteAsync(cancellationToken);
        return new AccessToken(authResult.AccessToken, authResult.ExpiresOn);
    }
}