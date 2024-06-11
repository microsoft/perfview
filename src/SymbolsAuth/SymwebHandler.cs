using Azure.Core;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Diagnostics.Symbols.Authentication
{
    /// <summary>
    /// Contains constants, static properties and helper methods pertinent to Symweb.
    /// </summary>
    internal static class Symweb
    {
        /// <summary>
        /// The OAuth scope to use when requesting tokens for Symweb.
        /// </summary>
        public const string Scope = "af9e1c69-e5e9-4331-8cc5-cdf93d57bafa/.default";

        /// <summary>
        /// The url host for Symweb.
        /// </summary>
        public const string SymwebHost = "symweb.azurefd.net";

        /// <summary>
        /// Try to find the authority endpoint for Symweb given a full URI.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authority, if found.</param>
        /// <returns>True if <paramref name="requestUri"/> represents a path to a
        /// resource in Symweb.</returns>
        public static bool TryGetAuthority(Uri requestUri, out Uri authority)
        {
            if (!requestUri.IsAbsoluteUri)
            {
                authority = null;
                return false;
            }

            UriBuilder builder = null;
            string host = requestUri.DnsSafeHost;
            if (host.Equals(SymwebHost, StringComparison.OrdinalIgnoreCase))
            {
                builder = new UriBuilder
                {
                    Host = SymwebHost
                };
            }

            if (builder is null)
            {
                // Not a Symweb URI.
                authority = null;
                return false;
            }

            builder.Scheme = requestUri.Scheme;
            if (!requestUri.IsDefaultPort)
            {
                builder.Port = requestUri.Port;
            }

            authority = builder.Uri;
            return true;
        }
    }

    /// <summary>
    /// A handler that adds authorization for Symweb.
    /// </summary>
    public sealed class SymwebHandler : SymbolReaderAuthHandler
    {
        /// <summary>
        /// The value of <see cref="Symweb.Scope"/> stored in a single element
        /// array suitable for passing to
        /// <see cref="TokenCredential.GetTokenAsync(TokenRequestContext, CancellationToken)"/>.
        /// </summary>
        private static readonly string[] s_scopes = new[] { Symweb.Scope };

        /// <summary>
        /// Prefix to put in front of logging messages.
        /// </summary>
        private const string LogPrefix = "SymwebAuth: ";

        /// <summary>
        /// A provider of access tokens.
        /// </summary>
        private readonly TokenCredential _tokenCredential;

        /// <summary>
        /// Protect <see cref="_tokenCredential"/> against concurrent access.
        /// </summary>
        private readonly SemaphoreSlim _tokenCredentialGate = new SemaphoreSlim(initialCount: 1);

        /// <summary>
        /// An HTTP client used to discover the authority (login endpoint and tenant) for Symweb.
        /// </summary>
        private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });

        /// <summary>
        /// Construct a new <see cref="SymwebHandler"/> instance.
        /// </summary>
        /// <param name="tokenCredential">A provider of access tokens.</param>
        public SymwebHandler(TextWriter log, TokenCredential tokenCredential) : base(log, LogPrefix)
        {
            _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
        }

        /// <summary>
        /// Try to find the authority endpoint for Symweb
        /// given a full URI.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authority, if found.</param>
        /// <returns>True if <paramref name="requestUri"/> represents a path to a
        /// resource in Symweb.</returns>
        protected override bool TryGetAuthority(Uri requestUri, out Uri authority) => Symweb.TryGetAuthority(requestUri, out authority);

        /// <summary>
        /// Get a token to access Symweb.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler.</param>
        /// <param name="authority">The Symweb instance.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An access token, or null if one could not be obtained.</returns>
        protected override async Task<AuthToken?> GetAuthTokenAsync(RequestContext context, SymbolReaderHandlerDelegate next, Uri authority, CancellationToken cancellationToken)
        {
            // Get a new access token from the credential provider.
            WriteLog("Asking for authorization to access {0}", authority);
            AuthToken token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);

            return token;
        }

        /// <summary>
        /// Get a new access token for Symweb from the <see cref="TokenCredential"/>.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        private async Task<AuthToken> GetTokenAsync(CancellationToken cancellationToken)
        {
            await _tokenCredentialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Use the token credential provider to acquire a new token.
                TokenRequestContext requestContext = new TokenRequestContext(s_scopes);
                AccessToken accessToken = await _tokenCredential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
                return AuthToken.FromAzureCoreAccessToken(accessToken);
            }
            catch (Exception ex)
            {
                WriteStatusLog("Exception getting token. {0}", ex);
                throw;
            }
            finally
            {
                _tokenCredentialGate.Release();
            }
        }
    }
}
