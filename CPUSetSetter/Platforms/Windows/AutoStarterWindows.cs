using CPUSetSetter.UI.Tabs.Processes;
using Microsoft.Win32.TaskScheduler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Task = Microsoft.Win32.TaskScheduler.Task;


namespace CPUSetSetter.Platforms
{
    public class AutoStarterWindows : IAutoStarter
    {
        private const string appName = "CPUSetSetter";
        private static string? appExePath = Environment.ProcessPath;

        public bool IsEnabled => TaskExists && TaskPathIsCorrect;

        public bool Enable()
        {
            if (appExePath is null)
                return false;

            if (!Environment.IsPrivilegedProcess)
                return RunElevated(appExePath, AutoStarter.LaunchArgumentEnable);

            try
            {
                if (TaskExists && !TaskPathIsCorrect)
                    Disable(); // Remove the AutoStart rule first if it is incorrect

                using TaskService taskService = new();
                using TaskDefinition taskDefinition = taskService.NewTask();
                string currentUser = WindowsIdentity.GetCurrent().Name;
                taskDefinition.RegistrationInfo.Description = "Start CPU Set Setter at startup";
                taskDefinition.Principal.UserId = currentUser;
                taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

                taskDefinition.Triggers.Add(new LogonTrigger { UserId = null, Enabled = true });

                taskDefinition.Actions.Add(new ExecAction(appExePath, null, Path.GetDirectoryName(appExePath)));

                taskService.RootFolder.RegisterTaskDefinition(appName, taskDefinition);
                return true;
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Failed to create AutoStart Task: {ex}");
                return false;
            }
        }

        public bool Disable()
        {
            if (appExePath is null)
                return false;
            
            if (!Environment.IsPrivilegedProcess)
                return RunElevated(appExePath, AutoStarter.LaunchArgumentDisable);

            try
            {
                using TaskService taskService = new();
                taskService.RootFolder.DeleteTask(appName, false);
                return true;
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Failed to remove AutoStart Task: {ex}");
                return false;
            }
        }

        private static bool TaskExists
        {
            get
            {
                using TaskService taskService = new();
                using Task? task = taskService.GetTask(appName);
                return task != null;
            }
        }

        private static bool TaskPathIsCorrect
        {
            get
            {
                using TaskService taskService = new();
                using Task? task = taskService.GetTask(appName);
                if (task is null)
                    return false;

                return task.Definition.Actions.Any(action =>
                {
                    if (action is ExecAction execAction)
                    {
                        string actualPath = Path.GetFullPath(execAction.Path ?? "").Trim('"');
                        string expectedPath = Path.GetFullPath(appExePath ?? "").Trim('"');
                        if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    return false;
                });
            }
        }

        private static bool RunElevated(string exePath, string argument)
        {
            ProcessStartInfo processStartInfo = new()
            {
                FileName = exePath,
                Arguments = argument,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            try
            {
                Process? process = Process.Start(processStartInfo);
                process?.WaitForExit();
                if (process is null)
                    return false;

                if (process.ExitCode == 0)
                    return true;
                return false;
            }
            catch (Win32Exception ex)
            {
                WindowLogger.Write($"Failed to create AutoStart task: {ex}");
                return false;
            }
        }
    }
}
