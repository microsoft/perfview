// Copyright (c) Microsoft Corporation.  All rights reserved
using Azure.Core;
using Azure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using PerfView.Dialogs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

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
        public SymbolReaderHttpHandler() : base(new HttpClientHandler() { CheckCertificateRevocationList = true })
        {
        }

        /// <summary>
        /// Gets or sets whether this instance has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
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

            // Prevent modification while we build the chain.
            lock (_handlers)
            {
                // Loop through the handlers in reverse so that the resulting
                // chain is in the order the handlers were added.
                for (int i = _handlers.Count - 1; i >= 0; i--)
                {
                    IAsyncSymbolReaderHandler handler = _handlers[i];
                    var next = new SymbolReaderHandlerDelegate(chain);
                    chain = () => handler.ProcessRequestAsync(context, next, cancellationToken);
                }
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
        /// Remove all the handlers.
        /// </summary>
        public void ClearHandlers()
        {
            lock (_handlers)
            {
                foreach (var handler in _handlers)
                {
                    if (handler is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                _handlers.Clear();
            }
        }

        /// <summary>
        /// Add a handler. Handlers will be called in the order they are added.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler AddHandler(IAsyncSymbolReaderHandler handler)
        {
            lock (_handlers)
            {
                _handlers.Add(handler);
            }

            return this;
        }

        /// <summary>
        /// Add a handler that uses Git Credential Manager for authentication.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="mainWindow">The main window to use when parenting modal dialogs.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler AddGitCredentialManagerAuthentication(TextWriter log, Window mainWindow)
            => AddHandler(new GitCredentialManagerHandler(log, GetWindowHandle(mainWindow)));

        /// <summary>
        /// Add a handler for Azure DevOps authentication using local credentials.
        /// It will try to use cached credentials from Visual Studio, VS Code, 
        /// Azure Powershell and Azure CLI.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="silent">If no local credentials can be found, then a browser window will
        /// be opened to prompt the user. Set this to true to if you don't want that.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler AddAzureDevOpsAuthentication(TextWriter log, bool silent = false)
        {
            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = silent,
                ExcludeManagedIdentityCredential = true // This is not designed to be used in a service.
            };

            return AddHandler(new AzureDevOpsHandler(log, new DefaultAzureCredential(options)));
        }

        /// <summary>
        /// Add a handler for GitHub device flow authentication.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="mainWindow">The Window to use for parenting any modal
        /// dialogs needed for authentication.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public SymbolReaderHttpHandler AddGitHubDeviceCodeAuthentication(TextWriter log, Window mainWindow)
            => AddHandler(new GitHubDeviceFlowHandler(log, mainWindow));

        /// <summary>
        /// Get the HWND of the given WPF window in a way that honors WPF
        /// threading rules.
        /// </summary>
        /// <param name="window">The WPF window.</param>
        /// <returns>The handle (HWND) of the given window.</returns>
        private static IntPtr GetWindowHandle(Window window)
            => window.Dispatcher.Invoke(() => new WindowInteropHelper(window).Handle);
    }

    /// <summary>
    /// The delegate type that handlers invoke to chain to the next handler.
    /// </summary>
    /// <returns>A task.</returns>
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
    /// Enumeration of supported authentication schemes.
    /// </summary>
    internal enum AuthScheme
    {
        /// <summary>
        /// Basic authentication.
        /// </summary>
        Basic,

        /// <summary>
        /// Bearer token.
        /// </summary>
        Bearer
    }

    /// <summary>
    /// This is similar to <see cref="AccessToken"/>, but it includes
    /// the authentication scheme.
    /// </summary>
    internal readonly struct AuthToken : IEquatable<AuthToken>
    {
        /// <summary>
        /// Creates a new instance of <see cref="AuthToken"/> using the supplied
        /// parameters.
        /// </summary>
        /// <param name="scheme">The scheme.</param>
        /// <param name="token">The token value.</param>
        /// <param name="expiresOn">The token's expiry time.</param>
        /// <param name="userName">Optional user name.</param>
        public AuthToken(AuthScheme scheme, string token, DateTime expiresOn, string userName = null)
        {
            Scheme = scheme;
            Token = token;
            ExpiresOn = expiresOn;
            UserName = userName;
            Password = null;
        }

        /// <summary>
        /// Create a <see cref="AuthScheme.Basic"/> using the given username
        /// and password.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        private AuthToken(string username, string password)
        {
            Scheme = AuthScheme.Basic;
            UserName = username;
            Password = password;
            ExpiresOn = DateTime.UtcNow + TimeSpan.FromDays(60);

            // The Token is the Base-64-encoded value representing the
            // username and password combined.
            // The lifetime is set to 60 days.
            string secret = username + ':' + password;
            byte[] utf8Secret = Encoding.UTF8.GetBytes(secret);
            Token = Convert.ToBase64String(utf8Secret);
        }

        /// <summary>
        /// Creates a new <see cref="AuthToken"/> with the <see cref="AuthScheme.Basic"/> 
        /// scheme from a user name and password. A single token value is computed as a
        /// Base-64 encoded value representing the username and password combined.
        /// If no lifetime is specified, the token will be valid for 60 days.
        /// </summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <returns>A constructed <see cref="AuthToken"/>.</returns>
        public static AuthToken CreateBasicFromUsernameAndPassword(string username, string password)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException($"'{nameof(username)}' cannot be null or empty.", nameof(username));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException($"'{nameof(password)}' cannot be null or empty.", nameof(password));
            }

            return new AuthToken(username, password);
        }

        /// <summary>
        /// Try to parse an <see cref="AuthToken"/> from an OAuth bearer token
        /// as a Json Web Token.
        /// </summary>
        /// <param name="userName">The user name, if any.</param>
        /// <param name="token">The Json Web Token (JWT) value.</param>
        /// <param name="authToken">The auth token.</param>
        /// <returns>True if the token could be parsed.</returns>
        public static bool TryParseFromOAuthToken(string userName, string token, out AuthToken authToken)
        {
            try
            {
                var jwt = new JsonWebToken(token);
                authToken= new AuthToken(AuthScheme.Bearer, token, jwt.ValidTo, userName);
                return true;
            }
            catch (Exception)
            {
                authToken = default;
                return false;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="AuthToken"/> from an <see cref="AccessToken"/>
        /// using the <see cref="AuthScheme.Bearer"/> scheme.
        /// </summary>
        /// <param name="token">The access token from Azure.Core</param>
        public static AuthToken FromAzureCoreAccessToken(AccessToken token)
            => new AuthToken(AuthScheme.Bearer, token.Token, token.ExpiresOn.UtcDateTime);

        /// <summary>
        /// Gets the authentication scheme to use with this token.
        /// </summary>
        public AuthScheme Scheme { get; }

        /// <summary>
        /// Gets the value of the token.
        /// </summary>
        public string Token { get; }

        /// <summary>
        /// Gets the time when the token expires.
        /// </summary>
        public DateTime ExpiresOn { get; }

        /// <summary>
        /// The username. May be null.
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// The password part of a basic authentication token.
        /// </summary>
        public string Password { get; }

        public override bool Equals(object obj) => obj is AuthToken token && Equals(token);

        public bool Equals(AuthToken other)
        {
            // Only the Scheme and Token take part
            // in equality.
            return Scheme == other.Scheme &&
                   Token == other.Token;
        }

        public override int GetHashCode()
        {
            // Only the Scheme and Token take part
            // in equality.
            int hashCode = -41169086;
            hashCode = hashCode * -1521134295 + Scheme.GetHashCode();
            hashCode = hashCode * -1521134295 + StringComparer.Ordinal.GetHashCode(Token);
            return hashCode;
        }

        public static bool operator ==(AuthToken left, AuthToken right) => left.Equals(right);

        public static bool operator !=(AuthToken left, AuthToken right) => !(left == right);
    }

    /// <summary>
    /// Interface for asynchronous symbol reader handlers.
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
    /// Common base class for all <see cref="IAsyncSymbolReaderHandler"/> implementations.
    /// Adds a logger with a handler-specific prefix.
    /// </summary>
    internal abstract class SymbolReaderHandlerBase : IAsyncSymbolReaderHandler
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly TextWriter _log;

        /// <summary>
        /// A prefix for all messages written by <see cref="WriteLog(string, object[])"/>.
        /// </summary>
        private readonly string _logPrefix;

        /// <summary>
        /// Creates a new <see cref="SymbolReaderHandlerBase"/> with the given parameters.
        /// </summary>
        /// <param name="log">A logger. May be null.</param>
        /// <param name="logPrefix">A prefix for log messages. May be null.</param>
        protected SymbolReaderHandlerBase(TextWriter log, string logPrefix)
        {
            _log = log ?? TextWriter.Null;
            _logPrefix = logPrefix;
        }

        /// <summary>
        /// Implemented in derived classes to handle the requests.
        /// Implementations should process the given request, modifying it if necessary
        /// before calling the next handler.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler in the chain.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when desired modifications have been made.</returns>
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

        /// <summary>
        /// Write a line to the log file and enclose it in square brackets
        /// so that the status bar logger will pick it up.
        /// </summary>
        /// <param name="format">A composite format string with optional placeholders.</param>
        /// <param name="args">An object array that contains zero or more objects to format and write.</param>
        protected void WriteStatusLog(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(_logPrefix))
            {
                _log.Write(_logPrefix);
            }

            _log.WriteLine('[' + string.Format(format, args) + ']');
        }
    }

    /// <summary>
    /// Base class for request handlers that add authorization headers to
    /// requests.
    /// </summary>
    internal abstract class SymbolReaderAuthHandlerBase : SymbolReaderHandlerBase
    {
        /// <summary>
        /// The scheme name for Bearer tokens.
        /// </summary>
        protected const string BearerScheme = "Bearer";

        /// <summary>
        /// The scheme for Basic tokens.
        /// </summary>
        private const string BasicScheme = "Basic";

        /// <summary>
        /// A cache of access tokens per authority. It is shared by all
        /// <see cref="SymbolReaderAuthHandlerBase"/> implementations.
        /// </summary>
        private static readonly ConcurrentDictionary<Uri, AuthToken> s_tokenCache = new ConcurrentDictionary<Uri, AuthToken>();

        /// <summary>
        /// Constructor to be called from derived classes.
        /// </summary>
        /// <param name="log">A logger. May be null.</param>
        /// <param name="logPrefix">A prefix for log messages. May be null.</param>
        protected SymbolReaderAuthHandlerBase(TextWriter log, string logPrefix) : base(log, logPrefix)
        {
        }

        /// <summary>
        /// Handles the request and calls the next handler in the chain, if necessary.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public override Task ProcessRequestAsync(RequestContext context, SymbolReaderHandlerDelegate next, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = context.Request;
            if (request.RequestUri.Scheme == Uri.UriSchemeHttps &&  // Require a secure connection
                request.Headers.Authorization is null &&            // No existing auth header
                TryGetAuthority(request.RequestUri, out Uri authority) // Is a recognized authority
                )
            {
                if (!TryGetCachedToken(authority, logExpiration: true, out AuthToken cachedToken))
                {
                    return GetAuthTokenCoreAsync(context, next, authority, cancellationToken);
                }

                AddAuthorizationHeader(request, cachedToken);
            }

            return next();
        }

        /// <summary>
        /// Call the derived class to get an access token for the given authority.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler.</param>
        /// <param name="authority">The authority to use to acquire a new token.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        private async Task GetAuthTokenCoreAsync(RequestContext context, SymbolReaderHandlerDelegate next, Uri authority, CancellationToken cancellationToken)
        {
            AuthToken? token = await GetAuthTokenAsync(context, next, authority, cancellationToken).ConfigureAwait(false);
            if (!token.HasValue)
            {
                // Call the next handler only if the derived class didn't.
                if (context.Response is null)
                {
                    await next().ConfigureAwait(false);
                }

                return;
            }

            TryAddCachedToken(authority, token.Value);
            AddAuthorizationHeader(context.Request, token.Value);

            await next().ConfigureAwait(false);

            if (context.Response?.StatusCode == HttpStatusCode.Unauthorized)
            {
                s_tokenCache.TryRemove(authority, out _);
                await RejectTokenAsync(context, authority, token.Value, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await AcceptTokenAsync(context, authority, token.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// May be overridden in derived classes to handle the case when 
        /// authorization with a token succeeded.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="authority">The authority.</param>
        /// <param name="token">The bad token.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when token acceptance has finished.</returns>
        protected virtual Task AcceptTokenAsync(RequestContext context, Uri authority, AuthToken token, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// May be overridden in derived classes to handle the case when 
        /// authorization failed.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="authority">The authority.</param>
        /// <param name="token">The bad token.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when token rejection has finished.</returns>
        protected virtual Task RejectTokenAsync(RequestContext context, Uri authority, AuthToken token, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Implemented in derived classes to try and determine the authority from the request URI.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authority, if known.</param>
        /// <returns>True if <paramref name="requestUri"/> is one you recognize and know the authority.</returns>
        protected abstract bool TryGetAuthority(Uri requestUri, out Uri authority);

        /// <summary>
        /// Overridden in derived classes to get an access token for the given authority.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler. Derived implementations do not need to
        /// call this unless they need to handle the unauthenticated response. The base
        /// class will check if <see cref="RequestContext.Response"/> is already set and
        /// stop further processing.</param>
        /// <param name="authority">The authority.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An access token, if available, or null otherwise.</returns>
        protected virtual Task<AuthToken?> GetAuthTokenAsync(RequestContext context, SymbolReaderHandlerDelegate next, Uri authority, CancellationToken cancellationToken) => Task.FromResult<AuthToken?>(null);

        /// <summary>
        /// Add an authorization header to the request using the Bearer scheme.
        /// </summary>
        /// <param name="request">The request to modify.</param>
        /// <param name="authToken">The authorization token to add.</param>
        protected static void AddAuthorizationHeader(HttpRequestMessage request, AuthToken authToken)
        {
            string scheme = GetAuthenticationHeaderScheme(authToken);
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, authToken.Token);
        }

        /// <summary>
        /// Checks if the access token is still valid vis-à-vis its lifetime.
        /// </summary>
        /// <param name="token">The access token.</param>
        /// <returns><c>true</c> if the access token has not expired and is not
        /// going to expire soon.</returns>
        protected static bool IsAccessTokenStillGood(in AuthToken token)
        {
            if (token.ExpiresOn == default || token.Token == null)
            {
                return false;
            }

            TimeSpan timeLeft = token.ExpiresOn - DateTime.UtcNow;

            // Try to refresh at least 5 minutes before expiration.
            return timeLeft > TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Derived classes may call this if they need to cache additional access tokens
        /// for authorities other than the one returned by <see cref="TryGetAuthority(Uri, out Uri)"/> 
        /// (which are already cached by <see cref="SymbolReaderAuthHandlerBase"/>).
        /// </summary>
        /// <param name="authority">The authority.</param>
        /// <param name="token">The token.</param>
        /// <returns>True if the token was successfully cached.</returns>
        protected bool TryAddCachedToken(Uri authority, in AuthToken token) => s_tokenCache.TryAdd(authority, token);

        /// <summary>
        /// Retrieve a token from the cache. If a cached token has expired, or is close to
        /// the end of its lifetime, then it will be removed from the cache and this
        /// will return false.
        /// </summary>
        /// <param name="authority">The authority.</param>
        /// <param name="token">The token.</param>
        /// <returns>True if a good access token was found in the cache.</returns>
        protected bool TryGetCachedToken(Uri authority, out AuthToken token) => TryGetCachedToken(authority, logExpiration: false, out token);

        /// <summary>
        /// Retrieve a token from the cache. If a cached token has expired, or is close to
        /// the end of its lifetime, then it will be removed from the cache and this
        /// will return false.
        /// </summary>
        /// <param name="authority">The authority.</param>
        /// <param name="logExpiration">Whether to write a line to the log when an expired token is removed from the cache.</param>
        /// <param name="token">The token.</param>
        /// <returns>True if a good access token was found in the cache.</returns>
        private bool TryGetCachedToken(Uri authority, bool logExpiration, out AuthToken token)
        {
            if (s_tokenCache.TryGetValue(authority, out token))
            {
                if (IsAccessTokenStillGood(token))
                {
                    return true;
                }

                if (logExpiration)
                {
                    WriteLog("The authorization token for {0} has expired (or is close to expiration).");
                }

                s_tokenCache.TryRemove(authority, out _);
            }

            token = default;
            return false;
        }

        /// <summary>
        /// Examines the <see cref="AuthToken.Scheme"/> value of an
        /// <see cref="AuthToken"/> and converts it to a string suitable for
        /// an HTTP Authorization header.
        /// </summary>
        /// <param name="authToken">The authorization token.</param>
        /// <returns>A string version of the scheme.</returns>
        /// <exception cref="ArgumentException">The scheme is unrecognized.</exception>
        private static string GetAuthenticationHeaderScheme(AuthToken authToken)
        {
            switch (authToken.Scheme)
            {
                case AuthScheme.Bearer:
                    return BearerScheme;

                case AuthScheme.Basic:
                    return BasicScheme;

                default:
                    throw new ArgumentException($"'{nameof(authToken)}' specified an unrecognized {nameof(AuthToken.Scheme)}", nameof(authToken));
            }
        }
    }

    /// <summary>
    /// A handler that uses Git Credential Manager (GCM) to authenticate source look-ups.
    /// </summary>
    internal sealed class GitCredentialManagerHandler : SymbolReaderAuthHandlerBase
    {
        /// <summary>
        /// Prefix to put in front of logging messages.
        /// </summary>
        private const string LogPrefix = "GCMAuth: ";

        /// <summary>
        /// The file name of the Git Credential Manager executable.
        /// </summary>
        private const string GitCredentialManagerExecutable = "git-credential-manager-core.exe";

        /// <summary>
        /// Set of paths relative to the root of a Git installation where
        /// extensions (such as GCM) are installed.
        /// </summary>
        private static readonly string[] GitCoreFoldersRelativeToGitRootFolder = new[]
        {
            @"mingw64\bin",                 // Current Git For Windows (2.37.1) installs it here
            @"mingw64\libexec\git-core",    // Older Git For Windows location.
        };

        /// <summary>
        /// A lazy task that discovers the location of the Git Credential Manager.
        /// </summary>
        private static readonly Lazy<Task<string>> s_gcmExecutableLocation = new Lazy<Task<string>>(LocateGcmExecutableAsync);

        /// <summary>
        /// A window handle to be used as the parent of modal dialogs.
        /// </summary>
        private readonly IntPtr _modalDialogParent;

        /// <summary>
        /// Creates a new <see cref="GitCredentialManagerHandler"/> instance with the given
        /// parameters.
        /// </summary>
        /// <param name="log">The logger to use.</param>
        /// <param name="modalDialogParent">The HWND of a window to be used as the parent for modal dialogs.</param>
        public GitCredentialManagerHandler(TextWriter log, IntPtr modalDialogParent) : base(log, LogPrefix)
        {
            _modalDialogParent = modalDialogParent;
        }

        /// <summary>
        /// Gets a value indicating whether the Git Credential Manager is installed.
        /// </summary>
        public static bool IsGitCredentialManagerInstalled
        {
            get
            {
                string location = s_gcmExecutableLocation.Value.GetAwaiter().GetResult();
                return !string.IsNullOrEmpty(location);
            }
        }

        /// <summary>
        /// Try to determine the authority from the request URI.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authority, if found.</param>
        /// <returns>True if the <paramref name="requestUri"/> is for a host we
        /// recognize  and the authority can be determined.</returns>
        protected override bool TryGetAuthority(Uri requestUri, out Uri authority)
        {
            // TODO: Add GitLab, BitBucket and others that GCM supports.
            return GitHub.TryGetAuthority(requestUri, out authority)
                || AzureDevOps.TryGetAuthority(requestUri, out authority);
        }

        /// <summary>
        /// Try to get a token for the given authority.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler.</param>
        /// <param name="authority">The authority.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An access token, or null if one could not be obtained.</returns>
        protected override async Task<AuthToken?> GetAuthTokenAsync(RequestContext context, SymbolReaderHandlerDelegate next, Uri authority, CancellationToken cancellationToken)
        {
            if (authority == GitHub.Authority)
            {
                // Because users can view public repositories in GitHub
                // even without a GitHub account, we don't want to prompt
                // unnecessarily. Sadly, GitHub doesn't give any indication
                // when a request fails due to lack of authorization. It
                // just returns 404, making it indistinguishable from other
                // "not found" results.
                // To deal with this, we'll try an unauthenticated call
                // first and prompt for authorization only if we get back a
                // 404. If we're able to get an access token, then we'll
                // retry the original request.
                await next().ConfigureAwait(false);
                if (context.Response.StatusCode != HttpStatusCode.NotFound)
                {
                    // No need to call GCM because the unauthenticated call
                    // worked -- or it failed in an unexpected way.
                    return null;
                }
            }

            AuthToken? token = await GetGcmAccessTokenForHostAsync(authority, cancellationToken).ConfigureAwait(false);
            if (token.HasValue)
            {
                // We got an auth token. Clear out the response, so
                // that the caller will retry with the added token.
                context.Response = null;
            }

            return token;
        }

        /// <summary>
        /// Handle the case when a token was used successfully for the first
        /// time. Store it in the GCM cache.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="authority">The authority.</param>
        /// <param name="token">The good token.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        protected override async Task AcceptTokenAsync(RequestContext context, Uri authority, AuthToken token, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(token.UserName))
            {
                WriteStatusLog("Something went wrong. We should have an auth token with a UserName at this point.");
            }
            else
            {
                WriteLog("Storing successful token.");
                GcmCommand storeCommand = GcmCommand.Store(authority, token.UserName, token.Password ?? token.Token);
                _ = await InvokeGcmAsync(storeCommand, cancellationToken).ConfigureAwait(false);
            }

            await base.AcceptTokenAsync(context, authority, token, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle the case when a GCM token didn't work the first time
        /// we used it. Erase it from the GCM store.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="authority">The authority.</param>
        /// <param name="token">The bad token.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        protected override async Task RejectTokenAsync(RequestContext context, Uri authority, AuthToken token, CancellationToken cancellationToken)
        {
            WriteLog("The token provided by GCM didn't work. Removing it from GCM.");
            GcmCommand eraseCommand = GcmCommand.Erase(authority);
            _ = await InvokeGcmAsync(eraseCommand, cancellationToken).ConfigureAwait(false);
            await base.RejectTokenAsync(context, authority, token, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get an access token from Git Credential Manager for the given host
        /// </summary>
        /// <param name="authority">The authority.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token as a string, or null if one could not be obtained.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<AuthToken?> GetGcmAccessTokenForHostAsync(Uri authority, CancellationToken cancellationToken)
        {
            WriteLog("Trying to get an access token for {0}", authority);
            GcmCommand getCommand = GcmCommand.Get(authority);
            string stdOut = await InvokeGcmAsync(getCommand, cancellationToken).ConfigureAwait(false);
            return CreateAuthTokenFromGcmOutput(stdOut);
        }

        /// <summary>
        /// Run the Git Credential Manager with the given command and input.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The contents of stdout.</returns>
        private async Task<string> InvokeGcmAsync(GcmCommand command, CancellationToken cancellationToken)
        {
            string gcmExecutableLocation = await s_gcmExecutableLocation.Value.ConfigureAwait(false);
            if (string.IsNullOrEmpty(gcmExecutableLocation))
            {
                WriteStatusLog("Couldn't find the GCM executable.");
                return null;
            }

            using (Process gcmProcess = new Process())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = gcmExecutableLocation,
                    Arguments = command.Argument,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                startInfo.EnvironmentVariables.Add("GCM_INTERACTIVE", "auto");
                startInfo.EnvironmentVariables.Add("GCM_MODAL_PARENTHWND", _modalDialogParent.ToString());
                startInfo.EnvironmentVariables.Add("GCM_AZREPOS_CREDENTIALTYPE", "oauth");

                gcmProcess.StartInfo = startInfo;
                gcmProcess.EnableRaisingEvents = true;
                TaskCompletionSource<object> exitedTcs = new TaskCompletionSource<object>(TaskContinuationOptions.ExecuteSynchronously);
                gcmProcess.Exited += (sender, e) => exitedTcs.TrySetResult(null);

                cancellationToken.ThrowIfCancellationRequested();

                gcmProcess.Start();
                void OnCancelled()
                {
                    gcmProcess.Kill();
                    exitedTcs.TrySetCanceled();
                }

                using (cancellationToken.Register(OnCancelled))
                {
                    gcmProcess.StandardInput.Write(command.GetInputString());
                    await exitedTcs.Task.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!gcmProcess.HasExited)
                    {
                        WriteStatusLog("Git Credential Manager timed out.");
                        gcmProcess.Kill();
                        return null;
                    }

                    if (gcmProcess.ExitCode != 0)
                    {
                        string stdError = "";
                        try
                        {
                            stdError = await gcmProcess.StandardError.ReadToEndAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        WriteStatusLog("Git Credential Manager exited with error code {0}.", gcmProcess.ExitCode);
                        WriteLog(stdError);
                        return null;
                    }

                    return await gcmProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Create an <see cref="AuthToken"/> from the standard output of a Git
        /// Credential Manager process.
        /// </summary>
        /// <param name="output">The standard output from GCM.</param>
        /// <returns>A new <see cref="AuthToken"/> or null if we couldn't parse
        /// the output.</returns>
        private AuthToken? CreateAuthTokenFromGcmOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                WriteStatusLog("The Git Credential Manager output was empty.");
                return null;
            }

            // Example GCM output:
            // protocol=https
            // host=github.com
            // path=
            // username=<username>
            // password=<password>
            const string UserNameKeyName = "username=";
            const string PasswordKeyName = "password=";
            string username = null, password = null;
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.StartsWith(UserNameKeyName, StringComparison.Ordinal))
                    {
                        username = line.Substring(UserNameKeyName.Length);
                    }
                    else if (line.StartsWith(PasswordKeyName, StringComparison.Ordinal))
                    {
                        password = line.Substring(PasswordKeyName.Length);
                    }
                }
            }

            if (username != null && password != null)
            {
                // Try OAuth first and, if that fails, assume Basic auth.
                return AuthToken.TryParseFromOAuthToken(username, password, out AuthToken token)
                    ? token
                    : AuthToken.CreateBasicFromUsernameAndPassword(username, password);
            }

            WriteStatusLog("Couldn't find a username and password in the output of Git Credential Manager.");
            return null;
        }

        /// <summary>
        /// Try to find the full path to the Git Credential Manager executable.
        /// </summary>
        /// <returns>The path, or null if one could not be found.</returns>
        private static Task<string> LocateGcmExecutableAsync()
        {
            return Task.Run(() =>
            {
                foreach (string path in EnumeratePossibleGcmExecutableLocations())
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// Try to locate the GCM executable in various places on disk.
        /// </summary>
        /// <returns>An enumeration of places to search. The values may not necessarily exist.</returns>
        private static IEnumerable<string> EnumeratePossibleGcmExecutableLocations()
        {
            // Do not allow GCM_CORE_PATH override if the app is running elevated as
            // this would potentially allow untrusted code to run as an administrator.
            if (!App.IsElevated)
            {
                // The GCM_CORE_PATH environment variable.
                yield return Environment.GetEnvironmentVariable("GCM_CORE_PATH");
            }

            // Well-known installation path.
            string gitRootFolder = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "Git");
            if (Directory.Exists(gitRootFolder))
            {
                foreach (var subFolder in GitCoreFoldersRelativeToGitRootFolder)
                {
                    yield return Path.Combine(gitRootFolder, subFolder, GitCredentialManagerExecutable);
                }
            }

            // Find git.exe on the path.
            string gitExePath = GetFileOnEnvironmentPath("git.exe");
            if (!string.IsNullOrEmpty(gitExePath))
            {
                // git.exe is in {gitRootFolder}\Cmd\git.exe
                gitRootFolder = Path.GetDirectoryName(Path.GetDirectoryName(gitExePath));
                foreach (var subFolder in GitCoreFoldersRelativeToGitRootFolder)
                {
                    yield return Path.Combine(gitRootFolder, subFolder, GitCredentialManagerExecutable);
                }
            }
        }

        /// <summary>
        /// Try to find the given filename in one of the folders specified
        /// in the PATH environment variable.
        /// </summary>
        /// <param name="fileName">The file name to find.</param>
        /// <returns>The full path to the first instance of <paramref name="fileName"/>
        /// on the PATH, or null if it couldn't be found.</returns>
        private static string GetFileOnEnvironmentPath(string fileName)
        {
            foreach (string folder in Environment.GetEnvironmentVariable("PATH").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string path = Path.Combine(folder, fileName);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// A command passed to Git Credential Manager.
        /// </summary>
        private class GcmCommand
        {
            /// <summary>
            /// A char array with a single slash used for trimming URI paths.
            /// </summary>
            private static readonly char[] s_singleSlashCharArray = new char[] { '/' };

            /// <summary>
            /// Create a new <see cref="GcmCommand"/> instance.
            /// </summary>
            /// <param name="argument">The command-line argument (get, store, erase).</param>
            /// <param name="authority">The authority part of the input string.</param>
            /// <exception cref="ArgumentException"><paramref name="argument"/> is null or empty.</exception>
            /// <exception cref="ArgumentNullException"><paramref name="authority"/> is null.</exception>
            private GcmCommand(string argument, Uri authority)
            {
                if (string.IsNullOrEmpty(argument))
                {
                    throw new ArgumentException($"'{nameof(argument)}' cannot be null or empty.", nameof(argument));
                }

                if (authority is null)
                {
                    throw new ArgumentNullException(nameof(authority));
                }

                Argument = argument;
                Protocol = authority.Scheme;
                Host = authority.DnsSafeHost;
                Path = authority.AbsolutePath.Trim(s_singleSlashCharArray);
            }

            /// <summary>
            /// The argument (get, store, erase)
            /// </summary>
            public string Argument { get; }

            /// <summary>
            /// The protocol (usually https)
            /// </summary>
            public string Protocol { get; }

            /// <summary>
            /// The host name.
            /// </summary>
            public string Host { get; }
            
            /// <summary>
            /// The path. May be null or empty.
            /// </summary>
            public string Path { get; }

            /// <summary>
            /// The user name. Neede only for stores.
            /// </summary>
            public string UserName { get; private set; }

            /// <summary>
            /// The password. Needed only for stores.
            /// </summary>
            public string Password { get; private set; }

            /// <summary>
            /// Compose an input string for Git Credential Manager from the command parameters.
            /// </summary>
            /// <returns>The composed string.</returns>
            public string GetInputString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("protocol=").AppendLine(Protocol);
                sb.Append("host=").AppendLine(Host);
                if (!string.IsNullOrEmpty(Path))
                {
                    sb.Append("path=").AppendLine(Path);
                }

                if (!string.IsNullOrEmpty(UserName))
                {
                    sb.Append("username=").AppendLine(UserName);
                }

                if (!string.IsNullOrEmpty(Password))
                {
                    sb.Append("password=").AppendLine(Password);
                }

                // Terminate with a blank line.
                return sb.AppendLine().ToString();
            }

            /// <summary>
            /// Create a Git Credential Manager "get" command.
            /// </summary>
            /// <param name="authority">The authority.</param>
            /// <returns>A new <see cref="GcmCommand"/> instance.</returns>
            public static GcmCommand Get(Uri authority) => new GcmCommand("get", authority);

            /// <summary>
            /// Create a Git Credential Manager "store" command.
            /// </summary>
            /// <param name="authority">The authority.</param>
            /// <param name="userName">The user name.</param>
            /// <param name="password">The password.</param>
            /// <returns>A new <see cref="GcmCommand"/> instance.</returns>
            /// <exception cref="ArgumentException">One of the string parameters is null or empty.</exception>
            public static GcmCommand Store(Uri authority, string userName, string password)
            {
                if (string.IsNullOrEmpty(userName))
                {
                    throw new ArgumentException($"'{nameof(userName)}' cannot be null or empty.", nameof(userName));
                }

                if (string.IsNullOrEmpty(password))
                {
                    throw new ArgumentException($"'{nameof(password)}' cannot be null or empty.", nameof(password));
                }

                return new GcmCommand("store", authority)
                {
                    UserName = userName,
                    Password = password
                };
            }

            /// <summary>
            /// Create a Git Credential Manager "erase" command.
            /// </summary>
            /// <param name="authority">The authority.</param>
            /// <returns>A new <see cref="GcmCommand"/> instance.</returns>
            public static GcmCommand Erase(Uri authority) => new GcmCommand("erase", authority);
        }
    }

    /// <summary>
    /// A handler that adds authorization for Azure DevOps instances.
    /// It works for both symbol server (artifacts) and SourceLink.
    /// </summary>
    internal sealed class AzureDevOpsHandler : SymbolReaderAuthHandlerBase
    {
        /// <summary>
        /// The value of <see cref="AzureDevOps.Scope"/> stored in a single element
        /// array suitable for passing to 
        /// <see cref="TokenCredential.GetTokenAsync(TokenRequestContext, CancellationToken)"/>.
        /// </summary>
        private static readonly string[] s_scopes = new[] { AzureDevOps.Scope };

        /// <summary>
        /// Prefix to put in front of logging messages.
        /// </summary>
        private const string LogPrefix = "AzDOAuth: ";

        /// <summary>
        /// A provider of access tokens.
        /// </summary>
        private readonly TokenCredential _tokenCredential;

        /// <summary>
        /// Protect <see cref="_tokenCredential"/> against concurrent access.
        /// </summary>
        private readonly SemaphoreSlim _tokenCredentialGate = new SemaphoreSlim(initialCount: 1);

        /// <summary>
        /// An HTTP client used to discover the authority (login endpoint and tenant) for an Azure Dev Ops instance.
        /// </summary>
        private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });

        /// <summary>
        /// Construct a new <see cref="AzureDevOpsHandler"/> instance.
        /// </summary>
        /// <param name="tokenCredential">A provider of access tokens.</param>
        public AzureDevOpsHandler(TextWriter log, TokenCredential tokenCredential) : base(log, LogPrefix)
        {
            _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));

            // I can't in the docs what this does, but all the Azure DevOps samples do this.
            _httpClient.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
        }

        /// <summary>
        /// Try to find the authority endpoint for an Azure DevOps instance
        /// given a full URI.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authortiy, if found.</param>
        /// <returns>True if <paramref name="requestUri"/> represents a path to a
        /// resource in an Azure DevOps instance.</returns>
        protected override bool TryGetAuthority(Uri requestUri, out Uri authority) => AzureDevOps.TryGetAuthority(requestUri, out authority);

        /// <summary>
        /// Get a token to access the given Azure DevOps instance.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler.</param>
        /// <param name="authority">The Azure DevOps instance.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An access token, or null if one could not be obtained.</returns>
        protected override async Task<AuthToken?> GetAuthTokenAsync(RequestContext context, SymbolReaderHandlerDelegate next, Uri authority, CancellationToken cancellationToken)
        {
            // Generate an authentication challenge from the Azure Dev Ops authority.
            using (HttpResponseMessage challenge = await GetChallengeAsync(authority, cancellationToken).ConfigureAwait(false))
            {
                if (!TryGetBearerAuthority(challenge.Headers.WwwAuthenticate, out Uri authorizationUri))
                {
                    WriteStatusLog("We expected a sign-in challenge from Azure DevOps, but we didn't get one. The response status code was {0}.", challenge.StatusCode);
                    return null;
                }

                // Check our authority cache again. This helps in the case where you have
                // several Azure DevOps instances (different organizations) in the same
                // tenant. They can share the same access token.
                // It also handles cases like "artifacts.dev.azure.com/org" and "dev.azure.com/org"
                // or "{org}.artifacts.visualstudio.com" and "{org}.visualstudio.com"
                if (TryGetCachedToken(authorizationUri, out AuthToken token))
                {
                    return token;
                }

                // Get a new access token from the credential provider.
                WriteLog("Asking for authorization to access {0}", authority);
                token = await GetTokenAsync(authorizationUri, cancellationToken).ConfigureAwait(false);

                // Store the token in the cache for both the OAuth authority.
                // The caller will cache the new token for the Azure DevOps authority.
                TryAddCachedToken(authorizationUri, token);
                return token;
            }
        }

        /// <summary>
        /// Send a HEAD request to the given authority in order to generate an
        /// authentication challenge.
        /// </summary>
        /// <param name="authority">The Azure DevOps authority (e.g. https://dev.azure.com/yourorg)</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The challenge response.</returns>
        private async Task<HttpResponseMessage> GetChallengeAsync(Uri authority, CancellationToken cancellationToken)
        {
            using (var challengeRequest = new HttpRequestMessage(HttpMethod.Head, authority))
            {
                return await _httpClient.SendAsync(challengeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Get a new access token for Azure DevOps from the <see cref="TokenCredential"/>.
        /// </summary>
        /// <param name="authorizationUri">The authorization URI. Usually https://login.microsoftonline.com/{tenantId}</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token.</returns>
        private async Task<AuthToken> GetTokenAsync(Uri authorizationUri, CancellationToken cancellationToken)
        {
            await _tokenCredentialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Use the token credential provider to acquire a new token.
                TokenRequestContext requestContext = new TokenRequestContext(s_scopes, tenantId: GetTenantIdFromAuthority(authorizationUri));
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

        /// <summary>
        /// Extract the tenant ID part of an authority.
        /// </summary>
        /// <example>
        /// For https://login.windows.net/77777777-8888-4444-9999-222222222222 the tenant ID is 77777777-8888-4444-9999-222222222222
        /// </example>
        /// <param name="authorizationUri">The authorization URI (authority)</param>
        /// <returns>The tenant ID part.</returns>
        private static string GetTenantIdFromAuthority(Uri authorizationUri) => authorizationUri.Segments[1].Trim('/');

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
        private static bool TryGetBearerAuthority(HttpHeaderValueCollection<AuthenticationHeaderValue> wwwAuthenticateHeaders, out Uri authorizationUri)
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
    }

    /// <summary>
    /// A handler that handles GitHub device flow authorization.
    /// </summary>
    internal sealed class GitHubDeviceFlowHandler : SymbolReaderAuthHandlerBase
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
        /// Prefix to put in front of logging messages.
        /// </summary>
        private const string LogPrefix = "GitHubDeviceFlow: ";

        /// <summary>
        /// An HTTP client for making device flow calls
        /// </summary>
        private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });

        /// <summary>
        /// Gate to protect against multiple calls to the device flow.
        /// </summary>
        private readonly SemaphoreSlim _deviceFlowGate = new SemaphoreSlim(initialCount: 1);

        /// <summary>
        /// The main window to use for parenting modal dialogs.
        /// </summary>
        private readonly Window _mainWindow;

        /// <summary>
        /// JSON serialization options. Adds a string-to-enum converter to the default options.
        /// </summary>
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Creates a new <see cref="GitHubDeviceFlowHandler"/> instance.
        /// </summary>
        /// <param name="log">The logger.</param>
        /// <param name="mainWindow">The Window to use for parenting any modal
        /// dialogs needed for authentication.</param>
        public GitHubDeviceFlowHandler(TextWriter log, Window mainWindow) : base(log, LogPrefix)
        {
            _httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
            _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("PerfView");
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// Detect whether this request could be authenticated by the GitHub authority.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authority, if recognized.</param>
        /// <returns>True if the URI is recognized as a GitHub URI.</returns>
        protected override bool TryGetAuthority(Uri requestUri, out Uri authority) => GitHub.TryGetAuthority(requestUri, out authority);

        /// <summary>
        /// Process the request.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="next">The next handler in the chain.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        protected override async Task<AuthToken?> GetAuthTokenAsync(RequestContext context, SymbolReaderHandlerDelegate next, Uri authority, CancellationToken cancellationToken)
        {
            // Because users can view public repositories in GitHub
            // even without a GitHub account, we don't want to prompt
            // unnecessarily. Sadly, GitHub doesn't give any indication
            // when a request fails due to lack of authorization. It
            // just returns 404, making it indistinguishable from other
            // "not found" results.
            // To deal with this, we'll try an unauthenticated call
            // first and prompt for authorization only if we get back a
            // 404. If we're able to get an access token, then we'll
            // retry the original request.
            await next().ConfigureAwait(false);
            if (context.Response.StatusCode != HttpStatusCode.NotFound)
            {
                // The call completed without needing further authorization.
                return null;
            }

            AuthToken? token = await TryGetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (token.HasValue)
            {
                // Clear out the original response and fall through to retry the request.
                context.Response = null;
            }
            else
            {
                WriteStatusLog("Device flow failed.");
            }

            return token;
        }

        /// <summary>
        /// Try to get an access token to call GitHub by using the device flow.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The access token, or null if one could not be obtained.</returns>
        private async Task<AuthToken?> TryGetAccessTokenAsync(CancellationToken cancellationToken)
        {
            // Start the device flow.
            await _deviceFlowGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                WriteLog("Start device flow authorization.");
                DeviceFlowResponse deviceFlowResponse = await PostDeviceFlowRequestAsync(cancellationToken).ConfigureAwait(false);

                // The user now needs to go to open a web browser and follow the device flow
                // to log into GitHub.
                Uri pollingUri = new Uri(GitHub.GetAccessTokenUri, $"?client_id={PerfViewClientId}&device_code={deviceFlowResponse.DeviceCode}&grant_type=urn:ietf:params:oauth:grant-type:device_code");
                using (var sharedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // Automatically cancel after the device code expires.
                    sharedCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(deviceFlowResponse.ExpiresIn));

                    // There are three ways to exit the device flow:
                    // 1. Closing the dialog will cancel the polling task.
                    // 2. The polling task will signal the dialog to close on success.
                    // 3. The original request is cancelled.
                    CancellationToken sharedCancellationToken = sharedCancellationTokenSource.Token;
                    Task<AuthToken?> pollingTask = Task.Run(() => PollForAccessTokenAsync(pollingUri, TimeSpan.FromSeconds(deviceFlowResponse.Interval), sharedCancellationTokenSource, sharedCancellationToken));

                    // Show the dialog and wait until it is closed.
                    WriteStatusLog("Showing device flow dialog so you can log into GitHub.");
                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        var deviceCodeDialog = new GitHubDeviceFlowDialog(_mainWindow, deviceFlowResponse.VerificationUri, deviceFlowResponse.UserCode, sharedCancellationToken);
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
        /// <param name="uri">The access token URI to poll.</param>
        /// <param name="pollingInterval">The minimum polling interval.</param>
        /// <param name="cancelDialog">A <see cref="CancellationTokenSource"/> that should be canceled when the token is obtained.</param>
        /// <param name="cancellationToken">A cancellation token to stop polling.</param>
        /// <returns>The access token.</returns>
        private async Task<AuthToken?> PollForAccessTokenAsync(Uri uri, TimeSpan pollingInterval, CancellationTokenSource cancelDialog, CancellationToken cancellationToken)
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
                            try
                            {
                                AccessTokenResponse accessTokenResponse = await JsonSerializer.DeserializeAsync<AccessTokenResponse>(stream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                                string token = accessTokenResponse.AccessToken;
                                if (!string.IsNullOrEmpty(token))
                                {
                                    cancelDialog.Cancel();
                                    // As far as I can tell, these tokens never expire.
                                    // Give them a really long lifetime.
                                    return new AuthToken(accessTokenResponse.TokenType, token, DateTime.UtcNow + TimeSpan.FromDays(60));
                                }
                            }
                            catch (JsonException ex)
                            {
                                WriteLog("JsonException deserializing device auth response. {0}", ex);
                                return null;
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
            Uri requestUri = new Uri(GitHub.DeviceFlowUri, query);
            using (HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content: null, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<DeviceFlowResponse>(stream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
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
            /// The token type. Usually <see cref="AuthScheme.Bearer"/>.
            /// </summary>
            [JsonPropertyName("token_type")]
            public AuthScheme TokenType { get; set; }

            /// <summary>
            /// The scope granted.
            /// </summary>
            [JsonPropertyName("scope")]
            public string Scope { get; set; }
        }
    }

    /// <summary>
    /// Contains constants, static properties and helper methods pertinent to Azure DevOps.
    /// </summary>
    internal static class AzureDevOps
    {
        /// <summary>
        /// The OAuth scope to use when requesting tokens for Azure DevOps.
        /// </summary>
        public const string Scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

        /// <summary>
        /// The suffix of the host name in URIs such as https://yourorg.visualstudio.com
        /// </summary>
        public const string VisualStudioDotComSuffix = ".visualstudio.com";

        /// <summary>
        /// The suffix of the host name in URIs such as https://yourorg.artifacts.visualstudio.com
        /// </summary>
        public const string ArtifactsVisualStudioDotComSuffix = ".artifacts" + VisualStudioDotComSuffix;

        /// <summary>
        /// The host name for Azure DevOps.
        /// </summary>
        public const string AzureDevOpsHost = "dev.azure.com";

        /// <summary>
        /// The host name for Azure DevOps' artifacts endpoint.
        /// </summary>
        public const string AzureDevOpsArtifactsHost = "artifacts." + AzureDevOpsHost;

        /// <summary>
        /// Determines if the given URI is hosted on Azure DevOps.
        /// </summary>
        /// <param name="uri">The URI to test.</param>
        /// <returns>True if the URI belongs to Azure DevOps.</returns>
        public static bool IsAzureDevOpsHost(Uri uri)
        {
            string host = uri.DnsSafeHost;
            return host.Equals(AzureDevOpsHost, StringComparison.OrdinalIgnoreCase) ||
                   host.Equals(AzureDevOpsArtifactsHost, StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(VisualStudioDotComSuffix, StringComparison.OrdinalIgnoreCase);
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
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authortiy, if found.</param>
        /// <returns>True if <paramref name="requestUri"/> represents a path to a
        /// resource in an Azure DevOps instance.</returns>
        public static bool TryGetAuthority(Uri requestUri, out Uri authority)
        {
            if (!requestUri.IsAbsoluteUri)
            {
                authority = null;
                return false;
            }

            UriBuilder builder = null;
            string host = requestUri.DnsSafeHost;
            if (host.Equals(AzureDevOpsHost, StringComparison.OrdinalIgnoreCase) ||
                host.Equals(AzureDevOpsArtifactsHost, StringComparison.OrdinalIgnoreCase))
            {
                // For dev.azure.com, the organization is the first part of the path.
                var segments = requestUri.Segments;
                if (segments.Length >= 2)
                {
                    builder = new UriBuilder
                    {
                        Host = AzureDevOpsHost,
                        Path = segments[1]
                    };
                }
            }

            if (host.EndsWith(VisualStudioDotComSuffix, StringComparison.OrdinalIgnoreCase))
            {
                string org;

                // For *.visualstudio.com, the organization is included in the host name.
                // e.g. yourorg.visualstudio.com or yourorg.artifacts.visualstudio.com
                if (host.EndsWith(ArtifactsVisualStudioDotComSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    // For *.artifacts.visualstudio.com, we want to drop the artifacts bit.
                    org = host.Substring(0, host.Length - ArtifactsVisualStudioDotComSuffix.Length);
                }
                else
                {
                    org = host.Substring(0, host.Length - VisualStudioDotComSuffix.Length);
                }

                builder = new UriBuilder
                {
                    Host = org + VisualStudioDotComSuffix
                };
            }

            if (builder is null)
            {
                // Not an Azure DevOps URI.
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
    /// Contains constants, static properties and helper methods pertinent to GitHub.
    /// </summary>
    internal static class GitHub
    {
        /// <summary>
        /// GitHub's host name
        /// </summary>
        public const string GitHubComHostName = "github.com";

        /// <summary>
        /// The authority for GitHUb
        /// </summary>
        public static Uri Authority { get; } = new Uri("https://" + GitHubComHostName);

        /// <summary>
        /// GitHub's host for downloading raw source files.
        /// </summary>
        public const string RawGitHubUserContentHostName = "raw.githubusercontent.com";

        /// <summary>
        /// GitHub URI to start the device flow. Requests a unique
        /// device code and user code.
        /// </summary>
        public static Uri DeviceFlowUri { get; } = new Uri("https://github.com/login/device/code");

        /// <summary>
        /// GitHub URI to finish the device flow. We poll this endpoint
        /// while the user completes the device flow (by signing into
        /// GitHub in the Browser).
        /// </summary>
        public static Uri GetAccessTokenUri { get; } = new Uri("https://github.com/login/oauth/access_token");

        /// <summary>
        /// Determines if the given URI represents a GitHub endpoint.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>True if <paramref name="uri"/> belongs to GitHub.</returns>
        public static bool IsGitHubHost(Uri uri)
            => uri.DnsSafeHost.Equals(GitHubComHostName, StringComparison.OrdinalIgnoreCase)
            || uri.DnsSafeHost.Equals(RawGitHubUserContentHostName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Detect whether this request could be authenticated by the GitHub authority.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="authority">The authority, if recognized.</param>
        /// <returns>True if the URI is recognized as a GitHub URI.</returns>
        public static bool TryGetAuthority(Uri requestUri, out Uri authority)
        {
            if (requestUri.IsAbsoluteUri && IsGitHubHost(requestUri))
            {
                var builder = new UriBuilder
                {
                    Scheme = requestUri.Scheme,
                    Host = GitHubComHostName,
                };

                if (!requestUri.IsDefaultPort)
                {
                    builder.Port = requestUri.Port;
                }

                authority = builder.Uri;
                return true;
            }

            authority = null;
            return false;
        }
    }
}
