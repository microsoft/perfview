using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Utilities
{
    /// <summary>
    /// Helper class to prevent ScrollViewer handling MouseWheel event.
    /// It allows vertical mouse wheel scrolling while mouse is over hidden horizontal ScrollViewers in the grid
    /// </summary>
    public class ScrollViewerMouseFix
    {
        public static bool GetDisableMouseScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(DisableMouseScrollProperty);
        }

        public static void SetDisableMouseScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(DisableMouseScrollProperty, value);
        }

        public static readonly DependencyProperty DisableMouseScrollProperty =
            DependencyProperty.RegisterAttached("DisableMouseScroll", typeof(bool), typeof(ScrollViewerMouseFix), new FrameworkPropertyMetadata(false,ScrollViewerMouseFix.OnDisableMouseScrollPropertyChanged));

        public static void OnDisableMouseScrollPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ScrollViewer viewer = sender as ScrollViewer;
            if (viewer == null)
            {
                throw new ArgumentException("The dependency property can only be attached to a ScrollViewer", "sender");
            }

            if ((bool)e.NewValue)
            {
                viewer.PreviewMouseWheel += HandlePreviewMouseWheel;
            }
            else if (!(bool)e.NewValue)
            {
                viewer.PreviewMouseWheel -= HandlePreviewMouseWheel;
            }
        }

        private static void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled && sender != null)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement;
                parent?.RaiseEvent(eventArg);            }
        }
    }
}
