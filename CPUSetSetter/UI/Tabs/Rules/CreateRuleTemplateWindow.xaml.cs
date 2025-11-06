using System.Windows;


namespace CPUSetSetter.UI.Tabs.Rules
{
    /// <summary>
    /// A Window that will prompt the user to input a new Rule Template
    /// </summary>
    public partial class CreateRuleTemplateWindow : Window
    {
        public CreateRuleTemplateWindow()
        {
            DataContext = new CreateRuleTemplateWindowViewModel(Close);
            InitializeComponent();
        }
    }
}
