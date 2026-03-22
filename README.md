# Spire Changelog

A Slay the Spire 2 mod that surfaces card, relic, and enemy balance changes from recent patches directly in the game UI.

## Features

- **Card borders** — Gold glow on cards that were changed in recent patches (deck views, rewards, shop — not during combat)
- **Relic borders** — Colored frame on changed relics, tinted by rarity
- **Inspect tooltips** — Right-click a card or relic to see its patch history
- **Enemy hover tips** — Hover over an enemy in combat to see what changed
- **Card library filter** — "Recently Changed" toggle in the card library
- **Console command** — Query changelog data via the in-game console

Only the 3 most recent patches are tracked. The mod is informational only (`affects_gameplay: false`) and won't affect multiplayer compatibility checks.

## Installation

1. Download the latest release
2. In Steam, right-click **Slay the Spire 2** → **Manage** → **Browse Local Files** to open the game folder
3. Create a `mods/SpireChangelog/` folder inside the game directory
   - On macOS, navigate into `SlayTheSpire2.app/Contents/MacOS/` first
4. Extract `SpireChangelog.dll` and `SpireChangelog.json` into that folder
5. Launch the game — the mod loads automatically

## Building from source

Requires .NET 9.0 SDK and game DLLs (`sts2.dll`, `0Harmony.dll`) in `lib/`.

```bash
dotnet build
./install.sh   # macOS: builds and copies to game mods directory
```

## Console Commands

Open the in-game console (backtick key) and use:

| Command | Description |
|---------|-------------|
| `changelog stats` | Total changes and patch count |
| `changelog list` | All changed cards, relics, and enemies |
| `changelog list cards` | Only changed cards |
| `changelog list relics` | Only changed relics |
| `changelog list enemies` | Only changed enemies |
| `changelog card <name>` | Patch history for a specific card |
| `changelog relic <name>` | Patch history for a specific relic |
| `changelog enemy <name>` | Patch history for a specific enemy |

## Limitations

- Only shows changes from the **3 most recent patches** (`MaxPatchesToShow` in `ChangelogDatabase.cs`)
- Name matching uses fuzzy lookup — some renamed entities may not match

## Note

This mod was mostly generated with AI (Claude Code).

