using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;


namespace CPUSetSetter
{
    /// <summary>
    /// Extension to set the default sorting direction of DataGrid column when it is clicked for the first time
    /// The extension can be applied to a DataGrid in XAML
    /// </summary>
    public static class DataGridExtensions
    {
        public static readonly DependencyProperty SortDescProperty = DependencyProperty.RegisterAttached(
            "SortDesc", typeof(bool), typeof(DataGridExtensions), new PropertyMetadata(false, OnSortDescChanged));

        private static void OnSortDescChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as DataGrid;
            if (grid != null)
            {
                grid.Sorting += (source, args) =>
                {
                    if (args.Column.SortDirection == null)
                    {
                        // here we check an attached property value of target column
                        var sortDesc = (bool)args.Column.GetValue(SortDescProperty);
                        if (sortDesc)
                        {
                            args.Column.SortDirection = ListSortDirection.Ascending;
                        }
                    }
                };
            }
        }

        public static void SetSortDesc(DependencyObject element, bool value)
        {
            element.SetValue(SortDescProperty, value);
        }

        public static bool GetSortDesc(DependencyObject element)
        {
            return (bool)element.GetValue(SortDescProperty);
        }
    }
}
