$ErrorActionPreference = 'Stop'
$deps = Join-Path $PSScriptRoot 'deps'
New-Item -ItemType Directory -Path $deps -Force | Out-Null
$sdkVersion = '1.0.4314.35'
$sdkFile = Join-Path $deps 'webview2.zip'
if (-not (Test-Path -LiteralPath $sdkFile)) {
  Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$sdkVersion/microsoft.web.webview2.$sdkVersion.nupkg" -OutFile $sdkFile
}
$sdkHash = '90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4'
if ((Get-FileHash -LiteralPath $sdkFile -Algorithm SHA256).Hash -ne $sdkHash) { throw 'The WebView2 SDK hash does not match the reviewed dependency.' }
$sdkFolder = Join-Path $deps 'webview2'
Expand-Archive -LiteralPath $sdkFile -DestinationPath $sdkFolder -Force
$bootstrap = Join-Path $deps 'MicrosoftEdgeWebview2Setup.exe'
if (-not (Test-Path -LiteralPath $bootstrap)) {
  Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrap
}
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrap
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)') { throw 'The runtime bootstrapper must have a valid Microsoft signature.' }
$report = @{
  WebView2Version = $sdkVersion
  WebView2SHA256 = $sdkHash
  BootstrapperSHA256 = (Get-FileHash -LiteralPath $bootstrap -Algorithm SHA256).Hash
  BootstrapperSigner = $signature.SignerCertificate.Thumbprint
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $deps 'dependency-lock.generated.json') -Encoding UTF8
Write-Output 'Dependencies verified. The current signed Microsoft bootstrapper is identified in dependency-lock.generated.json.'
