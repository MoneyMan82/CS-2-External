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
        public int ItemIdHigh { get; init; }
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
                if (!seen.Add(weapon) || !IsValidAddress(weapon))
                    continue;

                int defIndex = WeaponInventory.ReadDefinitionIndex(mem, weapon);
                if (defIndex <= 0)
                    continue;

                IntPtr itemView = weapon + Offsets.m_AttributeManager + Offsets.m_Item;
                int currentPaint = mem.ReadInt(weapon, Offsets.m_nFallbackPaintKit);
                int itemIdHigh = mem.ReadInt(itemView, Offsets.m_iItemIDHigh);
                bool configured = configs.TryGetValue(defIndex, out SkinConfig config) && config.PaintKit > 0;

                loadout.Add(new LoadoutWeaponInfo
                {
                    DefinitionIndex = defIndex,
                    Name = WeaponCatalog.GetName(defIndex),
                    CurrentPaintKit = currentPaint,
                    ItemIdHigh = itemIdHigh,
                    Configured = configured,
                });

                if (!configured)
                    continue;

                if (ApplySkin(mem, pawn, entitySystem, weapon, itemView, config))
                    applied++;
            }

            debug = new SkinChangerDebug
            {
                WeaponsFound = loadout.Count,
                SkinsApplied = applied,
                Status = BuildStatus(loadout.Count, configs.Count, applied),
                Loadout = loadout.ToArray(),
            };
        }

        private static string BuildStatus(int weaponsFound, int configCount, int applied)
        {
            if (weaponsFound == 0)
                return "No loadout weapons found — join a match with guns";

            if (configCount == 0)
                return "Enabled — save a skin for a weapon first";

            if (applied > 0)
                return $"Updated {applied} weapon(s) — drop/re-buy if still vanilla";

            return $"Watching {weaponsFound} weapon(s) — kit already set in memory";
        }

        private static bool ApplySkin(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            IntPtr weapon,
            IntPtr itemView,
            SkinConfig config)
        {
            int currentPaint = mem.ReadInt(weapon, Offsets.m_nFallbackPaintKit);
            int currentSeed = mem.ReadInt(weapon, Offsets.m_nFallbackSeed);
            int itemIdHigh = mem.ReadInt(itemView, Offsets.m_iItemIDHigh);
            float currentWear = mem.ReadFloat(weapon, Offsets.m_flFallbackWear);
            float targetWear = Math.Clamp(config.Wear, 0.001f, 0.99f);
            int targetSeed = Math.Clamp(config.Seed, 0, 999);
            ulong meshMask = config.LegacyModel ? 2UL : 1UL;

            bool needsUpdate =
                itemIdHigh != ForceFallbackItemId ||
                currentPaint != config.PaintKit ||
                currentSeed != targetSeed ||
                MathF.Abs(currentWear - targetWear) > 0.001f;

            if (!needsUpdate)
            {
                ApplyHudViewModelMesh(mem, pawn, entitySystem, weapon, meshMask);
                return false;
            }

            mem.WriteInt(itemView, Offsets.m_iItemIDHigh, ForceFallbackItemId);
            mem.WriteInt(itemView, Offsets.m_iItemIDLow, ForceFallbackItemId);

            if (Offsets.m_bInitialized != 0)
                mem.WriteBool(itemView, Offsets.m_bInitialized, false);

            if (Offsets.m_bDisallowSOC != 0)
                mem.WriteBool(itemView, Offsets.m_bDisallowSOC, true);

            mem.WriteInt(weapon, Offsets.m_nFallbackPaintKit, config.PaintKit);
            mem.WriteInt(weapon, Offsets.m_nFallbackSeed, targetSeed);
            mem.WriteFloat(weapon, Offsets.m_flFallbackWear, targetWear);

            if (config.StatTrakEnabled)
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, Math.Max(0, config.StatTrak));
            else
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, -1);

            ApplyMeshMask(mem, weapon, meshMask);
            ApplyHudViewModelMesh(mem, pawn, entitySystem, weapon, meshMask);
            BumpEconReload(mem, pawn);

            return true;
        }

        private static void BumpEconReload(GameMemory mem, IntPtr pawn)
        {
            if (Offsets.m_nCustomEconReloadEventId == 0)
                return;

            int eventId = mem.ReadInt(pawn, Offsets.m_nCustomEconReloadEventId);
            mem.WriteInt(pawn, Offsets.m_nCustomEconReloadEventId, eventId + 1);
        }

        private static void ApplyHudViewModelMesh(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            IntPtr weapon,
            ulong meshMask)
        {
            if (Offsets.m_hHudModelArms == 0 || Offsets.m_pGameSceneNode == 0)
                return;

            int armsHandle = mem.ReadInt(pawn, Offsets.m_hHudModelArms);
            IntPtr hudArms = EntityList.ResolveWeaponHandle(mem, entitySystem, armsHandle);
            if (!IsValidAddress(hudArms))
                return;

            IntPtr armsSceneNode = mem.ReadPtr(hudArms, Offsets.m_pGameSceneNode);
            if (!IsValidAddress(armsSceneNode))
                return;

            IntPtr childSceneNode = mem.ReadPtr(armsSceneNode, Offsets.m_pChild);
            int guard = 0;
            while (IsValidAddress(childSceneNode) && guard++ < 32)
            {
                IntPtr owner = mem.ReadPtr(childSceneNode, Offsets.m_pOwner);
                if (owner == weapon)
                    ApplyMeshMaskToSceneNode(mem, childSceneNode, meshMask);

                childSceneNode = mem.ReadPtr(childSceneNode, Offsets.m_pNextSibling);
            }
        }

        private static void ApplyMeshMask(GameMemory mem, IntPtr entity, ulong meshMask)
        {
            if (!IsValidAddress(entity) || Offsets.m_pGameSceneNode == 0)
                return;

            IntPtr sceneNode = mem.ReadPtr(entity, Offsets.m_pGameSceneNode);
            if (!IsValidAddress(sceneNode))
                return;

            ApplyMeshMaskToSceneNode(mem, sceneNode, meshMask);
        }

        private static void ApplyMeshMaskToSceneNode(GameMemory mem, IntPtr sceneNode, ulong meshMask)
        {
            if (Offsets.m_MeshGroupMask == 0 || Offsets.m_modelState == 0)
                return;

            IntPtr modelState = sceneNode + Offsets.m_modelState;
            mem.WriteULong(modelState, Offsets.m_MeshGroupMask, meshMask);
        }

        private static bool IsValidAddress(IntPtr address)
        {
            long value = address.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }
    }
}
