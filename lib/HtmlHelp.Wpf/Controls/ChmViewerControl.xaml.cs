using CefSharp;
using CefSharp.Handler;
using HtmlHelp.Wpf.Internal;
using HtmlHelp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HtmlHelp.Wpf.Controls;

public partial class ChmViewerControl : UserControl, IDisposable
{
	private ChmDocument _document;
	private string _documentId;
	private bool _disposed;

	public ChmViewerControl()
	{
		CefSharpInitializer.EnsureInitialized();

		InitializeComponent();
		Browser.RequestHandler = new ChmRequestHandler(this);
		Browser.LoadError += Browser_LoadError;
		Unloaded += OnUnloaded;
	}

	private void Browser_LoadError(object sender, LoadErrorEventArgs e)
	{
		// ERR_ABORTED is expected when navigating away before a request completes.
		// Keep this as debug output only.
		// Keep this as debug output only. This is a reusable library control.
		Debug.WriteLine($"[CefSharp][LoadError] {e.ErrorCode} url={e.FailedUrl} text={e.ErrorText} main={e.Frame?.IsMain}");
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

			if (_owner._documentId == null)
				return false;

			if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme.Equals("chm", StringComparison.OrdinalIgnoreCase))
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

			rewritten = $"chm://local/{_owner._documentId}/{local}";
			return true;
		}
	}

	public async Task LoadAsync(string chmFilePath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(chmFilePath);

		await Dispatcher.InvokeAsync(() =>
		{
			ThrowIfDisposed();
			CloseCurrentDocument();
		});

		// HtmlHelp + CHM parsing is CPU/IO bound; keep UI responsive.
		var doc = await Task.Run(() => new ChmDocument(chmFilePath), cancellationToken);

		await Dispatcher.InvokeAsync(() =>
		{
			ThrowIfDisposed();

			_document = doc;
			_documentId = Guid.NewGuid().ToString("N");
			ChmDocumentRegistry.Register(_documentId, doc);

			TocTree.ItemsSource = doc.TocItems.ToList();

			var startLocal = ChmUrlHelper.TryExtractLocalFromDefaultTopic(doc.DefaultTopicUrl)
				?? FindFirstNavigableLocal(TocTree.ItemsSource);

			if (!string.IsNullOrWhiteSpace(startLocal))
				NavigateToLocal(startLocal);
		});
	}

	private static string FindFirstNavigableLocal(object tocItemsSource)
	{
		if (tocItemsSource is not System.Collections.IEnumerable enumerable)
			return null;

		foreach (var item in enumerable)
		{
			if (item is TOCItem tocItem)
			{
				var local = FindFirstNavigableLocal(tocItem);
				if (!string.IsNullOrWhiteSpace(local))
					return local;
			}
		}

		return null;
	}

	private static string FindFirstNavigableLocal(TOCItem tocItem)
	{
		if (!string.IsNullOrWhiteSpace(tocItem.Local) && !ChmUrlHelper.IsExternalUrl(tocItem.Local))
			return tocItem.Local;

		if (tocItem.Children == null)
			return null;

		foreach (var child in tocItem.Children)
		{
			if (child is TOCItem childItem)
			{
				var local = FindFirstNavigableLocal(childItem);
				if (!string.IsNullOrWhiteSpace(local))
					return local;
			}
		}

		return null;
	}

	private void TocTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		if (_documentId == null)
			return;

		if (e.NewValue is not TOCItem item)
			return;

		if (string.IsNullOrWhiteSpace(item.Local))
			return;

		if (ChmUrlHelper.IsExternalUrl(item.Local))
		{
			Browser.Address = item.Local;
			return;
		}

		NavigateToLocal(item.Local);
	}

	private void NavigateToLocal(string local)
	{
		if (_documentId == null)
			return;

		local = ChmUrlHelper.NormalizeLocalPath(local);
		Debug.WriteLine($"[CefSharp][CHM] NavigateToLocal documentId={_documentId} local={local}");
		var url = $"http://chm.local/{_documentId}/{local}";
		Debug.WriteLine($"[CefSharp][CHM] Navigate url={url}");
		Browser.Address = url;
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		Dispose();
	}

	private void CloseCurrentDocument()
	{
		if (_documentId != null)
			ChmDocumentRegistry.Unregister(_documentId);

		_documentId = null;

		_document?.Dispose();
		_document = null;

		TocTree.ItemsSource = null;
		Browser.Address = "about:blank";
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(ChmViewerControl));
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		Unloaded -= OnUnloaded;
		Browser.LoadError -= Browser_LoadError;
		Browser.RequestHandler = null;

		CloseCurrentDocument();

		// Do not call Cef.Shutdown here; this is a library control and shutdown is process-wide.
	}
}
