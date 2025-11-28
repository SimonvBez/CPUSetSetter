using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace CPUSetSetter.Util
{
    /// <summary>
    /// Progress dialog window for displaying download progress with a cancel button
    /// </summary>
    public partial class ProgressDialog : Window
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public ProgressDialog(string message, CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
            InitializeComponent();
            MessageText.Text = message;
        }

        public void SetProgress(int percent)
        {
            ProgressBar.Value = percent;
            ProgressText.Text = $"{percent}%";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
