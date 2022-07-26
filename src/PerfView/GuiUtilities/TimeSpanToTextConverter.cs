using System;
using System.Globalization;
using System.Windows.Data;

namespace PerfView
{
    /// <summary>
    /// Convert a <see cref="TimeSpan"/> to English text.
    /// </summary>
    internal sealed class TimeSpanToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(string))
            {
                throw new ArgumentException("Target type must be string", nameof(targetType));
            }

            TimeSpan span = (TimeSpan)value;
            if (span.TotalDays > 1)
            {
                switch (span.Hours)
                {
                    case 0:
                        return $"{span.Days} days";
                    case 1:
                        return $"{span.Days} days, 1 hour";
                    default:
                        return $"{span.Days} days, {span.Hours} hours";
                }
            }

            if (span.TotalHours > 1)
            {
                switch (span.Minutes)
                {
                    case 0:
                        return $"{span.Hours} hours";
                    case 1:
                        return $"{span.Hours} hours, 1 minute";
                    default:
                        return $"{span.Hours} hours, {span.Minutes} minutes";
                }
            }

            if (span.TotalMinutes > 1)
            {
                switch (span.Seconds)
                {
                    case 0:
                        return $"{span.Minutes} minutes";
                    case 1:
                        return $"{span.Minutes} minutes, 1 second";
                    default:
                        return $"{span.Minutes} minutes, {span.Seconds} seconds";
                }
            }

            switch (span.Seconds)
            {
                case 0:
                    return "0 seconds";

                case 1:
                    return "1 second";

                default:
                    return $"{span.Seconds} seconds.";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
