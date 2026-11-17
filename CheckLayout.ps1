param([Parameter(Mandatory=$true)][string]$ApplicationDirectory)
$ErrorActionPreference = 'Stop'
$checkRoot = Join-Path $PSScriptRoot ('build/layout-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkRoot -Force | Out-Null
foreach ($file in 'BNBU-DDL.exe','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll') {
  Copy-Item -LiteralPath (Join-Path $ApplicationDirectory $file) -Destination $checkRoot
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/app.config') -Destination (Join-Path $checkRoot 'LayoutChecks.exe.config')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$executable = Join-Path $checkRoot 'LayoutChecks.exe'
& $compiler /nologo /target:winexe /platform:x64 /utf8output ('/out:'+$executable) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ('/reference:'+(Join-Path $checkRoot 'BNBU-DDL.exe')) (Join-Path $PSScriptRoot 'tests/LayoutChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Layout check compilation failed.' }
$report = Join-Path $checkRoot 'layout-checks.txt'
$process = Start-Process -FilePath $executable -ArgumentList ('"'+$report+'"') -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Layout checks timed out.' }
Get-Content -LiteralPath $report
if ($process.ExitCode -ne 0) { throw 'Layout checks failed.' }
Write-Output $report
