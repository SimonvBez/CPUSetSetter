using System.Windows.Controls;

using CPUSetSetter.Platforms;


namespace CPUSetSetter.UI.Tabs.Processes
{
    public partial class ProcessesTab : Grid
    {
        private ProcessesTabViewModel? _viewModel;

        public ProcessesTab()
        {
            InitializeComponent();

            Loaded += (_, _) => logBox.ScrollToEnd();
        }

        internal void Initialize(
            IGameModeService gameModeService,
            CancellationToken cancellationToken)
        {
            if (_viewModel is not null)
            {
                throw new InvalidOperationException("ProcessesTab is already initialized");
            }

            _viewModel = new(Dispatcher, gameModeService, cancellationToken);
            DataContext = _viewModel;
        }

        internal void DisposeViewModel()
        {
            _viewModel?.Dispose();
            _viewModel = null;
        }

        private void Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            logBox.ScrollToEnd();
        }
    }
}
