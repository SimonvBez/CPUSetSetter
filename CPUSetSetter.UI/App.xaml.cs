using System.Globalization;
using System.Windows;
using System.Windows.Markup;


namespace CPUSetSetter.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void OnStartup(object sender, StartupEventArgs e)
        {
            if (Environment.ProcessorCount > 64)
            {
                throw new NotImplementedException("More than 64 logical CPU cores are not supported");
            }

            SetAppCulture();
        }

        private static void SetAppCulture()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag))
            );
        }
    }
}

/*
 * TODO:
 * - Program icon
 * - Add tray icon and starting as minimized
 * - Low priority: Add list of saved process settings
 */
