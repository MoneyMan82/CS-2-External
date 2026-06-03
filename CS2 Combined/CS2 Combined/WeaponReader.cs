namespace External_Aimbot
{
    public enum WeaponClass
    {
        Unknown,
        Pistol,
        Smg,
        Rifle,
        Sniper,
        Shotgun,
        Lmg,
    }

    public enum WeaponFireMode
    {
        SemiAuto,
        FullAuto,
    }

    public readonly struct WeaponContext
    {
        public int DefinitionIndex { get; init; }
        public int ShotsFired { get; init; }
        public int RecoilIndex { get; init; }
        public float AccuracyPenalty { get; init; }
        public WeaponClass Class { get; init; }
        public WeaponFireMode FireMode { get; init; }
        public string Name { get; init; }
        public bool IsValid { get; init; }
        public bool HasRecoilPreset { get; init; }
        public bool BurstMode { get; init; }
        public bool IsAttacking { get; init; }

        public int SprayIndex
        {
            get
            {
                int index = RecoilIndex > 0 ? RecoilIndex : Math.Max(0, ShotsFired - 1);
                return BurstMode ? Math.Min(index, 2) : index;
            }
        }

        public bool SupportsRecoil => IsValid &&
            WeaponCatalog.SupportsRecoilPreset(Class) &&
            Class != WeaponClass.Sniper;

        public string FireModeLabel => BurstMode ? "burst" : "auto";
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
            IntPtr weapon = EntityList.ResolveWeaponHandle(mem, entitySystem, weaponHandle);
            if (weapon == IntPtr.Zero)
                return invalid;

            int defIndex = mem.ReadInt(weapon, Offsets.m_AttributeManager + Offsets.m_Item + Offsets.m_iItemDefinitionIndex);
            if (defIndex <= 0)
                return invalid;

            int recoilIndexRaw = mem.ReadInt(weapon, Offsets.m_iRecoilIndex);
            float recoilIndexFloat = BitConverter.Int32BitsToSingle(mem.ReadInt(weapon, Offsets.m_flRecoilIndex));
            int sprayIndex = recoilIndexRaw > 0
                ? recoilIndexRaw
                : Math.Max(0, (int)MathF.Round(recoilIndexFloat));

            WeaponClass weaponClass = WeaponCatalog.Classify(defIndex);
            WeaponFireMode fireMode = WeaponFireMode.FullAuto;
            bool burstMode = mem.ReadBool(weapon, Offsets.m_bBurstMode);
            bool isAttacking = InputState.IsAttackHeld();

            return new WeaponContext
            {
                IsValid = true,
                DefinitionIndex = defIndex,
                ShotsFired = mem.ReadInt(pawn, Offsets.m_iShotsFired),
                RecoilIndex = sprayIndex,
                AccuracyPenalty = BitConverter.Int32BitsToSingle(mem.ReadInt(weapon, Offsets.m_fAccuracyPenalty)),
                Class = weaponClass,
                FireMode = fireMode,
                Name = WeaponCatalog.GetName(defIndex),
                HasRecoilPreset = WeaponRecoilPresets.HasPattern(defIndex),
                BurstMode = burstMode,
                IsAttacking = isAttacking,
            };
        }
    }
}
