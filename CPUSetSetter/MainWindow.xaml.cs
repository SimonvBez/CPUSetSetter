using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;


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

            Loaded += (_, _) => logBox.ScrollToEnd();
        }

        private void Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            logBox.ScrollToEnd();
        }

        private void HotkeyInput_GotFocus(object sender, RoutedEventArgs e)
        {
            viewModel.OnHotkeyInputFocusChanged(true);
        }

        private void HotkeyInput_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.OnHotkeyInputFocusChanged(false);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }
    }
}
