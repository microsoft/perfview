using System;
using System.Text;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Diagnostics;
using System.Globalization;

namespace PerfView.Utilities
{

    public static class Helpers
    {
        // Concatinates all TextBlocks in obj
        public static string GetText(DependencyObject obj)
        {
            var textBlock = obj as TextBlock;
            if (textBlock != null)
            {
                return textBlock.Text;
            }

            string ret = "";
            var childCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                ret += GetText(VisualTreeHelper.GetChild(obj, i));
            }

            return ret;
        }

        public static DependencyObject FindVisualNode(DependencyObject obj, string nodeName)
        {
            var asFrameElem = obj as FrameworkElement;
            if (asFrameElem != null && asFrameElem.Name == nodeName)
            {
                return obj;
            }

            DependencyObject ret = null;
            var childCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                ret = FindVisualNode(VisualTreeHelper.GetChild(obj, i), nodeName);
                if (ret != null)
                {
                    break;
                }
            }
            return ret;
        }
        public static string PathTo(this DependencyObject obj)
        {
            StringBuilder sb = new StringBuilder();
            AppendPath(obj, sb);
            return sb.ToString();
        }
        public static string VisualTree(this object obj)
        {
            StringBuilder sb = new StringBuilder();
            VisualTree(obj, sb, 0);
            return sb.ToString();
        }
        public static string LogicalTree(this object obj)
        {
            StringBuilder sb = new StringBuilder();
            LogicalTree(obj, sb, 0);
            return sb.ToString();
        }
        public static T AncestorOfType<T>(this DependencyObject obj) where T : DependencyObject
        {
            for (; ; )
            {
                if (obj == null)
                {
                    return null;
                }

                T asT = obj as T;
                if (asT != null)
                {
                    return asT;
                }

                // TODO this is a hack.  GetParent throws an exception 
                if (obj is System.Windows.Documents.Hyperlink)
                {
                    return null;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }
        }
        public static DependencyObject ParentVisual(this object obj)
        {
            var asDepObj = obj as DependencyObject;
            if (asDepObj != null)
            {
                return VisualTreeHelper.GetParent(asDepObj);
            }

            return null;
        }
        public static DependencyObject RootVisual(this object obj)
        {
            var asDepObj = obj as DependencyObject;
            if (asDepObj == null)
            {
                return null;
            }

            for (; ; )
            {
                var parent = VisualTreeHelper.GetParent(asDepObj);
                if (parent == null)
                {
                    return asDepObj;
                }

                asDepObj = parent;
            }
        }
        public static int IndexInParent(this DependencyObject obj)
        {
            if (obj != null)
            {
                var parent = VisualTreeHelper.GetParent(obj);
                if (parent != null)
                {
                    var childCount = VisualTreeHelper.GetChildrenCount(parent);
                    for (int i = 0; i < childCount; i++)
                    {
                        if (VisualTreeHelper.GetChild(parent, i) == obj)
                        {
                            return i;
                        }
                    }
                }
            }
            return -1;
        }
        public static string DataGridLocation(this object obj)
        {
            int row = -1;
            int column = -1;
            var asDepObj = obj as DependencyObject;

            while (asDepObj != null)
            {
                if (asDepObj is DataGridColumn)
                {
                    column = ((DataGridColumn)asDepObj).DisplayIndex;
                }

                if (asDepObj is DataGridCell)
                {
                    column = ((DataGridCell)asDepObj).Column.DisplayIndex;
                }
                else if (asDepObj is DataGridRow)
                {
                    row = ((DataGridRow)asDepObj).IndexInParent();
                }

                asDepObj = VisualTreeHelper.GetParent(asDepObj);
            }
            return "[" + row.ToString() + "," + column.ToString() + "]";
        }

        #region private
        private static void VisualTree(object obj, StringBuilder sb, int depth)
        {
            var str = obj.ToString();
            var match = Regex.Match(str, "([A-Za-z0-9.]*) *(.*)");
            var name = match.Groups[1].Value;
            string arg = match.Groups[2].Value;
            if (obj is TextBlock)
            {
                arg = (obj as TextBlock).Text;
            }

            sb.Append(' ', depth).Append('<').Append(name);
            if (arg.Length > 0)
            {
                sb.Append(" Arg=\"").Append(arg).Append('"');
            }

            sb.Append(" Hash=\"").Append(obj.GetHashCode()).Append('"');


            var asFrameElem = obj as FrameworkElement;
            if (asFrameElem != null)
            {
                var elemName = asFrameElem.Name;
                if (!string.IsNullOrEmpty(elemName))
                {
                    sb.Append(" Name=\"").Append(elemName).Append('"');
                }

                var dataContext = asFrameElem.DataContext;
                if (dataContext != null)
                {
                    sb.Append(" DataContextHashCode=\"").Append(dataContext.GetHashCode()).Append('"');
                }
            }

            var asDepObj = obj as DependencyObject;
            if (asDepObj != null)
            {
                var childCount = VisualTreeHelper.GetChildrenCount(asDepObj);
                if (childCount != 0)
                {
                    sb.AppendLine(">");
                    for (int i = 0; i < childCount; i++)
                    {
                        VisualTree(VisualTreeHelper.GetChild(asDepObj, i), sb, depth + 1);
                    }

                    sb.Append(' ', depth).Append("</").Append(name).Append('>').AppendLine();
                }
                else
                {
                    sb.AppendLine("/>");
                }
            }
            else
            {
                sb.AppendLine("/>");
            }
        }

        private static void LogicalTree(object obj, StringBuilder sb, int depth)
        {
            var str = obj.ToString();
            var match = Regex.Match(str, "([A-Za-z0-9.]*) *(.*)");
            var name = match.Groups[1].Value;
            string arg = match.Groups[2].Value;
            if (obj is TextBlock)
            {
                arg = (obj as TextBlock).Text;
            }

            sb.Append(' ', depth).Append('<').Append(name);
            if (arg.Length > 0)
            {
                sb.Append(" Arg=\"").Append(arg).Append('"');
            }

            sb.Append(" Hash=\"").Append(obj.GetHashCode()).Append('"');


            var asFrameElem = obj as FrameworkElement;
            if (asFrameElem != null)
            {
                var elemName = asFrameElem.Name;
                if (!string.IsNullOrEmpty(elemName))
                {
                    sb.Append(" Name=\"").Append(elemName).Append('"');
                }

                var dataContext = asFrameElem.DataContext;
                if (dataContext != null)
                {
                    sb.Append(" DataContextHashCode=\"").Append(dataContext.GetHashCode()).Append('"');
                }
            }

            var asDepObj = obj as DependencyObject;
            if (asDepObj != null)
            {
                sb.AppendLine(">");
                foreach (var child in LogicalTreeHelper.GetChildren(asDepObj))
                {
                    LogicalTree(child, sb, depth + 1);
                }

                sb.Append(' ', depth).Append("</").Append(name).Append('>').AppendLine();
            }
            else
            {
                sb.AppendLine("/>");
            }
        }

        private static void AppendPath(DependencyObject obj, StringBuilder sb)
        {
            if (obj == null)
            {
                return;
            }

            var parent = VisualTreeHelper.GetParent(obj);
            if (parent != null)
            {
                AppendPath(parent, sb);
            }

            string name;
            if (obj is DataGridColumn)
            {
                name = " DataGridColumn(" + ((DataGridColumn)obj).DisplayIndex.ToString() + ")";
            }

            if (obj is DataGridCell)
            {
                name = " DataGridCell(" + ((DataGridCell)obj).Column.DisplayIndex.ToString() + ")";
            }
            else if (obj is DataGridRow)
            {
                name = " DataGridRow(" + ((DataGridRow)obj).IndexInParent().ToString() + ")";
            }
            else
            {
                name = obj.GetType().Name;
            }

            sb.Append(name);
            sb.AppendLine();
        }
        #endregion
    }

#if DEBUG
/// <summary>
/// This converter does nothing except breaking the debugger into the convert method
/// </summary>
public class DatabindingDebugConverter : IValueConverter
{
    public object Convert(object value, Type targetType,
        object parameter, CultureInfo culture)
    {
        Debugger.Break();
        return value;
    }
    public object ConvertBack(object value, Type targetType,
        object parameter, CultureInfo culture)
    {
        Debugger.Break();
        return value;
    }
}
#endif

}