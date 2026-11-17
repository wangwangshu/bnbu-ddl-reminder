$ErrorActionPreference = 'Stop'
$deps = Join-Path $PSScriptRoot 'deps'
New-Item -ItemType Directory -Path $deps -Force | Out-Null
$packages = @(
  @{ File='webview2.zip'; Folder='webview2'; Url='https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4314.35/microsoft.web.webview2.1.0.4314.35.nupkg'; Hash='90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4' },
  @{ File='windows-sdk-buildtools.zip'; Folder='windows-sdk-buildtools'; Url='https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/10.0.26100.9169/microsoft.windows.sdk.buildtools.10.0.26100.9169.nupkg'; Hash='6000C971FC9155052A8359779B30B6682C39E091664D83B3852C4702AB6D238E' }
)
foreach ($package in $packages) {
  $archive = Join-Path $deps $package.File
  if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $package.Url -OutFile $archive }
  if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.Hash) { throw "Dependency hash mismatch: $($package.File)" }
  Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $deps $package.Folder) -Force
}
Write-Output 'Store dependencies verified; no runtime installer is included.'
