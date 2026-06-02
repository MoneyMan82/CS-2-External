namespace External_Aimbot
{
    internal enum WeaponClass
    {
        Unknown,
        Pistol,
        Smg,
        Rifle,
        Sniper,
        Shotgun,
        Lmg,
    }

    internal readonly struct WeaponContext
    {
        public int DefinitionIndex { get; init; }
        public int ShotsFired { get; init; }
        public int RecoilIndex { get; init; }
        public float AccuracyPenalty { get; init; }
        public WeaponClass Class { get; init; }
        public string Name { get; init; }
        public bool IsValid { get; init; }

        public int SprayIndex => RecoilIndex > 0 ? RecoilIndex : Math.Max(0, ShotsFired - 1);
    }

    internal static class WeaponReader
    {
        public static WeaponContext Read(GameMemory mem, IntPtr pawn, IntPtr entitySystem)
        {
            var invalid = new WeaponContext { Name = "none", Class = WeaponClass.Unknown };

            if (pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return invalid;

            IntPtr weaponServices = mem.ReadPtr(pawn, Offsets.m_pWeaponServices);
            if (weaponServices == IntPtr.Zero)
                return invalid;

            int weaponHandle = mem.ReadInt(weaponServices, Offsets.m_hActiveWeapon);
            IntPtr weapon = EntityList.ResolveHandle(mem, entitySystem, weaponHandle);
            if (weapon == IntPtr.Zero)
                return invalid;

            int defIndex = mem.ReadInt(weapon, Offsets.m_AttributeManager + Offsets.m_Item + Offsets.m_iItemDefinitionIndex);
            if (defIndex <= 0)
                return invalid;

            return new WeaponContext
            {
                IsValid = true,
                DefinitionIndex = defIndex,
                ShotsFired = mem.ReadInt(pawn, Offsets.m_iShotsFired),
                RecoilIndex = mem.ReadInt(weapon, Offsets.m_iRecoilIndex),
                AccuracyPenalty = BitConverter.Int32BitsToSingle(mem.ReadInt(weapon, Offsets.m_fAccuracyPenalty)),
                Class = Classify(defIndex),
                Name = GetName(defIndex),
            };
        }

        private static WeaponClass Classify(int id) => id switch
        {
            9 or 40 or 38 or 11 => WeaponClass.Sniper,
            7 or 16 or 60 or 10 or 13 or 8 or 39 or 14 => WeaponClass.Rifle,
            17 or 34 or 23 or 24 or 19 or 26 or 33 or 30 => WeaponClass.Smg,
            35 or 25 or 29 or 27 => WeaponClass.Shotgun,
            28 => WeaponClass.Lmg,
            1 or 2 or 3 or 4 or 30 or 32 or 36 or 61 or 63 or 64 => WeaponClass.Pistol,
            _ => WeaponClass.Unknown,
        };

        private static string GetName(int id) => id switch
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
            28 => "Negev",
            1 => "Desert Eagle",
            61 => "USP-S",
            63 => "CZ75",
            64 => "R8",
            _ => $"weapon #{id}",
        };
    }
}
