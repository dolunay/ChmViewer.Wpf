using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ChmViewerTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly object LogLock = new();

        public App()
        {
            InitializeComponent();
			HookUnhandledExceptions();
            AppUtils.CefInitialize();
        }

		private void HookUnhandledExceptions()
		{
			DispatcherUnhandledException += OnDispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		}

		private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			try
			{
				WriteCrashLog("DispatcherUnhandledException", e.Exception);
				MessageBox.Show(e.Exception.ToString(), "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				// Keep the process alive so the exception becomes visible during debugging.
				e.Handled = true;
			}
		}

		private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is Exception ex)
				WriteCrashLog("AppDomain.UnhandledException", ex);
		}

		private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
			e.SetObserved();
		}

		private static void WriteCrashLog(string source, Exception exception)
		{
			try
			{
				var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChmViewerTest", "logs");
				Directory.CreateDirectory(baseDir);

				var file = Path.Combine(baseDir, "crash.log");
				var message = $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";

				lock (LogLock)
				{
					File.AppendAllText(file, message);
				}
			}
			catch
			{
				// ignore logging failures
			}
		}
    }
}
