using System.Windows;


namespace CPUSetSetter.UI.Tabs.Rules
{
    /// <summary>
    /// A Window that will prompt the user to input a new Program Rule
    /// </summary>
    public partial class CreateProgramRuleWindow : Window
    {
        public CreateProgramRuleWindow()
        {
            DataContext = new CreateProgramRuleWindowViewModel(Close);
            InitializeComponent();
        }
    }
}
