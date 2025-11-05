using System;
using System.Globalization;
using System.Windows.Data;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public sealed class PercentToHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return 0d;
            }

            if (!(values[0] is double containerHeight) || !(values[1] is double percent))
            {
                return 0d;
            }

            if (double.IsNaN(containerHeight) || containerHeight <= 0)
            {
                return 0d;
            }

            percent = Math.Clamp(percent, 0d, 100d);
            return containerHeight * (percent / 100d);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
