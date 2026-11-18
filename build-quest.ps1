<#
.SYNOPSIS
    Build ImpactFrame mod for Quest (Android ARM64)
.DESCRIPTION
    Builds the mod for Bonelab Quest using Quest-specific assemblies.
    Requires Quest MelonLoader assemblies extracted from a Quest device.
#>

param(
    [switch]$Install,
    [string]$Configuration = "Release",
    [string]$QuestAssembliesPath = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== ImpactFrame Quest Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Determine Quest assemblies path
if (-not $QuestAssembliesPath) {
    $QuestAssembliesPath = "$env:USERPROFILE\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\QuestAssemblies"
}

Write-Host "Quest assemblies path: $QuestAssembliesPath" -ForegroundColor Gray

# Verify Quest assemblies exist
$requiredDlls = @(
    "MelonLoader.dll",
    "0Harmony.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.AnimationModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.AudioModule.dll",
    "UnityEngine.ParticleSystemModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "SLZ.Marrow.dll",
    "Assembly-CSharp.dll"
)

$missing = @()
foreach ($dll in $requiredDlls) {
    $path = Join-Path $QuestAssembliesPath $dll
    if (-not (Test-Path $path)) {
        $missing += $dll
    }
}

if ($missing.Count -gt 0) {
    Write-Error "Missing Quest assemblies:`n$($missing | ForEach-Object { "  - $_" })"
    Write-Host "`nSETUP REQUIRED:" -ForegroundColor Yellow
    Write-Host "1. Install MelonLoader on Quest Bonelab" -ForegroundColor Yellow
    Write-Host "2. Launch Bonelab on Quest once" -ForegroundColor Yellow
    Write-Host "3. Pull assemblies from Quest:" -ForegroundColor Yellow
    Write-Host "   adb pull /sdcard/ModData/com.stresslevelzero.bonelab/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/ $QuestAssembliesPath" -ForegroundColor Gray
    Write-Host "   adb pull /sdcard/ModData/com.stresslevelzero.bonelab/MelonLoader/netstandard2.1/ $QuestAssembliesPath/netstandard2.1" -ForegroundColor Gray
    exit 1
}
Write-Host "All Quest assemblies found!" -ForegroundColor Green

# Build
Write-Host "`nBuilding for Quest (netstandard2.1)..." -ForegroundColor Cyan
dotnet build ImpactFrame.Quest.csproj -c $Configuration

$outputDll = "bin\$Configuration\netstandard2.1\ImpactFrame.dll"
if (-not (Test-Path $outputDll)) {
    Write-Error "Build failed - DLL not found at $outputDll"
    exit 1
}

$size = (Get-Item $outputDll).Length
Write-Host "`nBuild successful!" -ForegroundColor Green
Write-Host "Output: $outputDll ($([math]::Round($size/1KB)) KB)" -ForegroundColor Gray

# Install if requested (copies to local Mods for PC testing, user must sideload to Quest)
if ($Install) {
    Write-Host "`nCopying to local Mods folder for reference..." -ForegroundColor Cyan
    $modsPath = "C:\Program Files (x86)\Steam\steamapps\common\BONELAB\Mods"
    if (Test-Path $modsPath) {
        Copy-Item $outputDll -Destination (Join-Path $modsPath "ImpactFrame.Quest.dll") -Force
        Write-Host "Copied to $modsPath\ImpactFrame.Quest.dll" -ForegroundColor Green
    }
    Write-Host "`nTO INSTALL ON QUEST:" -ForegroundColor Yellow
    Write-Host "  adb push $outputDll /sdcard/ModData/com.stresslevelzero.bonelab/Mods/ImpactFrame.dll" -ForegroundColor Gray
}

Write-Host "`nDone!" -ForegroundColor Cyan