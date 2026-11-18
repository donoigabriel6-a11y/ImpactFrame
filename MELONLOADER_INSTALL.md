# Installing MelonLoader for Bonelab

## Method 1: Official Installer (Easiest)

1. **Download MelonLoader Installer**
   - Go to: https://github.com/HerpDerpinstine/MelonLoader/releases
   - Download the latest `MelonLoader.Installer.exe` (v0.6.4 or newer)

2. **Run the Installer**
   - Double-click `MelonLoader.Installer.exe`
   - **Game Selection**: Click "Select" and browse to your Bonelab installation:
     ```
     C:\Program Files (x86)\Steam\steamapps\common\Bonelab
     ```
   - **Version**: Select "0.6.4" or latest stable
   - **Install Type**: "Install" (not "Extract")
   - Click **Install**

3. **Launch Bonelab Once**
   - Start Bonelab from Steam
   - You'll see the MelonLoader loading screen
   - Let it fully load to the main menu
   - Close the game
   - **This generates the `Libraries` folder** required for mod development

---

## Method 2: Manual Install (If Installer Fails)

1. **Download MelonLoader ZIP**
   - From releases: `MelonLoader.x64.zip`

2. **Extract to Bonelab Folder**
   - Extract all contents to:
     ```
     C:\Program Files (x86)\Steam\steamapps\common\Bonelab\
     ```
   - Should create: `MelonLoader/`, `doorstop_config.ini`, `winhttp.dll`

3. **Launch Once** (same as above)

---

## Verify Installation

Check these folders exist:

```
C:\Program Files (x86)\Steam\steamapps\common\Bonelab\
├── MelonLoader\
│   ├── MelonLoader.dll
│   ├── Libraries\
│   │   ├── 0Harmony.dll
│   │   ├── UnityEngine.CoreModule.dll
│   │   ├── UnityEngine.PhysicsModule.dll
│   │   ├── UnityEngine.AnimationModule.dll
│   │   ├── UnityEngine.UI.dll
│   │   ├── SLZ.Marrow.dll
│   │   ├── SLZ.Marrow.Warehouse.dll
│   │   ├── SLZ.Rig.dll
│   │   ├── SLZ.Combat.dll
│   │   └── SLZ.Bonelab.dll
│   └── Mods\
├── doorstop_config.ini
└── winhttp.dll
```

---

## For GitHub Actions Self-Hosted Runner

The runner machine needs the **exact same setup**:

1. Install Bonelab via Steam on the runner machine
2. Install MelonLoader using Method 1 above
3. Launch Bonelab once (can be headless with `-batchmode -nographics`)

```cmd
# Optional: Launch headless to generate libraries
cd "C:\Program Files (x86)\Steam\steamapps\common\Bonelab"
Bonelab.exe -batchmode -nographics -quit
```

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "Access denied" on Program Files | Run installer as Administrator |
| MelonLoader menu doesn't open (F1) | Check `doorstop_config.ini` has `enabled=true` |
| Libraries folder missing | Launch Bonelab normally once (not headless) |
| Mods not loading | Check `%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\` |
| Version mismatch | Use MelonLoader 0.6.4+ (match game version) |

---

## Useful Paths

| Purpose | Path |
|---------|------|
| Game Install | `C:\Program Files (x86)\Steam\steamapps\common\Bonelab\` |
| MelonLoader DLLs | `...\Bonelab\MelonLoader\Libraries\` |
| Mods Folder (Game) | `...\Bonelab\Mods\` |
| Mods Folder (User) | `%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Mods\` |
| MelonPreferences.cfg | `%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\MelonPreferences.cfg` |
| Player.log | `%USERPROFILE%\AppData\LocalLow\Stress Level Zero\Bonelab\Player.log` |

---

## Next Steps

After installation, build and install the mod:

```powershell
cd C:\Users\gdono\Desktop\ImpactFrame
.\build-local.ps1 -Install
```

Then launch Bonelab and press **F1** to open MelonLoader menu → Mod Settings → ImpactFrame