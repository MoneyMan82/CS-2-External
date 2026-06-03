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
        public int SkinsRefreshed { get; init; }
        public bool RegenerateFound { get; init; }
        public string Status { get; init; }
        public LoadoutWeaponInfo[] Loadout { get; init; }
    }

    internal static class SkinChanger
    {
        private const int ForceFallbackItemId = -1;
        private const string RegeneratePattern = "48 83 EC ? E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? 48 8B 10";

        private static IntPtr _regenerateWeaponSkins;
        private static bool _regenerateScanAttempted;
        private static long _lastRefreshMs;

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            IReadOnlyDictionary<int, SkinConfig> configs,
            out SkinChangerDebug debug)
        {
            var loadout = new List<LoadoutWeaponInfo>();
            bool regenerateFound = EnsureRegenerateFunction(mem);
            debug = new SkinChangerDebug
            {
                Status = enabled ? "Active" : "Disabled",
                Loadout = [],
                RegenerateFound = regenerateFound,
            };

            if (!enabled || pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return;

            if (Offsets.m_nFallbackPaintKit == 0 || Offsets.m_hMyWeapons == 0)
            {
                debug = debug with { Status = "Skin offsets missing — update client_dll.json" };
                return;
            }

            if (!regenerateFound)
            {
                debug = debug with
                {
                    Status = "RegenerateWeaponSkins not found — skins cannot refresh after game updates",
                };
            }

            int applied = 0;
            int refreshed = 0;
            var seen = new HashSet<IntPtr>();
            var configuredWeapons = new List<IntPtr>();
            long now = Environment.TickCount64;
            bool shouldRefresh = regenerateFound && configs.Count > 0 && now - _lastRefreshMs >= 300;

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
                {
                    applied++;
                    configuredWeapons.Add(weapon);
                }
            }

            if (shouldRefresh && configuredWeapons.Count > 0)
            {
                foreach (IntPtr weapon in configuredWeapons)
                {
                    if (mem.TryCallFastcall(_regenerateWeaponSkins, weapon))
                        refreshed++;
                }

                _lastRefreshMs = now;
            }

            debug = new SkinChangerDebug
            {
                WeaponsFound = loadout.Count,
                SkinsApplied = applied,
                SkinsRefreshed = refreshed,
                RegenerateFound = regenerateFound,
                Status = loadout.Count == 0
                    ? "No loadout weapons found — join a match with guns"
                    : !regenerateFound
                        ? $"Wrote {applied} weapon(s), but refresh function missing"
                        : refreshed > 0
                            ? $"Applied {applied} weapon(s), refreshed {refreshed}"
                            : configs.Count == 0
                                ? "Enabled — save a skin for a weapon first"
                                : $"Tracking {loadout.Count} weapon(s), {applied} configured",
                Loadout = loadout.ToArray(),
            };
        }

        private static bool EnsureRegenerateFunction(GameMemory mem)
        {
            if (_regenerateWeaponSkins != IntPtr.Zero)
                return true;

            if (_regenerateScanAttempted)
                return false;

            _regenerateScanAttempted = true;
            _regenerateWeaponSkins = ModulePatternScanner.FindInModule(mem, "client.dll", RegeneratePattern);
            return _regenerateWeaponSkins != IntPtr.Zero;
        }

        private static bool ApplySkin(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            IntPtr weapon,
            SkinConfig config)
        {
            IntPtr itemView = weapon + Offsets.m_AttributeManager + Offsets.m_Item;

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
            if (Offsets.m_MeshGroupMask == 0 || Offsets.m_pGameSceneNode == 0)
                return;

            IntPtr sceneNode = mem.ReadPtr(entity, Offsets.m_pGameSceneNode);
            if (sceneNode == IntPtr.Zero)
                return;

            IntPtr modelState = sceneNode + Offsets.m_modelState;
            mem.WriteULong(modelState, Offsets.m_MeshGroupMask, meshMask);

            IntPtr dirtyAttributes = mem.ReadPtr(modelState, 0xD8);
            if (dirtyAttributes != IntPtr.Zero)
                mem.WriteULong(dirtyAttributes, 0x10, meshMask);
        }
    }

    internal static class HudWeaponResolver
    {
        public static IntPtr Find(GameMemory mem, IntPtr pawn, IntPtr entitySystem, IntPtr weapon)
        {
            if (Offsets.m_hHudModelArms == 0)
                return IntPtr.Zero;

            int armsHandle = mem.ReadInt(pawn, Offsets.m_hHudModelArms);
            IntPtr arms = EntityList.ResolveWeaponHandle(mem, entitySystem, armsHandle);
            if (arms == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr armsNode = mem.ReadPtr(arms, Offsets.m_pGameSceneNode);
            if (armsNode == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr child = mem.ReadPtr(armsNode, Offsets.m_pChild);
            while (child != IntPtr.Zero)
            {
                IntPtr owner = mem.ReadPtr(child, Offsets.m_pOwner);
                if (owner != IntPtr.Zero)
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
    }
}
