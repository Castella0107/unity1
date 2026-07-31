# ============================================================================
# PVPharmonics - update feed integrity monitor
#
# Verifies that the Velopack update feed on the VPS still matches the
# baseline recorded at release time (baseline.json). Detects supply-chain
# tampering: if someone modifies releases.win.json / Setup.exe on the server,
# this check fails and alerts.
#
# Run daily via Task Scheduler (see docs/deployment/velopack_release.md).
#   normal run : verifies releases.win.json only (tiny download)
#   -Full      : also downloads Setup.exe and verifies its hash (large)
#
# IMPORTANT: this script and baseline.json live on the LOCAL PC on purpose.
# An attacker who compromises the VPS cannot silence a monitor they cannot
# reach. Never store the baseline on the VPS.
# ============================================================================
param(
    [switch]$Full
)

$ErrorActionPreference = "Stop"
# PS 5.1: Invoke-WebRequest is 10-50x slower with the progress bar enabled
$ProgressPreference    = "SilentlyContinue"
$dir      = Split-Path -Parent $MyInvocation.MyCommand.Path
$baseline = Get-Content (Join-Path $dir "baseline.json") -Raw | ConvertFrom-Json
$feed     = $baseline.feed_url.TrimEnd('/')
$logFile  = Join-Path $dir "check.log"
$alertLog = Join-Path $dir "ALERT.log"

function Log([string]$msg) {
    $line = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

function Alert([string]$msg) {
    $line = "{0}  !!! TAMPERING SUSPECTED !!!  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Add-Content -Path $alertLog -Value $line
    Add-Content -Path $logFile  -Value $line
    Write-Host $line -ForegroundColor Red

    # Optional Discord webhook: put the webhook URL in discord_webhook.txt
    $hookFile = Join-Path $dir "discord_webhook.txt"
    if (Test-Path $hookFile) {
        $hook = (Get-Content $hookFile -Raw).Trim()
        if ($hook) {
            try {
                $body = @{ content = ":rotating_light: **PVPharmonics update feed integrity check FAILED**`n$msg`nDo NOT let testers launch the game until verified. Check the VPS." } | ConvertTo-Json
                Invoke-RestMethod -Uri $hook -Method Post -ContentType "application/json" -Body $body | Out-Null
            } catch {
                Log ("discord webhook failed: " + $_.Exception.Message)
            }
        }
    }

    # Popup (visible when run in the logged-on user session)
    try {
        Add-Type -AssemblyName PresentationFramework
        [System.Windows.MessageBox]::Show(
            "PVPharmonics update feed integrity check FAILED.`n`n$msg`n`nThe feed on the VPS no longer matches the recorded baseline. If you did not just publish a release, treat this as a possible compromise: stop testers from launching the game and inspect the VPS.",
            "PVPharmonics FEED ALERT", "OK", "Error") | Out-Null
    } catch { }

    exit 2
}

function RemoteSha256([string]$url, [string]$outPath) {
    Invoke-WebRequest -Uri $url -OutFile $outPath -UseBasicParsing | Out-Null
    return (Get-FileHash -Algorithm SHA256 $outPath).Hash.ToLower()
}

$tmp = Join-Path $env:TEMP "pvph_feed_check"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

# ---- 1. releases.win.json (the file that decides what clients install) ----
$name     = "releases.win.json"
$expected = $baseline.files.$name.ToLower()
$actual   = RemoteSha256 "$feed/$name" (Join-Path $tmp $name)
if ($actual -ne $expected) {
    Alert ("releases.win.json hash mismatch. expected=$expected actual=$actual")
}
Log ("OK releases.win.json ($actual)")

# ---- 2. Full mode: verify the installer binary too ----
if ($Full) {
    $name     = "PVPharmonics-win-Setup.exe"
    $expected = $baseline.files.$name.ToLower()
    $actual   = RemoteSha256 "$feed/$name" (Join-Path $tmp $name)
    if ($actual -ne $expected) {
        Alert ("Setup.exe hash mismatch. expected=$expected actual=$actual")
    }
    Log ("OK Setup.exe ($actual)")
}

Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
Log "feed integrity OK"
exit 0
