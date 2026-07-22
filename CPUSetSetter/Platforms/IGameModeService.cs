namespace CPUSetSetter.Platforms
{
    internal interface IGameModeService
    {
        bool IsEnabled { get; }

        event EventHandler<GameModeStateChangedEventArgs>? StateChanged;

        bool TrySetEnabled(bool isEnabled);
        Task MonitorAsync(CancellationToken cancellationToken);
    }

    internal sealed class GameModeStateChangedEventArgs : EventArgs
    {
        public bool IsEnabled { get; }

        public GameModeStateChangedEventArgs(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }
}
