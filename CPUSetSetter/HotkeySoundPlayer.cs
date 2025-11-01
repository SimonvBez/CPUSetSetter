using System.IO;
using System.Media;
using Application = System.Windows.Application;


namespace CPUSetSetter
{
    public class HotkeySoundPlayer
    {
        private readonly SoundPlayer _applied;
        private readonly SoundPlayer _cleared;

        public static HotkeySoundPlayer Default { get; } = new HotkeySoundPlayer();

        public HotkeySoundPlayer()
        {
            _applied = LoadSound("set_applied.wav");
            _cleared = LoadSound("set_cleared.wav");
        }

        private static SoundPlayer LoadSound(string resourceName)
        {
            using Stream resourceStream = Application.GetResourceStream(new Uri($"pack://application:,,,/CPUSetSetter;component/{resourceName}")).Stream;

            SoundPlayer player = new(resourceStream);
            player.Load();
            return player;
        }

        public void PlayApplied()
        {
            PlaySound(_applied);
        }

        public void PlayCleared()
        {
            PlaySound(_cleared);
        }

        public void PlayError()
        {
            if (!ConfigOld.Default.MuteHotkeySound)
            {
                SystemSounds.Hand.Play();
            }
        }

        private static void PlaySound(SoundPlayer player)
        {
            if (!ConfigOld.Default.MuteHotkeySound)
            {
                player.Play();
            }
        }
    }
}
