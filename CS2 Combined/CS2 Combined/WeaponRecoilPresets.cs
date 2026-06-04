using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace External_Aimbot
{
    internal sealed class WeaponRecoilPreset
    {
        public int WeaponId { get; init; }
        public string Name { get; init; } = "";
        public WeaponClass Class { get; init; }
        public float YawScale { get; init; } = 1f;
        public float PitchScale { get; init; } = 1f;
        public Vector2[] CumulativePattern { get; init; } = [];
        public bool FromFile { get; init; }
    }

    internal static class WeaponRecoilPresets
    {
        private const float DefaultPixelToYaw = 0.012f;
        private const float DefaultPixelToPitch = 0.082f;

        private static readonly Dictionary<WeaponClass, float> ClassScales = new()
        {
            [WeaponClass.Rifle] = 1f,
            [WeaponClass.Smg] = 0.88f,
            [WeaponClass.Lmg] = 1.12f,
            [WeaponClass.Sniper] = 0.35f,
            [WeaponClass.Pistol] = 0.58f,
            [WeaponClass.Shotgun] = 0.42f,
            [WeaponClass.Unknown] = 0.75f,
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static bool TryGet(int weaponId, out WeaponRecoilPreset preset) =>
            Presets.TryGetValue(weaponId, out preset!);

        public static bool HasPattern(int weaponId) => Presets.ContainsKey(weaponId);

        public static string GetPresetLabel(int weaponId) =>
            TryGet(weaponId, out WeaponRecoilPreset preset) ? preset.Name : "none";

        public static float GetClassScale(WeaponClass weaponClass) =>
            ClassScales.GetValueOrDefault(weaponClass, 0.75f);

        public static Vector2 GetCumulativeOffset(int weaponId, int shotIndex)
        {
            if (!TryGet(weaponId, out WeaponRecoilPreset preset) || shotIndex < 0)
                return Vector2.Zero;

            if (preset.CumulativePattern.Length == 0)
                return Vector2.Zero;

            int idx = Math.Clamp(shotIndex, 0, preset.CumulativePattern.Length - 1);
            return preset.CumulativePattern[idx];
        }

        public static Vector2 GetPerShotOffset(int weaponId, int shotIndex)
        {
            if (shotIndex <= 0)
                return Vector2.Zero;

            Vector2 current = GetCumulativeOffset(weaponId, shotIndex);
            Vector2 previous = GetCumulativeOffset(weaponId, shotIndex - 1);
            return current - previous;
        }

        private static Dictionary<int, WeaponRecoilPreset> BuildPresets()
        {
            var presets = new Dictionary<int, WeaponRecoilPreset>();
            TryLoadJsonOverride(presets);

            foreach ((int id, BuiltInPattern builtIn) in BuiltInPatterns())
            {
                if (presets.ContainsKey(id))
                    continue;

                presets[id] = CreatePreset(id, builtIn.Name, builtIn.Class, builtIn.Shots, builtIn.YawScale, builtIn.PitchScale, false);
            }

            foreach ((int id, int inheritId, string name, WeaponClass cls, float yaw, float pitch) in InheritedPatterns())
            {
                if (presets.ContainsKey(id) || !presets.TryGetValue(inheritId, out WeaponRecoilPreset? parent))
                    continue;

                presets[id] = new WeaponRecoilPreset
                {
                    WeaponId = id,
                    Name = name,
                    Class = cls,
                    YawScale = parent.YawScale * yaw,
                    PitchScale = parent.PitchScale * pitch,
                    CumulativePattern = ScalePattern(parent.CumulativePattern, yaw, pitch),
                    FromFile = false,
                };
            }

            return presets;
        }

        private static void TryLoadJsonOverride(Dictionary<int, WeaponRecoilPreset> presets)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "weapon_recoil.json");
            if (!File.Exists(path))
                return;

            try
            {
                using var stream = File.OpenRead(path);
                var file = JsonSerializer.Deserialize<WeaponRecoilFile>(stream, JsonOptions);
                if (file?.Weapons == null)
                    return;

                float pixelToYaw = file.Conversion?.PixelToYaw ?? DefaultPixelToYaw;
                float pixelToPitch = file.Conversion?.PixelToPitch ?? DefaultPixelToPitch;
                var rawPatterns = new Dictionary<int, Vector2[]>();

                foreach (WeaponRecoilEntry entry in file.Weapons)
                {
                    if (entry.Shots is { Length: > 0 })
                    {
                        rawPatterns[entry.Id] = BuildCumulative(
                            entry.Shots,
                            entry.YawScale * pixelToYaw,
                            entry.PitchScale * pixelToPitch);
                    }
                }

                foreach (WeaponRecoilEntry entry in file.Weapons)
                {
                    Vector2[] pattern;
                    if (entry.Shots is { Length: > 0 })
                    {
                        pattern = rawPatterns[entry.Id];
                    }
                    else if (entry.InheritId > 0 && rawPatterns.TryGetValue(entry.InheritId, out Vector2[]? inherited))
                    {
                        pattern = ScalePattern(inherited, entry.YawScale, entry.PitchScale);
                    }
                    else
                    {
                        continue;
                    }

                    presets[entry.Id] = new WeaponRecoilPreset
                    {
                        WeaponId = entry.Id,
                        Name = entry.Name ?? WeaponCatalog.GetName(entry.Id),
                        Class = ParseClass(entry.Class) ?? WeaponCatalog.Classify(entry.Id),
                        YawScale = entry.YawScale,
                        PitchScale = entry.PitchScale,
                        CumulativePattern = pattern,
                        FromFile = true,
                    };
                }
            }
            catch
            {
            }
        }

        private static WeaponRecoilPreset CreatePreset(
            int id,
            string name,
            WeaponClass weaponClass,
            int[][] pixelShots,
            float yawScale,
            float pitchScale,
            bool fromFile)
        {
            return new WeaponRecoilPreset
            {
                WeaponId = id,
                Name = name,
                Class = weaponClass,
                YawScale = yawScale,
                PitchScale = pitchScale,
                CumulativePattern = BuildCumulative(pixelShots, yawScale * DefaultPixelToYaw, pitchScale * DefaultPixelToPitch),
                FromFile = fromFile,
            };
        }

        private static Vector2[] BuildCumulative(int[][] pixelShots, float yawUnit, float pitchUnit)
        {
            var cumulative = new Vector2[pixelShots.Length];
            var sum = Vector2.Zero;

            for (int i = 0; i < pixelShots.Length; i++)
            {
                int[] shot = pixelShots[i];
                int px = shot.Length > 0 ? shot[0] : 0;
                int py = shot.Length > 1 ? shot[1] : 0;
                sum += new Vector2(px * yawUnit, py * pitchUnit);
                cumulative[i] = sum;
            }

            return cumulative;
        }

        private static Vector2[] ScalePattern(Vector2[] source, float yawScale, float pitchScale)
        {
            var scaled = new Vector2[source.Length];
            for (int i = 0; i < source.Length; i++)
                scaled[i] = new Vector2(source[i].X * yawScale, source[i].Y * pitchScale);
            return scaled;
        }

        private static WeaponClass? ParseClass(string? value) =>
            value?.ToLowerInvariant() switch
            {
                "rifle" => WeaponClass.Rifle,
                "smg" => WeaponClass.Smg,
                "lmg" => WeaponClass.Lmg,
                "sniper" => WeaponClass.Sniper,
                "pistol" => WeaponClass.Pistol,
                "shotgun" => WeaponClass.Shotgun,
                _ => null,
            };

        private sealed class WeaponRecoilFile
        {
            public ConversionEntry? Conversion { get; set; }
            public WeaponRecoilEntry[]? Weapons { get; set; }
        }

        private sealed class ConversionEntry
        {
            public float PixelToYaw { get; set; } = DefaultPixelToYaw;
            public float PixelToPitch { get; set; } = DefaultPixelToPitch;
        }

        private sealed class WeaponRecoilEntry
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Class { get; set; }
            public int InheritId { get; set; }
            public float YawScale { get; set; } = 1f;
            public float PitchScale { get; set; } = 1f;
            public int[][]? Shots { get; set; }
        }

        private readonly record struct BuiltInPattern(
            string Name,
            WeaponClass Class,
            int[][] Shots,
            float YawScale = 1f,
            float PitchScale = 1f);

        // Base spray tables (unique shapes). Other guns inherit with per-weapon scale in InheritedPatterns().
        private static IEnumerable<KeyValuePair<int, BuiltInPattern>> BuiltInPatterns()
        {
            yield return Key(7, "AK-47", WeaponClass.Rifle, Ak47Pixels);
            yield return Key(16, "M4A4", WeaponClass.Rifle, M4A4Pixels);
            yield return Key(60, "M4A1-S", WeaponClass.Rifle, M4A1Pixels, pitchScale: 0.92f);
            yield return Key(10, "FAMAS", WeaponClass.Rifle, FamasPixels, yawScale: 0.95f, pitchScale: 0.9f);
            yield return Key(13, "Galil AR", WeaponClass.Rifle, GalilPixels);
            yield return Key(39, "SG 553", WeaponClass.Rifle, Sg553Pixels);
            yield return Key(34, "MP9", WeaponClass.Smg, Mp9Pixels);
            yield return Key(17, "MAC-10", WeaponClass.Smg, Mac10Pixels);
            yield return Key(24, "UMP-45", WeaponClass.Smg, Ump45Pixels);
            yield return Key(4, "Glock-18", WeaponClass.Pistol, PistolPixels, yawScale: 0.5f, pitchScale: 0.45f);
        }

        private static IEnumerable<(int Id, int InheritId, string Name, WeaponClass Class, float Yaw, float Pitch)> InheritedPatterns()
        {
            // Rifles
            yield return (8, 16, "AUG", WeaponClass.Rifle, 0.92f, 0.88f);
            // SMGs
            yield return (23, 34, "MP5-SD", WeaponClass.Smg, 0.96f, 0.95f);
            yield return (19, 34, "P90", WeaponClass.Smg, 1.05f, 1f);
            yield return (33, 34, "MP7", WeaponClass.Smg, 0.94f, 0.92f);
            yield return (26, 24, "PP-Bizon", WeaponClass.Smg, 1.08f, 0.9f);
            // LMGs
            yield return (28, 16, "Negev", WeaponClass.Lmg, 1.15f, 1.1f);
            yield return (14, 7, "M249", WeaponClass.Lmg, 1.1f, 1.05f);
            // Pistols (each has its own scale on the Glock base table)
            yield return (1, 4, "Desert Eagle", WeaponClass.Pistol, 1.4f, 1.44f);
            yield return (61, 4, "USP-S", WeaponClass.Pistol, 0.9f, 0.89f);
            yield return (2, 4, "Dual Berettas", WeaponClass.Pistol, 1.1f, 1.11f);
            yield return (3, 4, "Five-SeveN", WeaponClass.Pistol, 0.96f, 0.93f);
            yield return (30, 4, "Tec-9", WeaponClass.Pistol, 1.04f, 1.07f);
            yield return (32, 4, "P2000", WeaponClass.Pistol, 0.88f, 0.84f);
            yield return (36, 4, "P250", WeaponClass.Pistol, 1f, 1f);
            yield return (63, 4, "CZ75-Auto", WeaponClass.Pistol, 1.3f, 1.33f);
            yield return (64, 4, "R8 Revolver", WeaponClass.Pistol, 1.6f, 1.67f);
        }

        private static KeyValuePair<int, BuiltInPattern> Key(
            int id,
            string name,
            WeaponClass weaponClass,
            int[][] shots,
            float yawScale = 1f,
            float pitchScale = 1f) =>
            new(id, new BuiltInPattern(name, weaponClass, shots, yawScale, pitchScale));

        private static readonly int[][] PistolPixels =
        [
            [0, 0], [0, 2], [0, 3], [0, 3], [0, 3], [0, 2], [0, 2], [0, 2], [0, 1], [0, 1],
            [0, 1], [0, 1], [0, 1], [0, 1], [0, 1], [0, 1], [0, 1], [0, 1], [0, 1], [0, 1],
        ];

        private static readonly int[][] Ak47Pixels =
        [
            [0,0],[0,0],[0,5],[0,6],[0,7],[0,7],[0,8],[0,7],[0,6],[0,7],[0,8],[-2,8],[1,7],[3,7],[6,7],[6,7],[6,7],[0,7],[1,7],[2,7],
            [2,8],[2,8],[2,9],[-3,-4],[-8,-1],[-15,-1],[-15,-1],[-5,0],[-5,0],[-5,0],[-5,0],[-1,1],[4,2],[4,2],[5,1],[-5,1],[-5,1],[-10,1],[-10,0],[-5,0],[-3,0],
            [0,0],[0,1],[0,1],[-2,1],[6,1],[8,2],[14,2],[15,2],[1,2],[1,2],[1,1],[1,1],[5,1],[6,1],[6,1],[6,1],[6,-1],[10,-1],[10,-2],[10,-3],
            [0,-5],[0,0],[-5,0],[-5,0],[-5,0],[0,0],[0,1],[0,2],[0,1],[0,1],[0,2],[0,2],[0,1],[0,1],[3,1],[3,-1],[3,-1],[0,0],[-3,0],[-4,0],
            [-4,0],[-4,0],[-4,0],[-4,0],[-7,0],[-7,0],[-8,0],[-8,-2],[-15,-3],[-16,-5],[-18,-7],[0,0],[0,0],
        ];

        private static readonly int[][] M4A4Pixels =
        [
            [0,0],[0,0],[0,4],[0,5],[0,6],[0,7],[0,5],[0,2],[0,5],[0,2],[0,5],[0,6],[-1,9],[0,8],[1,6],[0,7],[0,8],[1,8],[2,7],[2,7],
            [3,4],[4,-1],[4,-1],[4,-1],[3,1],[3,1],[3,1],[1,1],[0,1],[-3,1],[-5,1],[-8,1],[-10,1],[-10,1],[-10,1],[-10,1],[-10,1],[-5,-1],[-5,-1],[-5,-1],
            [-5,-1],[1,-1],[1,-1],[2,-1],[2,2],[2,2],[2,1],[0,1],[-2,1],[-2,1],[-2,1],[-4,-1],[-4,-1],[-2,1],[2,1],[4,1],[8,0],[14,0],[18,0],[0,0],
            [-2,0],[0,0],[5,0],[3,0],[2,0],[5,0],[3,0],[2,0],[5,0],[3,0],[2,0],[0,-1],[2,-1],[-5,3],[-5,3],[-3,2],[-3,1],[4,2],[8,1],[12,1],
            [0,1],[0,1],[0,1],[0,1],[0,1],[0,1],[0,1],
        ];

        private static readonly int[][] M4A1Pixels =
        [
            [0,0],[0,0],[0,1],[0,1],[0,2],[-1,2],[-1,3],[0,3],[-1,4],[1,4],[3,5],[3,4],[-1,4],[-2,4],[-2,5],[-1,4],[-2,4],[0,4],[0,4],[2,4],
            [4,4],[5,4],[5,4],[0,0],[1,0],[2,0],[2,0],[3,0],[-1,3],[-2,4],[-2,0],[-1,-2],[-1,2],[-2,3],[-2,5],[-2,0],[-5,0],[-6,0],[-7,-2],[-6,-2],
            [-4,0],[-4,0],[-4,0],[-4,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],
        ];

        private static readonly int[][] FamasPixels =
        [
            [0,0],[0,0],[-1,1],[-1,3],[-1,3],[-2,2],[-2,3],[-1,4],[-1,4],[0,5],[0,6],[1,5],[1,5],[3,5],[3,4],[3,2],[3,2],[4,3],[5,4],[1,4],
            [-2,3],[-3,3],[-5,3],[-5,3],[-5,3],[-5,3],[-5,3],[-5,3],[-5,2],[0,1],[0,0],[1,1],[2,0],[3,1],[4,1],[4,1],[3,1],[3,1],[3,1],[5,1],
            [5,1],[5,1],[5,-1],[5,-1],[0,-1],[1,-1],[3,-2],[5,-2],[0,0],[0,2],[0,2],[0,2],[0,1],[-2,1],[-3,1],[-3,1],[-3,0],[-3,0],[-2,0],[-3,0],
            [0,0],[2,0],[4,-1],[4,-1],[3,-2],[3,-2],[3,-2],[3,-2],[3,-1],[3,-2],[3,-1],
        ];

        private static readonly int[][] Mp9Pixels =
        [
            [0,1],[0,3],[0,3],[0,3],[0,5],[0,5],[1,5],[1,6],[1,7],[1,7],[0,7],[-2,8],[-3,8],[0,9],[3,9],[3,7],[5,0],[7,1],[7,1],[8,1],
            [8,1],[8,1],[8,0],[4,2],[0,2],[0,2],[0,2],[0,1],[-5,1],[-6,3],[-6,2],[-5,2],[-5,3],[-5,3],[-5,3],[-5,3],[-5,3],[-7,3],[-7,3],[-7,3],
            [-8,-3],[-8,-2],[0,-2],[0,-2],[0,-2],[3,-2],[5,-1],[7,0],[7,0],[3,0],[-1,0],[-1,0],[-5,1],[-5,2],[-7,2],[-7,2],[0,0],[0,0],[0,0],[-3,0],[-3,0],[0,0],[0,0],
        ];

        private static readonly int[][] Mac10Pixels =
        [
            [0,1],[0,2],[0,2],[0,2],[0,2],[0,2],[0,3],[2,5],[3,6],[4,6],[4,6],[4,6],[4,6],[4,6],[0,6],[0,6],[-2,6],[-2,6],[1,5],[3,5],
            [3,5],[3,4],[1,2],[1,1],[-2,2],[-2,2],[-2,2],[-2,1],[-1,1],[-1,1],[-1,0],[-1,1],[-3,1],[-5,-1],[-5,-1],[-6,-1],[-7,2],[-8,2],[-2,2],[-2,0],
            [-2,0],[-1,0],[-1,0],[-1,0],[0,0],[0,0],[0,0],[0,0],[-3,0],[-5,0],[-8,0],[-4,0],[0,0],[3,0],[6,0],[6,0],[6,0],[6,0],[3,0],[2,0],
            [3,0],[5,0],[4,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],
        ];

        // Distinct from AK — tighter early climb, less horizontal swing mid-spray.
        private static readonly int[][] GalilPixels =
        [
            [0,0],[0,0],[0,4],[0,5],[0,6],[0,6],[0,7],[0,6],[0,5],[0,6],[0,7],[-1,7],[0,6],[2,6],[4,6],[5,6],[5,6],[2,6],[1,6],[1,7],
            [1,7],[1,8],[-2,-3],[-6,-1],[-12,-1],[-12,-1],[-4,0],[-4,0],[-4,0],[-3,0],[-1,1],[3,2],[3,2],[4,1],[-4,1],[-4,1],[-8,1],[-8,0],[-4,0],[-2,0],
            [0,0],[0,1],[0,1],[-1,1],[5,1],[7,2],[12,2],[13,2],[1,2],[1,2],[1,1],[1,1],[4,1],[5,1],[5,1],[5,1],[5,-1],[8,-1],[8,-2],[8,-3],
            [0,-4],[0,0],[-4,0],[-4,0],[-4,0],[0,0],[0,1],[0,1],[0,1],[0,2],[0,2],[0,1],[0,1],[2,1],[2,-1],[2,-1],[0,0],[-2,0],[-3,0],
            [-3,0],[-3,0],[-3,0],[-3,0],[-6,0],[-6,0],[-7,0],[-7,-2],[-12,-3],[-14,-5],[-15,-6],[0,0],[0,0],
        ];

        // T-side scoped rifle — sharp vertical start, moderate side pull.
        private static readonly int[][] Sg553Pixels =
        [
            [0,0],[0,0],[0,5],[0,6],[0,7],[0,7],[0,8],[0,7],[0,6],[0,7],[0,8],[-2,8],[2,7],[4,7],[5,7],[5,7],[5,7],[1,7],[2,7],[3,7],
            [3,8],[3,8],[3,9],[-3,-4],[-7,-1],[-14,-1],[-14,-1],[-5,0],[-5,0],[-5,0],[-4,0],[-1,1],[4,2],[4,2],[5,1],[-5,1],[-5,1],[-9,1],[-9,0],[-5,0],[-3,0],
            [0,0],[0,1],[0,1],[-2,1],[6,1],[8,2],[13,2],[14,2],[1,2],[1,2],[1,1],[1,1],[5,1],[6,1],[6,1],[6,1],[6,-1],[9,-1],[9,-2],[9,-3],
            [0,-5],[0,0],[-5,0],[-5,0],[-5,0],[0,0],[0,1],[0,2],[0,1],[0,1],[0,2],[0,2],[0,1],[0,1],[3,1],[3,-1],[3,-1],[0,0],[-3,0],[-4,0],
            [-4,0],[-4,0],[-4,0],[-4,0],[-7,0],[-7,0],[-8,0],[-8,-2],[-14,-3],[-15,-5],[-17,-7],[0,0],[0,0],
        ];

        // UMP — low horizontal kick, steady climb.
        private static readonly int[][] Ump45Pixels =
        [
            [0,1],[0,2],[0,2],[0,2],[0,2],[0,2],[0,3],[1,4],[2,5],[3,5],[3,5],[3,5],[3,5],[3,5],[0,5],[0,5],[-1,5],[-1,5],[1,4],[2,4],
            [2,4],[2,3],[1,2],[0,1],[-1,2],[-1,2],[-1,2],[-1,1],[0,1],[0,1],[0,0],[0,1],[-2,1],[-4,-1],[-4,-1],[-5,-1],[-6,2],[-6,2],[-1,2],[-1,0],
            [-1,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[-2,0],[-4,0],[-6,0],[-3,0],[0,0],[2,0],[4,0],[4,0],[4,0],[4,0],[2,0],[1,0],
            [2,0],[3,0],[3,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],
        ];

        private static readonly Dictionary<int, WeaponRecoilPreset> Presets = BuildPresets();
    }

    internal static class WeaponCatalog
    {
        public static string GetName(int id) => id switch
        {
            7 => "AK-47",
            16 => "M4A4",
            60 => "M4A1-S",
            9 => "AWP",
            40 => "SSG 08",
            11 => "G3SG1",
            38 => "SCAR-20",
            10 => "FAMAS",
            13 => "Galil AR",
            8 => "AUG",
            39 => "SG 553",
            17 => "MAC-10",
            34 => "MP9",
            23 => "MP5-SD",
            24 => "UMP-45",
            19 => "P90",
            26 => "PP-Bizon",
            33 => "MP7",
            28 => "Negev",
            14 => "M249",
            1 => "Desert Eagle",
            2 => "Dual Berettas",
            3 => "Five-SeveN",
            4 => "Glock-18",
            30 => "Tec-9",
            32 => "P2000",
            36 => "P250",
            61 => "USP-S",
            63 => "CZ75-Auto",
            64 => "R8 Revolver",
            42 => "Knife",
            59 => "Knife (T)",
            500 => "Bayonet",
            503 => "Classic Knife",
            505 => "Flip Knife",
            506 => "Gut Knife",
            507 => "Karambit",
            508 => "M9 Bayonet",
            509 => "Huntsman Knife",
            512 => "Falchion Knife",
            514 => "Bowie Knife",
            515 => "Butterfly Knife",
            516 => "Shadow Daggers",
            517 => "Paracord Knife",
            518 => "Survival Knife",
            519 => "Ursus Knife",
            520 => "Navaja Knife",
            521 => "Nomad Knife",
            522 => "Stiletto Knife",
            523 => "Talon Knife",
            525 => "Skeleton Knife",
            526 => "Kukri Knife",
            35 => "Nova",
            25 => "XM1014",
            27 => "MAG-7",
            29 => "Sawed-Off",
            _ => $"weapon #{id}",
        };

        public static WeaponClass Classify(int id) => id switch
        {
            9 or 40 or 38 or 11 => WeaponClass.Sniper,
            7 or 16 or 60 or 10 or 13 or 8 or 39 => WeaponClass.Rifle,
            17 or 34 or 23 or 24 or 19 or 26 or 33 or 30 => WeaponClass.Smg,
            35 or 25 or 29 or 27 => WeaponClass.Shotgun,
            28 or 14 => WeaponClass.Lmg,
            1 or 2 or 3 or 4 or 32 or 36 or 61 or 63 or 64 => WeaponClass.Pistol,
            _ => WeaponClass.Unknown,
        };

        public static bool SupportsRecoilPreset(WeaponClass weaponClass) =>
            weaponClass is WeaponClass.Rifle or WeaponClass.Smg or WeaponClass.Lmg or WeaponClass.Pistol;

        public static bool IsSemiAuto(int defIndex) => defIndex switch
        {
            9 or 40 => true,
            1 or 2 or 3 or 4 or 32 or 36 or 61 or 64 => true,
            35 or 25 or 29 or 27 => true,
            _ => false,
        };
    }
}
