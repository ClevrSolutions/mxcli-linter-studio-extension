<#
.SYNOPSIS
    Builds the extension in Release mode and updates dist\clevrlint with the output.

.DESCRIPTION
    Run this script whenever you want to refresh the distributable package in dist\.
    It always uses Release configuration and replaces the DLLs, PDBs, deps.json,
    manifest.json, rules.json, and wwwroot assets. It never touches
    lint-scan-settings.json (that is written by Install-ClevrLint.ps1 at install time).

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

Write-Host "Building Release..." -ForegroundColor Cyan
dotnet build $csproj --configuration Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

Write-Host "Copying to dist\clevrlint..." -ForegroundColor Cyan

$filesToCopy = @(
    'Clevr.Lint.Extension.dll',
    'Clevr.Lint.Extension.pdb',
    'Clevr.Lint.Extension.deps.json',
    'Clevr.Lint.Normalizer.dll',
    'Clevr.Lint.Normalizer.pdb',
    'manifest.json',
    'rules.json'
)
foreach ($f in $filesToCopy) {
    Copy-Item (Join-Path $releaseOut $f) $distDir -Force
}

# wwwroot: full recursive sync
Copy-Item (Join-Path $releaseOut 'wwwroot\*') (Join-Path $distDir 'wwwroot') -Recurse -Force

# Remove any stale files that no longer come out of the build
Get-ChildItem $distDir -File | Where-Object { $_.Name -notin $filesToCopy -and $_.Name -ne 'lint-scan-settings.json' } | ForEach-Object {
    Write-Warning "Removing stale file: $($_.Name)"
    Remove-Item $_.FullName -Force
}

Write-Host "dist\clevrlint updated:" -ForegroundColor Green
Get-ChildItem $distDir -Recurse | Select-Object -ExpandProperty FullName
