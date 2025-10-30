using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace CPUSetSetter
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel;
        private bool isCtrlPressed = false;

        public MainWindow()
        {
            viewModel = new MainWindowViewModel(Dispatcher);
            DataContext = viewModel;
            InitializeComponent();

            Loaded += (_, _) => logBox.ScrollToEnd();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyUp += MainWindow_PreviewKeyUp;
        }

        private void MainWindow_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && isCtrlPressed)
            {
                isCtrlPressed = false;
                viewModel.ResumeListUpdates();
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !isCtrlPressed)
            {
                isCtrlPressed = true;
                viewModel.PauseListUpdates();
            }
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
            if (Config.Default.RunInBackground)
            {
                e.Cancel = true;
                Hide();
                base.OnClosing(e);
                return;
            }
            base.OnClosing(e);
        }
    }
}
