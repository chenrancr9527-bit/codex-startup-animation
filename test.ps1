$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$distRoot = Join-Path $projectRoot 'dist'
$testRoot = Join-Path $projectRoot 'test-output'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$setup = Join-Path $distRoot 'CodexStartupSetup.exe'
if (-not (Test-Path -LiteralPath $setup)) { throw 'Run build.ps1 first.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$checks = Join-Path $testRoot 'InstallerChecks.exe'
& $compiler /nologo /utf8output /target:exe /platform:x64 ('/out:' + $checks) ('/r:' + $setup) /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $projectRoot 'InstallerChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Checks build failed.' }
Copy-Item -LiteralPath $setup -Destination $testRoot -Force
& $checks $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Installer checks failed.' }
