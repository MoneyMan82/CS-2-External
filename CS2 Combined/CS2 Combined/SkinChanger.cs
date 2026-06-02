namespace External_Aimbot
{
    public readonly struct SkinConfig
    {
        public int PaintKit { get; init; }
        public int Seed { get; init; }
        public float Wear { get; init; }
        public bool StatTrakEnabled { get; init; }
        public int StatTrak { get; init; }
    }

    public readonly struct LoadoutWeaponInfo
    {
        public int DefinitionIndex { get; init; }
        public string Name { get; init; }
        public int CurrentPaintKit { get; init; }
        public bool Configured { get; init; }
    }

    public readonly struct SkinChangerDebug
    {
        public int WeaponsFound { get; init; }
        public int SkinsApplied { get; init; }
        public string Status { get; init; }
        public LoadoutWeaponInfo[] Loadout { get; init; }
    }

    internal static class SkinChanger
    {
        private const int MaxWeapons = 64;
        private const int ForceFallbackItemId = -1;

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            IReadOnlyDictionary<int, SkinConfig> configs,
            out SkinChangerDebug debug)
        {
            var loadout = new List<LoadoutWeaponInfo>();
            debug = new SkinChangerDebug { Status = enabled ? "Active" : "Disabled", Loadout = [] };

            if (!enabled || pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return;

            if (Offsets.m_nFallbackPaintKit == 0 || Offsets.m_hMyWeapons == 0)
            {
                debug = debug with { Status = "Skin offsets missing" };
                return;
            }

            int applied = 0;
            var seen = new HashSet<IntPtr>();

            foreach (IntPtr weapon in EnumerateWeapons(mem, pawn, entitySystem))
            {
                if (!seen.Add(weapon))
                    continue;

                int defIndex = ReadDefinitionIndex(mem, weapon);
                if (defIndex <= 0)
                    continue;

                int currentPaint = mem.ReadInt(weapon, Offsets.m_nFallbackPaintKit);
                bool configured = configs.TryGetValue(defIndex, out SkinConfig config) && config.PaintKit > 0;

                loadout.Add(new LoadoutWeaponInfo
                {
                    DefinitionIndex = defIndex,
                    Name = WeaponCatalog.GetName(defIndex),
                    CurrentPaintKit = currentPaint,
                    Configured = configured,
                });

                if (configured && ApplySkin(mem, weapon, config))
                    applied++;
            }

            debug = new SkinChangerDebug
            {
                WeaponsFound = loadout.Count,
                SkinsApplied = applied,
                Status = loadout.Count == 0
                    ? "No loadout weapons found"
                    : $"Applied {applied}/{loadout.Count} configured",
                Loadout = loadout.ToArray(),
            };
        }

        private static IEnumerable<IntPtr> EnumerateWeapons(GameMemory mem, IntPtr pawn, IntPtr entitySystem)
        {
            var seen = new HashSet<IntPtr>();
            IntPtr weaponServices = mem.ReadPtr(pawn, Offsets.m_pWeaponServices);
            if (weaponServices == IntPtr.Zero)
                yield break;

            foreach (int handle in ReadWeaponHandles(mem, weaponServices))
            {
                IntPtr weapon = EntityList.ResolveHandle(mem, entitySystem, handle);
                if (weapon != IntPtr.Zero && seen.Add(weapon))
                    yield return weapon;
            }
        }

        private static IEnumerable<int> ReadWeaponHandles(GameMemory mem, IntPtr weaponServices)
        {
            var handles = new HashSet<int>();

            void AddHandle(int handle)
            {
                if (handle > 0)
                    handles.Add(handle);
            }

            int vectorSize = mem.ReadInt(weaponServices, Offsets.m_hMyWeapons + 0x8);
            IntPtr vectorData = mem.ReadPtr(weaponServices, Offsets.m_hMyWeapons + 0x10);
            if (vectorSize > 0 && vectorSize <= MaxWeapons && vectorData != IntPtr.Zero)
            {
                for (int i = 0; i < vectorSize; i++)
                    AddHandle(mem.ReadInt(vectorData, i * 4));
            }

            vectorSize = mem.ReadInt(weaponServices, Offsets.m_hMyWeapons);
            vectorData = mem.ReadPtr(weaponServices, Offsets.m_hMyWeapons + 0x8);
            if (vectorSize > 0 && vectorSize <= MaxWeapons && vectorData != IntPtr.Zero)
            {
                for (int i = 0; i < vectorSize; i++)
                    AddHandle(mem.ReadInt(vectorData, i * 4));
            }

            for (int i = 0; i < MaxWeapons; i++)
                AddHandle(mem.ReadInt(weaponServices, Offsets.m_hMyWeapons + i * 4));

            AddHandle(mem.ReadInt(weaponServices, Offsets.m_hActiveWeapon));

            return handles;
        }

        private static int ReadDefinitionIndex(GameMemory mem, IntPtr weapon)
        {
            return mem.ReadInt(
                weapon,
                Offsets.m_AttributeManager + Offsets.m_Item + Offsets.m_iItemDefinitionIndex);
        }

        private static bool ApplySkin(GameMemory mem, IntPtr weapon, SkinConfig config)
        {
            IntPtr itemView = weapon + Offsets.m_AttributeManager + Offsets.m_Item;

            mem.WriteInt(itemView, Offsets.m_iItemIDHigh, ForceFallbackItemId);
            mem.WriteInt(itemView, Offsets.m_iItemIDLow, ForceFallbackItemId);

            mem.WriteInt(weapon, Offsets.m_nFallbackPaintKit, config.PaintKit);
            mem.WriteInt(weapon, Offsets.m_nFallbackSeed, Math.Clamp(config.Seed, 0, 999));
            mem.WriteFloat(weapon, Offsets.m_flFallbackWear, Math.Clamp(config.Wear, 0.001f, 1f));

            if (config.StatTrakEnabled)
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, Math.Max(0, config.StatTrak));
            else
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, 0);

            return true;
        }
    }
}
