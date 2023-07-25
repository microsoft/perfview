using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
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
    /// Extension methods for <see cref="SymbolReaderHttpHandler"/>.
    /// </summary>
    internal static class SymbolReaderHttpHandlerExtensions
    {
        /// <summary>
        /// Configure a <see cref="SymbolReaderHttpHandler"/> from authentication options.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="authenticationViewModel">The authentication view model.</param>
        /// <param name="log">A logger.</param>
        /// <param name="mainWindow">The main window to use as the parent of any modal dialogs.</param>
        /// <exception cref="ArgumentNullException">One of the parameters is null.</exception>
        public static void Configure(this SymbolReaderHttpHandler handler, AuthenticationViewModel authenticationViewModel, TextWriter log, Window mainWindow)
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
    }
}
