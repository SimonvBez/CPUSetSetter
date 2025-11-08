using System.Windows.Controls;


namespace CPUSetSetter.UI.Tabs.Masks
{
    public partial class MasksTab : Grid
    {
        private readonly MasksTabViewModel viewModel;

        public MasksTab()
        {
            viewModel = new();
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
