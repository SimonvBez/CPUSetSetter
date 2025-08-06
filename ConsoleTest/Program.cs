using CPUSetLib;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;


namespace ConsoleTest
{
    internal partial class Program
    {
        static readonly ConcurrentDictionary<uint, ProcessInfo> knownProcesses = new();
        static Dictionary<string, ulong> savedCpuSets = [];
        static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, IndentSize = 4 };

        static void Main(string[] args)
        {
            LoadCpuSets();

            CPUSetSetter setter = new();

            // Set up hotkey listeners
            using KeyboardHook keyHook = new();
            keyHook.KeyDown += (_, e) => OnKeyPressed(e.Key);

            // Set up process event listeners
            setter.OnNewProcess += (_, e) => OnNewProcess(e.Process);
            setter.OnExitedProcess += (_, e) => OnExitedProcess(e.Process);
            setter.Start();

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        static void OnKeyPressed(VKey key)
        {
            switch (key)
            {
                case VKey.Control | VKey.Alt | VKey.NumPad0:
                    // Clear CPU Set of current foreground window
                    SetCpuSetOfForegroundWindow(0);
                    break;
                case VKey.Control | VKey.Alt | VKey.NumPad1:
                    // Set CPU Set of current foreground window to CCD0
                    SetCpuSetOfForegroundWindow(0xFFFF);
                    break;
                case VKey.Control | VKey.Alt | VKey.NumPad2:
                    // Set CPU Set of current foreground window to CCD1
                    SetCpuSetOfForegroundWindow(0xFFFF0000);
                    break;
                case VKey.Control | VKey.Alt | VKey.NumPad4:
                    // Set CPU Set of current foreground window to CCD0
                    SetCpuSetOfForegroundWindow(0x5555);
                    break;
                case VKey.Control | VKey.Alt | VKey.NumPad5:
                    // Set CPU Set of current foreground window to CCD1
                    SetCpuSetOfForegroundWindow(0x55550000);
                    break;
            }
        }

        static void SetCpuSetOfForegroundWindow(ulong setMask)
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == 0)
            {
                return;
            }

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (knownProcesses.TryGetValue(pid, out ProcessInfo? pInfo))
            {
                if (setMask == 0)
                {
                    Console.WriteLine($"Clearing mask of '{pInfo.ExecutableName}'");
                    savedCpuSets.Remove(pInfo.ExecutableName!);
                }
                else
                {
                    Console.WriteLine($"Applying mask to '{pInfo.ExecutableName}': 0x{setMask:X}");
                    if (pInfo.ExecutableName is not null)
                    {
                        savedCpuSets.TryAdd(pInfo.ExecutableName, setMask);
                    }
                }
                SaveCpuSets();
                CPUSetSetter.ApplyCpuSetMaskToProcess(pid, setMask);
            }
        }

        static void SaveCpuSets()
        {
            using FileStream saveFile = File.Create("savedSets.json");
            JsonSerializer.Serialize(saveFile, savedCpuSets, options: jsonOptions);
        }

        static void LoadCpuSets()
        {
            try
            {
                using FileStream saveFile = File.OpenRead("savedSets.json");
                savedCpuSets = JsonSerializer.Deserialize<Dictionary<string, ulong>>(saveFile) ?? [];
            }
            catch (Exception) { }
        }

        static void OnNewProcess(ProcessInfo pInfo)
        {
            knownProcesses.TryAdd(pInfo.PID, pInfo);

            //if (pInfo.ExecutableName == "re8.exe")
            //{
            //    ulong setMask8 = 0xFFFF;
            //    Console.WriteLine($"Applying mask to '{pInfo.ExecutableName}': 0x{setMask8:X}");
            //    CPUSetSetter.ApplyCpuSetMaskToProcess(pInfo.PID, setMask8);
            //}

            if (pInfo.ExecutableName is null)
                return;
            
            if (savedCpuSets.TryGetValue(pInfo.ExecutableName!, out ulong setMask))
            {
                Console.WriteLine($"Applying mask to '{pInfo.ExecutableName}': 0x{setMask:X}");
                CPUSetSetter.ApplyCpuSetMaskToProcess(pInfo.PID, setMask);
            }

            //Console.WriteLine($"New process: {pInfo.ExecutableName}");

            //if (pInfo.ExecutableName == "python.exe")
            //{
            //    ulong mask = 0x5555;
            //    Console.Write($"Applying mask to '{pInfo.ExecutableName}': 0x{mask:X}...");
            //    if (CPUSetSetter.ApplyCpuSetMaskToProcess(pInfo.PID, mask))
            //    {
            //        Console.WriteLine("Success");
            //    }
            //    else
            //    {
            //        Console.WriteLine("Failed");
            //    }
            //}
        }

        static void OnExitedProcess(ProcessInfo pInfo)
        {
            knownProcesses.TryRemove(pInfo.PID, out ProcessInfo? _);
            //Console.WriteLine($"Exited process: {e.Process.ExecutableName}");
        }

        [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TranslateMessage(ref MSG lpMsg);

        [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
        public static partial IntPtr DispatchMessage(ref MSG lpMsg);

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
}
