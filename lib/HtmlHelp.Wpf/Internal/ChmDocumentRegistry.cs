using System;
using System.Collections.Concurrent;

namespace HtmlHelp.Wpf.Internal;

internal static class ChmDocumentRegistry
{
	private static readonly ConcurrentDictionary<string, WeakReference<ChmDocument>> Documents = new();

	internal static void Register(string documentId, ChmDocument document)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);

		Documents[documentId] = new WeakReference<ChmDocument>(document);
	}

	internal static void Unregister(string documentId)
	{
		if (string.IsNullOrWhiteSpace(documentId))
			return;

		Documents.TryRemove(documentId, out _);
	}

	internal static bool TryGet(string documentId, out ChmDocument document)
	{
		document = null;
		if (string.IsNullOrWhiteSpace(documentId))
			return false;

		if (!Documents.TryGetValue(documentId, out var weakRef))
			return false;

		if (!weakRef.TryGetTarget(out var target))
		{
			Documents.TryRemove(documentId, out _);
			return false;
		}

		document = target;
		return true;
	}
}
