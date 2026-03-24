using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
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
                if (!_disposed)
                {
                    Browser?.Dispose();
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Shared WebView2 environment task. Reusing a single environment for all WebBrowserWindow
        /// instances prevents the COMException (0x800700AA) "The requested resource is in use" that
        /// occurs when multiple windows concurrently try to create separate environments backed by the
        /// same user-data folder.
        /// </summary>
        private static Task<CoreWebView2Environment> s_environmentTask;
        private static readonly object s_environmentLock = new object();

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

            // All WebBrowserWindow instances share one environment task so that
            // CoreWebView2Environment.CreateAsync is only called once.  Concurrent calls with
            // the same user-data folder can throw COMException 0x800700AA.
            lock (s_environmentLock)
            {
                if (s_environmentTask == null)
                {
                    s_environmentTask = CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                }
            }

            var environmentAwaiter = s_environmentTask
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

                // Set the preferred color scheme directly on the profile
                Browser.CoreWebView2.Profile.PreferredColorScheme = GuiApp.MainWindow.ThemeViewModel.IsLightTheme
                    ? CoreWebView2PreferredColorScheme.Light
                    : CoreWebView2PreferredColorScheme.Dark;

                // Navigate to the current specified source
                Navigate();
            });
        }

        #endregion
    }
}
