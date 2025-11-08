using System.Globalization;
using System.Windows.Data;


namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public sealed class RelativeToAbsoluteConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Length != 2)
                return 0.0;

            if (values[0] is double relativeValue && values[1] is double maxValue)
            {
                return Math.Clamp(relativeValue, 0.0, 1.0) * maxValue;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
