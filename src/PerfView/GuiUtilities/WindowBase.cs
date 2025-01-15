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
            if (parentWindow != null)
            {
                // Try to set the owner, but it might fail (e.g. if parentWindow has never been displayed)
                // give up setting the owner in that case (it is not critical to have an owner)  
                try
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                catch (System.Exception) { }
            }
        }
    }
}
