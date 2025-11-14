using CPUSetSetter.UI.Tabs.Processes;
using System.Runtime.InteropServices;
using System.Threading.Channels;


namespace CPUSetSetter.Platforms
{
    public class HotkeyListenerWindows : IHotkeyListener
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private readonly IntPtr hookId;
        private readonly LowLevelKeyboardProc llCallback;
        private readonly HashSet<VKey> pressedKeys = [];
        private readonly List<HotkeyCallback> hotkeyCallbacks = [];
        private readonly ChannelWriter<KeyEvent> keyEventChannelWriter;
        private readonly Lock callbackLock = new();

        public event EventHandler<KeyEventArgs>? KeyPressed;

        public bool CallbacksEnabled { get; set; } = true;

        public HotkeyListenerWindows()
        {
            llCallback = HookCallback; // Store the bound method so it can be used to UnHook, should it be needed in the future

            Channel<KeyEvent> keyEventQueue = Channel.CreateUnbounded<KeyEvent>(new() { SingleReader = true, SingleWriter = true });
            keyEventChannelWriter = keyEventQueue.Writer;
            Task.Run(async () => await KeyEventsHandler(keyEventQueue.Reader));

            hookId = NativeMethods.SetWindowsHookExW(WH_KEYBOARD_LL, llCallback, 0, 0);
        }

        public void AddCallback(HotkeyCallback callback)
        {
            using (callbackLock.EnterScope())
            {
                hotkeyCallbacks.Add(callback);
            }
        }

        public void RemoveCallback(HotkeyCallback callback)
        {
            using (callbackLock.EnterScope())
            {
                hotkeyCallbacks.Remove(callback);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Get the parameters from the callback, and queue them to be processed by the KeyEventsHandler
                // This way as little time as possible is spent inside the HookCallback,
                // as this is holding up the key event from being passed on to other applications
                KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                KeyEvent keyEvent = new() { wParam = wParam, kbd = kbd };
                keyEventChannelWriter.TryWrite(keyEvent);
            }

            return NativeMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private async Task KeyEventsHandler(ChannelReader<KeyEvent> channelReader)
        {
            await foreach (KeyEvent keyEvent in channelReader.ReadAllAsync())
            {
                bool keyDown = keyEvent.wParam == WM_KEYDOWN || keyEvent.wParam == WM_SYSKEYDOWN;
                VKey vkCode = (VKey)keyEvent.kbd.vkCode;

                if (keyDown)
                {
                    bool isRepeat = !pressedKeys.Add(vkCode);
                    await InvokeKeyEvents(vkCode, isRepeat);
                }
                else
                {
                    pressedKeys.Remove(vkCode);
                }
            }
        }

        private async Task InvokeKeyEvents(VKey vkCode, bool isRepeat)
        {
            // Invoke the KeyPressed and hotkeyCallbacks on the MainThread, as these all interact with the UI
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    KeyPressed?.Invoke(this, new() { Key = vkCode, IsRepeat = isRepeat });
                    if (!CallbacksEnabled)
                        return;

                    using (callbackLock.EnterScope())
                    {
                        foreach (HotkeyCallback callback in hotkeyCallbacks)
                        {
                            if (isRepeat && !callback.AllowRepeats)
                                continue;

                            if (!pressedKeys.IsSupersetOf(callback.VKeys))
                                continue;

                            // Sometimes a keyUp event is not sent to the keyboard hook callback
                            // Remove any 'pressed' keys that aren't actually pressed anymore
                            foreach (VKey pressedKey in pressedKeys)
                            {
                                // The just pressed vkCode has not been registered by GetAsyncKeyState yet, so it is excluded
                                if (pressedKey != vkCode && NativeMethods.GetAsyncKeyState((int)pressedKey) == 0)
                                {
                                    // Remove the key when it is actually no longer pressed
                                    pressedKeys.Remove(pressedKey);
                                }
                            }

                            if (pressedKeys.SetEquals(callback.VKeys))
                            {
                                callback.InvokePressed();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WindowLogger.Write($"Uncaught exception when handling KeyEvent: {ex}");
                }
            });
        }

        private class KeyEvent
        {
            public IntPtr wParam;
            public KBDLLHOOKSTRUCT kbd;
        }
    }
}
