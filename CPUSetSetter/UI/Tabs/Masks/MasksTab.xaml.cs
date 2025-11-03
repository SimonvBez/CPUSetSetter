using System.Windows;
using System.Windows.Controls;


namespace CPUSetSetter.UI.Tabs.Masks
{
    public partial class MasksTab : Grid
    {
        private readonly MasksTabViewModel viewModel;

        public MasksTab()
        {
            viewModel = new();
            DataContext = viewModel;
            InitializeComponent();
        }

        private void HotkeyInput_GotFocus(object sender, RoutedEventArgs e)
        {
            viewModel.OnHotkeyInputFocusChanged(true);
        }

        private void HotkeyInput_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.OnHotkeyInputFocusChanged(false);
        }
    }
}