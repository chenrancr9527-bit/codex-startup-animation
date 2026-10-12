$ErrorActionPreference = 'Stop'
$setup = Join-Path $PSScriptRoot 'CodexStartupSetup.exe'
if (-not (Test-Path -LiteralPath $setup)) { $setup = Join-Path $PSScriptRoot 'dist\CodexStartupSetup.exe' }
if (-not (Test-Path -LiteralPath $setup)) { throw 'Download CodexStartupSetup.exe from Releases or run build.ps1 first.' }
Start-Process -FilePath $setup
