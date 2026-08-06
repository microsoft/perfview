using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PerfView.GuiUtilities;
using Xunit;

namespace PerfViewTests.GuiUtilities
{
    public class WebBrowserWindowAccessibilityTests
    {
        [WpfFact]
        public void WpfEscapeRequestsClose()
        {
            AssertCloseRequested(window =>
            {
                var keyEvent = new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(window),
                    0,
                    Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };

                window.RaiseEvent(keyEvent);
                Assert.True(keyEvent.Handled);
            });
        }

        [WpfFact]
        public void WebViewEscapeMessageRequestsClose()
        {
            AssertCloseRequested(window => window.ProcessWebMessage("\"PerfView.CloseWindow\""));
        }

        private static void AssertCloseRequested(Action<WebBrowserWindow> requestClose)
        {
            var window = new WebBrowserWindow(null)
            {
                Content = new Grid(),
                Style = new Style(typeof(Window))
            };
            bool closeRequested = false;
            CancelEventHandler cancelClose = (sender, e) =>
            {
                closeRequested = true;
                e.Cancel = true;
            };

            window.Closing += cancelClose;
            window.Show();
            window.Activate();
            DrainDispatcher(window);

            try
            {
                requestClose(window);
                DrainDispatcher(window);
                Assert.True(closeRequested);
            }
            finally
            {
                window.Closing -= cancelClose;
                window.Close();
            }
        }

        private static void DrainDispatcher(DispatcherObject dispatcherObject)
        {
#pragma warning disable VSTHRD001 // WpfFact already runs this synchronous test on its dedicated UI thread.
            dispatcherObject.Dispatcher.Invoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => { }));
#pragma warning restore VSTHRD001
        }
    }
}
