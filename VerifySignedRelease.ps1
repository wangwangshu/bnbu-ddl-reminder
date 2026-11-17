param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$outer = Get-AuthenticodeSignature -LiteralPath $installerPath
if ($outer.Status -ne 'Valid' -or $null -eq $outer.TimeStamperCertificate) { throw 'The installer must have a valid, timestamped Authenticode signature.' }
$assembly = [System.Reflection.Assembly]::LoadFile($installerPath)
$stream = $assembly.GetManifestResourceStream('payload.zip')
Add-Type -AssemblyName System.IO.Compression
$target = Join-Path $PSScriptRoot ('reports/signed-payload-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $target -Force | Out-Null
$zip = [System.IO.Compression.ZipArchive]::new($stream,[System.IO.Compression.ZipArchiveMode]::Read)
$foundMain = $false
try {
  foreach ($entry in $zip.Entries) {
    if ([System.IO.Path]::GetExtension($entry.Name) -notin '.exe','.dll') { continue }
    if ($entry.FullName -ne $entry.Name) { throw 'Unexpected nested executable path in the installer payload.' }
    $path = Join-Path $target $entry.Name
    if ($entry.Name -eq 'BNBU-DDL.exe') { $foundMain = $true }
    $inputStream = $entry.Open(); $outputStream = [System.IO.File]::Create($path)
    try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose();$outputStream.Dispose() }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.TimeStamperCertificate) { throw "Invalid payload signature: $($entry.Name)" }
  }
} finally { $zip.Dispose();$stream.Dispose() }
if (-not $foundMain) { throw 'The signed application is missing from the installer payload.' }
[pscustomobject]@{Status='Verified signed release candidate';InstallerSHA256=(Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash;SignerThumbprint=$outer.SignerCertificate.Thumbprint;TimestampSigner=$outer.TimeStamperCertificate.Thumbprint} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path $installerPath) 'signed-release-verification.json') -Encoding UTF8
Write-Output 'Installer and embedded executable signatures verified. Smart App Control acceptance still requires an eligible public-trust signer and device testing.'
