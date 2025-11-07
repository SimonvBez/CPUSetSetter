using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CPUSetSetter.UI.Tabs.Processes.CoreUsage
{
    /// <summary>
    /// Interaction logic for CoreUsageControl.xaml
    /// </summary>
    public partial class CoreUsageControl : System.Windows.Controls.UserControl
    {
        public CoreUsageControl()
        {
            InitializeComponent();
            DataContext = new CoreUsageControlViewModel();
        }
    }
}
