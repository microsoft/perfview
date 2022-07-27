// Copyright (c) Microsoft Corporation.  All rights reserved
using Azure.Core;
using Azure.Identity;
using PerfView.Dialogs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PerfView
{
    /// <summary>
    /// A handler for <see cref="HttpClient"/> that allows interception and
    /// modification of requests by a chain of <see cref="IAsyncSymbolReaderHandler"/>
    /// instances.
    /// </summary>
    /// <remarks>
    /// In PerfView, this is usually passed to the constructor of
    /// <see cref="Microsoft.Diagnostics.Symbols.SymbolReader"/>.
    /// </remarks>
    internal class SymbolReaderHttpHandler : DelegatingHandler
    {
        /// <summary>
        /// A list of handlers.
        /// </summary>
        private readonly List<IAsyncSymbolReaderHandler> _handlers = new List<IAsyncSymbolReaderHandler>();

        /// <summary>
        /// Construct a new <see cref="SymbolReaderHttpHandler"/> instance.
        /// </summary>
        public SymbolReaderHttpHandler() : base(new HttpClientHandler())
        {
        }

        /// <summary>
        /// Process the request by forwarding it to all the handlers.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The response.</returns>
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var context = new RequestContext(request);

            // Build a delegate chain, starting with the root handler.
            SymbolReaderHandlerDelegate chain = () => RootHandlerAsync(context, cancellationToken);
            foreach (IAsyncSymbolReaderHandler handler in _handlers)
            {
                var next = new SymbolReaderHandlerDelegate(chain);
                chain = () => handler.ProcessRequestAsync(context, next, cancellationToken);
            }

            await chain().ConfigureAwait(false);
            return context.Response;
        }

        /// <summary>
        /// The root of the delegate chain.
        /// Calls the inner handler and sets the response
        /// on the <see cref="RequestContext"/> object.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the response has been set.</returns>
        private async Task RootHandlerAsync(RequestContext context, CancellationToken cancellationToken)
        {
            context.Response = await base.SendAsync(context.Request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Add a handler.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler WithHandler(IAsyncSymbolReaderHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>
        /// Add a handler for Azure DevOps authentication using local credentials.
        /// Local credentials will try to use cached credentials from Visual Studio,
        /// VS Code, Azure Powershell and Azure CLI.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="silent">If no local credentials can be found, then a browser window will
        /// be opened to prompt the user. Set this to true to if you don't want that.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler WithAzureDevOpsAuthentication(TextWriter log, bool silent = false)
        {
            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = silent,
                ExcludeManagedIdentityCredential = true // This is not designed to be used in a service.
            };

            return WithHandler(new AzureDevOpsHandler(log, new DefaultAzureCredential(options)));
        }

        /// <summary>
        /// Add a handler for GitHub device flow authentication.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler WithGitHubDeviceCodeAuthentication(TextWriter log) 
            => WithHandler(new GitHubHandler(log));
    }

    /// <summary>
    /// Delegate type for handlers.
    /// </summary>
    /// <returns></returns>
    internal delegate Task SymbolReaderHandlerDelegate();

    /// <summary>
    /// Context object passed to <see cref="IAsyncSymbolReaderHandler.ProcessRequestAsync(RequestContext, SymbolReaderHandlerDelegate, CancellationToken)"/>
    /// </summary>
    internal sealed class RequestContext
    {
        /// <summary>
        /// The response.
        /// </summary>
        private HttpResponseMessage _response;

        public RequestContext(HttpRequestMessage request)
        {
            Request = request;
        }

        /// <summary>
        /// The request. Handlers may modify this as necessary
        /// before or after calling the next handler in the chain.
        /// </summary>
        public HttpRequestMessage Request { get; }

        /// <summary>
        /// Gets or sets the response.It is initially null, and
        /// it will be set by the innermost handler. Handlers
        /// may examine it and modify it, if necessary, after
        /// calling the next handler in the chain.
        /// </summary>
        public HttpResponseMessage Response
        {
            get => _response;
            set
            {
                if (value != _response)
                {
                    if (_response != null)
                    {
                        _response.Dispose();
                    }

                    _response = value;
                }
            }
        }
    }

    /// <summary>
    /// Interface for symbol reader handlers.
    /// </summary>
    internal interface IAsyncSymbolReaderHandler
    {
        /// <summary>
        /// Process the given request, modifying it if necessary and passing it on to the next handler.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler in the chain.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when desired modifications have been made.</returns>
        Task ProcessRequestAsync(RequestContext context, SymbolReaderHandlerDelegate next, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Base class for handlers.
    /// </summary>
    internal abstract class SymbolReaderHandlerBase : IAsyncSymbolReaderHandler
    {
        /// <summary>
        /// A logger
        /// </summary>
        private readonly TextWriter _log;

        /// <summary>
        /// Prefix for log messages.
        /// </summary>
        private readonly string _logPrefix;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="log">A logger. May be null.</param>
        /// <param name="logPrefix">A prefix for log messages. May be null.</param>
        protected SymbolReaderHandlerBase(TextWriter log, string logPrefix)
        {
            _log = log ?? TextWriter.Null;
            _logPrefix = logPrefix;
        }

        /// <summary>
        /// Implemented in derived classes to handle a request.
        /// Implementations should call <paramref name="next"/>, modifying
        /// the request as necessary.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler in the chain.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the message has been handled.</returns>
        public abstract Task ProcessRequestAsync(RequestContext context, SymbolReaderHandlerDelegate next, CancellationToken cancellationToken);

        /// <summary>
        /// Write a line to the log, prefixed with <see cref="_logPrefix"/>.
        /// </summary>
        /// <param name="format">A composite format string with optional placeholders.</param>
        /// <param name="args">An object array that contains zero or more objects to format and write.</param>
        protected void WriteLog(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(_logPrefix))
            {
                _log.Write(_logPrefix);
            }

            _log.WriteLine(format, args);
        }
    }

    /// <summary>
    /// Base class for handlers that add authorization via OAuth.
    /// Contains common helper methods for OAuth authorization.
    /// </summary>
    internal abstract class SymbolReaderHandlerOAuthBase : SymbolReaderHandlerBase
    {
        /// <summary>
        /// The scheme name for Bearer tokens.
        /// </summary>
        protected const string BearerScheme = "Bearer";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="log">A logger. May be null.</param>
        /// <param name="logPrefix">A prefix for log messages. May be null.</param>
        protected SymbolReaderHandlerOAuthBase(TextWriter log, string logPrefix) : base(log, logPrefix)
        {
        }

        /// <summary>
        /// Check if the access token is still valid vis-à-vis its lifetime.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns><c>true</c> if the access token has not expired and is not going to expire in the next 5 minutes.</returns>
        protected static bool AccessTokenIsStillGood(in AccessToken accessToken)
        {
            if (accessToken.ExpiresOn == default || accessToken.Token == null)
            {
                return false;
            }

            TimeSpan timeLeft = accessToken.ExpiresOn - DateTime.UtcNow;

            // Try to refresh at least 5 minutes before expiration.
            return timeLeft > TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Add an authorization header to the request using the Bearer scheme.
        /// </summary>
        /// <param name="request">The request to modify.</param>
        /// <param name="accessToken">The access token to add.</param>
        protected static void AddBearerToken(HttpRequestMessage request, AccessToken accessToken)
            => AddBearerToken(request, accessToken.Token);

        /// <summary>
        /// Add an authorization header to the request using the Bearer scheme.
        /// </summary>
        /// <param name="request">The request to modify.</param>
        /// <param name="bearerToken">The access token to add.</param>
        protected static void AddBearerToken(HttpRequestMessage request, string bearerToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, bearerToken);
        }

        /// <summary>
        /// Try to extract the authorization URI (authority) from the parameter of a WWW-Authenticate
        /// header with Bearer scheme.
        /// </summary>
        /// <example>
        /// Given WWW-Authenticate: Bearer authorization_uri=https://login.authority.com/my-tenant-id
        /// The authorizationUri is https://login.authority.com/my-tenant-id
        /// </example>
        /// <param name="wwwAuthenticateHeaders">The set of WWW-Authenticate headers.</param>
        /// <param name="authorizationUri">The authorization URI, if found.</param>
        /// <returns><c>true</c> if the authority could be found.</returns>
        protected static bool TryGetBearerAuthority(HttpHeaderValueCollection<AuthenticationHeaderValue> wwwAuthenticateHeaders, out Uri authorizationUri)
        {
            if (wwwAuthenticateHeaders != null)
            {
                foreach (AuthenticationHeaderValue wwwAuthenticate in wwwAuthenticateHeaders)
                {
                    if (wwwAuthenticate.Scheme.Equals(BearerScheme, StringComparison.OrdinalIgnoreCase))
                    {
                        // The parameter looks like: authorization_uri=https://login.windows.net/77777777-8888-4444-9999-222222222222
                        string authorization
                            = GetChallengeParameterValue(wwwAuthenticate, "authorization_uri")
                            ?? GetChallengeParameterValue(wwwAuthenticate, "authorization");

                        return Uri.TryCreate(authorization, UriKind.Absolute, out authorizationUri);
                    }
                }
            }

            authorizationUri = null;
            return false;
        }

        /// <summary>
        /// Parse the parameter of a WWW-Authenticate header to find the value for a given key.
        /// </summary>
        /// <param name="headerValue">The <see cref="AuthenticationHeaderValue" /> from a response.</param>
        /// <param name="key">The key of the key/value pair to locate.</param>
        /// <returns>The value, if found associated with the <paramref name="key"/>, or null if it is not present.</returns>
        private static string GetChallengeParameterValue(AuthenticationHeaderValue headerValue, string key)
        {
            ReadOnlySpan<char> parameter = headerValue.Parameter.AsSpan();
            ReadOnlySpan<char> wantedKey = key.AsSpan();

            while (TryGetNextChallengeParameter(ref parameter, out ReadOnlySpan<char> foundKey, out ReadOnlySpan<char> value))
            {
                if (wantedKey.Equals(foundKey, StringComparison.OrdinalIgnoreCase))
                {
                    return value.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Iterates through the parameter key=value pairs of a WWW-Authenticate challenge header.
        /// </summary>
        /// <param name="parameter">The full parameter value (after the scheme).</param>
        /// <param name="key">The parsed key.</param>
        /// <param name="value">The parsed value.</param>
        /// <returns>
        /// <c>true</c> if the next available challenge parameter was successfully parsed.
        /// <c>false</c> if there are no more parameters for the current challenge scheme or an additional challenge scheme was encountered in the <paramref name="parameter"/>.
        /// </returns>
        private static bool TryGetNextChallengeParameter(ref ReadOnlySpan<char> parameter, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
        {
            const char separator = '=';
            key = default;
            value = default;
            ReadOnlySpan<char> spaceOrComma = " ,".AsSpan();

            // Trim any separator prefixes.
            parameter = parameter.TrimStart(spaceOrComma);

            int nextSpace = parameter.IndexOf(' ');
            int nextSeparator = parameter.IndexOf(separator);
            if (nextSpace < nextSeparator && nextSpace != -1)
            {
                // We encountered another challenge value.
                return false;
            }

            if (nextSeparator < 0)
            {
                return false;
            }

            // Get the paramKey.
            key = parameter.Slice(0, nextSeparator).Trim();

            // Slice to remove the 'paramKey=' from the parameters.
            parameter = parameter.Slice(nextSeparator + 1);

            // The start of paramValue will usually be a quoted string. Find the first quote.
            int quoteIndex = parameter.IndexOf('\"');

            // Get the paramValue, which is delimited by the trailing quote.
            parameter = parameter.Slice(quoteIndex + 1);
            if (quoteIndex >= 0)
            {
                // The values are quote wrapped
                quoteIndex = parameter.IndexOf('\"');
                if (quoteIndex >= 0)
                {
                    value = parameter.Slice(0, quoteIndex);
                }
            }
            else
            {
                // The values are not quote wrapped.
                // Either find the next space, or go to the end since this is the last value.
                int trailingDelimiterIndex = parameter.IndexOfAny(spaceOrComma);
                if (trailingDelimiterIndex >= 0)
                {
                    value = parameter.Slice(0, trailingDelimiterIndex);
                }
                else
                {
                    value = parameter;
                }
            }

            // Slice to remove the '"paramValue"' from the parameters.
            if (parameter != value)
            {
                parameter = parameter.Slice(value.Length + 1);
            }

            return true;
        }

        /// <summary>
        /// Extract the tenant ID part of an authority.
        /// </summary>
        /// <example>
        /// For https://login.windows.net/77777777-8888-4444-9999-222222222222 the tenant ID is 77777777-8888-4444-9999-222222222222
        /// </example>
        /// <param name="authorizationUri">The authorization URI (authority)</param>
        /// <returns>The tenant ID part.</returns>
        protected static string GetTenantIdFromAuthority(Uri authorizationUri) => authorizationUri.Segments[1].Trim('/');
    }

    /// <summary>
    /// A handler that adds authorization for Azure DevOps instances.
    /// </summary>
    internal class AzureDevOpsHandler : SymbolReaderHandlerOAuthBase
    {
        /// <summary>
        /// The OAuth scope to use when requesting tokens for Azure DevOps.
        /// </summary>
        private const string Scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

        /// <summary>
        /// The <see cref="Scope"/> stored in a single element array suitable
        /// for passing to <see cref="TokenCredential.GetTokenAsync(TokenRequestContext, CancellationToken)"/>.
        /// </summary>
        private static readonly string[] s_scopes = new[] { Scope };

        /// <summary>
        /// Prefix to put in front of logging messages.
        /// </summary>
        private const string LogPrefix = "[AzDOAuth] ";

        /// <summary>
        /// A provider of access tokens.
        /// </summary>
        private readonly TokenCredential _tokenCredential;

        /// <summary>
        /// Protect <see cref="_tokenCredential"/> against concurrent access.
        /// </summary>
        private readonly SemaphoreSlim _tokenCredentialGate = new SemaphoreSlim(initialCount: 1);

        /// <summary>
        /// Cache of access tokens per authority. We cache tokens for both
        /// Azure DevOps instances and OAuth authorization endpoints.
        /// </summary>
        private readonly ConcurrentDictionary<Uri, AccessToken> _tokenCache = new ConcurrentDictionary<Uri, AccessToken>();

        /// <summary>
        /// A client used to discover the tenant for an Azure Dev Ops instance.
        /// </summary>
        private readonly HttpClient _nonRedirectingHttpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        /// <summary>
        /// Construct a new <see cref="AzureDevOpsHandler"/> instance.
        /// </summary>
        /// <param name="tokenCredential">A provider of access tokens.</param>
        public AzureDevOpsHandler(TextWriter log, TokenCredential tokenCredential) : base(log, LogPrefix)
        {
            _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
        }

        /// <summary>
        /// Process the request, adding an authorization header, if required.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="cancellationToken">A cancellationtoken.</param>
        /// <returns>A task that completes when processing is complete.</returns>
        public override async Task ProcessRequestAsync(RequestContext context, SymbolReaderHandlerDelegate next, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = context.Request;
            if (
                request.RequestUri.Scheme == Uri.UriSchemeHttps // Require a secure connection
                && request.Headers.Authorization == null        // Require unauthenticated
                && TryGetAzureDevOpsAuthority(request.RequestUri, out Uri authority) // Is an Azure DevOps request
                )
            {
                await AddAuthHeaderAsync(request, authority, cancellationToken).ConfigureAwait(false);
            }

            // Call the next handler.
            await next().ConfigureAwait(false);
        }

        /// <summary>
        /// Try to add an authorization header to the request.
        /// </summary>
        /// <param name="request">The request to modify.</param>
        /// <param name="authority">The Azure DevOps authority.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the request has been modified.</returns>
        private async Task AddAuthHeaderAsync(HttpRequestMessage request, Uri authority, CancellationToken cancellationToken)
        {
            // First check the token cache.
            if (_tokenCache.TryGetValue(authority, out AccessToken token))
            {
                if (AccessTokenIsStillGood(token))
                {
                    AddBearerToken(request, token);
                    return;
                }

                WriteLog("Existing authorization token for {0} has expired (or is close to expiration).");
                _tokenCache.TryRemove(authority, out _);
            }

            // Query the authority for the OAuth authorization endpoint.
            using (HttpResponseMessage challenge = await _nonRedirectingHttpClient.GetAsync(authority, cancellationToken).ConfigureAwait(false))
            {
                if (challenge.IsSuccessStatusCode || !TryGetBearerAuthority(challenge.Headers.WwwAuthenticate, out Uri authorizationUri))
                {
                    WriteLog("We expected a sign-in challenge from the DevOps authority, but we didn't get one.");
                    return;
                }

                // Check our authority cache again. This helps in the case where you have
                // several Azure DevOps instances (different organizations) in the same
                // tenant. They can share the same access token.
                // It also handles cases like "artifacts.dev.azure.com/org" and "dev.azure.com/org"
                // or "{org}.artifacts.visualstudio.com" and "{org}.visualstudio.com"
                if (_tokenCache.TryGetValue(authorizationUri, out token) && AccessTokenIsStillGood(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token.Token);
                    return;
                }

                await _tokenCredentialGate.WaitAsync(cancellationToken);
                try
                {
                    // Use the token credential provider to acquire a new token.
                    WriteLog("Asking for authorization to access {0}", authority);
                    TokenRequestContext requestContext = new TokenRequestContext(s_scopes, tenantId: GetTenantIdFromAuthority(authorizationUri));
                    token = await _tokenCredential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    WriteLog("Exception getting token. {0}", ex);
                    throw;
                }
                finally
                {
                    _tokenCredentialGate.Release();
                }

                // Store the token in the cache for both the AzDevOps authority and the OAuth authority.
                _tokenCache[authority] = token;
                _tokenCache[authorizationUri] = token;
            }

            AddBearerToken(request, token);
        }

        /// <summary>
        /// Try to find the authority endpoint for an Azure DevOps instance
        /// given a full URI. The authority endpoint is where we go to discover
        /// the tenant prior to signing in.
        /// </summary>
        /// <example>
        /// For https://artifacts.dev.azure.com/yourorg/_apis/Symbols/etc
        /// or  https://dev.azure.com/yourorg/yourproject/_apis/git/repositories/yourrepo/etc
        /// this generates https://dev.azure.com/yourorg
        /// </example>
        /// <example>
        /// For https://yourorg.artifacts.visualstudio.com/_apis/Symbol/symsrv/etc
        /// or https://yourorg.visualstudio.com/yourproject/_apis/git/repositories/yourrepo/etc
        /// this generates https://yourorg.visualstudio.com
        /// </example>
        /// <param name="uri">The request URI.</param>
        /// <param name="authority">The authortiy, if found.</param>
        /// <returns>True if <paramref name="uri"/> represents a path to a
        /// resource in an Azure DevOps instance.</returns>
        private static bool TryGetAzureDevOpsAuthority(Uri uri, out Uri authority)
        {
            // Extract the authority URI from a full Azure DevOps URI.
            string host = uri.Host;
            if (host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("artifacts.dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                // For dev.azure.com, the organization is the first part of the path.
                authority = new Uri("https://dev.azure.com/" + uri.Segments[1]);
                return true;
            }

            if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                // For *.visualstudio.com, the organization is included in the host name.
                // e.g. yourorg.visualstudio.com
                // or   yourorg.artifacts.visualstudio.com
                string org = host.Substring(0, host.IndexOf('.'));
                authority = new Uri("https://" + org + ".visualstudio.com");
                return true;
            }

            // Not an Azure DevOps URI.
            authority = null;
            return false;
        }
    }

    /// <summary>
    /// A handler that handles GitHub device flow authorization.
    /// </summary>
    internal class GitHubHandler : SymbolReaderHandlerOAuthBase
    {
        /// <summary>
        /// This is the OAuth client ID for the PerfView application.
        /// </summary>
        private const string PerfViewClientId = "19ac885cec7949d42a72";

        /// <summary>
        /// The scope requested. We need repo access to fetch source code from private repos.
        /// </summary>
        /// <remarks>
        /// See https://docs.github.com/en/developers/apps/building-oauth-apps/scopes-for-oauth-apps
        /// TODO: repo scope gives us write access which we don't need. Is there a narrower
        /// scope that gives us read-only?
        /// </remarks>
        private const string Scope = "repo";

        /// <summary>
        /// GitHub URI to start the device flow. Requests a unique
        /// device code and user code.
        /// </summary>
        private static readonly Uri _deviceFlowUri = new Uri("https://github.com/login/device/code");

        /// <summary>
        /// GitHub URI to finish the device flow. We poll this endpoint
        /// while the user completes the device flow (by signing into
        /// GitHub in the Browser).
        /// </summary>
        private static readonly Uri _getAccessTokenUri = new Uri("https://github.com/login/oauth/access_token");

        /// <summary>
        /// Prefix to put in front of logging messages.
        /// </summary>
        private const string LogPrefix = "[GitHubAuth] ";

        /// <summary>
        /// The access token.
        /// </summary>
        /// <remarks>
        /// As far as I can tell, once granted, this token never expires.
        /// </remarks>
        private string _accessToken;

        /// <summary>
        /// An HTTP client for making device flow calls
        /// </summary>
        private readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Gate to protect against multiple calls to the device flow.
        /// </summary>
        private readonly SemaphoreSlim _deviceFlowGate = new SemaphoreSlim(initialCount: 1);

        public GitHubHandler(TextWriter log) : base(log, LogPrefix)
        {
            _httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
        }

        /// <summary>
        /// Process the request.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler in the chain.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        public override async Task ProcessRequestAsync(RequestContext context, SymbolReaderHandlerDelegate next, CancellationToken cancellationToken)
        {
            if (ShouldHandleRequest(context.Request))
            {
                // If we have an access token, add it now.
                if (!string.IsNullOrEmpty(_accessToken))
                {
                    AddBearerToken(context.Request, _accessToken);
                }
                else
                {
                    // To avoid unnecessary authorization prompts, we want a way to detect
                    // if we're trying to access a private repo. Sadly, GitHub doesn't give
                    // any indication when a request fails due to lack of authorization. It
                    // just returns 404, making it indistinguishable from other "not found"
                    // results.
                    // To deal with this, we'll try an unauthenticated call first and prompt
                    // for authorization only if we get back a 404. If we're able to get an
                    // access token, then we'll retry the original request.
                    await next().ConfigureAwait(false);
                    if (context.Response.StatusCode != HttpStatusCode.NotFound)
                    {
                        return;
                    }

                    string accessToken = await TryGetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        WriteLog("Device flow failed.");
                        return;
                    }


                    // Remember the token for future requests.
                    _accessToken = accessToken;
                    WriteLog("Retrying the request with an access token acquired from device flow.");
                    AddBearerToken(context.Request, accessToken);

                    // Clear out the original response and fall through to retry the request.
                    context.Response = null;
                }
            }

            await next().ConfigureAwait(false);
        }

        /// <summary>
        /// Determines if the request is one we should handle.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>True if we should try and handle the request.</returns>
        private static bool ShouldHandleRequest(HttpRequestMessage request)
            => request.RequestUri.Scheme == Uri.UriSchemeHttps  // Over a secure connection
            && request.Headers.Authorization == null            // Currently unauthenticated
            && IsGitHubCom(request.RequestUri);                 // Is a GitHub URL

        /// <summary>
        /// Determines if the given URI belongs to GitHub.
        /// </summary>
        /// <param name="uri">The URI to test.</param>
        /// <returns>True if <paramref name="uri"/> belongs to GitHub.</returns>
        private static bool IsGitHubCom(Uri uri)
            => uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Try to get an access token to call GitHub by using the device flow.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token, or null if one could not be obtained.</returns>
        private async Task<string> TryGetAccessTokenAsync(CancellationToken cancellationToken)
        {
            // Start the device flow.
            await _deviceFlowGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                WriteLog("Start device flow authorization.");
                DeviceFlowResponse deviceFlowResponse = await PostDeviceFlowRequestAsync(cancellationToken).ConfigureAwait(false);

                // The user now needs to go to open a web browser and follow the device flow
                // to log into GitHub.
                Uri pollingUri = new Uri(_getAccessTokenUri, $"?client_id={PerfViewClientId}&device_code={deviceFlowResponse.DeviceCode}&grant_type=urn:ietf:params:oauth:grant-type:device_code");
                using (var sharedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // Automatically cancel after the device code expires.
                    sharedCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(deviceFlowResponse.ExpiresIn));

                    // There are three ways to exit the device flow:
                    // 1. Closing the dialog will cancel the polling task.
                    // 2. The polling task will signal the dialog to close on success.
                    // 3. The original request is cancelled.
                    CancellationToken sharedCancellationToken = sharedCancellationTokenSource.Token;
                    Task<string> pollingTask = Task.Run(() => PollForAccessTokenAsync(pollingUri, TimeSpan.FromSeconds(deviceFlowResponse.Interval), sharedCancellationTokenSource, sharedCancellationToken));

                    // Show the dialog and wait until it is closed.
                    WriteLog("Showing device flow dialog so you can log into GitHub.");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var deviceCodeDialog = new GitHubDeviceFlowDialog(Application.Current.MainWindow, deviceFlowResponse.VerificationUri, deviceFlowResponse.UserCode, sharedCancellationToken);
                        _ = deviceCodeDialog.ShowDialog();
                    });

                    sharedCancellationTokenSource.Cancel();
                    return await pollingTask.ConfigureAwait(false);
                }
            }
            finally
            {
                _deviceFlowGate.Release();
            }
        }

        /// <summary>
        /// Poll the access token API until the device flow completes.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="pollingInterval"></param>
        /// <param name="cancelDialog"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The access token.</returns>
        private async Task<string> PollForAccessTokenAsync(Uri uri, TimeSpan pollingInterval, CancellationTokenSource cancelDialog, CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
                    using (HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                            AccessTokenResponse accessTokenResponse = await JsonSerializer.DeserializeAsync<AccessTokenResponse>(stream).ConfigureAwait(false);
                            string accessToken = accessTokenResponse.AccessToken;
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                cancelDialog.Cancel();
                                return accessToken;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Start a device flow by posting a request to GitHub's device flow endpoint.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The device flow response.</returns>
        /// <remarks>
        /// See https://docs.github.com/en/developers/apps/building-oauth-apps/authorizing-oauth-apps#step-1-app-requests-the-device-and-user-verification-codes-from-github
        /// </remarks>
        private async Task<DeviceFlowResponse> PostDeviceFlowRequestAsync(CancellationToken cancellationToken)
        {
            string query = $"?client_id={PerfViewClientId}&scope={Scope}";
            Uri requestUri = new Uri(_deviceFlowUri, query);
            using (HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content: null, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<DeviceFlowResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Response from a device flow request.
        /// </summary>
        /// <remarks>
        /// See https://docs.github.com/en/developers/apps/building-oauth-apps/authorizing-oauth-apps#response-1
        /// </remarks>
        private sealed class DeviceFlowResponse
        {
            /// <summary>
            /// The device verification code is 40 characters and used to verify the device.
            /// </summary>
            [JsonPropertyName("device_code")]
            public string DeviceCode { get; set; }

            /// <summary>
            ///  The user verification code is displayed on the device so the user can enter
            ///  the code in a browser. This code is 8 characters with a hyphen in the middle.
            /// </summary>
            [JsonPropertyName("user_code")]
            public string UserCode { get; set; }

            /// <summary>
            /// The verification URL where users need to enter the user_code: https://github.com/login/device.
            /// </summary>
            [JsonPropertyName("verification_uri")]
            public Uri VerificationUri { get; set; }

            /// <summary>
            /// The number of seconds before the device_code and user_code expire.
            /// The default is 900 seconds or 15 minutes.
            /// </summary>
            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            /// <summary>
            /// The minimum polling interval in seconds.
            /// </summary>
            [JsonPropertyName("interval")]
            public int Interval { get; set; }
        }

        /// <summary>
        /// The result of a call to acquire an access token.
        /// </summary>
        private sealed class AccessTokenResponse
        {
            /// <summary>
            /// The access token.
            /// </summary>
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            /// <summary>
            /// The token type (Bearer).
            /// </summary>
            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            /// <summary>
            /// The scope granted.
            /// </summary>
            [JsonPropertyName("scope")]
            public string Scope { get; set; }
        }
    }

}
