using System;
using System.Windows;
using System.Windows.Controls;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for FeebackDialog.xaml
    /// </summary>
    public partial class FeedbackDialog : Window
    {
        public FeedbackDialog(Action<string> action)
        {
            m_action = action;
            InitializeComponent();
            TextBox.Focus();
        }

        #region private 
        private void SubmitClicked(object sender, RoutedEventArgs e)
        {
            m_action(TextBox.Text);
            Close();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private Action<string> m_action;
        #endregion
    }
}
