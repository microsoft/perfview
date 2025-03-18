using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;


namespace PerfView
{
    /// <summary>
    /// Unique Allocation Tick Site: type + location
    /// </summary>
    internal class AllocTick
    {
        internal string m_type;
        internal CodeAddressIndex m_caller1;
        internal CodeAddressIndex m_caller2;
        private double m_allocSmall;
        private int m_countSmall;
        private double m_allocLarge;
        private int m_countLarge;

        internal void Add(bool large, double val)
        {
            if (large)
            {
                m_countLarge++;
                m_allocLarge += val;
            }
            else
            {
                m_countSmall++;
                m_allocSmall += val;
            }
        }

        public string Type
        {
            get
            {
                return m_type;
            }
        }

        public double Alloc
        {
            get
            {
                return m_allocLarge + m_allocSmall;
            }
        }

        public double AllocLarge
        {
            get
            {
                return m_allocLarge;
            }
        }

        public double AllocSmall
        {
            get
            {
                return m_allocSmall;
            }
        }

        public CodeAddressIndex Caller1
        {
            get
            {
                return m_caller1;
            }
        }

        public CodeAddressIndex Caller2
        {
            get
            {
                return m_caller2;
            }
        }

        public int Count
        {
            get
            {
                return m_countSmall + m_countLarge;
            }
        }

        public int CountSmall
        {
            get
            {
                return m_countSmall;
            }
        }

        public int CountLarge
        {
            get
            {
                return m_countLarge;
            }
        }
    }

    /// <summary>
    /// AllocTickKey comparer
    /// </summary>
    internal class AllocTickComparer : IEqualityComparer<AllocTick>
    {
        public bool Equals(AllocTick a, AllocTick b)
        {
            return (a.m_type == b.m_type) &&
                   (a.m_caller1 == b.m_caller1) &&
                   (a.m_caller2 == b.m_caller2);
        }

        public int GetHashCode(AllocTick a)
        {
            int hash = 3;

            if (a.m_type != null)
            {
                hash = hash * 7 + a.m_type.GetHashCode();
            }

            hash = hash * 11 + (int)a.m_caller1;
            hash = hash * 11 + (int)a.m_caller2;

            return hash;
        }
    }


    /// <summary>
    /// Displaying Allocation Tick events in DataGrid
    /// </summary>
    public class HeapAllocView
    {
        private DataGrid m_grid;

        /// <summary>
        /// Create HeapAllocView panel
        /// </summary>
        internal Panel CreateHeapAllocPanel(TraceLog traceLog)
        {
            StackPanel controls = new StackPanel();
            controls.Background = Brushes.LightGray;
            controls.Width = 240;

            m_grid = new DataGrid();
            m_grid.Background = Brushes.LightGray;

            m_grid.AutoGenerateColumns = false;
            m_grid.IsReadOnly = true;
            m_grid.ColumnHeaderStyle = Toolbox.FocusableDataGridColumnHeaderStyle(m_grid.ColumnHeaderStyle);

            AllocTickConverter converter = new AllocTickConverter(traceLog);

            // Columns
            m_grid.AddColumn("Type", "Type");

            m_grid.AddColumn("CountL", "CountLarge", true);
            m_grid.AddColumn("AllocL", "AllocLarge", converter, null, true);
            m_grid.AddColumn("CountS", "CountSmall", true);
            m_grid.AddColumn("AllocS", "AllocSmall", converter, null, true);
            m_grid.AddColumn("Caller1", "Caller1", converter);
            m_grid.AddColumn("Caller2", "Caller2", converter);

            return Toolbox.DockTopLeft(null, controls, m_grid);
        }

        private List<AllocTick> m_allocSites;

        internal void SetAllocEvents(List<AllocTick> allocSites)
        {
            m_allocSites = allocSites;

            // Data binding, sort by total allocation size
            m_grid.ItemsSource = m_allocSites.OrderByDescending(a => a.Alloc);
        }
    }

    /// <summary>
    /// DataBinding for AllocTick in DataGrid
    /// </summary>
    public class AllocTickConverter : IValueConverter
    {
        private TraceLog m_traceLog;

        public AllocTickConverter(TraceLog log)
        {
            m_traceLog = log;
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Type valueType = value.GetType();

            if (valueType == typeof(double))
            {
                if ((double)value == 0)
                {
                    return "";
                }
                else
                {
                    return String.Format("{0:N3} mb", value);
                }
            }
            else if (valueType == typeof(CodeAddressIndex))
            {
                return m_traceLog.GetMethodName((CodeAddressIndex)value);
            }
            else
            {
                return value.ToString();
            }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}

