using Microsoft.VisualStudio.TestTools.UnitTesting;
using CPUSetSetter;
using System.Windows.Threading;
using System.Threading;

namespace CPUSetSetterTests
{
    [TestClass]
    public class CPUSetTests
    {
        [TestMethod]
        public void Remove_SelectedCpuSet_ShouldBeNull()
        {
            // Arrange
            var dispatcher = Dispatcher.CurrentDispatcher;
            var viewModel = new MainWindowViewModel(dispatcher);
            var cpuSet = new CPUSet("TestSet");
            Config.Default.CpuSets.Add(cpuSet);
            viewModel.SettingsSelectedCpuSet = cpuSet;

            // Act
            cpuSet.Remove();

            // Assert
            Assert.IsNull(viewModel.SettingsSelectedCpuSet, "SettingsSelectedCpuSet should be null after removing the selected CPUSet.");
        }
    }
}
