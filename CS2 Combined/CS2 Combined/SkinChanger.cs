namespace External_Aimbot
{
    public readonly struct SkinConfig
    {
        public int PaintKit { get; init; }
        public int Seed { get; init; }
        public float Wear { get; init; }
        public bool StatTrakEnabled { get; init; }
        public int StatTrak { get; init; }
        public bool LegacyModel { get; init; }
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
            debug = new SkinChangerDebug
            {
                Status = enabled ? "Active" : "Disabled",
                Loadout = [],
            };

            if (!enabled || pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return;

            if (Offsets.m_nFallbackPaintKit == 0 || Offsets.m_hMyWeapons == 0)
            {
                debug = debug with { Status = "Skin offsets missing — update client_dll.json" };
                return;
            }

            if (configs.Count == 0)
            {
                debug = debug with { Status = "Enabled — save a skin for a weapon first" };
            }

            int applied = 0;
            var seen = new HashSet<IntPtr>();

            foreach (IntPtr weapon in WeaponInventory.EnumerateWeapons(mem, pawn, entitySystem))
            {
                if (!seen.Add(weapon))
                    continue;

                int defIndex = WeaponInventory.ReadDefinitionIndex(mem, weapon);
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

                if (!configured)
                    continue;

                if (ApplySkin(mem, pawn, entitySystem, weapon, config))
                    applied++;
            }

            debug = new SkinChangerDebug
            {
                WeaponsFound = loadout.Count,
                SkinsApplied = applied,
                Status = loadout.Count == 0
                    ? "No loadout weapons found — join a match with guns"
                    : configs.Count == 0
                        ? "Enabled — save a skin for a weapon first"
                        : applied > 0
                            ? $"Applied {applied}/{loadout.Count} configured weapon(s)"
                            : $"Tracking {loadout.Count} weapon(s) — switch weapons or re-buy to refresh",
                Loadout = loadout.ToArray(),
            };
        }

        private static bool ApplySkin(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            IntPtr weapon,
            SkinConfig config)
        {
            if (!IsValidAddress(weapon))
                return false;

            IntPtr itemView = weapon + Offsets.m_AttributeManager + Offsets.m_Item;
            int itemIdHigh = mem.ReadInt(itemView, Offsets.m_iItemIDHigh);
            int currentPaint = mem.ReadInt(weapon, Offsets.m_nFallbackPaintKit);

            bool alreadyApplied =
                itemIdHigh == ForceFallbackItemId &&
                currentPaint == config.PaintKit;

            if (alreadyApplied)
                return false;

            mem.WriteInt(itemView, Offsets.m_iItemIDHigh, ForceFallbackItemId);
            mem.WriteInt(itemView, Offsets.m_iItemIDLow, 0);

            if (Offsets.m_bInitialized != 0)
                mem.WriteBool(itemView, Offsets.m_bInitialized, false);

            mem.WriteInt(weapon, Offsets.m_nFallbackPaintKit, config.PaintKit);
            mem.WriteInt(weapon, Offsets.m_nFallbackSeed, Math.Clamp(config.Seed, 0, 999));
            mem.WriteFloat(weapon, Offsets.m_flFallbackWear, Math.Clamp(config.Wear, 0.001f, 1f));

            if (config.StatTrakEnabled)
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, Math.Max(0, config.StatTrak));
            else
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, -1);

            ulong meshMask = config.LegacyModel ? 1UL : 2UL;
            ApplyMeshMask(mem, weapon, meshMask);

            IntPtr hudWeapon = HudWeaponResolver.Find(mem, pawn, entitySystem, weapon);
            if (hudWeapon != IntPtr.Zero)
                ApplyMeshMask(mem, hudWeapon, meshMask);

            return true;
        }

        private static void ApplyMeshMask(GameMemory mem, IntPtr entity, ulong meshMask)
        {
            if (!IsValidAddress(entity) || Offsets.m_MeshGroupMask == 0 || Offsets.m_pGameSceneNode == 0)
                return;

            IntPtr sceneNode = mem.ReadPtr(entity, Offsets.m_pGameSceneNode);
            if (!IsValidAddress(sceneNode))
                return;

            IntPtr modelState = sceneNode + Offsets.m_modelState;
            if (!IsValidAddress(modelState))
                return;

            mem.WriteULong(modelState, Offsets.m_MeshGroupMask, meshMask);
        }

        private static bool IsValidAddress(IntPtr address)
        {
            long value = address.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }
    }

    internal static class HudWeaponResolver
    {
        private const int MaxSceneNodes = 64;

        public static IntPtr Find(GameMemory mem, IntPtr pawn, IntPtr entitySystem, IntPtr weapon)
        {
            if (Offsets.m_hHudModelArms == 0 || !IsValidAddress(pawn) || !IsValidAddress(weapon))
                return IntPtr.Zero;

            int armsHandle = mem.ReadInt(pawn, Offsets.m_hHudModelArms);
            IntPtr arms = EntityList.ResolveWeaponHandle(mem, entitySystem, armsHandle);
            if (!IsValidAddress(arms))
                return IntPtr.Zero;

            IntPtr armsNode = mem.ReadPtr(arms, Offsets.m_pGameSceneNode);
            if (!IsValidAddress(armsNode))
                return IntPtr.Zero;

            IntPtr child = mem.ReadPtr(armsNode, Offsets.m_pChild);
            for (int i = 0; i < MaxSceneNodes && IsValidAddress(child); i++)
            {
                IntPtr owner = mem.ReadPtr(child, Offsets.m_pOwner);
                if (IsValidAddress(owner))
                {
                    int ownerHandle = mem.ReadInt(owner, Offsets.m_hOwnerEntity);
                    IntPtr ownerEntity = EntityList.ResolveWeaponHandle(mem, entitySystem, ownerHandle);
                    if (ownerEntity == weapon)
                        return owner;
                }

                child = mem.ReadPtr(child, Offsets.m_pNextSibling);
            }

            return IntPtr.Zero;
        }

        private static bool IsValidAddress(IntPtr address)
        {
            long value = address.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }
    }
}
