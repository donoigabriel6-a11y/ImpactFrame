# ImpactFrame Mod for Bonelab

Adds impact frame effects (screen freeze, color flash, time slow) when hitting NPCs or players with bone weapons.

## Features
- ✅ **Toggle on/off** - Enable or disable the mod entirely
- ✅ **Configurable chance** - Set probability (0-100%) for impact frame to trigger
- ✅ **Customizable color** - RGBA color for the impact flash effect
- ✅ **Adjustable duration** - How long the impact frame lasts
- ✅ **Intensity control** - Strength of time slow and visual effect
- ✅ **Persistent settings** - Saved via MelonPreferences
- ✅ **Works on NPCs & Players** - Triggers on both

## Installation

1. Install **MelonLoader** for Bonelab (v0.6.4+)
2. Build the mod (see Building below) or download the compiled `ImpactFrame.dll`
3. Place `ImpactFrame.dll` in your Bonelab `Mods` folder:
   ```
   %USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\
   ```
4. Launch Bonelab

## Configuration

Settings are stored in `MelonPreferences.cfg` and can be edited in-game via the MelonLoader menu or by editing the config file directly:

```ini
[ImpactFrame]
Enabled = true
ImpactChance = 0.3
ImpactColorR = 1.0
ImpactColorG = 0.2
ImpactColorB = 0.2
ImpactColorA = 1.0
ImpactDuration = 0.1
ImpactIntensity = 0.5
```

### Settings Explained

| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| `Enabled` | true/false | `true` | Master toggle for the mod |
| `ImpactChance` | 0.0 - 1.0 | `0.3` | Probability of impact frame triggering on hit (30%) |
| `ImpactColorR/G/B/A` | 0.0 - 1.0 | `1, 0.2, 0.2, 1` | RGBA color of the flash (Red by default) |
| `ImpactDuration` | > 0 | `0.1` | Duration in seconds of the impact frame |
| `ImpactIntensity` | 0.0 - 1.0 | `0.5` | Strength of time slow (0.5 = 50% speed) |

## Building from Source

### Prerequisites
- .NET SDK 6.0+
- Bonelab installed with MelonLoader

### Build Steps
```bash
cd ImpactFrame
dotnet build -c Release
```

The compiled DLL will be in `bin/Release/netstandard2.0/ImpactFrame.dll`

### References Required
The project references these from your MelonLoader installation:
- `MelonLoader.dll`
- `0Harmony.dll`
- `UnityEngine.CoreModule.dll`
- Bonelab game assemblies (via `Bonelab.GameLibs` NuGet)

## How It Works

1. **Hooks into** `CombatManager.OnHit` via Harmony
2. **Detects** bone weapon hits on NPCs/Players
3. **Rolls chance** based on `ImpactChance` setting
4. **Applies effect**: Time slow + color flash on target rig
5. **Restores** normal time after `ImpactDuration`

## Customization Ideas

Edit `ImpactFrameMod.cs` to:
- Add different effects per weapon type
- Add screen shake
- Add particle effects
- Add sound effects
- Make it work on ranged weapons too

## License

MIT - Feel free to modify and share!