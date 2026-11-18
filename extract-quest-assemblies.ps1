<#
.SYNOPSIS
    Automatically extract Quest assemblies from connected Quest device
.DESCRIPTION
    Pulls IL2CPP generated assemblies and netstandard2.1 MelonLoader assemblies
    from Quest Bonelab to local directory for building Quest mods.
#>

$ErrorActionPreference = "Stop"

Write-Host "=== Quest Assembly Extractor ===" -ForegroundColor Cyan

# Check adb
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) {
    Write-Error "adb not found in PATH. Install Android SDK Platform Tools."
    exit 1
}

# Check Quest connection
Write-Host "Checking Quest connection..." -ForegroundColor Gray
$devices = adb devices | Select-String "device$"
if (-not $devices) {
    Write-Error "No Quest device found. Connect Quest via USB and enable USB debugging."
    Write-Host "  1. Settings → Developer → USB Debugging" -ForegroundColor Yellow
    Write-Host "  2. Allow USB debugging on Quest headset" -ForegroundColor Yellow
    exit 1
}
Write-Host "Quest connected!" -ForegroundColor Green

# Target paths
$basePath = "$env:USERPROFILE\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\QuestAssemblies"
$cpp2ilPath = "$basePath\cpp2il_out"
$netstdPath = "$basePath\netstandard2.1"

Write-Host "`nExtracting to: $basePath" -ForegroundColor Gray

# Create directories
New-Item -ItemType Directory -Force -Path $cpp2ilPath | Out-Null
New-Item -ItemType Directory -Force -Path $netstdPath | Out-Null

# Pull cpp2il_out (IL2CPP generated assemblies)
Write-Host "`n[1/2] Pulling IL2CPP assemblies (cpp2il_out)..." -ForegroundColor Cyan
$src1 = "/sdcard/ModData/com.stresslevelzero.bonelab/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/"
adb pull $src1 $cpp2ilPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pull cpp2il_out. Is MelonLoader installed on Quest? Launch Bonelab once on Quest."
    exit 1
}
Write-Host "  Done!" -ForegroundColor Green

# Pull netstandard2.1 (MelonLoader assemblies)
Write-Host "`n[2/2] Pulling MelonLoader netstandard2.1 assemblies..." -ForegroundColor Cyan
$src2 = "/sdcard/ModData/com.stresslevelzero.bonelab/MelonLoader/netstandard2.1/"
adb pull $src2 $netstdPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pull netstandard2.1."
    exit 1
}
Write-Host "  Done!" -ForegroundColor Green

# Verify key assemblies
Write-Host "`nVerifying assemblies..." -ForegroundColor Gray
$required = @(
    "MelonLoader.dll", "0Harmony.dll",
    "UnityEngine.CoreModule.dll", "UnityEngine.PhysicsModule.dll",
    "UnityEngine.AnimationModule.dll", "UnityEngine.UI.dll",
    "UnityEngine.IMGUIModule.dll", "UnityEngine.AudioModule.dll",
    "UnityEngine.ParticleSystemModule.dll", "UnityEngine.TextRenderingModule.dll",
    "SLZ.Marrow.dll", "Assembly-CSharp.dll"
)

$missing = @()
foreach ($dll in $required) {
    $path = Join-Path $cpp2ilPath $dll
    if (-not (Test-Path $path)) { $path = Join-Path $netstdPath $dll }
    if (-not (Test-Path $path)) { $missing += $dll }
}

if ($missing.Count -gt 0) {
    Write-Warning "Some assemblies missing: $($missing -join ', ')"
} else {
    Write-Host "All required assemblies found!" -ForegroundColor Green
}

Write-Host "`n=== Extraction Complete ===" -ForegroundColor Cyan
Write-Host "Assemblies ready at: $basePath" -ForegroundColor Gray
Write-Host "`nNow build Quest mod:" -ForegroundColor Yellow
Write-Host "  dotnet build ImpactFrame.Quest.csproj -c Release" -ForegroundColor Gray
Write-Host "`nThen install on Quest:" -ForegroundColor Yellow
Write-Host "  adb push bin/Release/netstandard2.1/ImpactFrame.dll /sdcard/ModData/com.stresslevelzero.bonelab/Mods/" -ForegroundColor Gray