param([string]$IdentityFile = (Join-Path $PSScriptRoot 'store/identity.json'))
$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$identity = Get-Content -LiteralPath $IdentityFile -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($key in 'Name','Publisher','PublisherDisplayName','DisplayName','Version','StoreId') {
  if ([string]::IsNullOrWhiteSpace($identity.$key)) { throw "Missing Store identity: $key" }
}
if ($identity.Name -notmatch '^[A-Za-z0-9.-]{3,50}$' -or $identity.Publisher -notmatch '^CN=') { throw 'Invalid package identity.' }
$version = [version]$identity.Version
if ($version.Revision -ne 0) { throw 'Store package versions must end in .0.' }
$build = Join-Path $base ('build/store-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $build 'payload'
$assets = Join-Path $payload 'Assets'
$dist = Join-Path $base 'dist/store'
New-Item -ItemType Directory -Path $payload,$assets,$dist -Force | Out-Null
$src = Join-Path $base 'src'
$lib = Join-Path $base 'deps/webview2/lib/net462'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$icon = Join-Path $base 'assets/app.ico'
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $lib 'Microsoft.Web.WebView2.Core.dll')),('/reference:'+(Join-Path $lib 'Microsoft.Web.WebView2.WinForms.dll')))
$branding = @(('/win32icon:'+$icon),('/resource:'+$icon+',AppIcon.ico'),('/resource:'+(Join-Path $base 'assets/app-icon.png')+',AppIcon.png'))
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /define:STORE_BUILD $branding ('/win32manifest:'+(Join-Path $src 'app.manifest')) ('/out:'+(Join-Path $payload 'BNBU-DDL.exe')) $refs ('/resource:'+(Join-Path $src 'PortalExtractor.js')+',PortalExtractor.js') (Join-Path $src 'App.cs') (Join-Path $src 'Portal.cs') (Join-Path $src 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Store application compilation failed.' }
Copy-Item -LiteralPath (Join-Path $src 'app.config') -Destination (Join-Path $payload 'BNBU-DDL.exe.config')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/out:'+(Join-Path $payload 'StartupLauncher.exe')) /reference:System.dll (Join-Path $src 'StartupLauncher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Startup launcher compilation failed.' }
Copy-Item -LiteralPath (Join-Path $lib 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $lib 'Microsoft.Web.WebView2.WinForms.dll'),(Join-Path $base 'deps/webview2/runtimes/win-x64/native/WebView2Loader.dll') -Destination $payload
$notices = [IO.File]::ReadAllText((Join-Path $base 'deps/webview2/LICENSE.txt')) + "`r`n" + [IO.File]::ReadAllText((Join-Path $base 'deps/webview2/NOTICE.txt'))
[IO.File]::WriteAllText((Join-Path $payload 'ThirdPartyNotices.txt'),$notices,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $base 'LICENSE'),(Join-Path $base 'store/使用说明.txt') -Destination $payload
Add-Type -AssemblyName System.Drawing
$sourceImage = [Drawing.Image]::FromFile((Join-Path $base 'assets/app-icon.png'))
try {
  foreach ($size in 44,50,150) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
      $graphics.Clear([Drawing.Color]::Transparent)
      $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $graphics.DrawImage($sourceImage,0,0,$size,$size)
      $bitmap.Save((Join-Path $assets "Logo$size.png"),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
  }
} finally { $sourceImage.Dispose() }
$name = [Security.SecurityElement]::Escape($identity.Name)
$publisher = [Security.SecurityElement]::Escape($identity.Publisher)
$publisherName = [Security.SecurityElement]::Escape($identity.PublisherDisplayName)
$displayName = [Security.SecurityElement]::Escape($identity.DisplayName)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap5 rescap">
  <Identity Name="$name" Publisher="$publisher" Version="$version" ProcessorArchitecture="x64" />
  <Properties><DisplayName>$displayName</DisplayName><PublisherDisplayName>$publisherName</PublisherDisplayName><Logo>Assets\Logo50.png</Logo></Properties>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Resources><Resource Language="zh-CN" /><Resource Language="en-US" /></Resources>
  <Applications>
    <Application Id="App" Executable="BNBU-DDL.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="$displayName" Description="本地作业截止提醒与 iSpace 同步" BackgroundColor="transparent" Square44x44Logo="Assets\Logo44.png" Square150x150Logo="Assets\Logo150.png" />
      <Extensions><uap5:Extension Category="windows.startupTask" Executable="StartupLauncher.exe" EntryPoint="Windows.FullTrustApplication"><uap5:StartupTask TaskId="BnbuDdlReminderStartup" Enabled="false" DisplayName="$displayName" /></uap5:Extension></Extensions>
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
[IO.File]::WriteAllText((Join-Path $payload 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
$report = Join-Path $build 'reminder-checks.txt'
$check = Start-Process -FilePath (Join-Path $payload 'BNBU-DDL.exe') -ArgumentList @('--self-test',('"'+$report+'"')) -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'Store reminder checks failed.' }
Get-Content -LiteralPath $report
$allowed = @('BNBU-DDL.exe','BNBU-DDL.exe.config','StartupLauncher.exe','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','ThirdPartyNotices.txt','LICENSE','使用说明.txt','AppxManifest.xml','Assets/Logo44.png','Assets/Logo50.png','Assets/Logo150.png')
$files = @(Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object { $_.FullName.Substring($payload.Length + 1).Replace('\','/') })
if (@(Compare-Object $allowed $files).Count -ne 0) { throw 'Store payload contains unexpected or missing files.' }
$makeappx = Join-Path $base 'deps/windows-sdk-buildtools/bin/10.0.26100.0/x64/makeappx.exe'
$package = Join-Path $dist ("BNBU-DDL-Store_$($identity.Version)_x64.msix")
& $makeappx pack /d $payload /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging or manifest validation failed.' }
Copy-Item -LiteralPath $report -Destination (Join-Path $dist 'reminder-checks.txt') -Force
$layoutReport = @(& (Join-Path $base 'CheckLayout.ps1') -ApplicationDirectory $payload)
$layoutReport | Write-Output
Copy-Item -LiteralPath $layoutReport[-1] -Destination (Join-Path $dist 'layout-checks.txt') -Force
[pscustomobject]@{Status='Unsigned Store submission package; Microsoft signing requires Store certification';StoreId=$identity.StoreId;PackageIdentity=$identity.Name;Version=$identity.Version;SHA256=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash;Payload=$payload;AutomaticStartup='Disabled until user enables in Windows Startup settings';SelfTests='Passed'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dist 'build-report.json') -Encoding UTF8
Write-Output $package
