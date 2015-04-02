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
    /// TODO FIX NOW use or remove
    /// </summary>
    public partial class SourceNameLookup : Window
    {
        public SourceNameLookup()
        {
            InitializeComponent();
        }

        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(e.Parameter as string);
        }
    }
}
