using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Utilities
{
    public class ScrollSynchronizer : DependencyObject
    {
        // Variables
        public static DependencyProperty ScrollGroupProperty;
        static Dictionary<object, string> m_Scrollers;
        static Dictionary<string, List<object>> m_GroupScrollers;
        static Dictionary<string, double> m_HorizontalScrollPositions;
        static Dictionary<string, double> m_HorizontalScrollLengths;
        static Dictionary<string, double> m_VerticalScrollPositions;
        static Dictionary<string, double> m_VerticalScrollLengths;

        // Properties
        public string ScrollGroup
        {
            get { return (string)GetValue(ScrollGroupProperty); }
            set { SetValue(ScrollGroupProperty, value); }
        }

        // Constructors
        static ScrollSynchronizer()
        {
            ScrollGroupProperty = DependencyProperty.RegisterAttached(
                "ScrollGroup",
                typeof(string),
                typeof(ScrollSynchronizer),
                new PropertyMetadata(new PropertyChangedCallback(OnScrollGroupChanged)));
            m_Scrollers = new Dictionary<object, string>();
            m_GroupScrollers = new Dictionary<string, List<object>>();
            m_HorizontalScrollPositions = new Dictionary<string, double>();
            m_HorizontalScrollLengths = new Dictionary<string, double>();
            m_VerticalScrollPositions = new Dictionary<string, double>();
            m_VerticalScrollLengths = new Dictionary<string, double>();
        }

        // Get/Set
        public static void SetScrollGroup(DependencyObject obj, string nScrollGroup)
        {
            obj.SetValue(ScrollGroupProperty, nScrollGroup);
        }

        public static string GetScrollGroup(DependencyObject obj)
        {
            return (string)obj.GetValue(ScrollGroupProperty);
        }

        static void OnScrollGroupChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            ScrollViewer sv = obj as ScrollViewer;
            ScrollBar sb = obj as ScrollBar;
            if ((sv == null) && (sb == null))
                return;

            string ov = (string)e.OldValue;
            string nv = (string)e.NewValue;

            if (!string.IsNullOrEmpty(ov))
            {
                if ((sv != null) && (m_Scrollers.ContainsKey(sv)))
                {
                    sv.ScrollChanged -= new ScrollChangedEventHandler(ScrollViewer_ScrollChanged);
                    m_Scrollers.Remove(sv);
                    m_GroupScrollers[ov].Remove(sv);
                }

                if ((sb != null) && (m_Scrollers.ContainsKey(sb)))
                {
                    sb.IsEnabled = false;
                    sb.Scroll -= new ScrollEventHandler(ScrollBar_Scroll);
                    m_Scrollers.Remove(sb);
                    m_GroupScrollers[ov].Remove(sb);
                }

                // Kill off if nobody left in the group
                if (m_GroupScrollers[ov].Count == 0)
                {
                    m_GroupScrollers.Remove(ov);
                    m_HorizontalScrollPositions.Remove(ov);
                    m_HorizontalScrollLengths.Remove(ov);
                    m_VerticalScrollPositions.Remove(ov);
                    m_VerticalScrollLengths.Remove(ov);
                }
            }

            if (!string.IsNullOrEmpty(nv))
            {
                // Prepare the group
                if (!m_GroupScrollers.ContainsKey(nv))
                    m_GroupScrollers.Add(nv, new List<object>());

                if (sv != null)
                {
                    if (sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
                    {
                        if (m_HorizontalScrollPositions.ContainsKey(nv))
                            SetScrollViewerHorizontalPosition(sv, m_HorizontalScrollPositions[nv]);
                        else
                        {
                            m_HorizontalScrollPositions.Add(nv, GetScrollViewerHorizontalPosition(sv));
                            m_HorizontalScrollLengths.Add(nv, GetScrollViewerHorizontalLength(sv));
                        }
                    }

                    if (sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                    {
                        if (m_VerticalScrollPositions.ContainsKey(nv))
                            SetScrollViewerVerticalPosition(sv, m_VerticalScrollPositions[nv]);
                        else
                        {
                            m_VerticalScrollPositions.Add(nv, GetScrollViewerVerticalPosition(sv));
                            m_VerticalScrollLengths.Add(nv, GetScrollViewerVerticalLength(sv));
                        }
                    }

                    m_Scrollers.Add(sv, nv);
                    m_GroupScrollers[nv].Add(sv);

                    sv.ScrollChanged += new ScrollChangedEventHandler(ScrollViewer_ScrollChanged);
                }

                if (sb != null)
                {
                    sb.IsEnabled = true;

                    if (sb.Orientation == Orientation.Horizontal)
                    {
                        if (m_HorizontalScrollPositions.ContainsKey(nv))
                        {
                            SetScrollBarPosition(sb, m_HorizontalScrollPositions[nv]);
                            SetScrollBarLength(sb, m_HorizontalScrollLengths[nv]);
                        }
                        else
                        {
                            m_HorizontalScrollPositions.Add(nv, GetScrollBarPosition(sb));
                            m_HorizontalScrollLengths.Add(nv, GetScrollBarLength(sb));
                        }
                    }
                    else
                    {
                        if (m_VerticalScrollPositions.ContainsKey(nv))
                        {
                            SetScrollBarPosition(sb, m_VerticalScrollPositions[nv]);
                            SetScrollBarLength(sb, m_VerticalScrollLengths[nv]);
                        }
                        else
                        {
                            m_VerticalScrollPositions.Add(nv, GetScrollBarPosition(sb));
                            m_VerticalScrollLengths.Add(nv, GetScrollBarLength(sb));
                        }
                    }

                    m_Scrollers.Add(sb, nv);
                    m_GroupScrollers[nv].Add(sb);

                    sb.Scroll += new ScrollEventHandler(ScrollBar_Scroll);
                }
            }
        }

        static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Scroll(sender as ScrollViewer, (e.HorizontalChange != 0), (e.VerticalChange != 0));

            // Hide scroll bar under the grid if no need to scroll.
            // Only iterating over ScrollBars since ScrollViewer items inside grid are already hidden.
            Visibility v = e.ExtentWidth <= e.ViewportWidth ? Visibility.Collapsed : Visibility.Visible;
            foreach (object obj in m_GroupScrollers[m_Scrollers[sender]])
            {
                var sb = obj as ScrollBar;
                if (sb != null)
                {
                    sb.Visibility = v;
                }
            }
        }

        static void ScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            Scroll(sender as ScrollBar, false, false);
        }

        static void Scroll(object nChangeScroller, bool nHorzChange, bool nVertChange)
        {
            string group = m_Scrollers[nChangeScroller];

            ScrollViewer svc = nChangeScroller as ScrollViewer;
            ScrollBar sbc = nChangeScroller as ScrollBar;

            // Record the position and length
            if (svc != null)
            {
                if (svc.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
                {
                    m_HorizontalScrollPositions[group] = GetScrollViewerHorizontalPosition(svc);
                    m_HorizontalScrollLengths[group] = GetScrollViewerHorizontalLength(svc);
                }

                if (svc.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                {
                    m_VerticalScrollPositions[group] = GetScrollViewerVerticalPosition(svc);
                    m_VerticalScrollLengths[group] = GetScrollViewerVerticalLength(svc);
                }
            }

            if (sbc != null)
            {
                if (sbc.Orientation == Orientation.Horizontal)
                {
                    m_HorizontalScrollPositions[group] = GetScrollBarPosition(sbc);
                    m_HorizontalScrollLengths[group] = GetScrollBarLength(sbc);
                }
                else
                {
                    m_VerticalScrollPositions[group] = GetScrollBarPosition(sbc);
                    m_VerticalScrollLengths[group] = GetScrollBarLength(sbc);
                }
            }

            // Modify each scroller in the group
            foreach (object obj in m_GroupScrollers[group])
            {
                ScrollViewer sv = obj as ScrollViewer;
                ScrollBar sb = obj as ScrollBar;

                // Skip changing myself
                if ((sv == nChangeScroller) || (sb == nChangeScroller))
                    continue;

                // Modify an existing scrollviewer
                if (sv != null)
                {
                    // Modify from a scrollviewer (scrollviewer -> scrollviewer)
                    if (svc != null)
                    {
                        // Modify horizontal
                        if ((sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled) &&
                            nHorzChange && (svc.HorizontalScrollBarVisibility !=
                                            ScrollBarVisibility.Disabled))
                            SetScrollViewerHorizontalPosition(
                                sv,
                                GetScrollViewerHorizontalPosition(svc));

                        // Modify vertical
                        if ((sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled) &&
                            nVertChange && (svc.VerticalScrollBarVisibility !=
                                            ScrollBarVisibility.Disabled))
                            SetScrollViewerVerticalPosition(sv, GetScrollViewerVerticalPosition(svc));
                    }

                    // Modify from a scrollbar (scrollbar -> scrollviewer)
                    if (sbc != null)
                    {
                        // Modify horizontal
                        if ((sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled) &&
                            (sbc.Orientation == Orientation.Horizontal))
                            SetScrollViewerHorizontalPosition(sv, GetScrollBarPosition(sbc));

                        // Modify vertical
                        if ((sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled) &&
                            (sbc.Orientation == Orientation.Vertical))
                            SetScrollViewerVerticalPosition(sv, GetScrollBarPosition(sbc));
                    }
                }

                // Modify an existing scrollbar
                if (sb != null)
                {
                    // Modify from a scrollviewer (scrollviewer -> scrollbar)
                    if (svc != null)
                    {
                        // Modify horizontal
                        if ((sb.Orientation == Orientation.Horizontal) &&
                            (svc.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled))
                        {
                            SetScrollBarPosition(sb, GetScrollViewerHorizontalPosition(svc));
                            SetScrollBarLength(sb, GetScrollViewerHorizontalLength(svc));
                        }

                        // Modify vertical
                        if ((sb.Orientation == Orientation.Vertical) &&
                            (svc.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled))
                        {
                            SetScrollBarPosition(sb, GetScrollViewerVerticalPosition(svc));
                            SetScrollBarLength(sb, GetScrollViewerVerticalLength(svc));
                        }
                    }

                    // Modify from a scrollbar (scrollbar -> scrollbar)
                    if (sbc != null)
                    {
                        // Modify same orientation
                        if (sb.Orientation == sbc.Orientation)
                        {
                            SetScrollBarPosition(sb, GetScrollBarPosition(sbc));
                            SetScrollBarLength(sb, GetScrollBarLength(sbc));
                        }
                    }
                }
            }
        }

        static double GetScrollViewerHorizontalPosition(ScrollViewer nScrollViewer)
        {
            if (nScrollViewer.ViewportWidth >= nScrollViewer.ExtentWidth)
                return 0;

            return nScrollViewer.HorizontalOffset / nScrollViewer.ScrollableWidth;
        }

        static double GetScrollViewerHorizontalLength(ScrollViewer nScrollViewer)
        {
            if (nScrollViewer.ViewportWidth >= nScrollViewer.ExtentWidth)
                return 1;

            if (nScrollViewer.ExtentWidth <= 0)
                return 0;

            return nScrollViewer.ViewportWidth / nScrollViewer.ExtentWidth;
        }

        static double GetScrollViewerVerticalPosition(ScrollViewer nScrollViewer)
        {
            if (nScrollViewer.ViewportHeight >= nScrollViewer.ExtentHeight)
                return 0;

            return nScrollViewer.VerticalOffset / nScrollViewer.ScrollableHeight;
        }

        static double GetScrollViewerVerticalLength(ScrollViewer nScrollViewer)
        {
            if (nScrollViewer.ViewportHeight >= nScrollViewer.ExtentHeight)
                return 1;

            if (nScrollViewer.ExtentHeight <= 0)
                return 0;

            return nScrollViewer.ViewportHeight / nScrollViewer.ExtentHeight;
        }

        static double GetScrollBarPosition(ScrollBar nScrollBar)
        {
            double tracklen = nScrollBar.Maximum - nScrollBar.Minimum;

            return (nScrollBar.Value - nScrollBar.Minimum) / tracklen;
        }

        static double GetScrollBarLength(ScrollBar nScrollBar)
        {
            double tracklen = nScrollBar.Maximum - nScrollBar.Minimum;

            return nScrollBar.ViewportSize / (tracklen + nScrollBar.ViewportSize);
        }

        static void SetScrollViewerHorizontalPosition(ScrollViewer nScrollViewer, double nPosition)
        {
            nScrollViewer.ScrollToHorizontalOffset(nPosition * nScrollViewer.ScrollableWidth);
        }

        static void SetScrollViewerVerticalPosition(ScrollViewer nScrollViewer, double nPosition)
        {
            nScrollViewer.ScrollToVerticalOffset(nPosition * nScrollViewer.ScrollableHeight);
        }

        static void SetScrollBarPosition(ScrollBar nScrollBar, double nPosition)
        {
            double tracklen = nScrollBar.Maximum - nScrollBar.Minimum;

            nScrollBar.Value = nPosition * tracklen + nScrollBar.Minimum;
        }

        static void SetScrollBarLength(ScrollBar nScrollBar, double nLength)
        {
            double tracklen = nScrollBar.Maximum - nScrollBar.Minimum;

            if (nLength < 1)
            {
                nScrollBar.ViewportSize = nLength * tracklen / (1 - nLength);
                nScrollBar.LargeChange = nScrollBar.ViewportSize;
                nScrollBar.IsEnabled = true;
            }
            else
            {
                nScrollBar.ViewportSize = double.MaxValue;
                nScrollBar.IsEnabled = false;
            }
        }
    }
}
