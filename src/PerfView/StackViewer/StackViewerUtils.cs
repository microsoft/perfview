using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PerfView.StackViewer
{
    class StackViewerUtils
    {
        public static void PopulateViewMenuItemForPerfDataGrid(PerfDataGrid grid, MenuItem viewMenu)
        {
            foreach (DataGridColumn col in grid.Grid.Columns)
            {
                // Create MenuItem based off of column header name, make it checkable.
                // Initially checked because Visibility is set to visable.
                MenuItem mItem = new MenuItem()
                {
                    Header = ((TextBlock)col.Header).Text,
                    IsCheckable = true,
                    IsChecked = true
                };

                // Create Click handler to collapse if unchecked, and make it visable when checked.
                mItem.Click += delegate (object sender, RoutedEventArgs e)
                {
                    MenuItem source = sender as MenuItem;
                    if (source.IsChecked)
                    {
                        col.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        col.Visibility = Visibility.Collapsed;
                    }
                };

                viewMenu.Items.Add(mItem);
            }
        }
    }
}
