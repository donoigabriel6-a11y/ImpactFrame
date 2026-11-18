# Installation Guide for ImpactFrame Mod

## Step 1: Install MelonLoader

1. Download **MelonLoader 0.6.4+** from:
   - https://github.com/HerpDerpinstine/MelonLoader/releases
   - Or use the installer: `MelonLoader.Installer.exe`

2. Run the installer and select:
   - **Game**: Bonelab
   - **Install Location**: Your Bonelab installation folder
   - **Version**: 0.6.4 or later

3. Launch Bonelab once with MelonLoader to generate the `Libraries` folder

## Step 2: Build the Mod

After MelonLoader is installed and you've launched Bonelab once:

```cmd
cd C:\Users\gdono\Desktop\ImpactFrame
build.bat
```

Or manually:
```cmd
dotnet build -c Release
```

## Step 3: Install the Mod

Copy the built DLL to your Mods folder:
```cmd
copy bin\Release\netstandard2.0\ImpactFrame.dll "%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\"
```

## Step 4: Configure

Launch Bonelab. The mod will create default settings in `MelonPreferences.cfg`. You can edit them:

### Via Config File
Edit `%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonPreferences.cfg`:
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

### In-Game (MelonLoader Menu)
1. Press **F1** (or your MelonLoader menu key)
2. Go to **Mod Settings** → **ImpactFrame**
3. Adjust sliders/toggles in real-time

## Troubleshooting

### "Reference not found" errors
Make sure you've launched Bonelab with MelonLoader at least once - this generates the `Libraries` folder with all required assemblies.

### Mod doesn't load
Check `Player.log` in:
```
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Player.log
```
Look for `[ImpactFrame]` messages.

### Build fails with "could not find file"
Verify these files exist:
```
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonLoader\MelonLoader.dll
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonLoader\Libraries\0Harmony.dll
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonLoader\Libraries\UnityEngine.CoreModule.dll
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonLoader\Libraries\SLZ.Marrow.dll
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonLoader\Libraries\SLZ.Combat.dll
%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonLoader\Libraries\SLZ.Rig.dll
```

## Quick Test

1. Spawn a bone weapon (skull, femur, etc.) in-game
2. Hit an NPC or player
3. You should see a red flash + brief time slow (30% chance by default)

## Customization

Change the color to something else by editing RGB values (0-1):
- **Blue**: R=0, G=0.2, B=1
- **Green**: R=0.2, G=1, B=0.2
- **Yellow**: R=1, G=1, B=0
- **White**: R=1, G=1, B=1
- **Purple**: R=0.6, G=0.2, B=1

Increase `ImpactChance` to 1.0 for 100% trigger rate.
Increase `ImpactIntensity` to 0.8+ for stronger slow-mo.