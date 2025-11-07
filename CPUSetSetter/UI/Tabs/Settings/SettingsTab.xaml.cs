using System.Windows.Controls;


namespace CPUSetSetter.UI.Tabs.Settings
{
    /// <summary>
    /// Interaction logic for SettingsTab.xaml
    /// </summary>
    public partial class SettingsTab : Grid
    {
        public SettingsTab()
        {
            DataContext = new SettingsTabViewModel();
            InitializeComponent();
        }
    }
}
