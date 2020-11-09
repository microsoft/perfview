using PerfView;
using System.ComponentModel;
using System.Windows;

namespace Controls
{
    /// <summary>
    /// Interaction logic for TextEditorWindow.xaml
    /// </summary>
    public partial class TextEditorWindow : WindowBase
    {
        public TextEditorWindow(string[] args = null) : this (null, args) { }

        public TextEditorWindow(Window parentWindow, string[] args = null) : base(parentWindow)
        {
            InitializeComponent();

            if (args != null && args.Length == 1)
            {
                TextEditor.OpenText(args[0]);
            }

            TextEditor.Body.Focus();
        }
        public TextEditorControl TextEditor { get { return m_TextEditor; } }

        /// <summary>
        /// If set simply hide the window rather than closing it when the user requests closing. 
        /// </summary>
        public bool HideOnClose;
        #region private
        // We hide rather than close the editor.  
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (HideOnClose)
            {
                Hide();
                e.Cancel = true;
            }
        }
        #endregion
    }
}
