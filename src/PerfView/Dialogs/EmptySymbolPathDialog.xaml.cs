using System.Windows;

namespace PerfView.Dialogs
{
    /// <summary>
    /// Dialog poped when the symbol path is empty.  This gives the user control over whether PerfView will perform network operations
    /// 
    /// It is intended that this be a modal dialog (ShowDialog)
    /// </summary>
    public partial class EmptySymbolPathDialog : WindowBase
    {
        /// <summary>
        /// The action is given a 'true' value if MSSymbols should be used.  
        /// </summary>
        public EmptySymbolPathDialog(Window parentWindow) : base(parentWindow)
        {
            InitializeComponent();
        }

        public bool UseMSSymbols;

        #region private 
        private void UseEmptyPathClicked(object sender, RoutedEventArgs e)
        {
            UseMSSymbols = false;
            Close();
        }

        private void UseMSSymbolsClicked(object sender, RoutedEventArgs e)
        {
            UseMSSymbols = true;
            Close();
        }
        #endregion 
    }
}
