using CommunityToolkit.Mvvm.ComponentModel;
using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using CPUSetSetter.Util;


namespace CPUSetSetter.UI.Tabs.Processes
{
    /// <summary>
    /// Represents a row in the Processes list
    /// </summary>
    public partial class ProcessListEntryViewModel : ObservableObject, IDisposable
    {
        private readonly IProcessHandler _processHandler;
        private readonly IGameModeService _gameModeService;
        private LogicalProcessorMask _lastAppliedMask = LogicalProcessorMask.NoMask;

        public uint Pid { get; }
        public string Name { get; }
        public string ImagePath { get; }
        public bool AutoReapply { get; set; }

        private bool GameModeForcesNoMask => _gameModeService.IsEnabled && !AppConfig.Instance.KeepMasksDuringGameMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CpuPercentageStr))]
        private double _cpuUsage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayedMask))]
        private LogicalProcessorMask _mask;

        public LogicalProcessorMask DisplayedMask
        {
            get => GameModeForcesNoMask ? LogicalProcessorMask.NoMask : (Mask ?? LogicalProcessorMask.NoMask);
            set
            {
                if (!GameModeForcesNoMask && value is not null)
                {
                    Mask = value;
                }
            }
        }

        public bool IsMaskAllowed => !GameModeForcesNoMask;

        [ObservableProperty]
        private bool _previousApplyFailed;

        public string CpuPercentageStr => CpuUsage == -1 ? "" : $"{CpuUsage * 100:F1}%";

        internal ProcessListEntryViewModel(ProcessInfo pInfo, IGameModeService gameModeService)
        {
            Pid = pInfo.PID;
            Name = pInfo.Name;
            ImagePath = pInfo.ImagePath;
            _processHandler = pInfo.ProcessHandler;
            _gameModeService = gameModeService;

            ProgramRule? programRule = RuleHelpers.GetProgramRuleOrNull(pInfo.ImagePath);
            programRule?.AddRunningProcess(this);

            LogicalProcessorMask mask = programRule?.Mask ?? LogicalProcessorMask.NoMask;
            SetMask(mask, false);
            _mask = mask; // _mask is already set by SetMask, this just suppresses a warning
            AutoReapply = programRule?.AutoReapply ?? false;

            CpuUsage = _processHandler.GetCpuUsage();
        }

        public void UpdateCpuUsage()
        {
            CpuUsage = _processHandler.GetCpuUsage();
        }

        public void ResetCpuUsageSampling()
        {
            _processHandler.ResetCpuUsageSampling();
        }

        public void ReapplyMask()
        {
            if (GameModeForcesNoMask || Mask.MaskType == MaskApplyType.NoMask)
            {
                return;
            }

            PreviousApplyFailed = !_processHandler.ReapplyMask(Mask);
        }

        public bool SetMask(LogicalProcessorMask newMask, bool updateRule)
        {
            if (newMask == _lastAppliedMask)
            {
                return !PreviousApplyFailed;
            }

            _lastAppliedMask = newMask;
            Mask = newMask;

            bool ruleSuccess = true;
            if (updateRule && ImagePath.Length != 0)
            {
                ProgramRule? programRule = RuleHelpers.GetProgramRuleOrNull(ImagePath);
                if (programRule is null)
                {
                    programRule = new(ImagePath, newMask, false, true);
                    AppConfig.Instance.ProgramRules.Add(programRule);
                }
                ruleSuccess = programRule.SetMask(newMask, true);
            }

            bool success = ApplyMaskConsideringGameMode(newMask);
            PreviousApplyFailed = !success;
            return success && ruleSuccess;
        }

        public void RefreshGameModeApply()
        {
            PreviousApplyFailed = !ApplyMaskConsideringGameMode(Mask);
        }

        public void UpdateVisualMask()
        {
            OnPropertyChanged(nameof(DisplayedMask));
            OnPropertyChanged(nameof(IsMaskAllowed));
        }

        partial void OnMaskChanged(LogicalProcessorMask? oldValue, LogicalProcessorMask newValue)
        {
            if (oldValue is not null)
            {
                oldValue.MaskChanged -= OnMaskEdited;
            }
            newValue.MaskChanged += OnMaskEdited;
            SetMask(newValue, true);
        }

        private void OnMaskEdited(object? sender, EventArgs e)
        {
            PreviousApplyFailed = !ApplyMaskConsideringGameMode(Mask);
        }

        private bool ApplyMaskConsideringGameMode(LogicalProcessorMask mask)
        {
            LogicalProcessorMask appliedMask = GameModeForcesNoMask ? LogicalProcessorMask.NoMask : mask;
            return _processHandler.ApplyMask(appliedMask);
        }

        public void Dispose()
        {
            RuleHelpers.GetProgramRuleOrNull(ImagePath)?.RemoveRunningProcess(this);
            Mask.MaskChanged -= OnMaskEdited;
            _processHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
