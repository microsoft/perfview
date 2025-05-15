using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PerfView
{
    /// <summary>
    /// Interaction logic for SelectProcess.xaml
    /// </summary>
    public partial class SelectProcess : WindowBase
    {
        public SelectProcess(Window parentWindow, IEnumerable<IProcess> processes, TimeSpan maxLifetime, Action<List<IProcess>> action, bool hasAllProc = false) : base(parentWindow)
        {
            m_action = action;
            m_processes = processes;
            InitializeComponent();
            if (!hasAllProc)
            {
                AllProcsButton.Visibility = System.Windows.Visibility.Hidden;
            }

            ProcessFilterTextBox.Text = "";

            UpdateItemSource();
            var filteredProcesses = Grid.ItemsSource as List<IProcess>;

            // Set selection point to the first process
            if (filteredProcesses.Count > 0)
            {
                Select(filteredProcesses[0]);
            }

            Grid.Focus();
        }

        #region private
        private int GetSelectionIndex(int defaultValue)
        {
            var ret = defaultValue;
            var cells = Grid.SelectedCells;

            var item = Grid.SelectedItem;
            if (cells.Count > 0)
            {
                var cell = cells[0];
                var row = Grid.ItemContainerGenerator.ContainerFromItem(cell.Item);
                if (row != null)
                {
                    ret = Grid.ItemContainerGenerator.IndexFromContainer(row);
                }
            }
            return ret;
        }
        private void Select(object item)
        {
            Grid.SelectedItems.Clear();
            Grid.SelectedItem = item;
            Grid.ScrollIntoView(item);
        }
        private static int FindNextWithPrefix(List<IProcess> processes, int start, string prefix)
        {
            Debug.Assert(start >= 0);
            Debug.Assert(prefix != null && prefix.Length > 0);
            if (start >= processes.Count)
            {
                return -1;
            }

            int cur = start;
            for (; ; )
            {
                if (processes[cur].Name.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return cur;
                }

                cur++;
                if (cur >= processes.Count)
                {
                    cur = 0;
                }

                if (cur == start)
                {
                    return -1;
                }
            }
        }
        private void UpdateItemSource()
        {
            // This is also called in layout and we don't care at that point
            if (Grid == null)
            {
                return;
            }

            var filterText = ProcessFilterTextBox.Text;
            if (filterText == "")
            {
                Grid.ItemsSource = m_processes;
                return;
            }
            var regex = Regex.Escape(filterText);
            regex = regex.Replace(@"\*", ".*");
            var filterRegex = new Regex(regex, RegexOptions.IgnoreCase);

            List<IProcess> processes = new List<IProcess>();
            foreach (var process in m_processes)
            {
                if (filterRegex.Match(process.Name).Success ||
                    filterRegex.Match(process.CommandLine).Success ||
                    filterRegex.Match(process.ProcessID.ToString()).Success)
                {
                    processes.Add(process);
                }
            }

            Grid.ItemsSource = processes;
        }

        internal void OKClicked(object sender, RoutedEventArgs e)
        {
            var items = Grid.SelectedItems;
            if (items == null || items.Count == 0)
            {
                OKButton.ToolTip = "You must make a selection before hitting OK (or use Cancel)";
                SystemSounds.Beep.Play();
                return;
            }
            
            var ret = new List<IProcess>();
            var selectedProcesses = new List<IProcess>();
            
            // Add explicitly selected processes
            foreach (var item in items)
            {
                var process = (IProcess)item;
                selectedProcesses.Add(process);
                ret.Add(process);
            }
            
            // If checkbox is checked, add child processes
            if (IncludeChildProcessesCheckBox.IsChecked == true)
            {
                // Build dictionaries for process lookup
                Dictionary<int, IProcess> processById = new Dictionary<int, IProcess>();
                Dictionary<int, List<int>> childrenByParentId = new Dictionary<int, List<int>>();
                
                // First pass: build process ID mapping
                foreach (var process in m_processes)
                {
                    processById[process.ProcessID] = process;
                    
                    // Initialize empty children list for each parent
                    if (!childrenByParentId.ContainsKey(process.ParentID))
                    {
                        childrenByParentId[process.ParentID] = new List<int>();
                    }
                    
                    // Add this process as a child of its parent
                    childrenByParentId[process.ParentID].Add(process.ProcessID);
                }
                
                // Add all transitive children of selected processes
                HashSet<int> addedProcessIds = new HashSet<int>();
                foreach (var process in selectedProcesses)
                {
                    addedProcessIds.Add(process.ProcessID); // Mark selected processes as already added
                }
                
                // For each selected process, add all its descendants
                foreach (var process in selectedProcesses)
                {
                    AddChildProcesses(process.ProcessID, processById, childrenByParentId, ret, addedProcessIds);
                }
            }

            m_action(ret);
            Close();
        }
        
        private void AddChildProcesses(
            int processId, 
            Dictionary<int, IProcess> processById, 
            Dictionary<int, List<int>> childrenByParentId,
            List<IProcess> resultList,
            HashSet<int> addedProcessIds)
        {
            // Check if this parent has any children
            if (!childrenByParentId.ContainsKey(processId))
            {
                return;
            }
            
            // For each child process
            foreach (var childId in childrenByParentId[processId])
            {
                // Skip if already added (prevents potential infinite recursion if process tree has cycles)
                if (addedProcessIds.Contains(childId))
                {
                    continue;
                }
                
                // Skip if the process doesn't exist in our dictionary (shouldn't happen)
                if (!processById.ContainsKey(childId))
                {
                    continue;
                }
                
                // Add the child process to the result list
                var childProcess = processById[childId];
                resultList.Add(childProcess);
                addedProcessIds.Add(childId);
                
                // Recursively add its children
                AddChildProcesses(childId, processById, childrenByParentId, resultList, addedProcessIds);
            }
        }
        private void DoHyperlinkHelp(object sender, ExecutedRoutedEventArgs e)
        {
            MainWindow.DisplayUsersGuide(e.Parameter as string);
        }
        private void GridKeyDownHander(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKClicked(null, null);
                e.Handled = true;
                return;
            }
            var processes = Grid.ItemsSource as List<IProcess>;
            if (Key.A <= e.Key && e.Key <= Key.Z)
            {
                // TODO When people sort the list, you 'jump around' if you do it this way.  
                var prefix = new string((char)((e.Key - Key.A) + 'a'), 1);
                var startIdx = GetSelectionIndex(-1);
                var nextIdx = FindNextWithPrefix(processes, startIdx + 1, prefix);
                if (nextIdx >= 0)
                {
                    Select(processes[nextIdx]);
                }
                else
                {
                    SystemSounds.Beep.Play();
                }

                e.Handled = true;
            }
        }
        private void FilterTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateItemSource();
        }

        private void AllProcsClicked(object sender, RoutedEventArgs e)
        {
            m_action(null);
            Close();
        }
        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        internal IEnumerable<IProcess> m_processes;
        private Action<List<IProcess>> m_action;
        #endregion
    }
}
