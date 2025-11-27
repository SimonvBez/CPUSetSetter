using CPUSetSetter.UI.Tabs.Processes;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;


namespace CPUSetSetter.UI
{
    public partial class MainWindow : Window
    {
        private bool _listIsPaused = false;

        public MainWindow()
        {
            InitializeComponent();

            // Listen for the Ctrl key, so the processes list's live sorting can be paused
            PreviewKeyDown += (_, e) => KeyPressed(e);
            PreviewKeyUp += (_, e) => KeyReleased(e);

            Deactivated += (_, _) => ResumeListUpdates();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }

        private void KeyPressed(KeyEventArgs e)
        {
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !_listIsPaused)
            {
                _listIsPaused = true;
                ProcessesTabViewModel.Instance?.PauseListUpdates();
            }
        }

        private void KeyReleased(KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                ResumeListUpdates();
            }
        }

        private void ResumeListUpdates()
        {
            if (_listIsPaused)
            {
                _listIsPaused = false;
                ProcessesTabViewModel.Instance?.ResumeListUpdates();
            }
        }
    }
}
