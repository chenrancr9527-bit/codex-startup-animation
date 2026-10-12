$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$distRoot = Join-Path $projectRoot 'dist'
$appRoot = Join-Path $distRoot 'app'
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 C# compiler not found.' }
New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
$refs = @('/r:System.Xaml.dll', ('/r:' + (Join-Path $frameworkRoot 'WPF\PresentationCore.dll')), ('/r:' + (Join-Path $frameworkRoot 'WPF\PresentationFramework.dll')), ('/r:' + (Join-Path $frameworkRoot 'WPF\WindowsBase.dll')))
$sources = @('Launcher.cs', 'Scene.cs', 'Startup.cs', 'AssemblyInfo.cs') | ForEach-Object { Join-Path $projectRoot $_ }
$common = @('/nologo', '/utf8output', '/target:winexe', '/platform:x64', '/optimize+', ('/win32icon:' + (Join-Path $projectRoot 'Companion.ico')), ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')))
& $compiler @common ('/out:' + (Join-Path $appRoot 'Codex-启动动画.exe')) @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
& $compiler @common /define:PREVIEW ('/out:' + (Join-Path $appRoot '只看动画.exe')) @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Preview build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'Startup.wav') -Destination (Join-Path $appRoot '启动音.wav') -Force
foreach ($file in @('Companion.ico', 'README.md', 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $appRoot -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = Join-Path $distRoot 'payload.zip'
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload -Force }
$zip = [IO.Compression.ZipFile]::Open($payload, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @('Codex-启动动画.exe', '只看动画.exe', '启动音.wav', 'Companion.ico', 'README.md', 'LICENSE')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $appRoot $file), $file, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }
$setup = Join-Path $distRoot 'CodexStartupSetup.exe'
& $compiler @common ('/out:' + $setup) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/resource:' + $payload + ',Companion.Payload') (Join-Path $projectRoot 'Setup.cs') (Join-Path $projectRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$shareRoot = Join-Path $distRoot 'share'
New-Item -ItemType Directory -Path $shareRoot -Force | Out-Null
foreach ($file in @('README.md', 'LICENSE', 'Install.ps1', 'Uninstall.ps1')) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $shareRoot -Force }
Copy-Item -LiteralPath $setup -Destination $shareRoot -Force
$shareArchive = Join-Path $distRoot 'CodexStartupAnimation-1.0.0.zip'
Compress-Archive -Path (Join-Path $shareRoot '*') -DestinationPath $shareArchive -Force
$hashLines = @($setup, $shareArchive) | ForEach-Object { $h = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $h.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $distRoot 'SHA256SUMS.txt'), $hashLines, [Text.UTF8Encoding]::new($false))
Write-Output 'Built dist/CodexStartupSetup.exe and dist/CodexStartupAnimation-1.0.0.zip'
