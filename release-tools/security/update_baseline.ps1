# ============================================================================
# PVPharmonics - record a new feed integrity baseline
#
# Run this ONCE right after each release upload (pack_release.bat + scp).
# It hashes the files you just built in ..\releases\ (the exact bytes that
# were uploaded) and stores them as the new expected state, then immediately
# runs a verification against the live feed to confirm upload integrity.
# ============================================================================
$ErrorActionPreference = "Stop"
$dir        = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path (Split-Path -Parent $dir) "releases"
$feedUrl    = "https://pvpharmonics.duckdns.org/updates-x7q2mkv9tr4w/"

$files = @{}
foreach ($n in @("releases.win.json", "PVPharmonics-win-Setup.exe")) {
    $p = Join-Path $releaseDir $n
    if (-not (Test-Path $p)) { throw "not found: $p (run pack_release.bat first)" }
    $files[$n] = (Get-FileHash -Algorithm SHA256 $p).Hash.ToLower()
}
foreach ($p in Get-ChildItem $releaseDir -Filter "*.nupkg") {
    $files[$p.Name] = (Get-FileHash -Algorithm SHA256 $p.FullName).Hash.ToLower()
}

# best-effort version detection from the newest full package name
$ver = "unknown"
$full = Get-ChildItem $releaseDir -Filter "*-full.nupkg" | Sort-Object LastWriteTime | Select-Object -Last 1
if ($full -and $full.Name -match "-(\d+\.\d+\.\d+)-full\.nupkg$") { $ver = $Matches[1] }

$baseline = [ordered]@{
    recorded_at = (Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")
    version     = $ver
    feed_url    = $feedUrl
    files       = $files
}
$baseline | ConvertTo-Json | Set-Content (Join-Path $dir "baseline.json") -Encoding ASCII
Write-Host "baseline.json updated (version $ver)."

Write-Host "verifying live feed against the new baseline..."
& (Join-Path $dir "check_feed_integrity.ps1") -Full
