using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HtmlHelp.Wpf.Windows;

public partial class ChmViewerWindow : Window
{
	private bool _autoLoadPending;
	private bool _loaded;

	public ChmViewerWindow()
	{
		InitializeComponent();
		Loaded += OnLoaded;
	}

	public string ChmFilePath { get; set; }

	public Task LoadAsync(string chmFilePath, CancellationToken cancellationToken = default)
	{
		ChmFilePath = chmFilePath;
		_autoLoadPending = false;
		return Viewer.LoadAsync(chmFilePath, cancellationToken);
	}

	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (_loaded)
			return;

		_loaded = true;

		if (!_autoLoadPending)
			return;

		if (string.IsNullOrWhiteSpace(ChmFilePath))
			return;

		await Viewer.LoadAsync(ChmFilePath);
	}

	public void SetChmFilePathForAutoLoad(string chmFilePath)
	{
		ChmFilePath = chmFilePath;
		_autoLoadPending = true;
	}
}
