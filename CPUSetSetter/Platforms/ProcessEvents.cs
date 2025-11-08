namespace CPUSetSetter.Platforms
{
    /// <summary>
    /// Provides listeners for newly created and exited processes
    /// </summary>
    public static class ProcessEvents
    {
        public static event EventHandler<NewProcessEventArgs> ProcessCreated
        {
            add => Default.ProcessCreated += value;
            remove => Default.ProcessCreated -= value;
        }

        public static event EventHandler<ExitedProcessEventArgs> ProcessExited
        {
            add => Default.ProcessExited += value;
            remove => Default.ProcessExited -= value;
        }

        public static void Start() => Default.Start();

        private static IProcessEvents? _default;

#if WINDOWS
        public static IProcessEvents Default => _default ??= new ProcessEventsWindows();
#endif
    }

    public interface IProcessEvents
    {
        event EventHandler<NewProcessEventArgs>? ProcessCreated;
        event EventHandler<ExitedProcessEventArgs>? ProcessExited;

        void Start();
    }

    public class ProcessInfo
    {
        public string Name { get; }
        public string ImagePath { get; }
        public uint PID { get; }
        public IProcessHandler ProcessHandler { get; }

        public ProcessInfo(string name, string imagePath, uint pid, IProcessHandler processHandler)
        {
            Name = name;
            ImagePath = imagePath;
            PID = pid;
            ProcessHandler = processHandler;
        }
    }

    public class NewProcessEventArgs : EventArgs
    {
        public ProcessInfo Info { get; }

        public NewProcessEventArgs(ProcessInfo processInfo)
        {
            Info = processInfo;
        }
    }

    public class ExitedProcessEventArgs : EventArgs
    {
        public uint PID { get; }

        public ExitedProcessEventArgs(uint pid)
        {
            PID = pid;
        }
    }
}
