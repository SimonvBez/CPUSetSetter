using System.Windows.Controls;


namespace CPUSetSetter.UI.Tabs.Rules
{
    public partial class RulesTab : Grid
    {
        private readonly RulesTabViewModel viewModel;

        public RulesTab()
        {
            viewModel = new();
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
