using System.Windows;

namespace PerfView
{
    /// <summary>
    /// This Window Base class was created to solve the issue #680
    /// https://github.com/Microsoft/perfview/issues/680
    /// </summary>
    public class WindowBase : Window
    {
        /// <summary>
        /// This constructor is only used for Design Viewer.
        /// </summary>
        public WindowBase() { }

        public WindowBase(Window parentWindow)
        {
            Owner = parentWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }
}