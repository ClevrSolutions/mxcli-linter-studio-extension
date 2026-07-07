<#
.SYNOPSIS
    Builds the extension in Release mode and updates dist\clevrlint with the output.

.DESCRIPTION
    Run this script whenever you want to refresh dist\clevrlint for local testing.
    It always uses Release configuration and replaces the DLLs, PDBs, deps.json,
    manifest.json, and wwwroot assets. It never touches lint-scan-settings.json.
    End users install the extension from the Mendix Marketplace, not from dist\.

.EXAMPLE
    .\Pack-Dist.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot  = $PSScriptRoot
$csproj    = Join-Path $repoRoot 'src\Clevr.Lint.Extension\Clevr.Lint.Extension.csproj'
$releaseOut = Join-Path $repoRoot 'src\Clevr.Lint.Extension\bin\Release\net10.0'
$distDir   = Join-Path $repoRoot 'dist\clevrlint'

Write-Host "Building UI..." -ForegroundColor Cyan
Push-Location (Join-Path $repoRoot 'src\Clevr.Lint.Extension\ui')
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Error "UI build failed."; exit 1 }
Pop-Location

Write-Host "Building Release..." -ForegroundColor Cyan
# Clean the Release output first: since dist\ is now assembled by globbing whatever
# lands in $releaseOut, a stale DLL left over from a prior build (e.g. a renamed
# assembly) would otherwise get shipped forever instead of being swept.
if (Test-Path $releaseOut) {
    Remove-Item $releaseOut -Recurse -Force
}
dotnet build $csproj --configuration Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

Write-Host "Copying to dist\clevrlint..." -ForegroundColor Cyan
# Guard: if a stale file exists at the dist path (not a directory), remove it first.
if ((Test-Path $distDir) -and -not (Test-Path $distDir -PathType Container)) {
    Remove-Item $distDir -Force
}
$null = New-Item -ItemType Directory -Force -Path $distDir

# Deploy every DLL/PDB/deps.json the Release build actually produced, rather than a
# hardcoded list — a new NuGet dependency (or one dropped) is picked up automatically.
# Mendix.StudioPro.ExtensionsAPI is excluded on purpose: Studio Pro provides it itself
# (see ExcludeAssets="runtime" in the csproj), so it should never land in dist\.
$filesToCopy = Get-ChildItem $releaseOut -File |
    Where-Object { $_.Extension -in '.dll', '.pdb' -or $_.Name -like '*.deps.json' } |
    Where-Object { $_.Name -notlike 'Mendix.StudioPro*' } |
    Select-Object -ExpandProperty Name
$filesToCopy += 'manifest.json'

foreach ($f in $filesToCopy) {
    Copy-Item (Join-Path $releaseOut $f) $distDir -Force
}

# wwwroot: mirror, not merge — remove the existing copy first so assets renamed or
# deleted on the source side don't linger in dist\ forever.
$distWwwroot = Join-Path $distDir 'wwwroot'
if (Test-Path $distWwwroot) {
    Remove-Item $distWwwroot -Recurse -Force
}
Copy-Item (Join-Path $releaseOut 'wwwroot') $distDir -Recurse -Force

# Remove any stale top-level files that no longer come out of the build
Get-ChildItem $distDir -File | Where-Object { $_.Name -notin $filesToCopy -and $_.Name -ne 'lint-scan-settings.json' } | ForEach-Object {
    Write-Warning "Removing stale file: $($_.Name)"
    Remove-Item $_.FullName -Force
}

Write-Host "dist updated:" -ForegroundColor Green
Get-ChildItem (Join-Path $repoRoot 'dist') -Recurse | Select-Object -ExpandProperty FullName
