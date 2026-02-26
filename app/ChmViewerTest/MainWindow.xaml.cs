using Microsoft.Win32;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ChmViewerTest.Windows;

namespace ChmViewerTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel readerVM;
        private const string InternetExplorerRootKey = @"Software\Microsoft\Internet Explorer";
        private const string BrowserEmulationKey = InternetExplorerRootKey + @"\Main\FeatureControl\FEATURE_BROWSER_EMULATION";
		private string _libraryChmFileName;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void HelpSearchButton_Click(object sender, RoutedEventArgs e)
        {
            DataTable dataTable = readerVM.Search(HelpSearchBox.Text);
            if (dataTable.Rows.Count > 0)
            {
                HelpBrowser.Navigated -= HelpBrowser_Navigated;
                HelpBrowser.Navigated += HelpBrowser_Navigated;
                HelpBrowser.Navigate(dataTable.Rows[0]["URL"].ToString());
                ErrorBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                HelpBrowser.Navigate("about:blank");
                ErrorBlock.Visibility = Visibility.Visible;
            }
        }

        private void HelpBrowser_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            SetSilent(HelpBrowser, true);
        }

        public static void SetSilent(WebBrowser browser, bool silent)
        {
            if (browser == null)
                throw new ArgumentNullException("browser");

            // get an IWebBrowser2 from the document
            IOleServiceProvider sp = browser.Document as IOleServiceProvider;
            if (sp != null)
            {
                Guid IID_IWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
                Guid IID_IWebBrowser2 = new Guid("D30C1661-CDAF-11d0-8A3E-00C04FC9E26E");

                object webBrowser;
                sp.QueryService(ref IID_IWebBrowserApp, ref IID_IWebBrowser2, out webBrowser);
                if (webBrowser != null)
                {
                    webBrowser.GetType().InvokeMember("Silent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.PutDispProperty, null, webBrowser, new object[] { silent });
                }
            }
        }

        private async Task Extract(string fileName, string targetFolder)
        {
            await Task.Run(() =>
            {
                Process myProcess = new Process();
                myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.StartInfo.UseShellExecute = false;
                myProcess.StartInfo.FileName = "cmd.exe";
                myProcess.StartInfo.Arguments = "/c " + $"hh.exe - decompile {targetFolder} {fileName}";
                myProcess.EnableRaisingEvents = true;
                myProcess.Start();
                myProcess.WaitForExit();
            });
        }

        private void HtmlSearchButton_Click(object sender, RoutedEventArgs e)
        {
            string[] files = Directory.GetFiles(targetFolder, "*.htm", SearchOption.AllDirectories);
            var file = files.FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains(HtmlSearchBox.Text.ToLower()));
            if (file != null)
            {
                HtmlBrowser.Navigate(file);
                HtmlErrorBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                HtmlBrowser.Navigate("about:blank");
                HtmlErrorBlock.Visibility = Visibility.Visible;
            }
        }

        private static bool SetBrowserEmulationVersion(BrowserEmulationVersion browserEmulationVersion)
        {
            bool result;

            result = false;

            try
            {
                RegistryKey key;

                key = Registry.CurrentUser.OpenSubKey(BrowserEmulationKey, true);

                if (key != null)
                {
                    string programName;

                    programName = Path.GetFileName(Environment.GetCommandLineArgs()[0]);

                    if (browserEmulationVersion != BrowserEmulationVersion.Default)
                    {
                        // if it's a valid value, update or create the value
                        key.SetValue(programName, (int)browserEmulationVersion, RegistryValueKind.DWord);
                    }
                    else
                    {
                        // otherwise, remove the existing value
                        key.DeleteValue(programName, false);
                    }

                    result = true;
                }
            }
            catch (SecurityException)
            {
                // The user does not have the permissions required to read from the registry key.
            }
            catch (UnauthorizedAccessException)
            {
                // The user does not have the necessary registry rights.
            }

            return result;
        }

        private static int GetInternetExplorerMajorVersion()
        {
            int result;

            result = 0;

            try
            {
                RegistryKey key;

                key = Registry.LocalMachine.OpenSubKey(InternetExplorerRootKey);

                if (key != null)
                {
                    object value;

                    value = key.GetValue("svcVersion", null) ?? key.GetValue("Version", null);

                    if (value != null)
                    {
                        string version;
                        int separator;

                        version = value.ToString();
                        separator = version.IndexOf('.');
                        if (separator != -1)
                        {
                            int.TryParse(version.Substring(0, separator), out result);
                        }
                    }
                }
            }
            catch (SecurityException)
            {
                // The user does not have the permissions required to read from the registry key.
            }
            catch (UnauthorizedAccessException)
            {
                // The user does not have the necessary registry rights.
            }

            return result;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog() { DefaultExt = ".chm", Filter = "CHM files (*.chm) | *.chm" };
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                string fileName = openFileDialog.FileName;
                _libraryChmFileName = fileName;
                SearchFileNameTextBlock.Text = fileName;
                readerVM = new MainWindowViewModel(fileName);
                HelpSearchButton.IsEnabled = true;
				OpenCefViewerButton.IsEnabled = true;
            }
        }

		private async void OpenCefViewerButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(_libraryChmFileName) || !File.Exists(_libraryChmFileName))
				{
					MessageBox.Show(this, "Please select a CHM file first.", "CHM Viewer", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				var window = new ChmViewerWindow
				{
					Owner = this,
					WindowStartupLocation = WindowStartupLocation.CenterOwner
				};

				window.SetChmFilePathForAutoLoad(_libraryChmFileName);
				window.Show();
			}
			catch (InvalidOperationException ex)
			{
				MessageBox.Show(this, ex.Message, "CHM Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch (IOException ex)
			{
				MessageBox.Show(this, ex.Message, "CHM Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch (UnauthorizedAccessException ex)
			{
				MessageBox.Show(this, ex.Message, "CHM Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

        private string fileName;
        private string targetFolder;

        private void HtmlOpenButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog() { DefaultExt = ".chm", Filter = "CHM files (*.chm) | *.chm" };
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                fileName = openFileDialog.FileName;
                FileNameTextBlock.Text = fileName;
                HtmlSearchButton.IsEnabled = false;
            }
        }

        private void HtmlTargetButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult folderResult = folderBrowserDialog.ShowDialog();
            if (folderResult == System.Windows.Forms.DialogResult.OK)
            {
                targetFolder = folderBrowserDialog.SelectedPath;
                FolderNameTextBlock.Text = targetFolder;
                HtmlSearchButton.IsEnabled = false;
            }
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            HelpProgress.Visibility = Visibility.Visible;
            await Extract(fileName, targetFolder);
            HelpProgress.Visibility = Visibility.Collapsed;
            HtmlSearchButton.IsEnabled = true;
        }
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleServiceProvider
    {
        [PreserveSig]
        int QueryService([In] ref Guid guidService, [In] ref Guid riid, [MarshalAs(UnmanagedType.IDispatch)] out object ppvObject);
    }

    public enum BrowserEmulationVersion
    {
        Default = 0,
        Version7 = 7000,
        Version8 = 8000,
        Version8Standards = 8888,
        Version9 = 9000,
        Version9Standards = 9999,
        Version10 = 10000,
        Version10Standards = 10001,
        Version11 = 11000,
        Version11Edge = 11001
    }
}
