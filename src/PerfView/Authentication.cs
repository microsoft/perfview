using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Azure.Identity;
using Microsoft.Diagnostics.Symbols.Authentication;
using Utilities;

namespace PerfView
{
    /// <summary>
    /// Registered commands for authentication options.
    /// </summary>
    public static class AuthenticationCommands
    {
        /// <summary>
        /// A routed command for enabling/disabling Git Credential Manager authentication.
        /// </summary>
        public static readonly RoutedUICommand UseGitCredentialManager = new RoutedUICommand("Use _Git Credential Manager", nameof(UseGitCredentialManager), typeof(AuthenticationCommands));

        /// <summary>
        /// A routed command for enabling/disabling developer identity authentication.
        /// </summary>
        public static readonly RoutedUICommand UseDeveloperIdentity = new RoutedUICommand("Use _Developer identity for Azure DevOps", nameof(UseDeveloperIdentity), typeof(AuthenticationCommands));

        /// <summary>
        /// A routed command for enabling/disabling GitHub Device Flow authentication.
        /// </summary>
        public static readonly RoutedUICommand UseGitHubDeviceFlow = new RoutedUICommand("Use Device _Code Flow for GitHub", nameof(UseGitHubDeviceFlow), typeof(AuthenticationCommands));

        /// <summary>
        /// A routed command for enabling/disabling Basic Http authentication.
        /// </summary>
        public static readonly RoutedUICommand UseBasicHttpAuth = new RoutedUICommand("Use Basic Http Auth", nameof(UseBasicHttpAuth), typeof(AuthenticationCommands));
    }

    /// <summary>
    /// A view model for authentication options.
    /// </summary>
    public class AuthenticationViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// The key to use to store/update user config data.
        /// </summary>
        private const string UserConfigDataKey = "AuthenticationProviders";

        /// <summary>
        /// The configuration data. Updates to the UI are written back to the user configuration.
        /// </summary>
        private readonly ConfigData userConfigData;

        /// <summary>
        /// Enumeration of known authentication providers.
        /// </summary>
        [Flags]
        private enum AuthProviderFlags
        {
            None = 0,
            GitCredentialManager = 1,
            DeveloperIdentity = 2,
            GitHubDeviceFlow = 4,
            BasicHttpAuth = 8,
        }

        /// <summary>
        /// The currently enabled authentication providers.
        /// </summary>
        private AuthProviderFlags _state;

        /// <summary>
        /// Event raised when a property has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The default set of providers to use if the user hasn't yet chosen.
        /// </summary>
        /// <returns>The default set of providers.</returns>
        private static AuthProviderFlags GetDefault()
        {
            // Prefer to use GCM, if you have it installed, since it can handle
            // nearly everything, and we can add more support over time.
            return GitCredentialManagerHandler.IsGitCredentialManagerInstalled
                ? AuthProviderFlags.GitCredentialManager | AuthProviderFlags.BasicHttpAuth
                : AuthProviderFlags.DeveloperIdentity | AuthProviderFlags.GitHubDeviceFlow | AuthProviderFlags.BasicHttpAuth;
        }

        /// <summary>
        /// Creates a new <see cref="AuthenticationViewModel"/> instance.
        /// </summary>
        /// <param name="userConfigData">The user configuration data.</param>
        public AuthenticationViewModel(ConfigData userConfigData)
        {
            this.userConfigData = userConfigData;

            double savedState = userConfigData.GetDouble(UserConfigDataKey, double.NaN);
            if (double.IsNaN(savedState))
            {
                _state = GetDefault();
            }
            else
            {
                _state = (AuthProviderFlags)savedState;
            }
        }

        /// <summary>
        /// Render the current state as a string.
        /// </summary>
        /// <returns>The current state as a string.</returns>
        public override string ToString() => _state.ToString();

        /// <summary>
        /// Gets or sets whether the <see cref="AuthProviderFlags.GitCredentialManager"/> provider is enabled.
        /// </summary>
        public bool IsGitCredentialManagerEnabled
        {
            get => _state.HasFlag(AuthProviderFlags.GitCredentialManager);
            set => Enable(AuthProviderFlags.GitCredentialManager, value);
        }

        /// <summary>
        /// Gets or sets whether the <see cref="AuthProviderFlags.DeveloperIdentity"/> provider is enabled.
        /// </summary>
        public bool IsDeveloperIdentityEnabled
        {
            get => _state.HasFlag(AuthProviderFlags.DeveloperIdentity);
            set => Enable(AuthProviderFlags.DeveloperIdentity, value);
        }

        /// <summary>
        /// Gets or sets whether the <see cref="AuthProviderFlags.GitHubDeviceFlow"/> provider is enabled.
        /// </summary>
        public bool IsGitHubDeviceFlowEnabled
        {
            get => _state.HasFlag(AuthProviderFlags.GitHubDeviceFlow);
            set => Enable(AuthProviderFlags.GitHubDeviceFlow, value);
        }

        /// <summary>
        /// Gets or sets whether the <see cref="AuthProviderFlags.GitHubDeviceFlow"/> provider is enabled.
        /// </summary>
        public bool IsBasicHttpAuthEnabled
        {
            get => _state.HasFlag(AuthProviderFlags.BasicHttpAuth);
            set => Enable(AuthProviderFlags.BasicHttpAuth, value);
        }

        /// <summary>
        /// Enable or disable the given provider, notify the UI and write back
        /// to user configuration.
        /// </summary>
        /// <param name="provider">The provider to enable/disable.</param>
        /// <param name="enable">True to enable the provider or false to disable it.</param>
        /// <param name="propertyName">Name of the property that changed for notifying the UI.</param>
        private void Enable(AuthProviderFlags provider, bool enable, [CallerMemberName] string propertyName = null)
        {
            if (enable)
            {
                UpdateState(_state | provider, propertyName);
            }
            else
            {
                UpdateState(_state & ~provider, propertyName);
            }
        }

