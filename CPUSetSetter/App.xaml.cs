using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Application = System.Windows.Application;


namespace CPUSetSetter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Environment.ProcessorCount > 64)
            {
                throw new NotImplementedException("More than 64 logical CPU cores are not supported");
            }

            SetAppCulture();

            // Set up the tray icon
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open", null, (_, _) => ShowMainWindow());
            trayMenu.Items.Add("Close", null, (_, _) => ExitApp());
            using Stream iconStream = GetResourceStream(new Uri("pack://application:,,,/CPUSetSetter;component/tray.ico")).Stream;
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
                        ShowMainWindow();
                        break;
                    case MouseButtons.Middle:
                        ExitApp();
                        break;
                }
            };

            MainWindow = new MainWindow();
            if (!Config.Default.StartMinimized)
            {
                ShowMainWindow();
            }
        }

        private static void SetAppCulture()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag))
            );
        }

        private void ShowMainWindow()
        {
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void ExitApp()
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Shutdown();
        }
    }
}

/*
 * TODO:
 * - Program icon
 * - Add tray icon and starting as minimized
 * - Low priority: Add list of saved process settings
 */
