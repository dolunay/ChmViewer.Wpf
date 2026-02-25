using HtmlHelp;
using HtmlHelp.Wpf.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HtmlHelp.Wpf.Controls;

public sealed class ChmViewerViewModel : INotifyPropertyChanged, IDisposable
{
	private readonly Dispatcher _dispatcher;
	private ChmDocument _document;
	private string _documentId;
	private bool _disposed;
	private int _loadVersion;

	private IReadOnlyList<TOCItem> _tocItems = Array.Empty<TOCItem>();
	private TOCItem _selectedTocItem;
	private string _address = "about:blank";
	private bool _isBusy;

	public ChmViewerViewModel(Dispatcher dispatcher = null)
	{
		_dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public IReadOnlyList<TOCItem> TocItems
	{
		get => _tocItems;
		private set => SetProperty(ref _tocItems, value);
	}

	public TOCItem SelectedTocItem
	{
		get => _selectedTocItem;
		set
		{
			if (!SetProperty(ref _selectedTocItem, value))
				return;

			UpdateAddressFromSelection(value);
		}
	}

	public string Address
	{
		get => _address;
		private set => SetProperty(ref _address, value);
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set => SetProperty(ref _isBusy, value);
	}

	internal string DocumentId
	{
		get => _documentId;
		private set => SetProperty(ref _documentId, value);
	}

	public async Task LoadAsync(string chmFilePath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(chmFilePath);
		ThrowIfDisposed();

		var loadVersion = Interlocked.Increment(ref _loadVersion);

		await _dispatcher.InvokeAsync(() =>
		{
			ThrowIfDisposed();
			CloseCurrentDocumentCore();
			IsBusy = true;
		});

		ChmDocument doc = null;
		try
		{
			// HtmlHelp + CHM parsing is CPU/IO bound; keep UI responsive.
			doc = await Task.Run(() => new ChmDocument(chmFilePath), cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			doc?.Dispose();
			await _dispatcher.InvokeAsync(() => IsBusy = false);
			throw;
		}

		await _dispatcher.InvokeAsync(() =>
		{
			if (_disposed || loadVersion != _loadVersion || cancellationToken.IsCancellationRequested)
			{
				doc.Dispose();
				IsBusy = false;
				return;
			}

			_document = doc;
			DocumentId = Guid.NewGuid().ToString("N");
			ChmDocumentRegistry.Register(DocumentId, doc);

			TocItems = doc.TocItems.ToList();

			var startLocal = ChmUrlHelper.TryExtractLocalFromDefaultTopic(doc.DefaultTopicUrl)
				?? FindFirstNavigableLocal(TocItems);

			if (!string.IsNullOrWhiteSpace(startLocal))
				NavigateToLocal(startLocal);

			IsBusy = false;
		});
	}

	public void NavigateToLocal(string local)
	{
		if (string.IsNullOrWhiteSpace(local))
			return;

		if (DocumentId == null)
			return;

		local = ChmUrlHelper.NormalizeLocalPath(local);
		var url = $"http://chm.local/{DocumentId}/{local}";
		Debug.WriteLine($"[CefSharp][CHM] Navigate url={url}");
		Address = url;
	}

	public void Close()
	{
		if (_disposed)
			return;

		if (!_dispatcher.CheckAccess())
		{
			_dispatcher.Invoke(Close);
			return;
		}

		CloseCurrentDocumentCore();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		if (!_dispatcher.CheckAccess())
		{
			_dispatcher.Invoke(Dispose);
			return;
		}

		CloseCurrentDocumentCore();
		_disposed = true;
	}

	private void UpdateAddressFromSelection(TOCItem item)
	{
		if (item == null || DocumentId == null)
			return;

		if (string.IsNullOrWhiteSpace(item.Local))
			return;

		if (ChmUrlHelper.IsExternalUrl(item.Local))
		{
			Address = item.Local;
			return;
		}

		NavigateToLocal(item.Local);
	}

	private void CloseCurrentDocumentCore()
	{
		var currentId = DocumentId;
		if (currentId != null)
			ChmDocumentRegistry.Unregister(currentId);

		DocumentId = null;

		_document?.Dispose();
		_document = null;

		TocItems = Array.Empty<TOCItem>();
		SelectedTocItem = null;
		Address = "about:blank";
		IsBusy = false;
	}

	private static string FindFirstNavigableLocal(IReadOnlyList<TOCItem> tocItems)
	{
		foreach (var item in tocItems)
		{
			var local = FindFirstNavigableLocal(item);
			if (!string.IsNullOrWhiteSpace(local))
				return local;
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

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(ChmViewerViewModel));
	}

	private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}
