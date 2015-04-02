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
using System.IO;
using Utilities;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for EULADialog.xaml
    /// </summary>
    public partial class EULADialog : Window
    {
        public EULADialog()
        {
            InitializeComponent();
            var eulaFile = System.IO.Path.Combine(SupportFiles.SupportFileDir, "EULA.rtf");
            ReadFromFile(eulaFile);
        }

        private void ReadFromFile(string eulaFile)
        {
            var bodyRange = new TextRange(Body.Document.ContentStart, Body.Document.ContentEnd);
            using(var stream = File.OpenRead(eulaFile))
                bodyRange.Load(stream, DataFormats.Rtf);
        }

        private void AcceptClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
