using CPUSetSetter.Config.Models;
using CPUSetSetter.Platforms;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;


namespace CPUSetSetter.UI.Tabs.Masks
{
    /// <summary>
    /// UserControl that shows a grid of checkboxes (one for each logical processor on the system), and a text field to input hotkeys in
    /// </summary>
    public partial class MaskEditorControl : UserControl, IDisposable
    {
        private bool _suppressInnerToOuterMaskChanges = false;
        private bool _hotkeyInputSelected = false;
        private readonly List<MaskBitViewModel> innerMask;

        // DependencyProperties
        public static readonly DependencyProperty BoolMaskProperty =
            DependencyProperty.Register(
                nameof(BoolMask),
                typeof(ObservableCollection<bool>),
                typeof(MaskEditorControl),
                new PropertyMetadata(null, OnMaskChanged));

        public static readonly DependencyProperty HotkeysProperty =
            DependencyProperty.Register(
                nameof(Hotkeys),
                typeof(ObservableCollection<VKey>),
                typeof(MaskEditorControl),
                new PropertyMetadata(null, OnHotkeysChanged));

        public static readonly DependencyProperty MaskTypeProperty =
            DependencyProperty.Register(
                nameof(MaskType),
                typeof(MaskApplyType?),
                typeof(MaskEditorControl),
                new FrameworkPropertyMetadata(null, OnMaskTypeChanged) { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        // Properties
        public ObservableCollection<bool>? BoolMask
        {
            get => (ObservableCollection<bool>?)GetValue(BoolMaskProperty);
            set => SetValue(BoolMaskProperty, value);
        }

        public ObservableCollection<VKey>? Hotkeys
        {
            get => (ObservableCollection<VKey>?)GetValue(HotkeysProperty);
            set => SetValue(HotkeysProperty, value);
        }

        public MaskApplyType? MaskType
        {
            get => (MaskApplyType?)GetValue(MaskTypeProperty);
            set => SetValue(MaskTypeProperty, value);
        }

        // PropertyChanged handlers
        private static void OnMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MaskEditorControl)d;
            // Unsubscribe from the old outer mask
            if (e.OldValue is ObservableCollection<bool> oldMask)
                oldMask.CollectionChanged -= control.OnOuterMaskChanged;

            // Subscribe to the new outer mask
            if (e.NewValue is ObservableCollection<bool> newMask)
            {
                if (newMask.Count == 0)
                {
                    // Hide the checkboxes when the outer Mask is empty
                    control.maskItemsControl.Visibility = Visibility.Hidden;
                }
                else if (newMask.Count != control.innerMask.Count)
                {
                    // Throw an error when the outer Mask length is incorrect
                    throw new ArgumentException($"Bound Mask has incorrect length. Was {newMask.Count} but should have been {control.innerMask.Count}");
                }
                else
                {
                    // Show the inner mask
                    control.maskItemsControl.Visibility = Visibility.Visible;
                    newMask.CollectionChanged += control.OnOuterMaskChanged;
                    // Copy the values from the new outer mask to the control's inner mask
                    control._suppressInnerToOuterMaskChanges = true; // Don't change the Outer Mask based on itself
                    for (int i = 0; i < newMask.Count; ++i)
                    {
                        control.innerMask[i].IsEnabled = newMask[i];
                    }
                    control._suppressInnerToOuterMaskChanges = false; // Re-enable the inner-to-outer changes again
                }
            }
            else
            {
                // Hide the checkboxes when the outer Mask is null
                control.maskItemsControl.Visibility = Visibility.Hidden;
            }
        }

        private static void OnHotkeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MaskEditorControl)d;
            // Unsubscribe from the old outer hotkeys
            if (e.OldValue is ObservableCollection<VKey> oldKeys)
                oldKeys.CollectionChanged -= control.OnOuterHotkeysChanged;

            // Subscribe to the new outer hotkeys
            if (e.NewValue is ObservableCollection<VKey> newKeys)
            {
                newKeys.CollectionChanged += control.OnOuterHotkeysChanged;
                control.hotkeyGrid.Visibility = Visibility.Visible;
            }
            else
            {
                control.hotkeyGrid.Visibility = Visibility.Hidden;
            }

