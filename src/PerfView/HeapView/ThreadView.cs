using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace PerfView
{
    /// <summary>
    /// ThreadPanel: displaying/selecting threads based on ThreadMemoryInfo
    /// </summary>
    public class ThreadView
    {
        private int LeftPanelWidth = 240;
        private StackPanel m_leftPanel;
        private DataGrid m_grid;
        private ProcessMemoryInfo m_heapInfo;
        private List<ThreadMemoryInfo> m_threads;

        internal Panel CreateThreadViewPanel()
        {
            m_grid = new DataGrid();
            m_grid.AutoGenerateColumns = false;
            m_grid.IsReadOnly = true;
            m_grid.ColumnHeaderStyle = Toolbox.FocusableDataGridColumnHeaderStyle(m_grid.ColumnHeaderStyle);

            // Columns
            m_grid.AddColumn("Thread", "ThreadID");
            m_grid.AddColumn("Name", "Name");
            m_grid.AddColumn("CPU", "CpuSample", true, Toolbox.TimeFormatN0);
            m_grid.AddColumn("CPU %", "CpuSamplePercent", true, Toolbox.PercentageFormat);
            m_grid.AddColumn("FirstEvent", "FirstEvent", true, Toolbox.TimeFormat);
            m_grid.AddColumn("LastEvent", "LastEvent", true, Toolbox.TimeFormat);
            m_grid.AddColumn("CLRContention", "CLRContentionCount", true, Toolbox.CountFormatN0);

            m_leftPanel = new StackPanel();
            m_leftPanel.Width = LeftPanelWidth;
            m_leftPanel.Background = Brushes.LightGray;

            DockPanel threadPanel = Toolbox.DockTopLeft(null, m_leftPanel, m_grid);

            return threadPanel;
        }

        internal void SetData(ProcessMemoryInfo heapInfo)
        {
            m_heapInfo = heapInfo;

            IEnumerable<ThreadMemoryInfo> threads = m_heapInfo.Threads.Values;

            // Sort threads by name(type) and then CPU sample

            // .Net GC threads
            // .Net Finalizer threads
            // .Net BGC threads
            // .Net threads
            // Native threads
            m_threads = threads.OrderBy(e => e, new ThreadMemoryInfoComparer()).ToList();

            m_grid.ItemsSource = m_threads;
        }
    }

}
