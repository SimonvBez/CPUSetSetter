using CommunityToolkit.Mvvm.ComponentModel;


namespace CPUSetSetter.UI.Tabs.Processes
{
    public partial class WindowLogger : ObservableObject
    {
        [ObservableProperty]
        private string _text = "";

        private readonly List<(string Message, int Count)> _logLines = new();
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
                if (_logLines.Count > 0 && _logLines[^1].Message == message)
                {
                    var last = _logLines[^1];
                    _logLines[^1] = (last.Message, last.Count + 1);
                }
                else
                {
                    _logLines.Add((message, 1));
                }

                // Begin updating the logger text in the UI
                // A small delay is used before updating, so multiple logs can be rendered in one go
                if (!_isUpdating)
                {
                    _isUpdating = true;
                    _ = Task.Run(UpdateText);
                }
            }
        }

        private async Task UpdateText()
        {
            try
            {
                await Task.Delay(30);

                string newText;
                using (_lock.EnterScope())
                {
                    while (_logLines.Count > 50)
                    {
                        _logLines.RemoveAt(0);
                    }
                    newText = string.Join("", _logLines.Select(l => l.Count > 1 ? $"{l.Message} ({l.Count})\n" : $"{l.Message}\n"));
                    _isUpdating = false;
                }

                if (App.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
                {
                    await dispatcher.InvokeAsync(() => Text = newText);
                }
            }
            catch (Exception ex)
            {
                using (_lock.EnterScope())
                {
                    _isUpdating = false;
                }

                System.Diagnostics.Debug.WriteLine($"WindowLogger update failed: {ex}");
            }
        }
    }
}
