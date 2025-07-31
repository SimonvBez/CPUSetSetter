using CPUSetLib;


namespace ConsoleTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            CPUSetSetter setter = new();

            setter.OnNewProcessSpawned += (_, e) =>
            {
                Console.WriteLine($"New process spawned! Name={e.Process.ExecutableName}, Path={e.Process.FullPath}");
            };

            setter.Start();

            // Wait indefinitly, only handling events
            ManualResetEvent waitEvent = new ManualResetEvent(false);
            waitEvent.WaitOne();
        }
    }
}
