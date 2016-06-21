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
using System.ComponentModel;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Display a dialog box for executing user commands. 
    /// </summary>
    public partial class UserCommandDialog : Window
    {
        public UserCommandDialog(Action<string> doDommand)
        {
            m_DoCommand = doDommand;
            InitializeComponent();
            CommandTextBox.HistoryLength = 50;      // Keep a fair bit of history for user commands. 

            // Initialize from persistent store. 
            var history = App.ConfigData["UserCommandHistory"];
            if (history != null)
                CommandTextBox.SetHistory(history.Split(';'));

            Loaded += delegate(object sender, RoutedEventArgs e)
            {
                CommandTextBox.Focus();
            };
            Closing += delegate(object sender, CancelEventArgs e)
            {
                CommandTextBox.Text = "";
                CommandTextBox.Focus();
                Hide();
                e.Cancel = true;
            };

        }

        public void RemoveHistory(string command)
        {
            CommandTextBox.RemoveFromHistory(command);
            SaveHistory();
        }
        #region private 
        private void SaveHistory()
        {
            // Save in persistent store.  
            StringBuilder sb = new StringBuilder();
            foreach (string item in CommandTextBox.Items)
            {
                if (sb.Length != 0)
                    sb.Append(';');
                sb.Append(item);
            }
            App.ConfigData["UserCommandHistory"] = sb.ToString();
        }

        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(e.Parameter as string);
        }
        private void OKClicked(object sender, RoutedEventArgs e)
        {
            string command = CommandTextBox.Text;
            if (!string.IsNullOrWhiteSpace(command))
            {
                m_DoCommand(command);
                SaveHistory();
            }
            Close();
        }
        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HelpClicked(object sender, RoutedEventArgs e)
        {
            GuiApp.MainWindow.DoUserCommandHelp(null, null);
        }
        Action<string> m_DoCommand;
        #endregion

    }
}
