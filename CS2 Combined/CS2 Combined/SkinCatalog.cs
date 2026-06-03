namespace External_Aimbot
{
    public readonly struct SkinOption
    {
        public int PaintKit { get; init; }
        public string Name { get; init; }
        public bool LegacyModel { get; init; }
    }

    internal static class SkinCatalog
    {
        public static readonly int[] ConfigurableWeapons =
        [
            7, 16, 60, 9, 40, 10, 13, 8, 39,
            1, 4, 61, 36, 64,
            17, 34, 23, 24, 19,
            35, 25, 27, 29,
            42, 500, 503, 505, 506, 507, 508, 509, 512, 514, 515, 516, 517, 518, 519, 520, 521, 522, 523, 525, 526,
        ];

        private static readonly Dictionary<int, SkinOption[]> SkinsByWeapon = new()
        {
            [7] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 282, Name = "Redline" },
                new() { PaintKit = 180, Name = "Fire Serpent", LegacyModel = true },
                new() { PaintKit = 302, Name = "Vulcan" },
                new() { PaintKit = 801, Name = "Asiimov", LegacyModel = true },
                new() { PaintKit = 639, Name = "Bloodsport" },
                new() { PaintKit = 707, Name = "Neon Rider" },
                new() { PaintKit = 675, Name = "The Empress" },
            ],
            [16] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 255, Name = "Asiimov", LegacyModel = true },
                new() { PaintKit = 309, Name = "Howl" },
                new() { PaintKit = 588, Name = "Desolate Space" },
                new() { PaintKit = 695, Name = "Neo-Noir" },
            ],
            [60] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 984, Name = "Printstream" },
                new() { PaintKit = 430, Name = "Hyper Beast" },
                new() { PaintKit = 497, Name = "Golden Coil" },
                new() { PaintKit = 681, Name = "Decimator" },
            ],
            [9] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 344, Name = "Dragon Lore" },
                new() { PaintKit = 279, Name = "Asiimov" },
                new() { PaintKit = 624, Name = "Wildfire" },
                new() { PaintKit = 1026, Name = "Fade" },
            ],
            [40] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 624, Name = "Dragonfire" },
                new() { PaintKit = 538, Name = "Blood in the Water" },
            ],
            [10] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 919, Name = "Commemoration" },
                new() { PaintKit = 477, Name = "Mecha Industries" },
            ],
            [13] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 661, Name = "Sugar Rush" },
                new() { PaintKit = 379, Name = "Chatterbox" },
            ],
            [8] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 455, Name = "Akihabara Accept" },
                new() { PaintKit = 913, Name = "Momentum" },
            ],
            [39] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 661, Name = "Integrale" },
                new() { PaintKit = 598, Name = "Pulse" },
            ],
            [1] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 37, Name = "Blaze" },
                new() { PaintKit = 962, Name = "Printstream" },
                new() { PaintKit = 711, Name = "Code Red" },
            ],
            [4] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 38, Name = "Fade" },
                new() { PaintKit = 353, Name = "Water Elemental" },
                new() { PaintKit = 832, Name = "Neo-Noir" },
            ],
            [61] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 657, Name = "Kill Confirmed" },
                new() { PaintKit = 1029, Name = "Printstream" },
                new() { PaintKit = 504, Name = "Cortex" },
            ],
            [36] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 551, Name = "Asiimov" },
                new() { PaintKit = 678, Name = "See Ya Later" },
            ],
            [64] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 522, Name = "Fade" },
                new() { PaintKit = 843, Name = "Printstream" },
            ],
            [17] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 433, Name = "Neon Rider" },
            ],
            [34] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 496, Name = "Airlock" },
            ],
            [23] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 686, Name = "Phosphor" },
            ],
            [24] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 688, Name = "Momentum" },
            ],
            [19] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 311, Name = "Asiimov" },
            ],
            [35] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 344, Name = "Hyper Beast" },
            ],
            [25] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 616, Name = "Incinegator" },
            ],
            [27] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 514, Name = "Cobalt Core" },
            ],
            [29] =
            [
                new() { PaintKit = 0, Name = "Default" },
                new() { PaintKit = 638, Name = "Devourer" },
            ],
        };

        private static readonly SkinOption[] KnifeSkins =
        [
            new() { PaintKit = 0, Name = "Default" },
            new() { PaintKit = 38, Name = "Fade" },
            new() { PaintKit = 59, Name = "Slaughter" },
            new() { PaintKit = 415, Name = "Doppler (Ruby)" },
            new() { PaintKit = 568, Name = "Gamma Doppler" },
            new() { PaintKit = 413, Name = "Tiger Tooth" },
            new() { PaintKit = 414, Name = "Marble Fade" },
            new() { PaintKit = 417, Name = "Doppler (Sapphire)" },
            new() { PaintKit = 418, Name = "Doppler (Black Pearl)" },
            new() { PaintKit = 419, Name = "Lore" },
            new() { PaintKit = 420, Name = "Autotronic" },
        ];

        static SkinCatalog()
        {
            foreach (int knifeId in new[] { 42, 500, 503, 505, 506, 507, 508, 509, 512, 514, 515, 516, 517, 518, 519, 520, 521, 522, 523, 525, 526 })
                SkinsByWeapon[knifeId] = KnifeSkins;
        }

        public static SkinOption[] GetSkins(int weaponDefIndex)
        {
            if (SkinsByWeapon.TryGetValue(weaponDefIndex, out SkinOption[]? skins))
                return skins;

            return [new SkinOption { PaintKit = 0, Name = "Default" }];
        }

        public static bool IsKnife(int weaponDefIndex) =>
            weaponDefIndex is 42 or 59 or >= 500 and <= 526;
    }
}
