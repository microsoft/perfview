using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Utilities;


namespace PerfView.GuiUtilities
{
    /// <summary>
    /// Interaction logic for WebBrowserWindow.xaml
    /// </summary>
    public partial class WebBrowserWindow : WindowBase, IDisposable
    {
        public WebBrowserWindow(Window parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            
            // Register for application shutdown to ensure proper disposal
            Application.Current.Exit += OnApplicationExit;
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
            if (!_disposed && Source != null && _Browser.CoreWebView2 != null)
            {
                _Browser.CoreWebView2.Navigate(Source.ToString());
            }
        }

        #region private
        private bool _disposed = false;
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
                Dispose();
            }
        }

        /// <summary>
        /// Handle application exit to ensure WebView2 is disposed before finalization
        /// </summary>
        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Dispose WebView2 and cleanup resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method following standard dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Unregister from application events
                if (Application.Current != null)
                {
                    Application.Current.Exit -= OnApplicationExit;
                }

                // Dispose WebView2 on UI thread
                if (_Browser != null)
                {
                    try
                    {
                        // Ensure we're on the UI thread for WebView2 disposal
                        if (Dispatcher.CheckAccess())
                        {
                            _Browser.Dispose();
                        }
                        else if (!Dispatcher.HasShutdownStarted)
                        {
                            Dispatcher.Invoke(() => _Browser.Dispose());
                        }
                        // If dispatcher has shut down, we can't safely dispose WebView2
                        // but the process is exiting anyway so this is acceptable
                    }
                    catch (Exception ex)
                    {
                        // Log the exception but don't let it crash the application
                        System.Diagnostics.Debug.WriteLine($"WebView2 disposal error: {ex.Message}");
                    }
                }
            }

            _disposed = true;
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
                await _Browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

                // Navigate to the current specified source
                Navigate();
            });
        }
        #endregion
    }
}
