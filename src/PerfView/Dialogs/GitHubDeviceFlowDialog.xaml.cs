using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for GitHubDeviceFlowDialog.xaml
    /// </summary>
    public partial class GitHubDeviceFlowDialog : WindowBase
    {
        /// <summary>
        /// Construct a new instance.
        /// </summary>
        public GitHubDeviceFlowDialog(Window parentWindow, Uri verificationUri, string userCode, CancellationToken cancellationToken) : base(parentWindow)
        {
            var viewModel = new
            {
                VerificationUri = verificationUri,
                UserCode = userCode,
            };

            DataContext = viewModel;
            InitializeComponent();

            // Automatically Close the dialog when the cancellation token is canceled.
            cancellationToken.Register(() => Dispatcher.InvokeAsync(Close));
        }

        private void NavigateTo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Uri uri = (Uri)e.Parameter;
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            string userCode = e.Parameter.ToString();
            Clipboard.SetText(userCode);
            ((Button)e.Source).Content = "Copied";
            e.Handled = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }
    }
}
