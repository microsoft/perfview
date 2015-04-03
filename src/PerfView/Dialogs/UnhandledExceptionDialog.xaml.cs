using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for UnhandledExceptionDialog.xaml
    /// </summary>
    public partial class UnhandledExceptionDialog : Window
    {
        public UnhandledExceptionDialog(object exception, bool feedbackSent)
        {
            InitializeComponent();

            string reporting;
            if (feedbackSent)
                reporting = "The fact that this exception went unhanded is a programmer error.   The fact that this failure "
                          + "occured has been logged so it can be fixed.  You don't need to take any action.\r\n";
            else
                reporting = "The fact that this exception went unhanded is a programmer error.   It should be reported "
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
