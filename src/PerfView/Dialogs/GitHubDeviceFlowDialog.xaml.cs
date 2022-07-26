using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for GitHubDeviceFlowDialog.xaml
    /// </summary>
    public partial class GitHubDeviceFlowDialog : WindowBase
    {
        private readonly Timer _timer;

        /// <summary>
        /// Construct a new instance.
        /// </summary>
        public GitHubDeviceFlowDialog(Window parentWindow, Uri verificationUri, string userCode, TimeSpan duration, CancellationToken cancellationToken) : base(parentWindow)
        {
            var viewModel = new ViewModel
            {
                VerificationUri = verificationUri,
                UserCode = userCode,
                ExpiresIn = duration
            };

            DataContext = viewModel;
            InitializeComponent();

            // Automatically Close the dialog when the cancellation token is canceled.
            cancellationToken.Register(() => Dispatcher.InvokeAsync(Close));

            // The timer updates the "expires in" countdown.
            void TimerTick(object state) => ((ViewModel)state).ExpiresIn -= TimeSpan.FromSeconds(1);
            _timer = new Timer(TimerTick, state: viewModel, dueTime: TimeSpan.FromSeconds(1), period: TimeSpan.FromSeconds(1));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _timer.Dispose();
            base.OnClosing(e);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class ViewModel : INotifyPropertyChanged
        {
            private TimeSpan _expiresIn;

            /// <summary>
            /// The URI that users must visit in order to complete device flow authentication.
            /// </summary>
            public Uri VerificationUri { get; set; }
            
            /// <summary>
            /// The code that users must enter in the Web Page for device flow authentication.
            /// </summary>
            public string UserCode { get; set; }
            
            /// <summary>
            /// The time interval when the code expires.
            /// </summary>
            public TimeSpan ExpiresIn
            {
                get => _expiresIn;
                set => UpdateProperty(ref _expiresIn, value);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void UpdateProperty<TValue>(ref TValue field, TValue newValue, [CallerMemberName] string propertyName = null)
            {
                if (!EqualityComparer<TValue>.Default.Equals(field, newValue))
                {
                    field = newValue;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(((ViewModel)DataContext).UserCode);
            ((Button)sender).Content = "Copied";
        }
    }
}
