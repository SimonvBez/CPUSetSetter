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
            
            // Check for new version as an arg after restarting
            // after an update
            if (args.Length > 0)
            {
                app.UpdatedVersion = args[0];
            }
            
            app.Run();
        }
    }
}
