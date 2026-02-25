using System.Windows;

namespace ChmViewerTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            AppUtils.CefInitialize();
        }
    }
}
