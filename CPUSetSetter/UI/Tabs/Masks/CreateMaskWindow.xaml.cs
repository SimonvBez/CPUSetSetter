using System.ComponentModel;
using System.Windows;


namespace CPUSetSetter.UI.Tabs.Masks
{
    /// <summary>
    /// A Window that will prompt the user to input a new logical processor mask
    /// </summary>
    public partial class CreateMaskWindow : Window
    {
        public CreateMaskWindow()
        {
            DataContext = new CreateMaskWindowViewModel(() => DialogResult = true);
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            maskEditor.Dispose();
            base.OnClosing(e);
        }
    }
}
