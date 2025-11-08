using CPUSetSetter.Platforms;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;


namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    /// <summary>
    /// Shows the usage with a bar for every logical processor in the system, with a different color when a processor is parked
    /// Though technically not correct, "Core" just sounds a lot better than "logical processor"
    /// </summary>
    public partial class CoreUsageControl : UserControl
    {
        private static bool _isRunning = false;
        private static List<CoreUsage> coreUsages = CpuInfo.LogicalProcessorNames.Select(cpuName => new CoreUsage(cpuName)).ToList();

        // DependencyProperties
        public static readonly DependencyProperty BarBackgroundProperty =
            DependencyProperty.Register(
                nameof(BarBackground),
                typeof(Brush),
                typeof(CoreUsageControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(192, 192, 192))));

        public static readonly DependencyProperty BarForegroundProperty =
            DependencyProperty.Register(
                nameof(BarForeground),
                typeof(Brush),
                typeof(CoreUsageControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 173, 218))));

        public static readonly DependencyProperty BarBorderBrushProperty =
            DependencyProperty.Register(
                nameof(BarBorderBrush),
                typeof(Brush),
                typeof(CoreUsageControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(105, 105, 105))));

        public static readonly DependencyProperty BarParkedBackgroundProperty =
            DependencyProperty.Register(
                nameof(BarParkedBackground),
                typeof(Brush),
                typeof(CoreUsageControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 128, 128))));

        public static readonly DependencyProperty BarParkedForegroundProperty =
            DependencyProperty.Register(
                nameof(BarParkedForeground),
                typeof(Brush),
                typeof(CoreUsageControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(84, 94, 94))));

        // Properties
        public Brush BarBackground
        {
            get => (Brush)GetValue(BarBackgroundProperty);
            set => SetValue(BarBackgroundProperty, value);
        }

        public Brush BarForeground
        {
            get => (Brush)GetValue(BarForegroundProperty);
            set => SetValue(BarForegroundProperty, value);
        }

        public Brush BarBorderBrush
        {
            get => (Brush)GetValue(BarBorderBrushProperty);
            set => SetValue(BarBorderBrushProperty, value);
        }

        public Brush BarParkedBackground
        {
            get => (Brush)GetValue(BarParkedBackgroundProperty);
            set => SetValue(BarParkedBackgroundProperty, value);
        }

        public Brush BarParkedForeground
        {
            get => (Brush)GetValue(BarParkedForegroundProperty);
            set => SetValue(BarParkedForegroundProperty, value);
        }

        public CoreUsageControl()
        {
            InitializeComponent();

            coreUsagesItemsControl.ItemsSource = coreUsages;

            if (!_isRunning)
            {
                _isRunning = true;
                Task.Run(async () => await PerCoreUsageUpdateLoop(Dispatcher));
            }
        }

        private static async Task PerCoreUsageUpdateLoop(Dispatcher dispatcher)
        {
            try
            {
                await PerCoreUsageUpdateLoopInner(dispatcher);
            }
            catch (Exception ex)
            {
                WindowLogger.Write($"Error occurred in CoreUsage loop: {ex}");
            }
        }

        private static async Task PerCoreUsageUpdateLoopInner(Dispatcher dispatcher)
        {
            // Create the Utility% counters for each logical processor
            PerformanceCounter[] utilityCounters = new PerformanceCounter[coreUsages.Count];

            for (int i = 0; i < utilityCounters.Length; ++i)
            {
                utilityCounters[i] = new("Processor Information", "% Processor Utility", $"0,{i}");
            }

            // Create the Parking Status counters for each logical processor
            // We are making the assumption here that every logical processor will have a Parking Status counter.
            // If none or only some of the processors have a Parking Status, the parkingCounters will be set to null and they will not be checked
            PerformanceCounter[]? parkingCounters = new PerformanceCounter[coreUsages.Count];
            try
            {
                for (int i = 0; i < parkingCounters.Length; ++i)
                {
                    parkingCounters[i] = new("Processor Information", "Parking Status", $"0,{i}");
                }
            }
            catch (Exception)
            {
                // Parking counters may not exist on this system; leave null
                parkingCounters = null;
            }

            float[] utilityValues = new float[coreUsages.Count];
            bool[] parkedValues = new bool[coreUsages.Count];
            while (true)
            {
                for (int i = 0; i < coreUsages.Count; ++i)
                {
                    // Get the Utility% of each logical processor, and clamp it between 0.0-1.0
                    utilityValues[i] = Math.Clamp(utilityCounters[i].NextValue() / 100f, 0f, 1f);

                    // Get the Parking Status of each logical processor. 1.0 is parked, 0.0 is not parked.
                    if (parkingCounters is not null)
                        parkedValues[i] = parkingCounters[i].NextValue() > 0.5f; // treat >0.5 as parked
                    else
                        parkedValues[i] = false;
                }

                bool windowIsVisible = false;
                // Apply the new values on the dispatcher to make sure changes are done in the same UI frame
                await dispatcher.InvokeAsync(() =>
                {
                    windowIsVisible = App.Current.MainWindow.Visibility == Visibility.Visible;
                    for (int i = 0; i < coreUsages.Count; ++i)
                    {
                        coreUsages[i].Utility = utilityValues[i];
                        coreUsages[i].IsParked = parkedValues[i];
                    }
                });

                int delayTime = windowIsVisible ? 1000 : 5000; // Poll the CPU usage less often when not visible
                await Task.Delay(delayTime);
            }
        }
    }
}
