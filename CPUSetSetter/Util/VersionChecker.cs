using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Velopack;
using Velopack.Sources;


namespace CPUSetSetter.Util
{
    /// <summary>
    /// Check for a new GitHub Release on every app start, and once every 24 hours (for those no-reboot monkeys out there)
    /// </summary>
    public partial class VersionChecker : ObservableObject
    {
        public static VersionChecker Instance { get; } = new();

        // The app version is only set in Releases. During development it will be the default 1.0.0.0
        private static Version DevVersion => new(1, 0, 0, 1);

        [ObservableProperty]
        private UpdateInfo? _newVersionAvailable = null;

        [ObservableProperty]
        private string _versionCheckState = "Version check not ran yet";

        public Version? AppVersion { get; } = Assembly.GetEntryAssembly()?.GetName().Version;

        private readonly IFileDownloader FileDownloader = new FileDownloader();

        public UpdateManager UpdateManager;    

        public string VersionString
        {
            get
            {
                if (AppVersion is not null)
                {
                    if (AppVersion == DevVersion)
                        return "Dev";
                    return $"v{AppVersion}";
                }
                return "unknown";
            }
        }

        public VersionChecker() {
            UpdateManager = new UpdateManager(new GithubSource("https://github.com/raicovx/CPUSetSetter", "", false, FileDownloader));
        }

        public void RunVersionChecker()
        {
            if (AppVersion is not null)
            {
                if (AppVersion == DevVersion)
                    VersionCheckState = "Version check disabled (Dev)";
                else
                    Task.Run(RunUpdateChecker); // Only run the update checker when this app is properly versioned
            }
        }

        public static void OpenLatestReleasePage()
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/SimonvBez/CPUSetSetter/releases/latest", UseShellExecute = true });
        }

        partial void OnNewVersionAvailableChanged(UpdateInfo? value)
        {
            if (value == null)
                return;

            WindowLogger.Write("A new version of CPU Set Setter is available on GitHub!");

            if (AppConfig.Instance.ShowUpdatePopup)
            {
                App.Current.Dispatcher.InvokeAsync(async () =>
                {
                    MessageBoxResult result = MessageBox.Show(
                        "A new version of CPU Set Setter is available!\n" +
                        "Would you like to install the update now?\n" +
                        "(This popup can be disabled in the Settings tab)",
                        "New CPU Set Setter update available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                       
                        await UpdateManager.DownloadUpdatesAsync(value);
                        
                      
                        UpdateManager.ApplyUpdatesAndRestart(value);                        
                    }
                });
            }
        }

        private async Task RunUpdateChecker()
        {
            while (true)
            {
                TimeSpan nextRetry;
                try
                {
                    UpdateInfo? newUpdate = await UpdateManager.CheckForUpdatesAsync();
                    if (newUpdate != null)
                    {
                        //there is an update. lets prompt the user to install
                        NewVersionAvailable = newUpdate;
                        VersionCheckState = $"A new version is available! ({newUpdate.TargetFullRelease})";
                        return;
                    }
                    else
                    {
                        nextRetry = TimeSpan.FromHours(24); // No update available. Check again the next day                     
                        VersionCheckState = "Currently up-to-date";
                    }
                }
                catch (HttpRequestException e)
                {
                    // Failed to reach GitHub, assume internet is down and try again soon
                    VersionCheckState = "Version check failed" + e.Message;
                    WindowLogger.Write(e.Message);
                    nextRetry = TimeSpan.FromSeconds(60);
                }               
                catch (Exception e)
                {
                    // Any other exception (probably NoSuccessResponseException), assume GitHub doesn't want a request for a while
                    VersionCheckState = "Version check failed " + e.Message;
                    WindowLogger.Write(e.Message);
                    nextRetry = TimeSpan.FromHours(24);
                }

                await Task.Delay(nextRetry);
            }
        }

        private class NoSuccessResponseException : Exception { }
    }
}
