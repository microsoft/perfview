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

        Action<string> m_action;
        #endregion
    }
}
