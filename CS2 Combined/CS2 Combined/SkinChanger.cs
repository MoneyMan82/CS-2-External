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
        public int SkinsQueued { get; init; }
        public bool RefreshReady { get; init; }
        public string Status { get; init; }
        public LoadoutWeaponInfo[] Loadout { get; init; }
    }

    internal static class SkinChanger
    {
        private const int ForceFallbackItemId = -1;
        private const int AttributeSize = 0x48;
        private const int AttributePaintKit = 6;
        private const int AttributeSeed = 7;
        private const int AttributeWear = 8;
        private const string RegeneratePattern = "48 83 EC ? E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? 48 8B 10";

        private static IntPtr _regenerateWeaponSkins;
        private static bool _regenerateScanAttempted;
        private static long _lastRefreshMs;
        private static readonly Dictionary<IntPtr, IntPtr> _attributeBlocks = new();

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            bool visualRefresh,
            IReadOnlyDictionary<int, SkinConfig> configs,
            out SkinChangerDebug debug)
        {
            var loadout = new List<LoadoutWeaponInfo>();
            bool refreshReady = EnsureRegenerateFunction(mem);
            debug = new SkinChangerDebug
            {
                Status = enabled ? "Active" : "Disabled",
                Loadout = [],
                RefreshReady = refreshReady,
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
            int queued = 0;
            var seen = new HashSet<IntPtr>();
            var refreshTargets = new List<IntPtr>();
            long now = Environment.TickCount64;
            bool shouldRefresh = visualRefresh && refreshReady && now - _lastRefreshMs >= 500;

            foreach (IntPtr weapon in WeaponInventory.EnumerateWeapons(mem, pawn, entitySystem))
            {
                if (!seen.Add(weapon))
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
                {
                    applied++;
                    refreshTargets.Add(weapon);
                }
            }

            if (applied > 0 && Offsets.m_nCustomEconReloadEventId != 0)
            {
                int reloadId = mem.ReadInt(pawn, Offsets.m_nCustomEconReloadEventId);
                mem.WriteInt(pawn, Offsets.m_nCustomEconReloadEventId, reloadId + 1);
            }

            if (shouldRefresh && refreshTargets.Count > 0)
            {
                IntPtr hwnd = Cs2Window.FindHandle();
                uint threadId = hwnd != IntPtr.Zero ? Cs2Window.GetWindowThreadId(hwnd) : 0;

                foreach (IntPtr weapon in refreshTargets)
                {
                    if (threadId != 0 &&
                        mem.TryQueueApcFastcall(_regenerateWeaponSkins, weapon, threadId))
                    {
                        queued++;
                    }
                }

                if (queued > 0)
                    _lastRefreshMs = now;
            }

            debug = new SkinChangerDebug
            {
                WeaponsFound = loadout.Count,
                SkinsApplied = applied,
                SkinsQueued = queued,
                RefreshReady = refreshReady,
                Status = BuildStatus(loadout.Count, configs.Count, applied, queued, visualRefresh, refreshReady),
                Loadout = loadout.ToArray(),
            };
        }

        private static string BuildStatus(
            int weaponsFound,
            int configCount,
            int applied,
            int queued,
            bool visualRefresh,
            bool refreshReady)
        {
            if (weaponsFound == 0)
                return "No loadout weapons found — join a match with guns";

            if (configCount == 0)
                return "Enabled — save a skin for a weapon first";

            if (applied == 0)
                return $"Tracking {weaponsFound} weapon(s) — values already set in memory";

            if (!visualRefresh)
                return $"Wrote {applied} weapon(s) — enable Visual refresh if skin stays vanilla";

            if (!refreshReady)
                return $"Wrote {applied} weapon(s) — refresh function missing after game update";

            return queued > 0
                ? $"Applied {applied} weapon(s), queued {queued} refresh"
                : $"Applied {applied} weapon(s) — waiting for game thread refresh";
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
            IntPtr itemView,
            SkinConfig config)
        {
            if (!IsValidAddress(weapon))
                return false;

            mem.WriteInt(itemView, Offsets.m_iItemIDHigh, ForceFallbackItemId);
            mem.WriteInt(itemView, Offsets.m_iItemIDLow, ForceFallbackItemId);

            if (Offsets.m_bInitialized != 0)
                mem.WriteBool(itemView, Offsets.m_bInitialized, false);

            mem.WriteInt(weapon, Offsets.m_nFallbackPaintKit, config.PaintKit);
            mem.WriteInt(weapon, Offsets.m_nFallbackSeed, Math.Clamp(config.Seed, 0, 999));
            mem.WriteFloat(weapon, Offsets.m_flFallbackWear, Math.Clamp(config.Wear, 0.001f, 0.99f));

            if (config.StatTrakEnabled)
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, Math.Max(0, config.StatTrak));
            else
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, 0);

            TryWriteAttributes(mem, itemView, config);

            ulong meshMask = config.LegacyModel ? 1UL : 2UL;
            ApplyMeshMask(mem, weapon, meshMask);

            IntPtr hudWeapon = HudWeaponResolver.Find(mem, pawn, entitySystem, weapon);
            if (hudWeapon != IntPtr.Zero)
                ApplyMeshMask(mem, hudWeapon, meshMask);

            return true;
        }

        private static void TryWriteAttributes(GameMemory mem, IntPtr itemView, SkinConfig config)
        {
            if (Offsets.m_AttributeList == 0 || Offsets.m_Attributes == 0)
                return;

            IntPtr attributeVector = itemView + Offsets.m_AttributeList + Offsets.m_Attributes;
            int size = mem.ReadInt(attributeVector, 0);
            IntPtr existing = mem.ReadPtr(attributeVector, 0x8);
            if (size > 0 && IsValidAddress(existing))
                return;

            if (!_attributeBlocks.TryGetValue(itemView, out IntPtr block) || !IsValidAddress(block))
            {
                block = mem.AllocateRemote(AttributeSize * 3);
                if (!IsValidAddress(block))
                    return;

                _attributeBlocks[itemView] = block;
            }

            WriteAttribute(mem, block, 0, AttributePaintKit, config.PaintKit);
            WriteAttribute(mem, block, 1, AttributeSeed, config.Seed);
            WriteAttribute(mem, block, 2, AttributeWear, config.Wear);

            mem.WriteInt(attributeVector, 0, 3);
            mem.WriteULong(attributeVector, 0x8, (ulong)block.ToInt64());
        }

        private static void WriteAttribute(GameMemory mem, IntPtr block, int index, int definitionIndex, float value)
        {
            IntPtr attr = block + index * AttributeSize;
            mem.WriteInt(attr, 0x30, definitionIndex);
            mem.WriteFloat(attr, 0x34, value);
            mem.WriteFloat(attr, 0x38, value);
        }

        private static void ApplyMeshMask(GameMemory mem, IntPtr entity, ulong meshMask)
        {
            if (!IsValidAddress(entity) || Offsets.m_MeshGroupMask == 0 || Offsets.m_pGameSceneNode == 0)
                return;

            IntPtr sceneNode = mem.ReadPtr(entity, Offsets.m_pGameSceneNode);
            if (!IsValidAddress(sceneNode))
                return;

            IntPtr modelState = sceneNode + Offsets.m_modelState;
            for (int i = 0; i < 8; i++)
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
