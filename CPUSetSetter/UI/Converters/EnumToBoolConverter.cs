using System.Globalization;
using System.Windows.Data;


namespace CPUSetSetter.UI.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return false;
            }
            return value.Equals(parameter);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
            {
                return Binding.DoNothing;
            }
            return (bool)value ? parameter : Binding.DoNothing;
        }
    }
}
