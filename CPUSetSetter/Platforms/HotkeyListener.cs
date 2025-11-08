using System.Collections.Immutable;


namespace CPUSetSetter.Platforms
{
    public static class HotkeyListener
    {
        public static bool CallbacksEnabled
        {
            get => Default.CallbacksEnabled;
            set => Default.CallbacksEnabled = value;
        }

        public static event EventHandler<KeyEventArgs> KeyPressed
        {
            add => Default.KeyPressed += value;
            remove => Default.KeyPressed -= value;
        }

        public static void AddCallback(HotkeyCallback callback) => Default.AddCallback(callback);

        public static void RemoveCallback(HotkeyCallback callback) => RemoveCallback(callback);

        private static IHotkeyListener? _default;

#if WINDOWS
        public static IHotkeyListener Default => _default ??= new HotkeyListenerWindows();
#endif
    }

    public interface IHotkeyListener
    {
        bool CallbacksEnabled { get; set; }
        event EventHandler<KeyEventArgs>? KeyPressed;
        void AddCallback(HotkeyCallback callback);
        void RemoveCallback(HotkeyCallback callback);
    }

    public class HotkeyCallback
    {
        public ImmutableArray<VKey> VKeys { get; set; }
        public bool AllowRepeats { get; set; }
        public event EventHandler<EventArgs>? Pressed;

        public HotkeyCallback(VKey[] keys, bool allowRepeats = false)
        {
            VKeys = ImmutableArray.Create(keys);
            AllowRepeats = allowRepeats;
        }

        public void InvokePressed()
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }
    }

    public class KeyEventArgs : EventArgs
    {
        public VKey Key { get; set; }
        public bool IsRepeat { get; set; }
    }
}
