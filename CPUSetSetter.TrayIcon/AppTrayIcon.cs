using System.IO;


namespace CPUSetSetter.TrayIcon
{
    public class AppTrayIcon : IDisposable
    {
        private readonly NotifyIcon? _trayIcon;
        private readonly ToolStripMenuItem _gameModeItem;
        private bool _isGameModeEnabled;

        public event EventHandler? OpenClicked;
        public event EventHandler? CloseClicked;
        public event EventHandler<bool>? GameModeToggleRequested;

        public bool IsGameModeEnabled
        {
            get => _isGameModeEnabled;
            set
            {
                _isGameModeEnabled = value;
                _gameModeItem.Text = value ? "Disable Game Mode" : "Enable Game Mode";
            }
        }

        public AppTrayIcon(Stream iconStream)
        {
            ContextMenuStrip trayMenu = new();
            trayMenu.Items.Add("Open", null, (_, _) => OpenClicked?.Invoke(this, new()));
            trayMenu.Items.Add(new ToolStripSeparator());

            _gameModeItem = new ToolStripMenuItem();
            IsGameModeEnabled = false; // This triggers the setter and sets the initial text
            _gameModeItem.Click += (_, _) =>
            {
                GameModeToggleRequested?.Invoke(this, !_isGameModeEnabled);
            };
            trayMenu.Items.Add(_gameModeItem);

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Close", null, (_, _) => CloseClicked?.Invoke(this, new()));
            _trayIcon = new()
            {
                Icon = new Icon(iconStream),
                Visible = true,
                ContextMenuStrip = trayMenu,
                Text = "CPU Set Setter"
            };

            // Show the app when clicking the tray icon
            _trayIcon.MouseClick += (_, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        OpenClicked?.Invoke(this, new());
                        break;
                    case MouseButtons.Middle:
                        CloseClicked?.Invoke(this, new());
                        break;
                }
            };
        }

        public void Dispose()
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}
