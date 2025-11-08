using System.IO;


namespace CPUSetSetter.TrayIcon
{
    public class AppTrayIcon : IDisposable
    {
        private readonly NotifyIcon? _trayIcon;

        public event EventHandler? OpenClicked;
        public event EventHandler? CloseClicked;

        public AppTrayIcon(Stream iconStream)
        {
            ContextMenuStrip trayMenu = new();
            trayMenu.Items.Add("Open", null, (_, _) => OpenClicked?.Invoke(this, new()));
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
