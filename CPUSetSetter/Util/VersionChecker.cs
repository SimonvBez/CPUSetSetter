using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.UI.Tabs.Processes;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;


namespace CPUSetSetter.Util
{
    /// <summary>
    /// Check for a new GitHub Release on every app start, and once every 24 hours (for those no-reboot monkeys out there)
    /// </summary>
    public partial class VersionChecker : ObservableObject
    {
        public static VersionChecker Instance { get; } = new();

        // The app version is only set in Releases. During development it will be the default 1.0.0.0
        private static Version DevVersion => new(1, 0, 0, 0);

        [ObservableProperty]
        private bool _newVersionAvailable;

        [ObservableProperty]
        private string _versionCheckState = "Version check not ran yet";

        public Version? AppVersion { get; } = Assembly.GetEntryAssembly()?.GetName().Version;

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

        public VersionChecker() { }

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

        partial void OnNewVersionAvailableChanged(bool value)
        {
            if (!value)
                return;

            WindowLogger.Write("A new version of CPU Set Setter is available on GitHub!");

            if (AppConfig.Instance.ShowUpdatePopup)
            {
                App.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBoxResult result = MessageBox.Show(
                        "A new version of CPU Set Setter is available!\n" +
                        "Would you like to open the Release page now?\n" +
                        "(Sorry, self-updaters are hard)\n\n" +
                        "This update popup can be disabled in the Settings\n",
                        "New CPU Set Setter update available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        OpenLatestReleasePage();
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
                    string latestReleaseTag = await GetLatestReleaseTag();
                    if (latestReleaseTag.StartsWith('v'))
                    {
                        latestReleaseTag = latestReleaseTag[1..];
                        Version latestVersion = new(latestReleaseTag);
                        if (latestVersion > AppVersion!)
                        {
                            NewVersionAvailable = true;
                            VersionCheckState = "A new version is available!";
                            return;
                        }
                        VersionCheckState = "Currently up-to-date";
                        nextRetry = TimeSpan.FromHours(24); // No update available. Check again the next day
                    }
                    else
                    {
                        // A response was received, but not in the "vx.x.x" format like expected
                        VersionCheckState = "Failed to parse latest version";
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // Failed to reach GitHub, assume internet is down and try again soon
                    VersionCheckState = "Version check failed";
                    nextRetry = TimeSpan.FromSeconds(60);
                }
                catch (Exception)
                {
                    // Any other exception (probably NoSuccessResponseException), assume GitHub doesn't want a request for a while
                    VersionCheckState = "Version check failed";
                    nextRetry = TimeSpan.FromHours(24);
                }

                await Task.Delay(nextRetry);
            }
        }

        private static async Task<string> GetLatestReleaseTag()
        {
            using HttpClient client = new();

            // GitHub API requires a User-Agent header
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("CPUSetSetterUpdateChecker", "1.0"));

            string url = "https://api.github.com/repos/SimonvBez/CPUSetSetter/releases/latest";

            var response = await client.GetAsync(url);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new NoSuccessResponseException();
            }

            string json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("tag_name", out JsonElement tagElement))
            {
                return tagElement.GetString() ?? throw new NullReferenceException();
            }

            throw new KeyNotFoundException("tag_name not found in the API response.");
        }

        private class NoSuccessResponseException : Exception { }
    }
}
