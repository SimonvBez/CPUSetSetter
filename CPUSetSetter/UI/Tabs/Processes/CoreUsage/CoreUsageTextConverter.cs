using System;
using System.Globalization;
using System.Windows.Data;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public sealed class CoreUsageTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double usage && values[1] is bool isParked)
            {
                return isParked ? "Parked" : $"{usage:F0}%";
            }
            return "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}