        /// <summary>
        /// Update the state. If it has changed, notify the UI and write the
        /// new state back to user configuration.
        /// </summary>
        /// <param name="newState">The new state.</param>
        /// <param name="propertyName">Name of the property that changed for notifying the UI.</param>
        private void UpdateState(AuthProviderFlags newState, string propertyName)
        {
            if (newState != _state)
            {
                _state = newState;
                userConfigData[UserConfigDataKey] = ((double)newState).ToString();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    /// <summary>
    /// Extension methods for <see cref="SymbolReaderAuthenticationHandler"/>.
    /// </summary>
    internal static class SymbolReaderAuthenticationHandlerExtensions
    {
        /// <summary>
        /// Configure a <see cref="SymbolReaderAuthenticationHandler"/> from authentication options.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="authenticationViewModel">The authentication view model.</param>
        /// <param name="log">A logger.</param>
        /// <param name="mainWindow">The main window to use as the parent of any modal dialogs.</param>
        /// <exception cref="ArgumentNullException">One of the parameters is null.</exception>
        public static void Configure(this SymbolReaderAuthenticationHandler handler, AuthenticationViewModel authenticationViewModel, TextWriter log, Window mainWindow)
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (authenticationViewModel is null)
            {
                throw new ArgumentNullException(nameof(authenticationViewModel));
            }

            if (mainWindow is null)
            {
                throw new ArgumentNullException(nameof(mainWindow));
            }

            handler.ClearHandlers();

            // Always add Symweb authentication.
            handler.AddSymwebAuthentication(log);

            // The order isn't critical, but we chose to put GCM last
            // because the user might want to use GCM for GitHub and
            // Developer identity for Azure DevOps. If GCM were first,
            // it would always pre-empt the developer identity handler.
            if (authenticationViewModel.IsDeveloperIdentityEnabled)
            {
                handler.AddAzureDevOpsAuthentication(log);
            }

            if (authenticationViewModel.IsGitHubDeviceFlowEnabled)
            {
                handler.AddGitHubDeviceCodeAuthentication(log, mainWindow);
            }

            if (authenticationViewModel.IsGitCredentialManagerEnabled)
            {
                handler.AddGitCredentialManagerAuthentication(log, mainWindow);
            }

            if (authenticationViewModel.IsBasicHttpAuthEnabled)
            {
                handler.AddBasicHttpAuthentication(log, mainWindow);
            }
        }

        /// <summary>
        /// Add a handler for Symweb authentication using local credentials.
        /// It will try to use cached credentials from Visual Studio, VS Code,
        /// Azure Powershell and Azure CLI.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="silent">If no local credentials can be found, then a browser window will
        /// be opened to prompt the user. Set this to true to if you don't want that.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public static SymbolReaderAuthenticationHandler AddSymwebAuthentication(this SymbolReaderAuthenticationHandler httpHandler, TextWriter log, bool silent = false)
        {
            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = silent,
                ExcludeManagedIdentityCredential = true // This is not designed to be used in a service.
            };

            return httpHandler.AddHandler(new SymwebHandler(log, new DefaultAzureCredential(options)));
        }

        /// <summary>
        /// Add a handler that uses Git Credential Manager for authentication.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="mainWindow">The main window to use when parenting modal dialogs.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public static SymbolReaderAuthenticationHandler AddGitCredentialManagerAuthentication(this SymbolReaderAuthenticationHandler httpHandler, TextWriter log, Window mainWindow)
            => httpHandler.AddHandler(new GitCredentialManagerHandler(log, GetWindowHandle(mainWindow)));

        /// <summary>
        /// Add a handler for Azure DevOps authentication using local credentials.
        /// It will try to use cached credentials from Visual Studio, VS Code,
        /// Azure Powershell and Azure CLI.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="silent">If no local credentials can be found, then a browser window will
        /// be opened to prompt the user. Set this to true to if you don't want that.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public static SymbolReaderAuthenticationHandler AddAzureDevOpsAuthentication(this SymbolReaderAuthenticationHandler httpHandler, TextWriter log, bool silent = false)
        {
            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = silent,
                ExcludeManagedIdentityCredential = true // This is not designed to be used in a service.
            };

            return httpHandler.AddHandler(new AzureDevOpsHandler(log, new DefaultAzureCredential(options)));
        }

        /// <summary>
        /// Add a handler for GitHub device flow authentication.
        /// </summary>
        /// <param name="log">A logger.</param>
        /// <param name="mainWindow">The Window to use for parenting any modal
        /// dialogs needed for authentication.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public static SymbolReaderAuthenticationHandler AddGitHubDeviceCodeAuthentication(this SymbolReaderAuthenticationHandler httpHandler, TextWriter log, Window mainWindow)
            => httpHandler.AddHandler(new GitHubDeviceFlowHandler(log, mainWindow));

        public static SymbolReaderAuthenticationHandler AddBasicHttpAuthentication(this SymbolReaderAuthenticationHandler httpHandler, TextWriter log, Window mainWindow)
            => httpHandler.AddHandler(new BasicHttpAuthHandler(log));

        /// <summary>
        /// Get the HWND of the given WPF window in a way that honors WPF
        /// threading rules.
        /// </summary>
        /// <param name="window">The WPF window.</param>
        /// <returns>The handle (HWND) of the given window.</returns>
        private static IntPtr GetWindowHandle(Window window)
            => window.Dispatcher.Invoke(() => new WindowInteropHelper(window).Handle);
    }
}
