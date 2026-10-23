# sch v0.3.2 — Slay the Spire 2 card history

A lightweight, configurable Hearthstone-inspired sidebar. The existing **v0.2.1 working overlay bootstrap is preserved**, with multiplayer-aware owner attribution.

## New in v0.3.2

- **Scrollable card history**: fixed-height list, mouse-wheel scrolling, newest first. The title bar remains draggable.
- **Collapsible** with the +/- button beside the settings gear. Dragging works when collapsed too.
- **History retention** selectable at 100, 250 (default), or 500 cards, with oldest discarded.
- **Visible rows** (3 to 25, default 10) controls viewport height, not number of cards retained.
- Scroll position is preserved as best-effort when viewing older cards and new cards arrive.
- Incremental row insertion avoids recreating the full list for ordinary card plays.


- Each recorded card has its `CardModel.Owner.NetId`, local/remote designation and class.
- Colored strips for Ironclad, Silent, Defect, Necrobinder, Regent (and a Watcher fallback).
- Toggle class colors and player labels; filter **Everyone / Only me / Teammates** using the gear in combat.
- **Draggable overlay**: left-click and drag the **CARD HISTORY [drag]** title bar while in combat. The exact position is saved on mouse release and restored next time.
- **Reset window position** in the gear settings returns to left/right docking; "Right side (default)" also resets a custom position.
- Position is clamped to the visible window on load, drag and resolution changes. Dragging never touches game state.
- Old controls remain: number of visible cards, width, font size, opacity, enable/disable.
- All settings persist in `user://sch/settings.json`.
- Pure local UI: no custom network messages; no gameplay state changes; `affects_gameplay: false`.
- O(100) bounded storage; UI refresh only when cards/settings change; no network activity.

### IMPORTANT — what multiplayer support means

Only card plays seen by this client's `CardModel.OnPlayWrapper` are recorded. Co-op remote-card visibility and order have **not yet been verified in a 2-player session**. Missing remote cards cannot be made to appear without changing the hook to a shared game event. This is an **experimental multiplayer build**, not a guaranteed co-op release. The label for teammates uses abbreviated network IDs when no safe player display-name API is available. Classes not recognized get a neutral color.

## Build + install (Windows)

Install the .NET 9 SDK. Close the game. In PowerShell, at the repository root:

```powershell
.\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
```

The script compiles against your installed game libraries and copies `sch.dll` and `mod_manifest.json` to `<game>/mods/sch/`. Restart the game and start a combat. To uninstall, remove the folder.

## Verification

The following log messages were **previously verified by the user for v0.2.1**: `Overlay activated explicitly; panel=ready`, `Combat started; overlay=ready`, and `Card recorded: Strike; overlay=ready`. **v0.3.2 is not yet built or runtime-tested against game DLLs.**

```powershell
Select-String "$env:APPDATA\SlayTheSpire2\logs\godot.log" -Pattern '\[sch\]' | Select-Object -Last 25 | ForEach-Object { $_.Line }
```

Suggested co-op test: both players install the same version; play alternating cards, verify ordering and class colors on each client; try two identical classes, auto-play and filters; restart and verify settings. For matchmaking compatibility, `affects_gameplay: false` does not guarantee all mod configurations are accepted.

## Design notes

Owner attribution uses `CardModel.Owner` and the established `LocalContext.IsMe(owner)` method. Class matching is best-effort from the character model ID/type and is not dependent on a localized character label. No third-party framework is required. Godot's `_Ready()` callback for a raw modded C# subclass was not invoked in v0.2.0; explicit `Activate()` from `NRun._Ready` fixed this in v0.2.1 and remains in v0.3.0.

## Links

- https://github.com/Hexpion/Slay-the-spire-2 (game API reference mirror)
- https://github.com/TomFrederik/sts_modding/blob/main/MODDING_GUIDE.md

MIT license.