            control.UpdateHotkeysDisplay();
        }

        private static void OnMaskTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // TODO: Handle change of MaskType
        }

        // CollectionChanged handlers
        private void OnOuterMaskChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // The outer mask was changed. Reflect this change into the inner mask
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                innerMask[e.OldStartingIndex].IsEnabled = BoolMask![e.OldStartingIndex];
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void OnOuterHotkeysChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateHotkeysDisplay();
        }

        private void UpdateHotkeysDisplay()
        {
            hotkeyTextBox.Text = Hotkeys is null || Hotkeys.Count == 0 ?
                "<no hotkey>"
                : string.Join("+", Hotkeys);
        }

        public MaskEditorControl()
        {
            InitializeComponent();

            // Set up the inner mask
            int div = 2;
            while (CpuInfo.LogicalProcessorCount / div > 16)
            {
                div += 2;
            }
            int columnCount = Math.Max(1, CpuInfo.LogicalProcessorCount / div);
            innerMask = Enumerable.Range(0, CpuInfo.LogicalProcessorCount).Select(i => new MaskBitViewModel(i)).ToList();

            // Subscribe to each inner mask's bit, so we can also change the outer mask when the inner changes
            foreach (MaskBitViewModel maskBit in innerMask)
            {
                maskBit.MaskChanged += OnInnerMaskBitChanged;
            }

            // Apply the innerMask to the Control's UI
            maskItemsControl.ItemsSource = innerMask.Chunk(columnCount);
            // Hide the checkboxes by default. They will become visible once an outer mask has been set
            maskItemsControl.Visibility = Visibility.Hidden;
            hotkeyGrid.Visibility = Visibility.Hidden;

            // Listen for key presses so the Hotkey input can be updated once it is focused
            HotkeyListener.KeyPressed += OnKeyPressed;
        }

        private void OnInnerMaskBitChanged(object? sender, MaskBitChangedEventArgs e)
        {
            // Apply a change to the inner mask to the outer mask
            if (BoolMask is not null && !_suppressInnerToOuterMaskChanges)
            {
                BoolMask[e.MaskBitIndex] = e.IsEnabled;
            }
        }

        private void OnKeyPressed(object? sender, Platforms.KeyEventArgs e)
        {
            if (_hotkeyInputSelected && Hotkeys is not null && !Hotkeys.Contains(e.Key))
            {
                Hotkeys.Add(e.Key);
            }
        }

        private void HotkeyInput_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _hotkeyInputSelected = true;
            HotkeyListener.CallbacksEnabled = false;
        }

        private void HotkeyInput_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _hotkeyInputSelected = false;
            HotkeyListener.CallbacksEnabled = true;
        }

        private void ClearHotkeys_Click(object sender, RoutedEventArgs e)
        {
            Hotkeys?.Clear();
        }

        public void Dispose()
        {
            if (BoolMask is not null)
                BoolMask.CollectionChanged -= OnOuterMaskChanged;
            if (Hotkeys is not null)
                Hotkeys.CollectionChanged -= OnOuterMaskChanged;

            foreach (MaskBitViewModel maskBit in innerMask)
            {
                maskBit.MaskChanged -= OnInnerMaskBitChanged;
            }
            innerMask.Clear();

            HotkeyListener.KeyPressed -= OnKeyPressed;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Toggle the MaskBit when the mouse is hovered over the UI element with LMB down
        /// </summary>
        private void MaskBitBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            Border border = (Border)sender;
            BulletDecorator enteredBulletDecorator = (BulletDecorator)border.Child;
            CheckBox cb = (CheckBox)enteredBulletDecorator.Bullet;
            if (e.LeftButton == MouseButtonState.Pressed)
                cb.IsChecked = !cb.IsChecked;
        }

        /// <summary>
        /// Toggle the MaskBit when the LMB is pressed down on the UI element
        /// </summary>
        private void MaskBitBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Border border = (Border)sender;
            BulletDecorator enteredBulletDecorator = (BulletDecorator)border.Child;
            CheckBox cb = (CheckBox)enteredBulletDecorator.Bullet;
            cb.IsChecked = !cb.IsChecked;
        }
    }
}
