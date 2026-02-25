using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CefSharp;
using CefSharp.Wpf;

namespace HtmlHelp.Wpf.Internal;

internal static class CefSharpInitializer
{
    private static int _initialized;

    internal static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        Debug.WriteLine("[CefSharp][Init] EnsureInitialized");

        // In some hosting scenarios a ChromiumWebBrowser may have already triggered Cef initialization.
        // Cef.Initialize can only be called once per process, so we must guard with Cef.IsInitialized.
        if (Cef.IsInitialized == true)
        {
            Debug.WriteLine("[CefSharp][Init] Cef.IsInitialized=true; registering scheme handler factory only");
            // Registering the factory is safe even after initialization.
            // Use an existing standard scheme so we can register even when CEF was initialized elsewhere.
            Cef.GetGlobalRequestContext().RegisterSchemeHandlerFactory("http", "chm.local", new ChmSchemeHandlerFactory());
            return;
        }

        var settings = new CefSettings
        {
            // Put cache under %LOCALAPPDATA% to avoid writing into application folder.
            CachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HtmlHelp.Wpf",
                "CefSharpCache")
        };

        // When using the runtime-pack style layout, CEF native resources are under
        // `runtimes/<rid>/native`. Help CefSharp locate them explicitly.
        var rid = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        var nativeDir = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
        if (Directory.Exists(nativeDir))
        {
            settings.ResourcesDirPath = nativeDir;

            var localesDir = Path.Combine(nativeDir, "locales");
            if (Directory.Exists(localesDir))
                settings.LocalesDirPath = localesDir;

            var subProcessExe = Path.Combine(nativeDir, "CefSharp.BrowserSubprocess.exe");
            if (File.Exists(subProcessExe))
                settings.BrowserSubprocessPath = subProcessExe;
        }

        Debug.WriteLine("[CefSharp][Init] Calling Cef.Initialize");

        Cef.Initialize(settings);
        Debug.WriteLine("[CefSharp][Init] Cef.Initialize completed");

        // Register handler for a dedicated domain on a standard scheme.
        Cef.GetGlobalRequestContext().RegisterSchemeHandlerFactory("http", "chm.local", new ChmSchemeHandlerFactory());
    }
}
