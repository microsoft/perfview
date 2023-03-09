using System;
using System.Windows;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for UnhandledExceptionDialog.xaml
    /// </summary>
    public partial class UnhandledExceptionDialog : WindowBase
    {
        public UnhandledExceptionDialog(Window parentWindow, object exception) : base(parentWindow)
        {
            InitializeComponent();

            string reporting = "The fact that this exception went unhanded is a programmer error.   It should be reported "
                          + "so it can be fixed.  Please set along the following stack trace information which will be "
                          + "useful in diagnosing the problem.\r\n";

            Body.Text = "An unhanded exception occured.\r\n"
                      + "\r\n"
                      + "At this point you can opt to continue, however it is possible that the aborted computation will "
                      + "cause additional failures.   Because PerfView generally only opens files for reading, there is no "
                      + "danger of corrupting  files, so it generally does not hurt to try.   However be on guard for "
                      + "unusual/incorrect behavior going forward.\r\n"
                      + "\r\n"
                      + "You can of course exit and restart PerfView to be completely safe.\r\n"
                      + "\r\n"
                      + reporting
                      + "\r\n"
                      + "StackTrace:\r\n"
                      + exception.ToString();
        }

        private void ContinueClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ExitClicked(object sender, RoutedEventArgs e)
        {
            // TODO allow restart.  
            Environment.Exit(-1);
        }
    }
}
