using System.Runtime.InteropServices;


namespace CPUSetSetter.Platforms
{
    public class HotkeyListenerWindows : IHotkeyListener
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private readonly IntPtr _hookId;
        private readonly LowLevelKeyboardProc _llCallback;
        private readonly HashSet<VKey> _pressedKeys = [];
        private readonly List<HotkeyCallback> _hotkeyCallbacks = [];
        private readonly object _lock = new();

        public event EventHandler<KeyEventArgs>? KeyPressed;

        public bool CallbacksEnabled { get; set; } = true;

        public HotkeyListenerWindows()
        {
            _llCallback = HookCallback;
            _hookId = NativeMethods.SetWindowsHookExW(WH_KEYBOARD_LL, _llCallback, 0, 0);
        }

        public void AddCallback(HotkeyCallback callback)
        {
            lock (_lock)
            {
                _hotkeyCallbacks.Add(callback);
            }
        }

        public void RemoveCallback(HotkeyCallback callback)
        {
            lock (_lock)
            {
                _hotkeyCallbacks.Remove(callback);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                bool keyDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;

                KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam)!;
                VKey vkCode = (VKey)kbd.vkCode;

                if (keyDown)
                {
                    bool isRepeat = !_pressedKeys.Add(vkCode);
                    KeyPressed?.Invoke(this, new() { Key = vkCode, IsRepeat = isRepeat });
                    if (CallbacksEnabled)
                    {
                        foreach (HotkeyCallback callback in _hotkeyCallbacks)
                        {
                            // Remove any 'pressed' keys that aren't actually pressed anymore
                            if (_pressedKeys.IsSupersetOf(callback.VKeys))
                            {
                                foreach (VKey pressedKey in _pressedKeys)
                                {
                                    // The just pressed vkCode has not been registered by GetAsyncKeyState yet, so it is excluded
                                    if (pressedKey != vkCode && NativeMethods.GetAsyncKeyState((int)pressedKey) == 0)
                                    {
                                        // Remove the key when it is actually no longer pressed
                                        _pressedKeys.Remove(pressedKey);
                                    }
                                }
                            }

                            if (_pressedKeys.SetEquals(callback.VKeys) && (!isRepeat || callback.AllowRepeats))
                            {
                                callback.InvokePressed();
                            }
                        }
                    }
                }
                else
                {
                    _pressedKeys.Remove(vkCode);
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
