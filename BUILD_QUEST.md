# Building ImpactFrame for Quest (Android)

## Overview

The mod now supports **both PC (Windows) and Quest (Android)** from a single codebase using conditional compilation (`#if QUEST`).

## Architecture

| Platform | Target Framework | Define Constant | Default Particles | Time Scale |
|----------|------------------|-----------------|-------------------|------------|
| PC | net472 | (none) | Enabled (200 max) | 0.02x |
| Quest | netstandard2.1 | `QUEST;ANDROID` | Disabled (50 max) | 0.05x |

## Prerequisites for Quest Build

### 1. Install MelonLoader on Quest
- Use [QuestLoader](https://github.com/QuestLoader/QuestLoader) or sideload MelonLoader APK
- Launch Bonelab on Quest once to generate IL2CPP assemblies

### 2. Extract Quest Assemblies
```bash
# Create local directory
mkdir %USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\QuestAssemblies

# Pull IL2CPP generated assemblies (cpp2il_out)
adb pull /sdcard/ModData/com.stresslevelzero.bonelab/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/ %USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\QuestAssemblies

# Pull netstandard2.1 assemblies
adb pull /sdcard/ModData/com.stresslevelzero.bonelab/MelonLoader/netstandard2.1/ %USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\QuestAssemblies/netstandard2.1
```

## Building

### PC Version (Windows)
```bash
dotnet build ImpactFrame.csproj -c Release
# Output: bin/Release/net472/ImpactFrame.dll
```

### Quest Version (Android ARM64)
```bash
# Option 1: Use build script
.\build-quest.ps1 -Install

# Option 2: Manual
dotnet build ImpactFrame.Quest.csproj -c Release
# Output: bin/Release/netstandard2.1/ImpactFrame.dll
```

## Installing on Quest

```bash
# Push mod to Quest
adb push bin/Release/netstandard2.1/ImpactFrame.dll /sdcard/ModData/com.stresslevelzero.bonelab/Mods/ImpactFrame.dll

# Or use QuestLoader's mod installer
```

## Platform-Specific Defaults

The mod automatically adjusts for Quest performance:

| Setting | PC Default | Quest Default |
|---------|------------|---------------|
| Particles | Enabled (200 max) | **Disabled** (50 max) |
| TimeScale | 0.02x (near stop) | 0.05x (less aggressive) |
| FlashIntensity | 2.0 | 1.5 |
| ShakeMagnitude | 0.12 | 0.08 |
| SoundVolume | 0.7 | 0.6 |
| HitMarkerSize | 0.3 | 0.25 |
| Combo Font | 42 | 36 |
| KillSlowMo Scale | 0.1 | 0.15 |

## Configuration (Works on Both Platforms)

Press **F1** (PC) or use QuestLoader menu → **Mod Settings** → **ImpactFrame**

All 35+ settings work identically on both platforms:
- `ActiveEffects` bitmask (disable Particles on Quest for performance)
- `ImpactChance`, `Cooldown`, `MinDamageThreshold`
- Time, Flash, Shake, Particle, Sound, HitMarker, Combo, KillSlowMo settings

## Code Structure

```
ImpactFrameMod.cs          # Single source file with #if QUEST blocks
ImpactFrame.csproj         # PC build (net472)
ImpactFrame.Quest.csproj   # Quest build (netstandard2.1)
build-quest.ps1            # Quest build script
```

## Conditional Compilation

```csharp
#if QUEST
    // Quest-specific code
    public const bool IS_QUEST = true;
    public const int DEFAULT_PARTICLE_CAP = 50;
#else
    // PC-specific code
    public const bool IS_QUEST = false;
    public const int DEFAULT_PARTICLE_CAP = 200;
#endif

// Usage in code:
public static bool EnableParticles = !IS_QUEST; // Auto-disabled on Quest
```

## CI/CD for Both Platforms

Add to GitHub Actions:

```yaml
jobs:
  build-pc:
    runs-on: [self-hosted, windows, bonelab]
    steps:
      - uses: actions/checkout@v4
      - run: dotnet build ImpactFrame.csproj -c Release
      - uses: actions/upload-artifact@v4
        with:
          name: ImpactFrame-PC
          path: bin/Release/net472/ImpactFrame.dll

  build-quest:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '7.0.x'
      # Note: Requires Quest assemblies as artifacts or cached
      - run: dotnet build ImpactFrame.Quest.csproj -c Release
      - uses: actions/upload-artifact@v4
        with:
          name: ImpactFrame-Quest
          path: bin/Release/netstandard2.1/ImpactFrame.dll
```

## Testing Quest Build Locally

Without a Quest device, you can verify the build compiles:

```bash
# This will fail without Quest assemblies but validates syntax
dotnet build ImpactFrame.Quest.csproj -c Release --no-restore 2>&1 | head -20
```

## Performance Tips for Quest

1. **Disable Particles** - `ActiveEffects` without bit 16 (Particles)
2. **Reduce ParticleCount** - Max 50 (enforced in config)
3. **Lower TimeScale** - 0.05x instead of 0.02x
4. **Shorter Duration** - 0.12s default
5. **Disable Combo UI** if not needed
6. **Use FlashTargetOnly** - avoids full-screen effects

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Assembly not found" | Verify Quest assemblies extracted to correct path |
| "CS0246: Type not found" | Check all UnityEngine.* references in .csproj |
| Mod doesn't load on Quest | Check QuestLoader logs: `adb logcat | grep MelonLoader` |
| Crashes on hit | Disable Particles, reduce ParticleCount |
| Low FPS | Disable ScreenShake, reduce Duration |

## File Checklist for Quest Release

When releasing, provide both:
- `ImpactFrame.dll` (PC, net472) → `Mods/` folder
- `ImpactFrame.dll` (Quest, netstandard2.1) → Quest `Mods/` folder via adb

Both built from the **same source code**!