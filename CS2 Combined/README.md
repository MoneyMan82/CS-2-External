# CS2 Combined

External overlay tool for Counter-Strike 2 that combines aim assistance, trigger bot, ESP, and auto bhop in one ImGui menu.

> **Warning:** This is **not** VAC-safe or undetectable. Using it on Valve servers may result in a permanent ban. See [Disclaimer](#disclaimer).

## Features

### Aimbot
- Aimbot with configurable FOV and smoothness
- Recoil control and recoil predictor (per-weapon spray presets)
- Visibility check with optional map raytracing (`.tri` collision meshes)
- Target lines and FOV circle overlay
- Casual / Deathmatch modes
- Configurable aim hotkey (default: Mouse 4)

### Trigger Bot
- Fires when crosshair is on a valid target
- Separate game mode, hotkey, click delay, and cooldown settings
- Live status panel (default hotkey: Mouse 5)

### ESP
- Box, bones, name, health, weapon, distance, snaplines, head dot, armor
- Color by visibility
- Casual / Deathmatch modes with optional teammate display

### Auto Bhop
- Writes jump input when on ground via `client.dll` button offset
- Hold Space mode (default) or always-on
- Live ground-state debug info

## Requirements

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Counter-Strike 2 running (`cs2.exe`)
- Run as **Administrator** if memory access fails

## Build

```powershell
dotnet build "CS2 Combined.sln" -c Release
```

Output:

```
CS2 Combined/CS2 Combined/bin/Release/net8.0-windows/CS2 Combined.exe
```

Close the exe before rebuilding if the copy step fails (file locked).

## Usage

1. Start CS2 and join a match.
2. Run `CS2 Combined.exe` from the build output folder.
3. Use the **CS2 Combined** overlay window to configure each tab.
4. Default hotkeys:
   - **Mouse 4** — aimbot (when enabled)
   - **Mouse 5** — trigger bot (when enabled)
   - **Space** — auto bhop (when enabled and “hold Space” is on)

The overlay attaches to the CS2 window automatically.

## Offset files

These files sit next to the exe and can be updated after CS2 patches:

| File | Source |
|------|--------|
| `offsets.json` | [cs2-dumper](https://github.com/a2x/cs2-dumper) `output/offsets.json` |
| `client_dll.json` | cs2-dumper `output/client_dll.json` |
| `buttons.json` | cs2-dumper `output/buttons.json` |

On startup the app tries to load local offsets, validate them against the running game, and optionally fetch updated `offsets.json` from cs2-dumper if the current set is invalid.

## Map raytracing (optional)

For wall checks via map geometry, place `.tri` files in a `maps/` folder next to the exe (e.g. `maps/de_dust2.tri`). Without map files, visibility falls back to strict behavior when raytracing is enabled.

## Project structure

```
CS2 Combined/
├── CS2 Combined.sln
├── CS2 Combined/
│   ├── Program.cs          # Main loop
│   ├── Renderer.cs         # ImGui overlay + tabs
│   ├── GameMemory.cs       # Process memory read/write
│   ├── offsets.json
│   ├── client_dll.json
│   ├── buttons.json
│   └── weapon_recoil.json
└── README.md
```

## Disclaimer

This project is game-modification software for Counter-Strike 2. It reads and writes CS2 process memory and simulates input while the game is running.

**This is not VAC-safe or "undetectable."** Using this software on Valve-controlled servers may result in a permanent VAC ban, reduced Trust Factor, or other restrictions. Detection methods change over time; absence of bans in testing does not mean the software is safe.

This repository is provided for transparency and learning about external tooling. The authors do not encourage cheating in online matchmaking. Use at your own risk on accounts you are willing to lose.

You are responsible for complying with Valve's [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/) and applicable laws.

## License

This project is licensed under the [MIT License](LICENSE). Add your name to the copyright line in `LICENSE` if you want it attributed (e.g. `Copyright (c) 2026 YourGitHubUsername`).

Third-party data and libraries (cs2-dumper output, NuGet packages, recoil preset sources) remain under their own terms — see [Credits](#credits).

## Credits

- Offsets/schema: [a2x/cs2-dumper](https://github.com/a2x/cs2-dumper)
- Recoil presets: adapted from public Logitech no-recoil scripts (JambonCru)
- Overlay: [ClickableTransparentOverlay](https://www.nuget.org/packages/ClickableTransparentOverlay)
