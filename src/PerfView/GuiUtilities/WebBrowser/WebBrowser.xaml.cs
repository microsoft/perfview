using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;


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
        public WebBrowser Browser { get { return _Browser; } }
        /// <summary>
        /// LIke Broswer.Navigate, but you don't have to be on the GUI thread to use it.  
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
        public static void Navigate(WebBrowser browser, string url)
        {
            try
            {
                browser.Navigate(url);
            }
            catch (COMException)
            {
                // This can happen on Win10 systems without IE installed.  
                throw new ApplicationException("Error Trying to open a Web Browser.   Is Internet Explorer Installed?");
            }
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
        /// <summary>
        /// The browser looses where it is when it resizes, which is very confusing to people
        /// Thus force a resync when the window resizes.  
        /// </summary>
        private void Browser_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (m_notFirst)
            {
                Browser.Navigate(Browser.Source);
            }

            m_notFirst = true;
        }

        private bool m_notFirst;
        #endregion 
    }
}
