using System.Windows;
using System.Windows.Controls;


namespace CPUSetSetter.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            viewModel = new MainWindowViewModel(Dispatcher);
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
