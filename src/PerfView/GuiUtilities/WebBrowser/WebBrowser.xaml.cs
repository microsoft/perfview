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
            if (!_disposed && Browser.CanGoBack)
            {
                Browser.GoBack();
            }
        }

        private void ForwardClick(object sender, RoutedEventArgs e)
        {
            if (!_disposed && Browser.CanGoForward)
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
                // Properly dispose WebView2 to prevent finalizer crashes
                Dispose();
            }
        }

        /// <summary>
        /// Ensure that we configure the WebView2 environment to specify where the user data is stored.
        /// </summary>
        private void Browser_Loaded(object sender, RoutedEventArgs e)
        {
            var userDataFolder = Path.Combine(SupportFiles.SupportFileDir, "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var environmentAwaiter = CoreWebView2Environment
                .CreateAsync(userDataFolder: userDataFolder)
                .ConfigureAwait(true)
                .GetAwaiter();

            environmentAwaiter.OnCompleted(async () =>
            {
                var environment = environmentAwaiter.GetResult();
                await _Browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

                // Navigate to the current specified source
                Navigate();
            });
        }

        /// <summary>
        /// Dispose of WebView2 resources to prevent finalizer crashes.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _Browser != null)
                {
                    _Browser.Dispose();
                }
                _disposed = true;
            }
        }
        #endregion
    }
}
