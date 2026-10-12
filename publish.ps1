$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_TOKEN -or -not $env:GITHUB_REPOSITORY -or -not $env:GITHUB_SHA) { throw 'This script runs in the manual GitHub Actions publish workflow.' }
if ($env:GITHUB_REPOSITORY -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository name.' }
$headers = @{ Authorization = ('Bearer ' + $env:GITHUB_TOKEN); Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
$base = 'https://api.github.com/repos/' + $env:GITHUB_REPOSITORY
$existing = @(Invoke-RestMethod -Uri ($base + '/releases') -Headers $headers)
$release = $existing | Where-Object { $_.tag_name -eq 'v1.0.0' } | Select-Object -First 1
if (-not $release) {
    $body = @{ tag_name = 'v1.0.0'; target_commitish = $env:GITHUB_SHA; name = 'v1.0.0 · Codex 启动动画'; body = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'RELEASE_NOTES.md')); draft = $true; prerelease = $false } | ConvertTo-Json
    $release = Invoke-RestMethod -Method Post -Uri ($base + '/releases') -Headers $headers -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json'
}
$files = @('CodexStartupSetup.exe', 'CodexStartupAnimation-1.0.0.zip', 'SHA256SUMS.txt')
foreach ($name in $files) {
    $path = Join-Path $PSScriptRoot ('dist\' + $name)
    $info = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $asset = $release.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if ($asset) {
        if ($asset.size -ne $info.Length -or ($asset.digest -and $asset.digest -ne ('sha256:' + $hash))) { throw ('Existing release asset differs: ' + $name + '. Publish a new version rather than replacing it.') }
    } else {
        $upload = 'https://uploads.github.com/repos/' + $env:GITHUB_REPOSITORY + '/releases/' + $release.id + '/assets?name=' + [Uri]::EscapeDataString($name)
        $asset = Invoke-RestMethod -Method Post -Uri $upload -Headers $headers -InFile $path -ContentType 'application/octet-stream'
        if ($asset.size -ne $info.Length -or ($asset.digest -and $asset.digest -ne ('sha256:' + $hash))) { throw ('Uploaded release asset did not match: ' + $name) }
    }
    Write-Output ('Verified release asset: ' + $name)
}
if ($release.draft) {
    $release = Invoke-RestMethod -Method Patch -Uri ($base + '/releases/' + $release.id) -Headers $headers -Body '{"draft":false}' -ContentType 'application/json'
}
Write-Output ('Published: ' + $release.html_url)
