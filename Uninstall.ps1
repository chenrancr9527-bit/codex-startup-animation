$ErrorActionPreference = 'Stop'
$uninstaller = Join-Path $env:LOCALAPPDATA 'Programs\CodexStartupAnimation\Uninstall.exe'
if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Codex Startup Animation is not installed by the share installer.' }
Start-Process -FilePath $uninstaller -ArgumentList '--uninstall'
