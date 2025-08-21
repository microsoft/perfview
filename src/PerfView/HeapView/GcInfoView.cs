using Microsoft.Diagnostics.Tracing.Analysis.GC;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace PerfView
{
    /// <summary>
    /// Wrapper around GCEvent for data binding (needs public properties)
    /// </summary>
    internal class GcEventWrapper
    {
        private TraceGC m_event;

        internal GcEventWrapper(TraceGC evnt)
        {
            m_event = evnt;
        }

        internal TraceGC Event
        {
            get
            {
                return m_event;
            }
        }

        public int Number
        {
            get
            {
                return m_event.Number;
            }
        }

        public object GenerationName
        {
            get
            {
                return m_event.GCGenerationName;
            }
        }

        public GCReason Reason
        {
            get
            {
                return m_event.Reason;
            }
        }

        public double PauseStart
        {
            get
            {
                return m_event.PauseStartRelativeMSec;
            }
        }

        public double PauseDuration
        {
            get
            {
                return m_event.PauseDurationMSec;
            }
        }

        public int? GcHandleCount
        {
            get
            {
                GCHeapStats stat = m_event.HeapStats;

                if (stat != null)
                {
                    return stat.GCHandleCount;
                }
                else
                {
                    return null;
                }
            }
        }

        public int? SyncBlockCount
        {
            get
            {
                GCHeapStats stat = m_event.HeapStats;

                if (stat != null)
                {
                    return stat.SinkBlockCount;
                }
                else
                {
                    return null;
                }
            }
        }

        public int? PinObjectCount
        {
            get
            {
                GCHeapStats stat = m_event.HeapStats;

                if (stat != null)
                {
                    return stat.PinnedObjectCount;
                }
                else
                {
                    return null;
                }
            }
        }

        public double SizeBefore
        {
            get
            {
                return m_event.HeapSizeBeforeMB;
            }
        }

        public double SizeAfter
        {
            get
            {
                return m_event.HeapSizeAfterMB;
            }
        }
    }


    /// <summary>
    /// GCInfo Panel
    /// </summary>
    public class GcInfoView
    {
        private List<GcEventWrapper> m_gcEvents;
        private TextBox m_helpBox;
        private DataGrid m_grid;
        private CheckBox m_inducedBlocking;
        private CheckBox m_inducedNonblocking;
        private CheckBox m_lowMemory;
        private CheckBox m_allocSOH;
        private CheckBox m_AllocLOH;
        private CheckBox m_g0;
        private CheckBox m_g1;
        private CheckBox m_g2Blocking;
        private CheckBox m_g2Background;

        /// <summary>
        /// Help request from Hyperlink
        /// </summary>
        private void OnHelp(object sender, RoutedEventArgs e)
        {
            Hyperlink link = sender as Hyperlink;

            if (link != null)
            {
                string param = link.CommandParameter as string;

                if (param != null)
                {
                    m_helpBox.Text = HelpText[param];
                }
            }
        }

        /// <summary>
        /// Filter events according to checkboxes
        /// </summary>
        private IEnumerable<GcEventWrapper> FilterEvent()
        {
            bool no_g0 = m_g0.UnChecked();
            bool no_g1 = m_g1.UnChecked();
            bool no_g2Blk = m_g2Blocking.UnChecked();
            bool no_g2Bak = m_g2Background.UnChecked();

            bool no_inducedBlocking = m_inducedBlocking.UnChecked();
            bool no_inducedNonBlock = m_inducedNonblocking.UnChecked();
            bool no_lowMemory = m_lowMemory.UnChecked();
            bool no_allocSOH = m_allocSOH.UnChecked();
            bool no_allocLOH = m_AllocLOH.UnChecked();

            foreach (GcEventWrapper er in m_gcEvents)
            {
                TraceGC e = er.Event;

                GCType typ = e.Type;

                switch (e.Generation)
                {
                    case 0:
                        if (no_g0)
                        {
                            continue;
                        }

                        break;

                    case 1:
                        if (no_g1)
                        {
                            continue;
                        }

                        break;

                    case 2:
                        if (no_g2Blk && (typ == GCType.NonConcurrentGC))
                        {
                            continue;
                        }

                        if (no_g2Bak && (typ == GCType.BackgroundGC))
                        {
                            continue;
                        }

                        break;
                }

                switch (e.Reason)
                {
                    case GCReason.OutOfSpaceSOH:
                    case GCReason.AllocSmall:
                        if (no_allocSOH)
                        {
                            continue;
                        }

                        break;

                    case GCReason.InducedNotForced:
                    case GCReason.Induced:
                        if (no_inducedBlocking && (typ == GCType.NonConcurrentGC))
                        {
                            continue;
                        }

                        if (no_inducedNonBlock && (typ == GCType.BackgroundGC))
                        {
                            continue;
                        }

                        break;

                    case GCReason.OutOfSpaceLOH:
                    case GCReason.AllocLarge:
                        if (no_allocLOH)
                        {
                            continue;
                        }

                        break;
                    case GCReason.LowMemory:
                    case GCReason.InducedLowMemory:
                        if (no_lowMemory)
                        {
                            continue;
                        }

                        break;

                    case GCReason.Empty:
                    case GCReason.Internal:
                    default:
                        break;
                }

                yield return er;
            }
        }

        /// <summary>
        /// GCReason SelctAll
        /// </summary>
        private void ReasonSelectAll(object sender, RoutedEventArgs e)
        {
            if (m_inducedBlocking.UnChecked() || m_inducedNonblocking.UnChecked() || m_lowMemory.UnChecked() || m_allocSOH.UnChecked() || m_AllocLOH.UnChecked())
            {
                m_inducedBlocking.IsChecked = true;
                m_inducedNonblocking.IsChecked = true;
                m_lowMemory.IsChecked = true;
                m_allocSOH.IsChecked = true;
                m_AllocLOH.IsChecked = true;

                UpdateEvents(sender, e);
            }
        }

        /// <summary>
        /// Generation SelectAll
        /// </summary>
        private void GenerationSelectAll(object sender, RoutedEventArgs e)
        {
            if (m_g0.UnChecked() || m_g1.UnChecked() || m_g2Blocking.UnChecked() || m_g2Background.UnChecked())
            {
                m_g0.IsChecked = true;
                m_g1.IsChecked = true;
                m_g2Background.IsChecked = true;
                m_g2Blocking.IsChecked = true;

                UpdateEvents(sender, e);
            }
        }

        /// <summary>
        /// Event selection checkbox handler
        /// </summary>
        private void UpdateEvents(object sender, RoutedEventArgs e)
        {
            m_grid.ItemsSource = FilterEvent();
        }

        internal void SetGCEvents(List<TraceGC> events)
        {
            m_gcEvents = new List<GcEventWrapper>(events.Count);

            for (int i = 0; i < events.Count; i++)
            {
                m_gcEvents.Add(new GcEventWrapper(events[i]));
            }

            // Data binding
            m_grid.ItemsSource = m_gcEvents;
        }

        /// <summary>
        /// Create GCInfo panel
        /// </summary>
        internal Panel CreateGCInfoPanel(TextBox helpBox)
        {
            m_helpBox = helpBox;

            StackPanel reason = new StackPanel();
            {
                reason.AddTextBlock("GC Reason ", "GCReason", OnHelp, 3, 2);
                var selectAllReasonButton = reason.AddButton("Select All", 60, ReasonSelectAll, 3, 2);
                AutomationProperties.SetName(selectAllReasonButton, "Select all GC reasons");

                m_inducedBlocking = reason.AddCheckBox(true, "Induced Blocking", 3, 2, UpdateEvents);
                m_inducedNonblocking = reason.AddCheckBox(true, "Induced Nonblocking", 3, 0, UpdateEvents);
                m_lowMemory = reason.AddCheckBox(true, "Low Memory", 3, 0, UpdateEvents);
                m_allocSOH = reason.AddCheckBox(true, "Alloc SOH", 3, 0, UpdateEvents);
                m_AllocLOH = reason.AddCheckBox(true, "Alloc LOH", 3, 0, UpdateEvents);
            }

            StackPanel gen = new StackPanel();
            {
                gen.AddTextBlock("Generation ", "Generation", OnHelp, 3, 2);
                var selectAllGenerationsButton = gen.AddButton("Select All", 60, GenerationSelectAll, 3, 2);
                AutomationProperties.SetName(selectAllGenerationsButton, "Select all generations");

                m_g0 = gen.AddCheckBox(true, "0", 3, 0, UpdateEvents);
                m_g1 = gen.AddCheckBox(true, "1", 3, 0, UpdateEvents);
                m_g2Blocking = gen.AddCheckBox(true, "2(Blocking)", 3, 0, UpdateEvents);
                m_g2Background = gen.AddCheckBox(true, "2(Background)", 3, 0, UpdateEvents);
            }

            StackPanel controls = new StackPanel();
            controls.Background = Brushes.LightGray;
            controls.Width = 240;
            controls.Children.Add(Toolbox.Stack(Orientation.Horizontal, reason, gen));

            m_grid = new DataGrid();
            m_grid.Background = Brushes.LightGray;
            m_grid.MouseRightButtonUp += MouseRightButtonUp;
            m_grid.AutoGenerateColumns = false;
            m_grid.IsReadOnly = true;
            m_grid.ColumnHeaderStyle = Toolbox.FocusableDataGridColumnHeaderStyle(m_grid.ColumnHeaderStyle);

            // Columns
            m_grid.AddColumn(Toolbox.CreateTextBlock("GCIndex ", "GCIndex", OnHelp), "Number", true, Toolbox.CountFormatN0);
            m_grid.AddColumn(Toolbox.CreateTextBlock("Gen ", "GenNumberWithSuffix", OnHelp), "GenerationName");
            m_grid.AddColumn(Toolbox.CreateTextBlock("Reason ", "GCReason", OnHelp), "Reason");
            m_grid.AddColumn("PauseStart", "PauseStart", true, Toolbox.TimeFormat);
            m_grid.AddColumn("Pause", "PauseDuration", true, Toolbox.TimeFormat);

            m_grid.AddColumn("SizeBefore", "SizeBefore", true, Toolbox.MemoryFormatN0);
            m_grid.AddColumn("SizeAfter", "SizeAfter", true, Toolbox.MemoryFormatN0);

            m_grid.AddColumn("GCHandle", "GcHandleCount", true, Toolbox.CountFormatN0);
            m_grid.AddColumn("SyncBlock", "SyncBlockCount", true, Toolbox.CountFormatN0);
            m_grid.AddColumn("PinnedObj", "PinObjectCount", true, Toolbox.CountFormatN0);

            InitHelpText();

            return Toolbox.DockTopLeft(null, controls, m_grid);
        }

        private Dictionary<string, string> HelpText = new Dictionary<string, string>();

        /// <summary>
        /// Populate help message dictionary
        /// </summary>
        private void InitHelpText()
        {
            HelpText.Add("GCIndex", "This is the index of the GC.");

            HelpText.Add("Generation", "Check the desired generation(s) of which you want to see the GC info. " +
                                       "\r\n\r\n" +
                                       "Gen0 and Gen1 GCs are always done as blocking, meaning the managed threads are suspended for the duration of the GC. " +
                                       "\r\n\r\n" +
                                       "Full GCs (Gen2 GCs) can be done either as blocking or in the background, in which case managed threads are only suspended for a small amount of time during the GC.");

            HelpText.Add("Pause", "This is the pause GCs introduce by suspending managed threads. " +
                                   "Note that when a GC is done in the background (called background GCs), it does not suspend managed threads for the duration of the GC. " +
                                   "\r\n\r\n" +
                                   "Type in the desired pause time range in the textboxes then press Enter." +
                                   "\r\n\r\n" +
                                   "If you don't want to specify the min or max, leave it blank");

            HelpText.Add("GCReason", "This is the reason why GCs were triggered. " +
                                     "\r\n\r\n" +
                                     "Induced means someone triggered a GC via either calling GC.Collect or some API that triggers a GC. " +
                                     "\r\n\r\n" +
                                     "Induced Blocking means a GC will suspend all managed threads for the duration of the GC; " +
                                     "Induced Nonblocking was newly instroduced in 4.5, meaning it's up to GC to decide whether it should be a blocking GC or not." +
                                     "\r\n\r\n" +
                                     "If the machine is running very low on physical memory, a GC would be triggered and the reason would be Low Memory" +
                                     "\r\n\r\n" +
                                     "Most of the time GCs are triggered due to allocations on the managed heap and these are indicated by either " +
                                     "Alloc SOH (allocating on the small object heap) or Alloc LOH (allocating on the large objectr heap).");

            HelpText.Add("GenNumberWithSuffix", "N means NonConcurrent, a blocking GC" +
                                       "\r\n\r\n" +
                                       "B means Background" +
                                       "\r\n\r\n" +
                                       "F means Foreground, an ephemeral GC triggered while a background is running" +
                                       "\r\n\r\n" +
                                       "I means Induced blocking" +
                                       "\r\n\r\n" +
                                       "i means Induced Nonblocking");

            m_helpBox.Text = "Click on the ? next to an item to display help text for it here." +
                             "\r\n\r\n" +
                             "Use Generation, Pause and GC Reason to filter the GCs you want to see.";
        }

        private ContextMenu cxMenu = null;

        private void GenerateGridColumns(object sender, EventArgs e)
        {
            CreateContextMenu();
        }

        private string GetHeaderText(object header)
        {
            TextBlock block = header as TextBlock;

            if (block != null)
            {
                Run run = block.Inlines.FirstInline as Run;

                if (run != null)
                {
                    return run.Text;
                }
            }

            return header.ToString();
        }

        private void CreateContextMenu()
        {
            if (cxMenu == null)
            {
                cxMenu = new ContextMenu();

                foreach (DataGridColumn item in m_grid.Columns)
                {
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = GetHeaderText(item.Header);
                    menuItem.IsChecked = true;
                    cxMenu.Items.Add(menuItem);
                    menuItem.Click += new RoutedEventHandler(menuItem_Click);
                    menuItem.Checked += new RoutedEventHandler(menuItem_Checked);
                    menuItem.Unchecked += new RoutedEventHandler(menuItem_Unchecked);
                }
            }
        }

        private void menuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            //if (item.IsChecked)
            //{
            //    item.IsChecked = false;
            //}
            //else
            //{
            //    item.IsChecked = true;
            //}

            item.IsChecked = !item.IsChecked;
        }

        private void menuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;

            foreach (DataGridColumn column in m_grid.Columns)
            {
                if (GetHeaderText(column.Header).Contains(GetHeaderText(item.Header)))
                {
                    column.Visibility = System.Windows.Visibility.Hidden;
                    break;
                }
            }
        }

        private void menuItem_Checked(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            List<string> menuList = new List<string>();
            menuList.Clear();

            foreach (MenuItem menuItem in cxMenu.Items)
            {
                if (menuItem.IsChecked == true)
                {
                    menuList.Add(GetHeaderText(menuItem.Header));
                }
            }

            foreach (string menuItem in menuList)
            {
                foreach (DataGridColumn column in m_grid.Columns)
                {
                    if (GetHeaderText(column.Header) == menuItem)
                    {
                        column.Visibility = System.Windows.Visibility.Visible;
                        break;
                    }
                }
            }
        }

        private void AddNewColumn()
        {
            DataGridTextColumn newColumn = new DataGridTextColumn();
            Binding b = new Binding("");
            b.Converter = new NewColumnData();
            newColumn.Binding = b;
            newColumn.Header = "new";

            m_grid.Columns.Add(newColumn);
        }

        private void MouseRightButtonUp(object sender, RoutedEventArgs e)
        {
            DependencyObject depObj = (DependencyObject)e.OriginalSource;

            while ((depObj != null) && !(depObj is DataGridColumnHeader))
            {
                depObj = VisualTreeHelper.GetParent(depObj);
            }

            if (depObj == null)
            {
                return;
            }

            if (depObj is DataGridColumnHeader)
            {
                DataGridColumnHeader colHeader = depObj as DataGridColumnHeader;
                CreateContextMenu();
                colHeader.ContextMenu = cxMenu;
            }
        }

    }

    public class NewColumnData : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //throw new NotImplementedException();
            return 1234;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

