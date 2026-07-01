# Character Data Sync (Extended)

A fork of [Xpahtalo/Dalamud.CharacterSync](https://github.com/Xpahtalo/Dalamud.CharacterSync) that adds command panel syncing across your alts.

## Install

> **Repo URL:** `https://raw.githubusercontent.com/redactedkai/Dalamud.CharacterSync-Extended/refs/heads/master/repo.json`

1. In-game, open **Dalamud Settings** -> **Experimental** -> **Custom Plugin Repositories**
2. Add the URL above and save.
3. Open **Plugin Installer** -> search **Character Data Sync (Extended)** -> Install
4. Open `/pcharsync`, log in as your main character, and click **"Set save data to current character"**
5. (Recommended) RESTART YOUR GAME! This ensures the sync will work properly.
6. Log in on any alt or newly created character

## Known Quirks

### Command Panel shortcut icons (first login only)

On the first login to an alt, the panel shortcut icons in a hotbar may show as the default numbered placeholders instead of your main character's icons.

**Fix:** Open the Command Panel settings (gear icon at top-right), then click any of the shortcut buttons on your hotbar. This is a one-time fix.

| Before | After |
|---|---|
| ![Before](doc-media/command-panel-shortcut-before.png) | ![After](doc-media/command-panel-shortcut-after.png) |

## Build Instructions

For build instructions, dev plugin loading, and technical implementation details, see [DEVELOPMENT-NOTES.md](DEVELOPMENT-NOTES.md).
