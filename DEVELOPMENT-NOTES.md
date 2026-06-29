# Development Notes

Technical reference and build guide for the CharacterSync (Extended fork) codebase.

---

## Build Requirements

- [.NET 10 SDK](https://dot.net/download) — the project targets `net10.0-windows` to match Dalamud 15.x
- [XIVLauncher](https://goatcorp.github.io/) with Dalamud installed — the build references assemblies from `%APPDATA%\XIVLauncher\addon\Hooks\dev\`

---

## Build Instructions

```bash
git clone https://github.com/redactedkai/Dalamud.CharacterSync-Extended.git
cd Dalamud.CharacterSync-Extended
dotnet build Dalamud.CharacterSync.sln -c Debug
```

Output: `Dalamud.CharacterSync\bin\Debug\Dalamud.CharacterSync.dll`

### Loading as a dev plugin

1. Launch FFXIV via XIVLauncher
2. In-game: `/xldev` → **Dev Plugin Locations** → **Add**:
   ```
   <path-to-repo>\Dalamud.CharacterSync-Extended\Dalamud.CharacterSync\bin\Debug
   ```
3. Click **Load**. After a rebuild, click **Reload** to hot-reload without restarting the game.

> **Note:** If the installed version of Character Data Sync is active, disable it first — two instances of the same plugin will conflict.

> **Note:** The plugin only enables syncing when loaded at the Boot stage (game start). Loading or reloading it mid-session will show a notification but will not apply sync until the next game restart.

---

## Existing sync mechanism (file hook)

The original plugin hooks the game's native `FileInterface::OpenFile` function via signature scan (`PluginAddressResolver.cs`). When the game opens a character DAT file (e.g. `HOTBAR.DAT`) for an alt, the detour rewrites the path to point at the main character's folder instead:

```
FFXIV_CHR{altCid}/HOTBAR.DAT  →  FFXIV_CHR{mainCid}/HOTBAR.DAT
```

Files synced this way: `HOTBAR.DAT`, `MACRO.DAT`, `KEYBIND.DAT`, `LOGFLTR.DAT`, `COMMON.DAT`, `CONTROL0.DAT`, `CONTROL1.DAT`, `GS.DAT`, `ADDON.DAT`

`UISAVE.DAT` is **explicitly excluded** from this hook — it contains per-character data (retainer timers, teleport history, friends lists, etc.) that must not be shared. The Command Panel lives inside `UISAVE.DAT`, which is why it requires a separate approach.

---

## Command Panel sync (`CommandPanelSync.cs`)

### Why a different approach

The Command Panel (Quick Panel) is stored as segment `0x29` (`QPNL`) inside `UISAVE.DAT`. Because the file hook can't selectively sync one segment out of `UISAVE.DAT`, the sync is done **in-memory at login** instead.

### Data source

The sync reads and writes directly to `QuickPanelModule.Instance()` — the live in-memory struct managed by the game's UI system. The data region is:

```
Start:  QuickPanelModule.Instance() + sizeof(UserFileManager.UserFileEvent)  (offset 0x48)
Size:   sizeof(QuickPanelModule) - sizeof(UserFileManager.UserFileEvent)      (0x228 bytes)
End:    QuickPanelModule.Instance() + 0x270
```

This covers:
- All 4 panels × 25 slots (command types + command IDs)
- 32-byte undocumented region (0x248–0x268) — stores custom page icons for each panel tab
- Settings (0x268): tint, close-on-action, open-at-cursor, disable-drag, panel open index

`UiSavePackModule.GetSegment(DataSegment.QPNL)` was considered but rejected — it returns a pointer to the serialization buffer, not the live struct, so it misses the page icon and settings region.

### Permanent write

After writing to `QuickPanelModule` memory, `UiSavePackModule.Instance()->SaveFile(true)` is called to persist the change to `UISAVE.DAT` on disk. This mirrors the pattern used by VanillaPlus's `HUDPresetManager.LoadPreset()` (`addonConfig->SaveFile(true)`).

### Login/logout flow

```
OnLogin (main):   read QuickPanelModule → save to CommandPanelSync/Shared.v2.qpnl.dat
OnLogin (alt):    read Shared.v2.qpnl.dat → write to QuickPanelModule → SaveFile(true)
OnLogout (main):  read QuickPanelModule → overwrite Shared.v2.qpnl.dat (capture in-session edits)
OnLogout (alt):   no-op (permanent write already happened at login)
```

Thread safety: `Login`/`Logout` events fire on the framework thread — memory reads/writes in those handlers are safe. File I/O runs off-thread via `Task.Run`; memory writes from async continuations use `Service.Framework.RunOnFrameworkThread`.

### Snapshot location

```
%APPDATA%\XIVLauncher\pluginConfigs\Dalamud.CharacterSync\CommandPanelSync\Shared.v2.qpnl.dat
```

### First-login icon refresh quirk

On an alt's very first login, the game initialises the hotbar UI from `UISAVE.DAT` **before** Dalamud's `Login` event fires. Our sync writes correct data to `QuickPanelModule` and calls `SaveFile(true)`, but the already-rendered hotbar shortcut slots do not redraw automatically. Opening the Command Panel settings dialog forces the game to re-read `QuickPanelModule` and the icons update immediately.

On all subsequent logins the icons are correct from the start — `UISAVE.DAT` already contains the synced data before the UI initialises.

There is no documented virtual function on `QuickPanelModule` or `UiSavePackModule` that forces a hotbar redraw, so this quirk is left as-is.

---

## Project structure

| File | Purpose |
|---|---|
| `CharacterSyncPlugin.cs` | Plugin entry point, file-hook setup, Boot/Installer/Update load reason handling |
| `PluginAddressResolver.cs` | Signature scan for `FileInterface::OpenFile` |
| `CommandPanelSync.cs` | Command Panel sync — login/logout handlers, file I/O, memory read/write |
| `Config/CharacterSyncConfig.cs` | Serialised plugin configuration |
| `Interface/ConfigWindow.cs` | ImGui configuration window |
| `Service.cs` | Dalamud service injection container |
