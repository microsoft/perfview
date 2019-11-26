using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView.Utilities;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PerfView
{
    /// <summary>
    /// Interaction logic for CallerCalleeView.xaml
    /// </summary>
    public partial class CallerCalleeView : UserControl
    {
        public CallerCalleeView()
        {
            InitializeComponent();
            // Customize the view
            CallersGrid.Grid.CanUserSortColumns = true;
            CalleesGrid.Grid.CanUserSortColumns = true;
        }
        public void RemoveCountColumn(string columnName)
        {
            CallersGrid.RemoveColumn(columnName);
            CalleesGrid.RemoveColumn(columnName);
            FocusGrid.RemoveColumn(columnName);
        }

        public string FocusName
        {
            get
            {
                var focusNode = FocusNode;
                if (focusNode == null)
                {
                    return null;
                }

                return FocusNode.Name;
            }
        }
        public CallTreeNodeBase FocusNode
        {
            get
            {
                var ret = DataContext as CallTreeNodeBase;
                if (ret == null)
                {
                    if (m_callTree == null)
                    {
                        return null;
                    }

                    return m_callTree.Root;
                }
                return ret;
            }
        }

        public bool SetFocus(string value, CallTree tree = null)
        {
            if (tree != null)
            {
                m_callTree = tree;
            }

            if (m_callTree == null)
            {
                return false;
            }

            if (value == null)
            {
                value = m_callTree.Root.Name;
            }

            var ret = true;
            var newNode = new CallerCalleeNode(value, m_callTree);
            if (newNode.InclusiveMetric == 0)
            {
                newNode = new CallerCalleeNode(m_callTree.Root.Name, m_callTree);
                ret = false;
            }

            var oldFocus = DataContext as CallerCalleeNode;
            if (oldFocus != null)
            {
                oldFocus.FreeMemory();
            }

            DataContext = newNode;
            CallersGrid.Grid.ItemsSource = newNode.Callers;
            CalleesGrid.Grid.ItemsSource = newNode.Callees;
            FocusGrid.Grid.ItemsSource = new CallTreeNodeBase[] { newNode };
            return ret;
        }

        public bool Find(string pat)
        {
            if (pat == null)
            {
                m_findPat = null;
                return true;
            }

            if (m_curFindGrid == null || m_findPat == null || m_findPat != pat)
            {
                m_findPat = pat;
                m_curFindGrid = CallersGrid;
            }
            Debug.Assert(m_curFindGrid != null);
            m_curFindGrid.Focus();
            for (; ; )
            {
                if (m_curFindGrid.Find(pat))
                {
                    return true;
                }

                m_curFindGrid = NextGrid(m_curFindGrid);
                if (m_curFindGrid == null)
                {
                    m_findPat = null;
                    return false;
                }
            }
        }

        #region private
        private void CallerCallee_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var depObj = sender as DependencyObject;
            var stackWindow = depObj.AncestorOfType<StackWindow>();
            if (stackWindow == null)
            {
                return;
            }

            stackWindow.DataGrid_MouseDoubleClick(sender, e);
        }

        private PerfDataGrid NextGrid(PerfDataGrid grid)
        {
            if (grid == CallersGrid)
            {
                return FocusGrid;
            }

            if (grid == FocusGrid)
            {
                return CalleesGrid;
            }

            Debug.Assert(grid == CalleesGrid);
            return null;
        }

        private CallTree m_callTree;
        internal PerfDataGrid m_curFindGrid;
        private string m_findPat;
        #endregion
    }
}
