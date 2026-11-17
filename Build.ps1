param(
  [ValidateSet('All','Application','Installer')][string]$Stage = 'All',
  [switch]$RequireSignedApplication
)
$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$src = Join-Path $base 'src'
$payload = Join-Path $base 'build/payload'
$dist = Join-Path $base 'dist'
$sdk = Join-Path $base 'deps/webview2'
$lib = Join-Path $sdk 'lib/net462'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$icon = Join-Path $base 'assets/app.ico'
$branding = @(('/win32icon:'+$icon),('/resource:'+$icon+',AppIcon.ico'),('/resource:'+(Join-Path $base 'assets/app-icon.png')+',AppIcon.png'))
New-Item -ItemType Directory -Path $payload,$dist -Force | Out-Null
if ($Stage -ne 'Installer') {
  $refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $lib 'Microsoft.Web.WebView2.Core.dll')),('/reference:'+(Join-Path $lib 'Microsoft.Web.WebView2.WinForms.dll')))
  & $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output $branding ('/win32manifest:'+(Join-Path $src 'app.manifest')) ('/out:'+(Join-Path $payload 'BNBU-DDL.exe')) $refs ('/resource:'+(Join-Path $src 'PortalExtractor.js')+',PortalExtractor.js') (Join-Path $src 'App.cs') (Join-Path $src 'Portal.cs') (Join-Path $src 'AssemblyInfo.cs')
  if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
  Copy-Item -LiteralPath (Join-Path $src 'app.config') -Destination (Join-Path $payload 'BNBU-DDL.exe.config') -Force
  Copy-Item -LiteralPath (Join-Path $lib 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $lib 'Microsoft.Web.WebView2.WinForms.dll'),(Join-Path $sdk 'runtimes/win-x64/native/WebView2Loader.dll'),(Join-Path $base 'deps/MicrosoftEdgeWebview2Setup.exe'),$icon,(Join-Path $base '使用说明.txt') -Destination $payload -Force
  $notices = [System.IO.File]::ReadAllText((Join-Path $sdk 'LICENSE.txt'))+"`r`n"+[System.IO.File]::ReadAllText((Join-Path $sdk 'NOTICE.txt'))
  [System.IO.File]::WriteAllText((Join-Path $payload 'ThirdPartyNotices.txt'),$notices,[System.Text.UTF8Encoding]::new($false))
}
if ($Stage -eq 'Application') { Write-Output $payload; return }
if ($RequireSignedApplication) {
  if (-not (Test-Path -LiteralPath (Join-Path $payload 'BNBU-DDL.exe'))) { throw 'The signed application is missing from the payload.' }
  foreach ($file in Get-ChildItem -LiteralPath $payload -File | Where-Object { $_.Extension -in '.exe','.dll' }) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.TimeStamperCertificate) { throw "Payload signature or timestamp is invalid: $($file.Name)" }
  }
}
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath (Join-Path $base 'build/payload.zip') -Force
$installer = Join-Path $dist 'BNBU-DDL-Setup.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output $branding ('/win32manifest:'+(Join-Path $src 'app.manifest')) ('/out:'+$installer) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ('/resource:'+(Join-Path $base 'build/payload.zip')+',payload.zip') (Join-Path $src 'Setup.cs') (Join-Path $src 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Copy-Item -LiteralPath (Join-Path $base '使用说明.txt'),(Join-Path $base 'assets/app-icon.png') -Destination $dist -Force
$signature = Get-AuthenticodeSignature -LiteralPath $installer
[pscustomobject]@{Status='Unsigned installer candidate; not a signed release';SHA256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash;Authenticode=$signature.Status.ToString()} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dist 'release-status.json') -Encoding UTF8
Write-Output $dist
