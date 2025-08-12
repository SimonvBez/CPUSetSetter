using System.Windows;
using System.Windows.Controls;


namespace CPUSetSetter
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel;

        public MainWindow()
        {
            viewModel = new MainWindowViewModel(Dispatcher);
            DataContext = viewModel;
            InitializeComponent();
        }

        private void Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).ScrollToEnd();
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
