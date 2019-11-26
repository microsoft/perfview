using PerfView.Utilities;
using System.IO;
using System.Windows;
using System.Windows.Documents;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Interaction logic for EULADialog.xaml
    /// </summary>
    public partial class EULADialog : WindowBase
    {
        public EULADialog(Window parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            var eulaFile = System.IO.Path.Combine(SupportFiles.SupportFileDir, "EULA.rtf");
            ReadFromFile(eulaFile);
        }

        private void ReadFromFile(string eulaFile)
        {
            var bodyRange = new TextRange(Body.Document.ContentStart, Body.Document.ContentEnd);
            using (var stream = File.OpenRead(eulaFile))
            {
                bodyRange.Load(stream, DataFormats.Rtf);
            }
        }

        private void AcceptClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
