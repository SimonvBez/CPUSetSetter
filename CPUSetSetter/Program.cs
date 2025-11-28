using Velopack;

namespace CPUSetSetter
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            App app = new();
            app.Run();
        }
    }
}
