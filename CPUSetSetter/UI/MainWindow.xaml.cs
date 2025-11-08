using CPUSetSetter.UI.Tabs.Processes;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;


namespace CPUSetSetter.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Listen for the Ctrl key, so the processes list's live sorting can be paused
            PreviewKeyDown += (_, e) => KeyPressed(e);
            PreviewKeyUp += (_, e) => KeyReleased(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }

        private static void KeyPressed(KeyEventArgs e)
        {
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !e.IsRepeat)
            {
                ProcessesTabViewModel.Instance?.PauseListUpdates();
            }
        }

        private static void KeyReleased(KeyEventArgs e)
        {
            if ((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !e.IsRepeat)
            {
                ProcessesTabViewModel.Instance?.ResumeListUpdates();
            }
        }
    }
}
