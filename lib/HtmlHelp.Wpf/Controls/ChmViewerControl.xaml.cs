using CefSharp;
using CefSharp.Handler;
using HtmlHelp.Wpf.Internal;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HtmlHelp.Wpf.Controls;

public partial class ChmViewerControl : UserControl, IDisposable
{
	public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
		nameof(StatusText),
		typeof(string),
		typeof(ChmViewerControl),
		new PropertyMetadata("Ready"));

	public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
		nameof(IsLoading),
		typeof(bool),
		typeof(ChmViewerControl),
		new PropertyMetadata(false));

	public static readonly DependencyProperty CanGoBackProperty = DependencyProperty.Register(
		nameof(CanGoBack),
		typeof(bool),
		typeof(ChmViewerControl),
		new PropertyMetadata(false));

	public static readonly DependencyProperty CanGoForwardProperty = DependencyProperty.Register(
		nameof(CanGoForward),
		typeof(bool),
		typeof(ChmViewerControl),
		new PropertyMetadata(false));

	public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
		nameof(ViewModel),
		typeof(ChmViewerViewModel),
		typeof(ChmViewerControl),
		new PropertyMetadata(null, OnViewModelChanged));

	public static readonly DependencyProperty ChmFilePathProperty = DependencyProperty.Register(
		nameof(ChmFilePath),
		typeof(string),
		typeof(ChmViewerControl),
		new PropertyMetadata(null, OnChmFilePathChanged));

	private readonly ChmViewerViewModel _createdViewModel;
	private CancellationTokenSource _autoLoadCts;
	private bool _suppressAutoLoad;
	private bool _settingFallbackViewModel;
	private bool _disposed;
	private bool _ownsViewModel;
	private volatile string _documentIdForRequests;
	private ChmViewerViewModel _subscribedViewModel;

	public ChmViewerControl()
	{
		CefSharpInitializer.EnsureInitialized();

		InitializeComponent();

		_createdViewModel = new ChmViewerViewModel(Dispatcher);
		_ownsViewModel = true;
		SetCurrentValue(ViewModelProperty, _createdViewModel);
		AttachToViewModel(_createdViewModel);

		Browser.RequestHandler = new ChmRequestHandler(this);
		Browser.LoadError += Browser_LoadError;
		Browser.LoadingStateChanged += Browser_LoadingStateChanged;
		Browser.StatusMessage += Browser_StatusMessage;
		Unloaded += OnUnloaded;
	}

	public string StatusText
	{
		get => (string)GetValue(StatusTextProperty);
		private set => SetValue(StatusTextProperty, value);
	}

	public bool IsLoading
	{
		get => (bool)GetValue(IsLoadingProperty);
		private set => SetValue(IsLoadingProperty, value);
	}

	public bool CanGoBack
	{
		get => (bool)GetValue(CanGoBackProperty);
		private set => SetValue(CanGoBackProperty, value);
	}

	public bool CanGoForward
	{
		get => (bool)GetValue(CanGoForwardProperty);
		private set => SetValue(CanGoForwardProperty, value);
	}

	public ChmViewerViewModel ViewModel
	{
		get => (ChmViewerViewModel)GetValue(ViewModelProperty) ?? _createdViewModel;
		set => SetValue(ViewModelProperty, value);
	}

	public string ChmFilePath
	{
		get => (string)GetValue(ChmFilePathProperty);
		set => SetValue(ChmFilePathProperty, value);
	}

	private void Browser_LoadError(object sender, LoadErrorEventArgs e)
	{
		// ERR_ABORTED is expected when navigating away before a request completes.
		// Keep this as debug output only.
		// Keep this as debug output only. This is a reusable library control.
		Debug.WriteLine($"[CefSharp][LoadError] {e.ErrorCode} url={e.FailedUrl} text={e.ErrorText} main={e.Frame?.IsMain}");
	}

	private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
	{
		Dispatcher.BeginInvoke(() =>
		{
			if (_disposed)
				return;

			SetCurrentValue(IsLoadingProperty, e.IsLoading);
			SetCurrentValue(CanGoBackProperty, e.CanGoBack);
			SetCurrentValue(CanGoForwardProperty, e.CanGoForward);
		});
	}

	private void Browser_StatusMessage(object sender, StatusMessageEventArgs e)
	{
		Dispatcher.BeginInvoke(() =>
		{
			if (_disposed)
				return;

			SetCurrentValue(StatusTextProperty, e.Value ?? string.Empty);
		});
	}

	private void BackButton_Click(object sender, RoutedEventArgs e)
		=> Browser.Back();

	private void ForwardButton_Click(object sender, RoutedEventArgs e)
		=> Browser.Forward();

	private void ReloadButton_Click(object sender, RoutedEventArgs e)
		=> Browser.Reload();

	private void OpenButton_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			DefaultExt = ".chm",
			Filter = "CHM files (*.chm)|*.chm"
		};

		var result = dialog.ShowDialog();
		if (result != true)
			return;

		if (string.IsNullOrWhiteSpace(dialog.FileName) || !File.Exists(dialog.FileName))
			return;

		SetCurrentValue(ChmFilePathProperty, dialog.FileName);
	}

	private sealed class ChmRequestHandler(ChmViewerControl owner) : RequestHandler
	{
		private readonly ChmViewerControl _owner = owner;

		protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
		{
			if (request == null)
				return false;

			if (TryRewriteToChmUrl(request.Url, out var rewritten))
			{
				Debug.WriteLine($"[CefSharp][CHM] Rewrite url={request.Url} -> {rewritten}");
				frame.LoadUrl(rewritten);
				return true;
			}

			return false;
		}

		protected override bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
		{
			if (TryRewriteToChmUrl(targetUrl, out var rewritten))
			{
				Debug.WriteLine($"[CefSharp][CHM] Rewrite(new tab) url={targetUrl} -> {rewritten}");
				frame.LoadUrl(rewritten);
				return true;
			}

			return false;
		}

		private bool TryRewriteToChmUrl(string url, out string rewritten)
		{
			rewritten = null;

			if (string.IsNullOrWhiteSpace(url))
				return false;

			// IMPORTANT: Do not touch DependencyObject/DPs here. CefSharp may invoke this callback
			// on a non-UI thread, and accessing WPF DPs will throw VerifyAccess.
			var documentId = _owner._documentIdForRequests;
			if (string.IsNullOrWhiteSpace(documentId))
				return false;

			if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
				&& uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
				&& uri.Host.Equals("chm.local", StringComparison.OrdinalIgnoreCase))
				return false;

			// CHM HTML commonly links to topics/resources using MSITStore-style URLs.
			if (!url.Contains("::/", StringComparison.Ordinal)
				&& !url.StartsWith("mk:@MSITStore:", StringComparison.OrdinalIgnoreCase)
				&& !url.StartsWith("ms-its:", StringComparison.OrdinalIgnoreCase)
				&& !url.StartsWith("its:", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			var local = ChmUrlHelper.NormalizeLocalPath(url);
			if (string.IsNullOrWhiteSpace(local))
				return false;

			rewritten = $"http://chm.local/{documentId}/{local}";
			return true;
		}
	}

	public async Task LoadAsync(string chmFilePath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(chmFilePath);
		ThrowIfDisposed();

		_suppressAutoLoad = true;
		try
		{
			SetCurrentValue(ChmFilePathProperty, chmFilePath);
		}
		finally
		{
			_suppressAutoLoad = false;
		}

		await ViewModel.LoadAsync(chmFilePath, cancellationToken);
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		Dispose();
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(ChmViewerControl));
	}

	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((ChmViewerControl)d).OnViewModelChanged((ChmViewerViewModel)e.OldValue, (ChmViewerViewModel)e.NewValue);

	private void OnViewModelChanged(ChmViewerViewModel oldViewModel, ChmViewerViewModel newViewModel)
	{
		DetachFromViewModel(oldViewModel);
		oldViewModel?.Close();

		if (newViewModel == null)
		{
			if (_settingFallbackViewModel)
				return;

			_settingFallbackViewModel = true;
			try
			{
				SetCurrentValue(ViewModelProperty, _createdViewModel);
			}
			finally
			{
				_settingFallbackViewModel = false;
			}

			return;
		}

		AttachToViewModel(newViewModel);

		_ownsViewModel = ReferenceEquals(newViewModel, _createdViewModel);

		if (ReferenceEquals(DataContext, oldViewModel))
			DataContext = null;

		DataContext = newViewModel;
	}

	private void AttachToViewModel(ChmViewerViewModel viewModel)
	{
		if (viewModel == null)
			return;

		_subscribedViewModel = viewModel;
		_documentIdForRequests = viewModel.DocumentId;
		viewModel.PropertyChanged += ViewModel_PropertyChanged;
	}

	private void DetachFromViewModel(ChmViewerViewModel viewModel)
	{
		if (viewModel == null)
			return;

		if (!ReferenceEquals(_subscribedViewModel, viewModel))
			return;

		viewModel.PropertyChanged -= ViewModel_PropertyChanged;
		_subscribedViewModel = null;
		_documentIdForRequests = null;
	}

	private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (!string.Equals(e.PropertyName, nameof(ChmViewerViewModel.DocumentId), StringComparison.Ordinal))
			return;

		if (sender is ChmViewerViewModel vm)
			_documentIdForRequests = vm.DocumentId;
	}

	private static void OnChmFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((ChmViewerControl)d).OnChmFilePathChanged((string)e.NewValue);

	private async void OnChmFilePathChanged(string chmFilePath)
	{
		if (_disposed || _suppressAutoLoad)
			return;

		_autoLoadCts?.Cancel();
		_autoLoadCts?.Dispose();
		_autoLoadCts = null;

		if (string.IsNullOrWhiteSpace(chmFilePath))
		{
			ViewModel.Close();
			return;
		}

		_autoLoadCts = new CancellationTokenSource();
		try
		{
			SetCurrentValue(StatusTextProperty, "Loading...");
			await ViewModel.LoadAsync(chmFilePath, _autoLoadCts.Token);
			SetCurrentValue(StatusTextProperty, "Ready");
		}
		catch (OperationCanceledException)
		{
			// ignore
		}
		catch (ArgumentException ex)
		{
			Debug.WriteLine($"[CefSharp][CHM] AutoLoad failed path={chmFilePath} error={ex}");
			SetCurrentValue(StatusTextProperty, ex.Message);
		}
		catch (IOException ex)
		{
			Debug.WriteLine($"[CefSharp][CHM] AutoLoad failed path={chmFilePath} error={ex}");
			SetCurrentValue(StatusTextProperty, ex.Message);
		}
		catch (UnauthorizedAccessException ex)
		{
			Debug.WriteLine($"[CefSharp][CHM] AutoLoad failed path={chmFilePath} error={ex}");
			SetCurrentValue(StatusTextProperty, ex.Message);
		}
		catch (SecurityException ex)
		{
			Debug.WriteLine($"[CefSharp][CHM] AutoLoad failed path={chmFilePath} error={ex}");
			SetCurrentValue(StatusTextProperty, ex.Message);
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_autoLoadCts?.Cancel();
		_autoLoadCts?.Dispose();
		_autoLoadCts = null;

		Unloaded -= OnUnloaded;
		Browser.LoadError -= Browser_LoadError;
		Browser.LoadingStateChanged -= Browser_LoadingStateChanged;
		Browser.StatusMessage -= Browser_StatusMessage;
		Browser.RequestHandler = null;
		DetachFromViewModel(ViewModel);

		ViewModel?.Close();
		if (_ownsViewModel)
			ViewModel?.Dispose();

		// Do not call Cef.Shutdown here; this is a library control and shutdown is process-wide.
	}
}
