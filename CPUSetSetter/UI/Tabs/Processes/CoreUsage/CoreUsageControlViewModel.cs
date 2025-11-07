using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    public partial class CoreUsageControlViewModel : ObservableObject
    {
        private readonly Dispatcher _dispatcher;

        public ObservableCollection<CoreUsage> CoreUsages { get; } = new(
            Enumerable.Range(0, Environment.ProcessorCount).Select(i => new CoreUsage(i))
        );

        [GeneratedRegex(@"^\d+,\d+$")]
        private static partial Regex ProcessorInfoRegex();

        public CoreUsageControlViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            Task.Run(PerCoreUsageUpdateLoop);
        }

        private async Task PerCoreUsageUpdateLoop()
        {
            // Use Windows per-processor performance counters: "Processor", "% Processor Time"; and parking from "Processor Information", "Parking Status".
            var usageCounters = new System.Diagnostics.PerformanceCounter[Environment.ProcessorCount];
            System.Diagnostics.PerformanceCounter[]? parkingCounters = null;
            try
            {
                for (int i = 0; i < usageCounters.Length; i++)
                {
                    usageCounters[i] = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    _ = usageCounters[i].NextValue();
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                var cat = new System.Diagnostics.PerformanceCounterCategory("Processor Information");
                var instanceNames = cat.GetInstanceNames()
                .Where(n => n != "_Total" && Regex.IsMatch(n, @"^\d+,\d+$"))
                .Select(n =>
                {
                    var parts = n.Split(',');
                    return new { Name = n, Node = int.Parse(parts[0]), Cpu = int.Parse(parts[1]) };
                })
                .OrderBy(x => x.Node).ThenBy(x => x.Cpu)
                .Select(x => x.Name)
                .ToArray();

                int count = Math.Min(instanceNames.Length, CoreUsages.Count);
                parkingCounters = new System.Diagnostics.PerformanceCounter[count];
                for (int i = 0; i < count; i++)
                {
                    parkingCounters[i] = new System.Diagnostics.PerformanceCounter("Processor Information", "Parking Status", instanceNames[i]);
                    _ = parkingCounters[i].NextValue();
                }
            }
            catch
            {
                // Parking counters may not exist; leave null
            }

            while (true)
            {
                float[] usageValues = new float[CoreUsages.Count];
                bool[] parkedValues = new bool[CoreUsages.Count];

                for (int i = 0; i < usageValues.Length; i++)
                {
                    float v = 0f;
                    try { v = usageCounters[i]?.NextValue() ?? 0f; } catch { }
                    usageValues[i] = Math.Clamp(v, 0f, 100f);

                    if (parkingCounters is not null && i < parkingCounters.Length)
                    {
                        try
                        {
                            float p = parkingCounters[i]?.NextValue() ?? 0f; // Typically 0 or 1
                            parkedValues[i] = p > 0.5f; // treat >0.5 as parked
                        }
                        catch
                        {
                            parkedValues[i] = false;
                        }
                    }
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    for (int i = 0; i < CoreUsages.Count; i++)
                    {
                        CoreUsages[i].UsagePercent = usageValues[i];
                        if (i < parkedValues.Length)
                            CoreUsages[i].IsParked = parkedValues[i];
                        else
                            CoreUsages[i].IsParked = false;
                    }
                });

                await Task.Delay(1000);
            }
        }
    }
}