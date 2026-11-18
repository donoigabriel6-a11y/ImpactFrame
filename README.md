# ImpactFrame Mod for Bonelab

**v2.0** - Professional impact frame effects with Quest (Android) support.

Adds cinematic impact frames when hitting NPCs/players with bone weapons: time stop/slow, screen flash, camera shake, particles, sound, hit markers, and combo system.

## Features

| Category | Features |
|----------|----------|
| **Time Effects** | TimeStop (near-freeze), TimeSlow (smooth slow-mo), custom ease curves |
| **Visual Effects** | Target emission flash, screen shake (Perlin noise), world-space hit markers |
| **Particles** | Burst particles with gradient colors (configurable, optional) |
| **Audio** | Procedural synthesized hit sounds (no external files) |
| **Combat** | Combo counter with on-screen display, kill slow-mo bonus |
| **Detection** | Bone weapon detection (20+ bone names), damage threshold, NPC/Player targeting |
| **Configuration** | 35+ settings via MelonPreferences (F1 menu) |
| **Platforms** | **PC (Windows)** and **Quest (Android)** from single codebase |

## Installation

### PC (Windows)
1. Install **MelonLoader 0.6.4+** for Bonelab
2. Build or download `ImpactFrame.dll`
3. Place in Mods folder:
   ```
   %USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\
   ```
4. Launch Bonelab

### Quest (Android)
1. Install **MelonLoader** on Quest (via QuestLoader)
2. Launch Bonelab on Quest once (generates assemblies)
3. Extract Quest assemblies (see [BUILD_QUEST.md](BUILD_QUEST.md))
4. Build Quest version: `dotnet build ImpactFrame.Quest.csproj -c Release`
5. Install via adb:
   ```bash
   adb push bin/Release/netstandard2.1/ImpactFrame.dll /sdcard/ModData/com.stresslevelzero.bonelab/Mods/
   ```

## Configuration (In-Game)

Press **F1** (PC) or open QuestLoader menu → **Mod Settings** → **ImpactFrame**

### Quick Settings
| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Master toggle |
| `ImpactChance` | `0.35` | Trigger probability (35%) |
| `ActiveEffects` | `127` | Bitmask: 1=TimeStop, 2=TimeSlow, 4=Flash, 8=Shake, 16=Particles, 32=Sound, 64=HitMarker |
| `TimeScale` | `0.02` (PC) / `0.05` (Quest) | Time scale during effect |
| `Duration` | `0.15` | Effect duration (seconds) |

### Effect Bitmask Values
```
1   = TimeStop      (near-total freeze)
2   = TimeSlow      (smooth slow-mo)
4   = ScreenFlash   (target emission glow)
8   = ScreenShake   (camera shake)
16  = Particles     (burst particles)
32  = Sound         (procedural hit sound)
64  = HitMarker     (world-space crosshair)
127 = All effects
```

### Advanced Settings
- **Trigger**: `RequireBoneWeapon`, `TriggerOnNPCs`, `TriggerOnPlayers`, `MinDamageThreshold`, `Cooldown`
- **Time**: `EaseInTime`, `EaseOutTime` for custom curves
- **Flash**: `FlashColor` (RGBA), `FlashIntensity`, `FlashTargetOnly`
- **Shake**: `ShakeMagnitude`, `ShakeRoughness`, `ShakeFadeIn/Out`
- **Particles**: `EnableParticles`, `ParticleCount`, `Size`, `Lifetime`, `Speed`, `Color` (gradient)
- **Sound**: `EnableSound`, `SoundVolume`, `SoundPitch`
- **HitMarker**: `ShowHitMarker`, `Size`, `Duration`, `Color`, `Thickness`
- **Combo**: `EnableCombo`, `ComboWindow`, `MaxComboCount`, `ShowComboUI`
- **Kill Slow-Mo**: `KillSlowMo`, `KillSlowMoDuration`, `KillSlowMoScale`

## Building from Source

### PC Version
```bash
dotnet build ImpactFrame.csproj -c Release
# Output: bin/Release/net472/ImpactFrame.dll
```

### Quest Version
```bash
# Requires Quest assemblies extracted (see BUILD_QUEST.md)
dotnet build ImpactFrame.Quest.csproj -c Release
# Output: bin/Release/netstandard2.1/ImpactFrame.dll
```

### Build Scripts
- `build.bat` - Simple PC build
- `build-local.ps1` - PC build with auto-install (`-Install`)
- `build-quest.ps1` - Quest build with instructions

## Platform Differences

| Feature | PC (net472) | Quest (netstandard2.1) |
|---------|-------------|------------------------|
| Target Framework | .NET Framework 4.7.2 | .NET Standard 2.1 |
| Particles Default | Enabled (200 max) | **Disabled** (50 max) |
| TimeScale Default | 0.02x | 0.05x |
| Flash Intensity | 2.0 | 1.5 |
| Shake Magnitude | 0.12 | 0.08 |
| Combo Font Size | 42 | 36 |
| Sound Length | 0.12s | 0.10s |

*All controlled via `#if QUEST` conditional compilation in single source file.*

## How It Works

1. **Harmony patches** `CombatManager.OnHit` (via reflection)
2. **Detects** bone weapons by name (skull, femur, humerus, ribs, spine, etc.)
3. **Filters** by target type (NPC/Player), damage threshold, cooldown
4. **Rolls chance** based on `ImpactChance`
5. **Applies effects** via coroutine:
   - Time manipulation with easing curves
   - Target renderer emission flash
   - Camera shake with Perlin noise
   - Particle emission at hit point
   - Procedural audio synthesis
   - World-space hit marker crosshair
6. **Restores** original state after duration

## Files

```
ImpactFrameMod.cs          # Main mod code (shared, #if QUEST)
ImpactFrame.csproj         # PC build project
ImpactFrame.Quest.csproj   # Quest build project
build.bat                  # Simple PC build
build-local.ps1            # PC build + install
build-quest.ps1            # Quest build script
BUILD_QUEST.md             # Quest build guide
MELONLOADER_INSTALL.md     # MelonLoader install guide
```

## Customization

Edit `ImpactFrameMod.cs` to add:
- Weapon-type specific effects
- Different flash colors per enemy type
- Screen-space effects (vignette, chromatic aberration)
- Haptic feedback (Quest controllers)
- Replay system for epic moments

## Requirements

- **PC**: Bonelab (Steam) + MelonLoader 0.6.4+
- **Quest**: Bonelab (Quest Store) + MelonLoader + QuestLoader
- **Build**: .NET 7 SDK, Bonelab with MelonLoader launched once

## License

MIT - Feel free to modify, share, and use in modpacks!

---

**Credits**: Built with MelonLoader, HarmonyLib, and Unity IL2CPP reflection magic.