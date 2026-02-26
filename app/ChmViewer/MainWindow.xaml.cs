using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ChmViewer
{
    public partial class MainWindow : Window
    {
		private bool _autoLoadPending;
		private bool _loaded;

        public MainWindow()
        {
            InitializeComponent();
			Loaded += OnLoaded;
			TrySetChmPathFromCommandLineArgs();
        }

		public string ChmFilePath { get; set; }

		public Task LoadAsync(string chmFilePath, CancellationToken cancellationToken = default)
		{
			ChmFilePath = chmFilePath;
			_autoLoadPending = false;
			return Viewer.LoadAsync(chmFilePath, cancellationToken);
		}

		public void SetChmFilePathForAutoLoad(string chmFilePath)
		{
			ChmFilePath = chmFilePath;
			_autoLoadPending = true;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			if (_loaded)
				return;

			_loaded = true;

			if (!_autoLoadPending)
				return;

			if (string.IsNullOrWhiteSpace(ChmFilePath))
				return;

			Viewer.ChmFilePath = ChmFilePath;
		}

		private void TrySetChmPathFromCommandLineArgs()
		{
			string[] args = Environment.GetCommandLineArgs();
			if (args == null || args.Length <= 1)
				return;

			for (int i = 1; i < args.Length; i++)
			{
				string candidate = args[i];
				if (string.IsNullOrWhiteSpace(candidate))
					continue;

				if (!candidate.EndsWith(".chm", StringComparison.OrdinalIgnoreCase))
					continue;

				if (!File.Exists(candidate))
					continue;

				SetChmFilePathForAutoLoad(candidate);
				return;
			}
		}
    }
}
