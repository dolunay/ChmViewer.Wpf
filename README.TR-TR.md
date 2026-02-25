# ChmViewer

`ChmViewer`, Windows üzerinde çalışan bir WPF uygulamasıdır. Bir `.chm` yardım dosyasının içeriğini okuyup görüntüler ve içerik içinde arama yapmanıza yardımcı olur.

Bu repo hem uygulamayı (`app/`) hem de CHM okuma/görüntüleme için kullanılan kütüphaneleri (`lib/`) içerir.

## Çözüm yapısı

Çözüm dosyası: `ChmViewer.sln`

Projeler:

- `app/ChmViewer` (WPF Uygulama)
  - Hedef: `net10.0-windows`
  - CHM seçme, arama ve görüntüleme UI akışı.
  - WPF `WebBrowser` (IE tabanlı) ile basit görüntüleme ve ek olarak CefSharp tabanlı görüntüleyici.

- `lib/HtmlHelp` (Çekirdek CHM okuma kütüphanesi)
  - Hedef: `net10.0`
  - CHM içeriğini açma, indeks/TOC okuma ve arama.

- `lib/HtmlHelp.Wpf` (WPF entegrasyonu)
  - Hedef: `net10.0-windows`
  - WPF tarafında CHM içeriğini görüntülemek için yardımcı sınıflar ve CefSharp bağımlılıkları.

- `lib/HtmlHelp.WinForms` (WinForms entegrasyonu)
  - Hedef: `net10.0-windows`

## Uygulamada kullanılan yöntemler

Uygulama iki farklı yaklaşımı örnekler:

### Yöntem 1: CHM içi arama (kütüphane tabanlı)

`lib/HtmlHelp` içindeki CHM okuma altyapısı ile anahtar kelime araması yapılır ve bulunan içeriğin URL’si UI’da açılır.

Bu akış uygulamada `ChmViewerViewModel` üzerinden çalışır ve sonuçları `DataTable` olarak döndürür.

### Yöntem 2: CHM’i HTML’e çıkarma + dosya arama

Windows’in `hh.exe` aracı kullanılarak CHM dosyası seçilen bir klasöre “decompile” edilir, ardından `.htm` dosyaları içinde basit bir dosya adı aramasıyla hedef içerik bulunup görüntülenir.

Notlar:

- Bu yöntem `hh.exe` çağırdığı için yalnızca Windows’ta çalışır.
- Çıkarma işlemi için hedef klasöre yazma izniniz olmalıdır.

## Bağımlılıklar (NuGet paketleri)

Paketler proje bazında aşağıdaki gibidir:

### `app/ChmViewer`

- `HtmlAgilityPack` (`1.11.24`) – HTML işleme/parsing.
- `CefSharp.Wpf.NETCore` (`144.0.120`) – Chromium tabanlı WPF tarayıcı kontrolü.

### `lib/HtmlHelp`

- `SharpZipLib` (`1.4.2`) – bazı içerik/stream işlemleri.
- `System.Resources.Extensions` (`10.0.2`) – kaynak/Resource desteği.

### `lib/HtmlHelp.Wpf`

- `CefSharp.Wpf.NETCore` (`144.0.120`)
- `chromiumembeddedframework.runtime.win-x64` (`144.0.12`)
- `chromiumembeddedframework.runtime.win-x86` (`144.0.12`)

## Kurulum ve derleme gereksinimleri

### İşletim sistemi

- Windows (WPF ve `hh.exe` bağımlılığı nedeniyle)

### Visual Studio

`ChmViewer.sln` başlığında `Visual Studio Version 18` görünüyor. Bu nedenle:

- `.NET 10` destekleyen bir Visual Studio sürümü (çoğu senaryoda Preview) gerekir.
- Visual Studio kurulumunda **.NET Desktop Development** workload’ünün yüklü olması gerekir.

### .NET SDK

- `app/ChmViewer`, `lib/HtmlHelp.Wpf`, `lib/HtmlHelp.WinForms`: `net10.0-windows`
- `lib/HtmlHelp`: `net10.0`

Makinenizde `.NET 10 SDK` (Preview olabilir) kurulu olmalıdır.

## Visual Studio ile çalıştırma

1. `ChmViewer.sln` dosyasını açın.
2. Başlangıç projesi olarak `ChmViewer` projesini seçin.
3. Platform olarak `x64` (önerilir) veya `x86` seçin.
4. `F5` ile çalıştırın.
5. Uygulama içinden bir `.chm` dosyası seçin ve arama/görüntüleme akışlarını deneyin.

## Komut satırı ile derleme

Visual Studio olmadan derlemek için, repo kök dizininde:

- `dotnet restore`
- `dotnet build ChmViewer.sln -c Release`

## Yardımcı scriptler

- `delete-bin-obj-folders.bat`: Tüm projelerdeki `bin/` ve `obj/` klasörlerini temizlemek için kullanılabilir.

## Sık karşılaşılan sorunlar

- **CefSharp bağımlılıkları bulunamıyor / çalışmıyor**: Seçtiğiniz platform (`x64`/`x86`) ile runtime paketinin uyumlu olduğundan emin olun.
- **CefSharp cache kilitli**: Uygulama aynı cache dizinini kullanan başka bir süreç tarafından çalıştırılıyorsa başlatma sırasında hata alabilirsiniz (cache yolu kullanıcı profilinde `LocalAppData` altındadır).
- **WPF `WebBrowser` davranışı**: `WebBrowser` kontrolü Windows/IE bileşenlerine bağlıdır; içerik render davranışı makinedeki IE/Edge WebView bileşenlerine göre değişebilir.

## Yayınlama (publish) notları

`app/ChmViewer/ChmViewer.csproj` içinde tek dosya (single-file) publish senaryosu için ayarlar mevcuttur. `PublishSingleFile=true` ile publish edildiğinde CefSharp tarafı için özel bir `StartupObject` kullanılacak şekilde yapılandırılmıştır.
