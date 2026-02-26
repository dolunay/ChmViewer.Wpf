# ChmViewer

`ChmViewer` is a Windows WPF application for viewing the contents of a `.chm` help file. It also provides basic search/navigation capabilities inside the CHM.

This repository contains both the application (`app/`) and the supporting CHM reading/viewing libraries (`lib/`).

## Solution structure

Solution file: `ChmViewer.sln`

Projects:

- `app/ChmViewer` (WPF Application)
  - Target: `net10.0-windows`
	- Hosts the CefSharp-based WPF control `HtmlHelp.Wpf.Controls.ChmViewerControl`.
	- Supports opening a CHM file using the built-in toolbar **Open** button.
	- Supports opening a CHM file via command-line argument (pass a `.chm` file path).
	- Toolbar and status bar visibility can be controlled via `ChmViewerViewModel.ShowToolbar` and `ChmViewerViewModel.ShowStatusBar`.

- `app/ChmViewerTest` (WPF Application)
	- Target: `net10.0-windows`
	- Demonstrates additional/legacy flows (library-based searching, optional CHM decompile + file search).
	- Includes sample documents under `app/ChmViewerTest/Documents/`.

- `lib/HtmlHelp` (Core CHM reader library)
  - Target: `net10.0`
  - Opens CHM content, reads index/TOC, and performs searches.

- `lib/HtmlHelp.Wpf` (WPF integration)
  - Target: `net10.0-windows`
	- Provides `HtmlHelp.Wpf.Controls.ChmViewerControl` and `HtmlHelp.Wpf.Controls.ChmViewerViewModel` for embedding a CHM viewer in any WPF window.

- `lib/HtmlHelp.WinForms` (WinForms integration)
  - Target: `net10.0-windows`

## Viewer options in this repository

### Embedded CefSharp viewer (recommended)

The main viewer experience is implemented by `HtmlHelp.Wpf.Controls.ChmViewerControl`:

- TOC on the left
- CefSharp Chromium browser on the right
- Optional toolbar (Back/Forward/Reload/Open)
- Optional status bar

The control is driven by `HtmlHelp.Wpf.Controls.ChmViewerViewModel`.

### Legacy/demo flows (in `app/ChmViewerTest`)

`app/ChmViewerTest` contains additional flows used for testing/demonstration, including:

- In-CHM keyword search using `lib/HtmlHelp`.
- Optional decompile-to-HTML flow using Windows `hh.exe`.

Notes:

- The `hh.exe` approach only works on Windows.
- You need write permissions to the target folder.

## Dependencies (NuGet packages)

Packages used per project:

### `app/ChmViewer`

- `HtmlAgilityPack` (`1.11.24`) – HTML parsing/processing.
- `CefSharp.Wpf.NETCore` (`144.0.120`) – Chromium-based WPF browser control.

### `lib/HtmlHelp`

- `SharpZipLib` (`1.4.2`) – used for some content/stream operations.
- `System.Resources.Extensions` (`10.0.2`) – resource support.

### `lib/HtmlHelp.Wpf`

- `CefSharp.Wpf.NETCore` (`144.0.120`)
- `chromiumembeddedframework.runtime.win-x64` (`144.0.12`)
- `chromiumembeddedframework.runtime.win-x86` (`144.0.12`)

## Setup and build requirements

### Operating system

- Windows (required by WPF and `hh.exe`)

### Visual Studio

The `ChmViewer.sln` header shows `Visual Studio Version 18`, so:

- You need a Visual Studio version that supports `.NET 10` (often Preview).
- Ensure the **.NET Desktop Development** workload is installed.

### .NET SDK

- `app/ChmViewer`, `lib/HtmlHelp.Wpf`, `lib/HtmlHelp.WinForms`: `net10.0-windows`
- `lib/HtmlHelp`: `net10.0`

You need the `.NET 10 SDK` installed (may be Preview).

## Run with Visual Studio

1. Open `ChmViewer.sln`.
2. Set `ChmViewer` as the startup project.
3. Choose `x64` (recommended) or `x86`.
4. Press `F5`.
5. Select a `.chm` file using the toolbar **Open** button.

If you want to run the demo/test flows instead, set `ChmViewerTest` as the startup project.

## Build from the command line

From the repository root:

- `dotnet restore`
- `dotnet build ChmViewer.sln -c Release`

## Helper scripts

- `delete-bin-obj-folders.bat`: Removes `bin/` and `obj/` folders across all projects.

## Common issues

- **CefSharp dependencies missing / not working**: Ensure the selected platform (`x64`/`x86`) matches the runtime packages.
- **CefSharp cache locked**: Startup can fail if another process is already using the same cache directory (under `LocalAppData`).
- **WPF `WebBrowser` behavior**: The `WebBrowser` control depends on Windows/IE components; rendering behavior can vary by machine configuration.

## Publishing notes

`app/ChmViewer/ChmViewer.csproj` contains settings for single-file publish. When publishing with `PublishSingleFile=true`, the project is configured to use a dedicated `StartupObject` for CefSharp.
