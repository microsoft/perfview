using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;


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

        public bool CanGoForward { get { return Browser.CanGoForward; } }
        public bool CanGoBack { get { return Browser.CanGoBack; } }
        public WebView2 Browser { get { return _Browser; } }

        /// <summary>
        /// LIke Browser.Navigate, but you don't have to be on the GUI thread to use it.  
        /// </summary>
        public void Navigate(string uri)
        {
            // Make sure we do it on the GUI thread.
            Dispatcher.BeginInvoke((Action)delegate
            {
                WebBrowserWindow.Navigate(Browser, uri);
            });
        }

        /// <summary>
        /// A simple helper wrapper that translates some exceptions nicely.  
        /// </summary>
        public static void Navigate(WebView2 browser, string url)
        {
            browser.Source = new Uri(url);
        }

        #region private
        private void BackClick(object sender, RoutedEventArgs e)
        {
            if (Browser.CanGoBack)
            {
                Browser.GoBack();
            }
        }

        private void ForwardClick(object sender, RoutedEventArgs e)
        {
            if (Browser.CanGoForward)
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
        }
        #endregion 
    }
}
