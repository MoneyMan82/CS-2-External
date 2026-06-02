# Map Raytracing Setup

The aimbot uses **world physics collision** from CS2 maps to block wallbangs. Each map needs a pre-extracted `.tri` file next to the exe.

## Quick setup

1. Install/extract map meshes once (see below).
2. Copy files into:
   ```
   External Aimbot\bin\Release\net8.0\maps\de_dust2.tri
   External Aimbot\bin\Release\net8.0\maps\de_mirage.tri
   ...
   ```
3. Run the aimbot in a match. The menu shows **Map: de_xxx | Mesh: loaded**.
4. Keep **visibility check** and **map raytracing** enabled.

If the mesh is missing, the cheat falls back to spotted-mask visibility (less accurate).

## Extract `.tri` files (recommended tool)

Use **[itzlaith/cs2-phys-extractor](https://github.com/itzlaith/cs2-phys-extractor)** (C#):

```powershell
git clone https://github.com/itzlaith/cs2-phys-extractor
cd cs2-phys-extractor
dotnet run
```

Point it at your CS2 install. It reads `game\csgo\maps\*.vpk`, pulls `world_physics.vphys_c`, and writes `{mapname}.tri`.

Alternative: **[AtomicBool/cs2-map-parser](https://github.com/AtomicBool/cs2-map-parser)** (C++ CLI).

## Manual extraction

1. Open `Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\maps\de_dust2.vpk`
2. Extract `maps/de_dust2/world_physics.vphys_c`
3. Decompile with [Source2Viewer CLI](https://github.com/ValveResourceFormat/ValveResourceFormat):
   ```powershell
   Source2Viewer-CLI.exe -i world_physics.vphys_c -o de_dust2.vphys
   ```
4. Convert `.vphys` → `.tri` with cs2-map-parser or cs2-phys-extractor

## `.tri` format

Binary file: repeated 36-byte records

```
struct Triangle {
    float p1.x, p1.y, p1.z;
    float p2.x, p2.y, p2.z;
    float p3.x, p3.y, p3.z;
};
```

Same format as AtomicBool / cs2-map-parser output.

## Typical file sizes

| Map        | Approx size |
|-----------|-------------|
| de_mirage | ~6 MB       |
| de_dust2  | ~13 MB      |
| de_inferno| ~300 MB     |

First load builds a BVH in memory (a few seconds on large maps). Later spawns reuse the cached mesh until the map name changes.

## Workshop / custom maps

Custom maps need their own `{mapname}.tri`. The menu **Map:** line shows the name to use.

## After CS2 updates

Re-run the extractor if Valve changes map physics. Gameplay offsets (`offsets.json`) and map meshes are separate.
