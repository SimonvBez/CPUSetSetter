using System.Windows.Controls;


namespace CPUSetSetter.UI.Tabs.Processes
{
    public partial class ProcessesTab : Grid
    {
        private readonly ProcessesTabViewModel viewModel;

        public ProcessesTab()
        {
            viewModel = new(Dispatcher);
            DataContext = viewModel;
            InitializeComponent();

            Loaded += (_, _) => logBox.ScrollToEnd();
        }

        private void Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            logBox.ScrollToEnd();
        }
    }
}
