using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace PerfView
{
    /// <summary>
    /// ThreadPanel: displaying/selecting threads based on ThreadMemoryInfo
    /// </summary>
    public class ThreadView
    {
        int LeftPanelWidth = 240;

        StackPanel             m_leftPanel;
        DataGrid               m_grid;
        ProcessMemoryInfo      m_heapInfo;
        List<ThreadMemoryInfo> m_threads;

        internal Panel CreateThreadViewPanel()
        {
            m_grid = new DataGrid();
            m_grid.AutoGenerateColumns = false;
            m_grid.IsReadOnly = true;

            // Columns
            m_grid.AddColumn("Thread",     "ThreadID");
            m_grid.AddColumn("Name",       "Name");
            m_grid.AddColumn("CPU",        "CpuSample",        true, Toolbox.TimeFormatN0);
            m_grid.AddColumn("CPU %",      "CpuSamplePercent", true, Toolbox.PercentageFormat);
            m_grid.AddColumn("FirstEvent", "FirstEvent",       true, Toolbox.TimeFormat);
            m_grid.AddColumn("LastEvent",  "LastEvent",        true, Toolbox.TimeFormat);
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
            m_threads  = threads.OrderBy(e => e, new ThreadMemoryInfoComparer()).ToList();
            
            m_grid.ItemsSource = m_threads;
        }
    }
    
}
