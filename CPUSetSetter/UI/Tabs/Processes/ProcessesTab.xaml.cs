using System.Windows.Controls;


namespace CPUSetSetter.UI.Tabs.Processes
{
    /// <summary>
    /// Interaction logic for ProcessesTab.xaml
    /// </summary>
    public partial class ProcessesTab : Grid
    {
        private readonly ProcessesTabViewModel viewModel;

        public ProcessesTab()
        {
            viewModel = new(Dispatcher);
            DataContext = viewModel;
            InitializeComponent();
        }

        private void Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            logBox.ScrollToEnd();
        }
    }
}
