using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Utilities;


namespace PerfView.GuiUtilities
{
    /// <summary>
    /// Interaction logic for WebBrowserWindow.xaml
    /// </summary>
    public partial class WebBrowserWindow : WindowBase
    {
        public WebBrowserWindow(Window parentWindow) : base(parentWindow)
        {
            InitializeComponent();
        }

        /// <summary>
        /// If set simply hide the window rather than closing it when the user requests closing. 
        /// </summary>
        public bool HideOnClose;

        public bool CanGoForward { get { return _disposed ? false : Browser.CanGoForward; } }
        public bool CanGoBack { get { return _disposed ? false : Browser.CanGoBack; } }
        public WebView2 Browser { get { return _Browser; } }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(Uri),
            typeof(WebBrowser),
            new PropertyMetadata(OnSourceChanged));

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as WebBrowserWindow)?.Navigate();
        }

        /// <summary>
        /// If WebView2 has been initialized, navigate to current source. If WebView2 is not initialized yet, it will 
        /// be navigated to once initialization has completed.
        /// </summary>
        private void Navigate()
        {
            if (!_disposed && Source?.ToString() is { } source)
            {
                Browser?.CoreWebView2.Navigate(source);
            }
        }

        #region private
        private void BackClick(object sender, RoutedEventArgs e)
        {
            if (CanGoBack)
            {
                Browser.GoBack();
            }
        }

        private void ForwardClick(object sender, RoutedEventArgs e)
        {
            if (CanGoForward)
            {
                Browser.GoForward();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Browser_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            ProcessWebMessage(e.WebMessageAsJson);
        }

        internal void ProcessWebMessage(string messageJson)
        {
            if (messageJson == CloseWindowMessageJson)
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (IsLoaded)
                    {
                        Close();
                    }
                }));
            }
        }

        /// <summary>
        /// We hide rather than close the editor.  
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (HideOnClose)
            {
                Hide();
                e.Cancel = true;
            }
            else
            {
                // Dispose WebView2 to prevent finalizer crashes
                if (!_disposed)
                {
                    if (_webMessageHandlerAttached)
                    {
                        Browser.CoreWebView2.WebMessageReceived -= Browser_WebMessageReceived;
                        _webMessageHandlerAttached = false;
                    }

                    Browser?.Dispose();
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Ensure that we configure the WebView2 environment to specify where the user data is stored.
        /// </summary>
        private void Browser_Loaded(object sender, RoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var userDataFolder = Path.Combine(SupportFiles.SupportFileDir, "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var environmentAwaiter = CoreWebView2Environment
                .CreateAsync(userDataFolder: userDataFolder)
                .ConfigureAwait(true)
                .GetAwaiter();

            environmentAwaiter.OnCompleted(async () =>
            {
                if (_disposed)
                {
                    return;
                }

                var environment = environmentAwaiter.GetResult();
                await Browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

                if (!_webMessageHandlerAttached)
                {
                    Browser.CoreWebView2.WebMessageReceived += Browser_WebMessageReceived;
                    await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(CloseWindowOnEscapeScript).ConfigureAwait(true);
                    _webMessageHandlerAttached = true;
                }

                // Set the preferred color scheme directly on the profile
                Browser.CoreWebView2.Profile.PreferredColorScheme = GuiApp.MainWindow.ThemeViewModel.IsLightTheme
                    ? CoreWebView2PreferredColorScheme.Light
                    : CoreWebView2PreferredColorScheme.Dark;

                // Navigate to the current specified source
                Navigate();
            });
        }

        private const string CloseWindowMessage = "PerfView.CloseWindow";
        private const string CloseWindowMessageJson = "\"" + CloseWindowMessage + "\"";
        private const string CloseWindowOnEscapeScript = @"
            window.addEventListener('keydown', function (event) {
                if (event.key === 'Escape' &&
                    !event.altKey &&
                    !event.ctrlKey &&
                    !event.metaKey &&
                    !event.shiftKey) {
                    window.chrome.webview.postMessage('PerfView.CloseWindow');
                }
            }, true);";
        private bool _disposed = false;
        private bool _webMessageHandlerAttached;

        #endregion
    }
}
