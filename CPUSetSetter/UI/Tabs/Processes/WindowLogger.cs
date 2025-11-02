using CommunityToolkit.Mvvm.ComponentModel;


namespace CPUSetSetter.UI.Tabs.Processes
{
    public partial class WindowLogger : ObservableObject
    {
        [ObservableProperty]
        private string _text = "";

        private Queue<string> _logLines = new();
        private readonly Lock _lock = new();
        private bool _isUpdating = false;

        public static WindowLogger Default { get; } = new WindowLogger();

        public static void Write(string message)
        {
            Default.WriteImp(message);
        }

        private void WriteImp(string message)
        {
            using (_lock.EnterScope())
            {
                _logLines.Enqueue(message + "\n");

                // Begin updating the logger text in the UI
                // A small delay is used before updating, so multiple logs can be rendered in one go
                if (!_isUpdating)
                {
                    _isUpdating = true;
                    Task.Run(UpdateText);
                }
            }
        }

        private async Task UpdateText()
        {
            await Task.Delay(30);

            using (_lock.EnterScope())
            {
                while (_logLines.Count > 50)
                {
                    _logLines.Dequeue();
                }
                Text = string.Join("", _logLines);
                _isUpdating = false;
            }
        }
    }
}
