using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HtmlHelp;
using HtmlHelp.ChmDecoding;

namespace HtmlHelp.Wpf.Internal;

internal sealed class ChmDocument : IDisposable
{
	private readonly HtmlHelpSystem _system;
	private readonly CHMFile _primaryFile;

	internal ChmDocument(string chmFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(chmFilePath);

		_system = new HtmlHelpSystem();
		HtmlHelpSystem.UrlPrefix = "mk:@MSITStore:";
		_system.OpenFile(chmFilePath);

		var files = _system.FileList;
		if (files is not { Length: > 0 })
			throw new InvalidOperationException("CHM file could not be opened.");

		_primaryFile = files[0];
	}

	internal string DefaultTopicUrl => _system.DefaultTopic;

	internal IEnumerable<TOCItem> TocItems
	{
		get
		{
			var toc = _system.TableOfContents?.TOC;
			if (toc == null)
				yield break;

			foreach (var item in toc)
			{
				if (item is TOCItem tocItem)
					yield return tocItem;
			}
		}
	}

	internal byte[] TryGetFileBytes(string localPath)
	{
		if (string.IsNullOrWhiteSpace(localPath))
			return null;

		localPath = ChmUrlHelper.NormalizeLocalPath(localPath);
		if (string.IsNullOrWhiteSpace(localPath))
			return null;

		try
		{
			return _primaryFile.ReadFileBytes(localPath);
		}
		catch (COMException)
		{
			return null;
		}
	}

	public void Dispose()
	{
		_system.CloseAllFiles();
	}
}
