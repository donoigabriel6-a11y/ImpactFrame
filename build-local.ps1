<#
.SYNOPSIS
    Build ImpactFrame mod locally
.DESCRIPTION
    Builds the mod and optionally installs it to the Bonelab Mods folder.
    Requires MelonLoader 0.6.4+ installed and Bonelab launched at least once.
#>

param(
    [switch]$Install,
    [switch]$Clean,
    [string]$Configuration = "Release",
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Bonelab"
)

$ErrorActionPreference = "Stop"

Write-Host "=== ImpactFrame Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Paths
$projectFile = "ImpactFrame.csproj"
$melonLoaderPath = Join-Path $GamePath "MelonLoader"
$libsPath = Join-Path $melonLoaderPath "Libraries"
$modsPath = Join-Path $GamePath "Mods"
$outputDll = "bin\$Configuration\netstandard2.0\ImpactFrame.dll"

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean $projectFile -c $Configuration
    Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue
}

# Verify MelonLoader libraries exist
Write-Host "`nChecking MelonLoader libraries..." -ForegroundColor Gray
$requiredDlls = @(
    "MelonLoader.dll",
    "0Harmony.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.AnimationModule.dll",
    "UnityEngine.UI.dll",
    "SLZ.Marrow.dll",
    "SLZ.Marrow.Warehouse.dll",
    "SLZ.Rig.dll",
    "SLZ.Combat.dll",
    "SLZ.Bonelab.dll"
)

$missing = @()
foreach ($dll in $requiredDlls) {
    $path = if ($dll -eq "MelonLoader.dll") { Join-Path $melonLoaderPath $dll } else { Join-Path $libsPath $dll }
    if (-not (Test-Path $path)) {
        $missing += $dll
    }
}

if ($missing.Count -gt 0) {
    Write-Error "Missing required assemblies:`n$($missing | ForEach-Object { "  - $_" })"
    Write-Host "`nPlease ensure:" -ForegroundColor Yellow
    Write-Host "  1. MelonLoader 0.6.4+ is installed" -ForegroundColor Yellow
    Write-Host "  2. Bonelab has been launched at least once with MelonLoader" -ForegroundColor Yellow
    Write-Host "  3. Game path is correct: $GamePath" -ForegroundColor Yellow
    exit 1
}
Write-Host "All assemblies found!" -ForegroundColor Green

# Build
Write-Host "`nBuilding project..." -ForegroundColor Cyan
dotnet build $projectFile -c $Configuration --no-restore

if (-not (Test-Path $outputDll)) {
    Write-Error "Build failed - DLL not found at $outputDll"
    exit 1
}

$size = (Get-Item $outputDll).Length
Write-Host "`nBuild successful!" -ForegroundColor Green
Write-Host "Output: $outputDll ($([math]::Round($size/1KB)) KB)" -ForegroundColor Gray

# Install if requested
if ($Install) {
    Write-Host "`nInstalling to Mods folder..." -ForegroundColor Cyan
    if (-not (Test-Path $modsPath)) {
        Write-Host "Creating Mods folder..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $modsPath | Out-Null
    }

    Copy-Item $outputDll -Destination $modsPath -Force
    Write-Host "Installed to: $modsPath\ImpactFrame.dll" -ForegroundColor Green
}

Write-Host "`nDone!" -ForegroundColor Cyan