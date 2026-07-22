using CPUSetSetter.Platforms;
using CPUSetSetter.UI.Tabs.Processes;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;


namespace CPUSetSetter.UI
{
    public partial class MainWindow : Window, IDisposable
    {
        private readonly IGameModeService _gameModeService;
        private readonly EventHandler<GameModeStateChangedEventArgs> _gameModeChangedHandler;
        private bool _isUpdatingGameModeCheckBox;
        private bool _isDisposed;

        internal MainWindow(
            IGameModeService gameModeService,
            CancellationToken cancellationToken)
        {
            _gameModeService = gameModeService;
            _gameModeChangedHandler = OnGameModeStateChanged;

            InitializeComponent();
            ProcessesTabControl.Initialize(gameModeService, cancellationToken);

            PreviewKeyDown += OnPreviewKeyDown;
            PreviewKeyUp += OnPreviewKeyUp;
            Activated += OnActivated;
            GameModeCheckBox.IsChecked = _gameModeService.IsEnabled;
            GameModeCheckBox.Checked += OnGameModeChecked;
            GameModeCheckBox.Unchecked += OnGameModeUnchecked;
            _gameModeService.StateChanged += _gameModeChangedHandler;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isDisposed)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnClosing(e);
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                ProcessesTabViewModel.Instance?.SetListPauseReason(ProcessListPauseReason.UserInput, true);
            }
        }

        private void OnPreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                bool otherControlKeyIsPressed = e.Key == Key.LeftCtrl
                    ? Keyboard.IsKeyDown(Key.RightCtrl)
                    : Keyboard.IsKeyDown(Key.LeftCtrl);
                ProcessesTabViewModel.Instance?.SetListPauseReason(
                    ProcessListPauseReason.UserInput,
                    otherControlKeyIsPressed);
            }
        }

        private static void OnActivated(object? sender, EventArgs e)
        {
            bool controlKeyIsPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            ProcessesTabViewModel.Instance?.SetListPauseReason(
                ProcessListPauseReason.UserInput,
                controlKeyIsPressed);
        }

        private void OnGameModeChecked(object sender, RoutedEventArgs e)
        {
            RequestGameModeState(true);
        }

        private void OnGameModeUnchecked(object sender, RoutedEventArgs e)
        {
            RequestGameModeState(false);
        }

        private void RequestGameModeState(bool isEnabled)
        {
            if (_isUpdatingGameModeCheckBox)
            {
                return;
            }

            _gameModeService.TrySetEnabled(isEnabled);
            UpdateGameModeCheckBox(_gameModeService.IsEnabled);
        }

        private void OnGameModeStateChanged(object? sender, GameModeStateChangedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateGameModeCheckBox(e.IsEnabled);
            }
            else if (!Dispatcher.HasShutdownStarted)
            {
                Dispatcher.InvokeAsync(() => UpdateGameModeCheckBox(e.IsEnabled));
            }
        }

        private void UpdateGameModeCheckBox(bool isEnabled)
        {
            if (GameModeCheckBox.IsChecked == isEnabled)
            {
                return;
            }

            _isUpdatingGameModeCheckBox = true;
            try
            {
                GameModeCheckBox.IsChecked = isEnabled;
            }
            finally
            {
                _isUpdatingGameModeCheckBox = false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            PreviewKeyDown -= OnPreviewKeyDown;
            PreviewKeyUp -= OnPreviewKeyUp;
            Activated -= OnActivated;
            GameModeCheckBox.Checked -= OnGameModeChecked;
            GameModeCheckBox.Unchecked -= OnGameModeUnchecked;
            _gameModeService.StateChanged -= _gameModeChangedHandler;
            ProcessesTabControl.DisposeViewModel();
            GC.SuppressFinalize(this);
        }
    }
}
