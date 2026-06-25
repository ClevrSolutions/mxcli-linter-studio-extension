<#
.SYNOPSIS
    Installs the CLEVR ACR Studio Pro extension into a Mendix project.

.DESCRIPTION
    Copies the bundled 'clevracr' extension folder into <your Mendix project>\extensions\clevracr,
    detects mxcli on your PATH, and writes a clean acr-scan-settings.json for your project.
    Run this from the folder where it lives (next to the 'clevracr' folder).

    REQUIRES Mendix Studio Pro 11 or higher (Extensibility API 11.10 / .NET 10). The extension
    will NOT load on Mendix 10 or lower.

.PARAMETER ProjectPath
    Path to your Mendix project: the project folder, or the .mpr file inside it.
    If omitted, the script asks for it. The folder must contain a .mpr.

.EXAMPLE
    .\Install-ClevrAcr.ps1
    (prompts for the project path)

.EXAMPLE
    .\Install-ClevrAcr.ps1 -ProjectPath "<path to your Mendix project>"
#>
[CmdletBinding()]
param(
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

# ----------------------------------------------------------------------------------------------
# Where to point developers if mxcli is not installed. mxcli is a Mendix Labs tool and has no
# fixed install location inside CLEVR, so we detect it on PATH only (below) and otherwise refer
# to the official instruction.
# mxcli is a Mendix Labs tool distributed via GitHub releases.
$MxcliInstallUrl = 'https://github.com/mendixlabs/mxcli/releases'
$MxcliApiLatest  = 'https://api.github.com/repos/mendixlabs/mxcli/releases/latest'
$MxcliAssetName  = 'mxcli-windows-amd64.exe'
# ----------------------------------------------------------------------------------------------

# Downloads the latest mxcli Windows release binary to $DestPath, verifying its integrity
# (sha256 from the GitHub API + byte size). Returns the path on success, or $null on ANY failure
# (no network, API error, asset/digest missing, hash/size mismatch). Never throws to the caller,
# never leaves an unverified executable behind.
function Install-Mxcli {
    param([Parameter(Mandatory)] [string]$DestPath)

    $tmp = "$DestPath.download"
    try {
        # GitHub requires a User-Agent; Windows PowerShell 5.1 may default to old TLS.
        try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}
        $headers = @{ 'User-Agent' = 'clevr-acr-installer'; 'Accept' = 'application/vnd.github+json' }

        Write-Host "  Querying GitHub for the latest mxcli release..." -ForegroundColor Cyan
        $rel = Invoke-RestMethod -Uri $MxcliApiLatest -Headers $headers -TimeoutSec 30
        $asset = $rel.assets | Where-Object { $_.name -eq $MxcliAssetName } | Select-Object -First 1
        if (-not $asset) { Write-Warning "  Asset '$MxcliAssetName' not found in release $($rel.tag_name)."; return $null }

        # Integrity baseline from the API metadata (sha256 + size). No digest = refuse to install.
        $digest = "$($asset.digest)"
        if ($digest -notmatch '^sha256:') {
            Write-Warning "  No sha256 digest published for '$MxcliAssetName' - refusing to install an unverifiable binary."
            return $null
        }
        $expectedHash = ($digest -replace '^sha256:').Trim().ToLowerInvariant()
        $expectedSize = [int64]$asset.size

        New-Item -ItemType Directory -Force -Path (Split-Path $DestPath) | Out-Null
        if (Test-Path $tmp) { Remove-Item $tmp -Force }

        Write-Host ("  Downloading {0} {1} ({2:N0} bytes)..." -f $MxcliAssetName, $rel.tag_name, $expectedSize) -ForegroundColor Cyan
        $oldPref = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tmp -Headers @{ 'User-Agent' = 'clevr-acr-installer' } -TimeoutSec 600
        $ProgressPreference = $oldPref

        # Verify size AND sha256 before trusting the file.
        $actualSize = (Get-Item $tmp).Length
        $actualHash = (Get-FileHash $tmp -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualSize -ne $expectedSize -or $actualHash -ne $expectedHash) {
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
            Write-Warning ("  Integrity check FAILED - download discarded.`n    size   : got {0}, expected {1}`n    sha256 : got {2}`n             expected {3}" -f $actualSize, $expectedSize, $actualHash, $expectedHash)
            return $null
        }

        if (Test-Path $DestPath) { Remove-Item $DestPath -Force }
        Move-Item $tmp $DestPath -Force
        Write-Host ("  mxcli {0} downloaded and verified (sha256 OK): {1}" -f $rel.tag_name, $DestPath) -ForegroundColor Green
        return $DestPath
    }
    catch {
        Write-Warning "  mxcli download failed: $($_.Exception.Message)"
        if (Test-Path $tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
        return $null
    }
}

Write-Host ""
Write-Host "CLEVR ACR - extension installer" -ForegroundColor Cyan
Write-Host "Requires Mendix Studio Pro 11 or higher." -ForegroundColor DarkYellow
Write-Host ""

# The 'clevracr' folder must sit next to this script.
$sourceDir = Join-Path $PSScriptRoot 'clevracr'
if (-not (Test-Path $sourceDir)) {
    Write-Error "Could not find the 'clevracr' folder next to this script ($sourceDir). Keep the script in the package folder."
    exit 1
}

# --- Ask for and validate the Mendix project path (mandatory) ---------------------------------
if (-not $ProjectPath) {
    $ProjectPath = Read-Host "Enter the path to your Mendix project (the project folder, or its .mpr file)"
}
$ProjectPath = ($ProjectPath -replace '"', '').Trim()
if (-not $ProjectPath) { Write-Error "No project path given. Installation cancelled."; exit 1 }

# Resolve to the project folder (accept either a .mpr file or the folder).
if (Test-Path $ProjectPath -PathType Leaf) {
    if ([System.IO.Path]::GetExtension($ProjectPath) -ne '.mpr') {
        Write-Error "That file is not a .mpr: $ProjectPath"
        exit 1
    }
    $projectDir = Split-Path -Parent $ProjectPath
} elseif (Test-Path $ProjectPath -PathType Container) {
    $projectDir = (Resolve-Path $ProjectPath).Path
} else {
    Write-Error "Path does not exist: $ProjectPath"
    exit 1
}

# The folder MUST contain a .mpr to be a Mendix project.
$mprs = @(Get-ChildItem -Path $projectDir -Filter '*.mpr' -File -ErrorAction SilentlyContinue)
if ($mprs.Count -eq 0) {
    Write-Error "No .mpr file found in '$projectDir'. This is not a Mendix project folder."
    exit 1
}
Write-Host ("Mendix project: {0}  ({1})" -f $mprs[0].Name, $projectDir) -ForegroundColor Green
if ($mprs.Count -gt 1) {
    Write-Warning "Multiple .mpr files found. The scan expects exactly one; remove the extra(s) or the scan will ask you to pin one."
}

# --- Resolve mxcli: PATH -> our cached copy -> offer download -> manual fallback ---------------
$mxcliCacheDir = Join-Path $env:LOCALAPPDATA 'clevr-acr\mxcli'
$mxcliCached   = Join-Path $mxcliCacheDir 'mxcli.exe'
$mxcliPath = ""

# 1) Already on PATH? Use that; download nothing.
$mxcli = Get-Command mxcli -ErrorAction SilentlyContinue
if ($mxcli) {
    $mxcliPath = $mxcli.Source
    Write-Host ("mxcli found on PATH: {0}" -f $mxcliPath) -ForegroundColor Green
}
# 2) Downloaded by us earlier? Reuse it.
elseif (Test-Path $mxcliCached) {
    $mxcliPath = $mxcliCached
    Write-Host ("mxcli found from an earlier download: {0}" -f $mxcliPath) -ForegroundColor Green
}
# 3) Offer to download (explicit confirmation - never a silent internet download).
else {
    Write-Host ""
    Write-Warning "mxcli was not found on your PATH."
    Write-Host "  The CLEVR ACR scan needs mxcli (the official Mendix Labs release binary)." -ForegroundColor Yellow
    $dl = Read-Host "  Download the latest mxcli release now and install it locally (with checksum verification)? (Y/n)"
    if ($dl -ne 'n') {
        $mxcliPath = Install-Mxcli -DestPath $mxcliCached   # returns the path, or $null on any failure
    }
    if (-not $mxcliPath) {
        Write-Host ""
        Write-Host "  mxcli was not installed. Install it manually before scanning:" -ForegroundColor Yellow
        Write-Host ("    1. Download '{0}' from {1}" -f $MxcliAssetName, $MxcliInstallUrl) -ForegroundColor Yellow
        Write-Host "       Use the RELEASE BINARY - NOT the 'git clone + make build' developer route" -ForegroundColor Yellow
        Write-Host "       from the repo README (make is not available on Windows)." -ForegroundColor Yellow
        Write-Host "    2. Put it on your PATH, or set its full path in acr-scan-settings.json (mxcliPath)." -ForegroundColor Yellow
        Write-Host "  Setup will continue; mxcliPath is left empty so it resolves via PATH once mxcli is installed." -ForegroundColor Yellow
        Write-Host ""
    }
}

# --- Target: <project>\extensions\clevracr ----------------------------------------------------
$targetDir = Join-Path (Join-Path $projectDir 'extensions') 'clevracr'

# On upgrade: preserve existing, working settings values (don't overwrite without need).
$existing = $null
$settingsFile = Join-Path $targetDir 'acr-scan-settings.json'
if (Test-Path $settingsFile) {
    try { $existing = Get-Content -Raw $settingsFile | ConvertFrom-Json } catch { $existing = $null }
}

if (Test-Path $targetDir) {
    Write-Host "An existing install was found at: $targetDir" -ForegroundColor DarkYellow
    $answer = Read-Host "Overwrite it? (Y/n)"
    if ($answer -eq 'n') { Write-Host "Cancelled."; exit 1 }
    Remove-Item -Recurse -Force $targetDir
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $sourceDir '*') $targetDir

# --- Compute the clean settings (prefer existing valid values on upgrade) ----------------------
# projectPath: keep an existing valid one, else the project we are installing into.
$finalProject = $projectDir
if ($existing -and $existing.projectPath -and (Test-Path $existing.projectPath)) {
    $finalProject = $existing.projectPath
}
# mxcliPath: keep an existing one only if it still points to a real file (a working config);
# otherwise use what we resolved/downloaded this run (or empty -> PATH fallback at runtime).
$finalMxcli = $mxcliPath
if ($existing -and $existing.mxcliPath -and (Test-Path $existing.mxcliPath)) { $finalMxcli = $existing.mxcliPath }
# mxlint is verwijderd: alle regels lezen via mxcli (catalog/describe). Geen mxlintPath meer.
$settingsObj = [ordered]@{
    mxcliPath   = $finalMxcli
    projectPath = $finalProject
}
$json = ($settingsObj | ConvertTo-Json)
[System.IO.File]::WriteAllText($settingsFile, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Host ("Wrote settings: {0}" -f $settingsFile) -ForegroundColor Green

# --- Verify the critical files landed ---------------------------------------------------------
$required = @('Clevr.Acr.Extension.dll', 'Clevr.Acr.Normalizer.dll', 'manifest.json', 'rules.json', 'acr-scan-settings.json', 'wwwroot\index.html', 'wwwroot\main.js', 'wwwroot\clevr-logo.png')
$missing = @()
foreach ($f in $required) { if (-not (Test-Path (Join-Path $targetDir $f))) { $missing += $f } }
if ($missing.Count -gt 0) {
    Write-Error ("Install incomplete - missing: {0}" -f ($missing -join ', '))
    exit 1
}

Write-Host ""
Write-Host "Installed to: $targetDir" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Open the project in Mendix Studio Pro 11+."
Write-Host "  2. Enable extension development (one-time):"
Write-Host "       Edit > Preferences > General/Extensions > tick 'Enable extension development'."
Write-Host "     (Or launch Studio Pro once with --enable-extension-development.)"
Write-Host "  3. Restart Studio Pro."
Write-Host "  4. Open the panel via the Extensions menu > 'CLEVR ACR' and click Scan."
if (-not $mxcliPath) {
    Write-Host "  5. Install mxcli and ensure it is on your PATH (see the link above), then run Scan." -ForegroundColor Yellow
}
Write-Host ""
