using CommunityToolkit.Mvvm.ComponentModel;


namespace CPUSetSetter
{
    public partial class WindowLogger : ObservableObject
    {
        [ObservableProperty]
        private string _text = "";

        public static WindowLogger Default { get; } = new WindowLogger();

        public void Write(string message)
        {
            Text += message + "\n";
        }
    }
}
