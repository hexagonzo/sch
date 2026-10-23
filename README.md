# sch (simple card history)

A small, Hearthstone-inspired sidebar that lists recently played cards in the current combat. **Early-access source preview — not game-tested.** No gameplay modifications.

## Features
- Cards appear newest-first; autoplay is included.
- Overlay on the right or left side. Gear button opens **in-game settings**.
- Configurable enable/disable, visible rows (3–35), width, font size, opacity.
- Settings saved in `user://sch/settings.json`.
- UI redraws only on changes. Max 100 stored card names per fight. All nodes removed after combat.
- No external mod framework, no custom `.pck`, no network requests.

## Requirements
- Slay the Spire 2 Early Access (compatible API not guaranteed across patches).
- .NET **9 SDK** for building (not needed when installing a compiled release).
- The game's own `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll` for compilation.

## Build + one-command install (Windows)
Open PowerShell at the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2'
```

This generates the DLL and copies it and `mod_manifest.json` to `<game>/mods/sch/`.

Linux/macOS: set `STS2_DATA_DIR` to the platform-specific folder containing `sts2.dll`, then run `bash scripts/build.sh`. Copy the DLL and manifest as described below.

## Manual install (compiled release)
1. Create `<game>/mods/sch/`.
2. Put `sch.dll` and `mod_manifest.json` in that folder.
3. Launch the game, enable sch in **Mods**, and enter combat.
4. Press the gear on the sidebar to configure it.

The mod does **not** require ModConfig or BaseLib. To uninstall, remove the folder. Settings remain in Godot user data until manually removed.

## Compatibility / known limitations
- **Not compiled against your local game build or tested inside STS2**. Please validate before publishing a release or Workshop item.
- Game patches may change the `CardModel.OnPlayWrapper` signature, the combat lifecycle events, or Godot UI behavior.
- Text-based history only; the first version has no card art or tooltips.
- Card names use the model's public `Title`/`Name`/`DisplayName`/`Id` when present, with a safe class-name fallback. Localization may therefore vary.
- The mod tracks executed card-play hook calls, not queued card commands, and may include auto-played cards.
- In multiplayer it follows events observed by the local client; multiplayer correctness has not been verified.

## Release checklist
- Build against the exact target game version's assemblies.
- Start a combat, play 20+ cards and verify order; test autoplay.
- Finish combat, begin another, verify empty history.
- Test every settings control and restart the game to verify persistence.
- Test at small and ultrawide resolutions; test with other overlay mods.
- Package the **compiled** DLL and manifest, not just these sources.

## Technical references
- [Modding tutorial](https://github.com/fresh-milkshake/Modding-Tutorial)
- [Timeline](https://github.com/sts2mods/Timeline) (independent event overlay and useful API reference)
- [ModConfig](https://github.com/xhyrzldf/ModConfig-STS2) (optional framework; not needed here)

MIT licensed.
