using System.Windows;
using System.Windows.Input;

namespace PerfView.Dialogs
{
    /// <summary>
    /// TODO FIX NOW use or remove
    /// </summary>
    public partial class SourceNameLookup : WindowBase
    {
        public SourceNameLookup(Window parentWindow) : base(parentWindow)
        {
            InitializeComponent();
        }

        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(e.Parameter as string);
        }
    }
}
