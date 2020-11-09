using Microsoft.Diagnostics.Symbols;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for SymbolPathDialog.xaml
    /// </summary>
    public partial class SymbolPathDialog : WindowBase
    {
        /// <summary>
        /// Kind should be either 'symbol' or 'source' depending on which path variable to set.  
        /// </summary>
        public SymbolPathDialog(Window parentWindow, string defaultValue, string kind, Action<string> action) : base(parentWindow)
        {
            InitializeComponent();
            m_kind = kind;
            if (kind != "Symbol")
            {
                AddMSSymbols.Visibility = System.Windows.Visibility.Hidden;
            }

            m_action = action;
            Title = "Setting " + kind + " Path";
            TitleHyperLink.CommandParameter = kind + "PathTextBox";
            TitleHyperLinkText.Text = kind + " Path";
            SymbolPathTextBox.Text = defaultValue.Replace(";", ";\r\n") + "\r\n";
            GetValue();
            SymbolPathTextBox.SelectionStart = 0;
            SymbolPathTextBox.SelectionLength = 0;
            SymbolPathTextBox.Focus();
        }
        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(e.Parameter as string);
        }
        private void OKClicked(object sender, RoutedEventArgs e)
        {
            m_action(GetValue());
            Close();
        }

        private void AddMSSymbolsClicked(object sender, RoutedEventArgs e)
        {
            var symPath = new SymbolPath(GetValue());
            symPath.Add("SRV*https://msdl.microsoft.com/download/symbols");
            SymbolPathTextBox.Text = symPath.InsureHasCache(symPath.DefaultSymbolCache()).CacheFirst().ToString();
            GetValue();
        }

        private string GetValue()
        {
            var ret = Regex.Replace(SymbolPathTextBox.Text.Trim(), @"(\s*(;|(\r\n))\s*)+", ";");
            EnvVarTextBox.Text = "set _NT_" + m_kind.ToUpper() + "_PATH=" + ret;
            return ret;
        }
        private void DoTextChanged(object sender, TextChangedEventArgs e)
        {
            GetValue();
        }
        private void DoKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private Action<string> m_action;
        private string m_kind;


    }
}
