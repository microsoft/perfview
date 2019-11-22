using System;
using System.Globalization;
using System.Windows.Data;

namespace PerfView.StackViewer
{
    internal class ColorValidatorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return "false";
            }
            // Do the conversion from bool to visibility
            return "true";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
