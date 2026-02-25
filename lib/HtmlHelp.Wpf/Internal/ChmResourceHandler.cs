using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CefSharp;

namespace HtmlHelp.Wpf.Internal;

internal sealed class ChmResourceHandler : ResourceHandler
{
	private readonly ChmDocument _document;
	private readonly string _localPath;

	internal ChmResourceHandler(ChmDocument document, string localPath)
	{
		_document = document ?? throw new ArgumentNullException(nameof(document));
		_localPath = localPath ?? throw new ArgumentNullException(nameof(localPath));
	}

	public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(callback);

		// Do not use IRequest on background threads. Capture the values we need for logging.
		var url = request.Url;
		var resourceType = request.ResourceType;
		Debug.WriteLine($"[CefSharp][CHM] ProcessRequest url={url} resourceType={resourceType} localPath={_localPath}");

		// Handle synchronously and return Continue; this avoids cross-thread access issues
		// and ensures response headers/body are ready when CEF reads them.
		using (callback)
		{
			try
			{
				var normalized = ChmUrlHelper.NormalizeLocalPath(_localPath);
				if (string.IsNullOrWhiteSpace(normalized))
				{
					Debug.WriteLine($"[CefSharp][CHM] 404 url={url} resourceType={resourceType} localPath={_localPath}");

					StatusCode = 404;
					MimeType = "text/plain";
					Charset = "utf-8";
					var payload = Encoding.UTF8.GetBytes("Not Found");
					ResponseLength = payload.Length;
					Stream = new MemoryStream(payload, writable: false);
					callback.Continue();
					return CefReturnValue.Continue;
				}

				var bytes = _document.TryGetFileBytes(normalized);
				if (bytes == null)
				{
					Debug.WriteLine($"[CefSharp][CHM] 404 url={url} resourceType={resourceType} localPath={_localPath} normalized={normalized}");

					StatusCode = 404;
					MimeType = "text/plain";
					Charset = "utf-8";
					var payload = Encoding.UTF8.GetBytes("Not Found");
					ResponseLength = payload.Length;
					Stream = new MemoryStream(payload, writable: false);
					callback.Continue();
					return CefReturnValue.Continue;
				}

				StatusCode = 200;
				MimeType = GetMimeTypeFromPath(normalized, resourceType);
				Charset = MimeType == "text/html" ? "utf-8" : null;
				ResponseLength = bytes.Length;
				Stream = new MemoryStream(bytes, writable: false);
				Debug.WriteLine($"[CefSharp][CHM] 200 url={url} resourceType={resourceType} normalized={normalized} mime={MimeType} length={bytes.Length}");
				callback.Continue();
				return CefReturnValue.Continue;
			}
			catch (InvalidOperationException ex)
			{
				Debug.WriteLine($"[CefSharp][CHM] 500 url={url} resourceType={resourceType} localPath={_localPath} error={ex.GetType().Name}:{ex.Message}");
				StatusCode = 500;
				MimeType = "text/plain";
				Charset = "utf-8";
				var payload = Encoding.UTF8.GetBytes("Internal Server Error");
				ResponseLength = payload.Length;
				Stream = new MemoryStream(payload, writable: false);
				callback.Continue();
				return CefReturnValue.Continue;
			}
			catch (COMException ex)
			{
				Debug.WriteLine($"[CefSharp][CHM] 500 url={url} resourceType={resourceType} localPath={_localPath} error={ex.GetType().Name}:0x{ex.ErrorCode:X8}:{ex.Message}");
				StatusCode = 500;
				MimeType = "text/plain";
				Charset = "utf-8";
				var payload = Encoding.UTF8.GetBytes("Internal Server Error");
				ResponseLength = payload.Length;
				Stream = new MemoryStream(payload, writable: false);
				callback.Continue();
				return CefReturnValue.Continue;
			}
			catch (IOException ex)
			{
				Debug.WriteLine($"[CefSharp][CHM] 500 url={url} resourceType={resourceType} localPath={_localPath} error={ex.GetType().Name}:{ex.Message}");
				StatusCode = 500;
				MimeType = "text/plain";
				Charset = "utf-8";
				var payload = Encoding.UTF8.GetBytes("Internal Server Error");
				ResponseLength = payload.Length;
				Stream = new MemoryStream(payload, writable: false);
				callback.Continue();
				return CefReturnValue.Continue;
			}
		}
	}

	private static string GetMimeTypeFromPath(string path, ResourceType resourceType)
	{
		if (string.IsNullOrWhiteSpace(path))
			return resourceType is ResourceType.MainFrame or ResourceType.SubFrame
				? "text/html"
				: "application/octet-stream";

		// Avoid `Path.GetExtension` here. CHM content can contain URL-like paths that aren't valid
		// Windows file system paths, which can make `Path` APIs throw.
		var queryIdx = path.IndexOfAny(['?', '#']);
		if (queryIdx >= 0)
			path = path[..queryIdx];

		var lastSlash = path.LastIndexOf('/');
		var lastDot = path.LastIndexOf('.');
		if (lastDot < 0 || lastDot < lastSlash)
			return resourceType is ResourceType.MainFrame or ResourceType.SubFrame
				? "text/html"
				: "application/octet-stream";

		var ext = path[lastDot..];
		if (string.IsNullOrWhiteSpace(ext))
			return resourceType is ResourceType.MainFrame or ResourceType.SubFrame
				? "text/html"
				: "application/octet-stream";

		switch (ext.ToLowerInvariant())
		{
			case ".htm":
			case ".html":
				return "text/html";
			case ".css":
				return "text/css";
			case ".js":
				return "text/javascript";
			case ".png":
				return "image/png";
			case ".jpg":
			case ".jpeg":
				return "image/jpeg";
			case ".gif":
				return "image/gif";
			case ".svg":
				return "image/svg+xml";
			case ".ico":
				return "image/x-icon";
			case ".xml":
				return "application/xml";
			case ".json":
				return "application/json";
			case ".txt":
				return "text/plain";
			default:
				return resourceType is ResourceType.MainFrame or ResourceType.SubFrame
					? "text/html"
					: "application/octet-stream";
		}
	}
}
