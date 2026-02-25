using System;

namespace HtmlHelp.Wpf.Internal;

internal static class ChmUrlHelper
{
	internal static bool IsExternalUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return false;

		return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			|| url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
			|| url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
			|| url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
	}

	internal static string TryExtractLocalFromDefaultTopic(string defaultTopicUrl)
	{
		if (string.IsNullOrWhiteSpace(defaultTopicUrl))
			return null;

		var idx = defaultTopicUrl.IndexOf("::/", StringComparison.Ordinal);
		if (idx < 0)
			return null;

		var local = defaultTopicUrl[(idx + 3)..];
		if (local.StartsWith('/'))
			local = local[1..];

		return string.IsNullOrWhiteSpace(local) ? null : local;
	}

	internal static string NormalizeLocalPath(string localPath)
	{
		if (string.IsNullOrWhiteSpace(localPath))
			return null;

		// HtmlHelp often returns locals in the form:
		//   mk:@MSITStore:C:\path\file.chm::/topic.html
		// or:
		//   file.chm::/topic.html
		// We only need the CHM-internal part (after ::/).
		var idx = localPath.IndexOf("::/", StringComparison.Ordinal);
		if (idx >= 0)
			localPath = localPath[(idx + 3)..];

		// CEF hands paths in URL form; CHM locals are generally stored without leading '/'
		while (localPath.Length > 0 && (localPath[0] == '/' || localPath[0] == '\\'))
			localPath = localPath[1..];

		// Avoid Windows path escaping; CHM storage uses forward slashes.
		localPath = localPath.Replace('\\', '/');

		return string.IsNullOrWhiteSpace(localPath) ? null : localPath;
	}
}
