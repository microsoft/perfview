using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Azure.Core;
using Azure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net;

namespace Microsoft.Diagnostics.Symbols.Authentication
{
    /// <summary>
    /// A handler for <see cref="HttpClient"/> that allows interception and
    /// modification of requests by a chain of <see cref="IAsyncSymbolReaderHandler"/>
    /// instances with the express purpose of supporting authentication.
    /// </summary>
    public class SymbolReaderAuthenticationHandler : DelegatingHandler
    {
        /// <summary>
        /// A list of handlers.
        /// </summary>
        private readonly List<IAsyncSymbolReaderHandler> _handlers = new List<IAsyncSymbolReaderHandler>();

        /// <summary>
        /// Construct a new <see cref="SymbolReaderAuthenticationHandler"/> instance.
        /// </summary>
        public SymbolReaderAuthenticationHandler() : base(new HttpClientHandler() { CheckCertificateRevocationList = true })
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
        public SymbolReaderAuthenticationHandler AddHandler(IAsyncSymbolReaderHandler handler)
        {
            lock (_handlers)
            {
                _handlers.Add(handler);
            }

            return this;
        }
    }

    /// <summary>
    /// The delegate type that handlers invoke to chain to the next handler.
    /// </summary>
    /// <returns>A task.</returns>
    public delegate Task SymbolReaderHandlerDelegate();

    /// <summary>
    /// Context object passed to <see cref="IAsyncSymbolReaderHandler.ProcessRequestAsync(RequestContext, SymbolReaderHandlerDelegate, CancellationToken)"/>
    /// </summary>
    public sealed class RequestContext
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
    public enum AuthScheme
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
    public readonly struct AuthToken : IEquatable<AuthToken>
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
                authToken = new AuthToken(AuthScheme.Bearer, token, jwt.ValidTo, userName);
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
    public interface IAsyncSymbolReaderHandler
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
    public abstract class SymbolReaderHandler : IAsyncSymbolReaderHandler
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
        /// Creates a new <see cref="SymbolReaderHandler"/> with the given parameters.
        /// </summary>
        /// <param name="log">A logger. May be null.</param>
        /// <param name="logPrefix">A prefix for log messages. May be null.</param>
        protected SymbolReaderHandler(TextWriter log, string logPrefix)
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
    public abstract class SymbolReaderAuthHandler : SymbolReaderHandler
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
        /// <see cref="SymbolReaderAuthHandler"/> implementations.
        /// </summary>
        private static readonly ConcurrentDictionary<Uri, AuthToken> s_tokenCache = new ConcurrentDictionary<Uri, AuthToken>();

        /// <summary>
        /// Constructor to be called from derived classes.
        /// </summary>
        /// <param name="log">A logger. May be null.</param>
        /// <param name="logPrefix">A prefix for log messages. May be null.</param>
        protected SymbolReaderAuthHandler(TextWriter log, string logPrefix) : base(log, logPrefix)
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
        /// (which are already cached by <see cref="SymbolReaderAuthHandler"/>).
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
}
