# Maintenance Guide

How to update this plugin after FFXIV patches.

---

## After every patch

### 1. Check FFXIVClientStructs

The community updates [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) within hours of most patches. Check if any of the structs this plugin uses have changed:

- `QuickPanelModule` — if fields are added or moved, `DataSize = 0x228` in `CommandPanelSync.cs` becomes wrong and will corrupt synced command panel data
- `UserFileManager.UserFileEvent` — if the inherited header changes, the data start offset (currently `sizeof(UserFileEvent)` = 0x48) shifts accordingly
- `UiSavePackModule` — used for `SaveFile(true)` to persist changes to `UISAVE.DAT`

Once FFXIVClientStructs confirms the relevant structs are correct, update the NuGet package:

```xml
<!-- Dalamud.CharacterSync/Dalamud.CharacterSync.csproj -->
<PackageReference Include="FFXIVClientStructs" Version="YYYY.M.D.X" />
```

> Do not release a post-patch build until FFXIVClientStructs has been verified for the structs above.

### 2. Verify the file hook signature

`PluginAddressResolver.cs` contains a byte signature for `FileInterface::OpenFile`. If the game recompiles that function, the signature will no longer match and the file hook (hotbars, macros, keybinds, etc.) silently stops working.

Check the Dalamud log on first login after a patch for a scan failure message. If the signature is broken, it needs to be re-scanned using a tool like [Dalamud.FindAnything](https://github.com/goatcorp/Dalamud.FindAnything) or IDA/Ghidra against the new binary.

### 3. Check for Dalamud API level bumps

Major FFXIV patches occasionally come with a Dalamud API level increment. If the API level changes:

1. Update `Dalamud.NET.Sdk` in `.csproj`:
   ```xml
   <Project Sdk="Dalamud.NET.Sdk/NEW_VERSION">
   ```
2. Fix any breaking API changes (check the [Dalamud migration guide](https://dalamud.dev/))
3. Update `DalamudApiLevel` in `repo.json` and `Dalamud.CharacterSync.Extended.json`

---

## Releasing an update

1. **Bump the version** in two places:

   `Dalamud.CharacterSync/Dalamud.CharacterSync.csproj`:
   ```xml
   <AssemblyVersion>1.0.1.0</AssemblyVersion>
   ```

   `repo.json`:
   ```json
   "AssemblyVersion": "1.0.1.0",
   "LastUpdated": 1234567890
   ```
   (`LastUpdated` is a Unix timestamp — use [unixtimestamp.com](https://www.unixtimestamp.com/) or `[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()` in PowerShell)

2. **Build a clean release:**
   ```
   dotnet clean
   dotnet build Dalamud.CharacterSync.sln -c Release
   ```
   Output ZIP: `Dalamud.CharacterSync\bin\x64\Release\Dalamud.CharacterSync.Extended\latest.zip`

3. **Create a new GitHub release** tagged with the new version (e.g. `1.0.1.0`) and upload `latest.zip` as a release asset.

4. **Update `DownloadLink*` URLs** in `repo.json` to point to the new tag:
   ```json
   "DownloadLinkInstall": "https://github.com/redactedkai/Dalamud.CharacterSync-Extended/releases/download/1.0.1.0/latest.zip",
   "DownloadLinkTesting": "...",
   "DownloadLinkUpdate": "..."
   ```

5. **Commit and push** — Dalamud reads `repo.json` from the branch and will offer users an update when it sees the version bump.

---

## Key files at a glance

| File | What to check after a patch |
|---|---|
| `CommandPanelSync.cs` | `DataSize` constant if `QuickPanelModule` layout changed |
| `PluginAddressResolver.cs` | Signature for `FileInterface::OpenFile` |
| `Dalamud.CharacterSync.csproj` | `Dalamud.NET.Sdk` and `FFXIVClientStructs` package versions |
| `repo.json` | `AssemblyVersion`, `DalamudApiLevel`, `LastUpdated`, download URLs |
| `Dalamud.CharacterSync.Extended.json` | `AssemblyVersion`, `DalamudApiLevel` |
