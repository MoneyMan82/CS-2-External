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
        public int ExpectedPaintKit { get; init; }
        public int ItemIdHigh { get; init; }
        public bool Configured { get; init; }
        public bool Active { get; init; }
    }

    public readonly struct SkinChangerDebug
    {
        public int WeaponsFound { get; init; }
        public int SkinsApplied { get; init; }
        public string Status { get; init; }
        public LoadoutWeaponInfo[] Loadout { get; init; }
        public bool RegenerateFound { get; init; }
        public string RegenerateStatus { get; init; }
        public bool SchemaOffsetsOk { get; init; }
        public int ActivePaintKit { get; init; }
        public int ActiveExpectedPaint { get; init; }
        public int ActiveItemIdHigh { get; init; }
    }

    internal static class SkinChanger
    {
        private const int ForceFallbackItemId = -1;
        private const int EconReloadIntervalMs = 250;
        private const int RegenerateIntervalMs = 350;

        private static long _lastEconReloadMs;
        private static IntPtr _lastEconPawn;
        private static long _lastRegenerateMs;
        private static IntPtr _lastRegenerateWeapon;
        private static int _lastRegeneratePaint;
        private static volatile bool _forceRefresh;

        public static void RequestForceRefresh()
        {
            _forceRefresh = true;
            _lastRegenerateMs = 0;
            SkinFunctionResolver.ResetCache();
        }

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            bool useRegenerate,
            IReadOnlyDictionary<int, SkinConfig> configs,
            out SkinChangerDebug debug)
        {
            var loadout = new List<LoadoutWeaponInfo>();
            SkinFunctionAddresses functions = SkinFunctionResolver.Resolve(mem);
            IntPtr regenerateFn = functions.RegenerateWeaponSkins;
            bool schemaOk = ClientSchemaLoader.HasSkinOffsets;
            debug = new SkinChangerDebug
            {
                Status = enabled ? "Active" : "Disabled",
                Loadout = [],
                RegenerateFound = regenerateFn != IntPtr.Zero,
                RegenerateStatus = BuildRegenerateLabel(functions),
                SchemaOffsetsOk = schemaOk,
            };

            if (!enabled || pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return;

            if (!schemaOk || Offsets.m_nFallbackPaintKit == 0 || Offsets.m_hMyWeapons == 0)
            {
                debug = debug with
                {
                    Status = "Skin offsets missing — restart app (auto-updates client_dll.json)",
                };
                return;
            }

            if (configs.Count == 0)
            {
                debug = debug with { Status = "Enabled — save a skin for a weapon first" };
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int applied = 0;
            var seen = new HashSet<IntPtr>();
            bool anyConfigured = false;
            int activePaint = 0;
            int activeExpected = 0;
            int activeItemIdHigh = 0;
            bool forceRefresh = _forceRefresh;
            _forceRefresh = false;

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
                bool isActive = weapon == activeWeapon;
                int expected = configured ? config.PaintKit : 0;

                loadout.Add(new LoadoutWeaponInfo
                {
                    DefinitionIndex = defIndex,
                    Name = WeaponCatalog.GetName(defIndex),
                    CurrentPaintKit = currentPaint,
                    ExpectedPaintKit = expected,
                    ItemIdHigh = itemIdHigh,
                    Configured = configured,
                    Active = isActive,
                });

                if (isActive)
                {
                    activePaint = currentPaint;
                    activeExpected = expected;
                    activeItemIdHigh = itemIdHigh;
                }

                if (!configured)
                    continue;

                anyConfigured = true;
                if (ApplySkin(mem, pawn, entitySystem, weapon, itemView, config))
                    applied++;
            }

            if (anyConfigured)
                MaybeBumpEconReload(mem, pawn);

            string regenStatus = debug.RegenerateStatus;
            bool paintMismatch = activeExpected > 0 && activePaint != activeExpected;
            bool idMismatch = activeExpected > 0 && activeItemIdHigh != ForceFallbackItemId;

            if (useRegenerate && regenerateFn != IntPtr.Zero && activeWeapon != IntPtr.Zero &&
                configs.TryGetValue(WeaponInventory.ReadDefinitionIndex(mem, activeWeapon), out SkinConfig activeConfig) &&
                activeConfig.PaintKit > 0)
            {
                if (forceRefresh || paintMismatch || idMismatch ||
                    ShouldRegenerate(activeWeapon, activeConfig.PaintKit))
                {
                    bool ok = RefreshWeaponMaterials(mem, activeWeapon, functions);
                    regenStatus = ok ? "Refreshed active weapon" : "Refresh call failed — run as Admin?";
                }
            }
            else if (useRegenerate && regenerateFn == IntPtr.Zero)
            {
                regenStatus = "No game refresh sig — CS2 updated?";
            }

            debug = new SkinChangerDebug
            {
                WeaponsFound = loadout.Count,
                SkinsApplied = applied,
                Status = BuildStatus(loadout.Count, configs.Count, applied, anyConfigured, activePaint, activeExpected, activeItemIdHigh),
                Loadout = loadout.ToArray(),
                RegenerateFound = regenerateFn != IntPtr.Zero,
                RegenerateStatus = regenStatus,
                SchemaOffsetsOk = schemaOk,
                ActivePaintKit = activePaint,
                ActiveExpectedPaint = activeExpected,
                ActiveItemIdHigh = activeItemIdHigh,
            };
        }

        private static string BuildRegenerateLabel(SkinFunctionAddresses functions)
        {
            if (functions.RegenerateWeaponSkins == IntPtr.Zero)
                return "Signature not found";

            string sub = functions.UpdateSubClass != IntPtr.Zero ? " + UpdateSubClass" : "";
            return $"{functions.RegenerateSource}{sub}";
        }

        private static bool ShouldRegenerate(IntPtr weapon, int paintKit)
        {
            long now = Environment.TickCount64;
            if (_lastRegenerateWeapon == weapon &&
                _lastRegeneratePaint == paintKit &&
                now - _lastRegenerateMs < RegenerateIntervalMs)
            {
                return false;
            }

            _lastRegenerateWeapon = weapon;
            _lastRegeneratePaint = paintKit;
            _lastRegenerateMs = now;
            return true;
        }

        private static bool RefreshWeaponMaterials(GameMemory mem, IntPtr weapon, SkinFunctionAddresses functions)
        {
            if (functions.UpdateSubClass != IntPtr.Zero)
                mem.TryInvokeFastcall(functions.UpdateSubClass, weapon);

            if (functions.RegenerateWeaponSkins == IntPtr.Zero)
                return false;

            return mem.TryInvokeFastcall(functions.RegenerateWeaponSkins, weapon);
        }

        private static string BuildStatus(
            int weaponsFound,
            int configCount,
            int applied,
            bool anyConfigured,
            int activePaint,
            int activeExpected,
            int activeItemIdHigh)
        {
            if (weaponsFound == 0)
                return "No weapons — buy/spawn guns in a match";

            if (configCount == 0)
                return "Save a skin, enable, then hold that gun";

            if (activeExpected > 0 && activeItemIdHigh != ForceFallbackItemId)
                return "Item ID not -1 — drop gun & buy again";

            if (activeExpected > 0 && activePaint == activeExpected)
                return $"Active gun shows kit {activePaint}";

            if (activeExpected > 0 && activePaint != activeExpected)
                return $"Memory: kit {activePaint}, want {activeExpected} — click Force refresh";

            if (applied > 0)
                return $"Writing {applied} weapon(s) each frame";

            if (anyConfigured)
                return "Configured weapons not in loadout yet";

            return "Watching loadout";
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
            int targetQuality = config.StatTrakEnabled ? 9 : 0;

            bool changed =
                itemIdHigh != ForceFallbackItemId ||
                currentPaint != config.PaintKit ||
                currentSeed != targetSeed ||
                MathF.Abs(currentWear - targetWear) > 0.001f;

            if (Offsets.m_bAttributesInitialized != 0)
                mem.WriteBool(weapon, Offsets.m_bAttributesInitialized, false);

            if (Offsets.m_bInitialized != 0)
                mem.WriteBool(itemView, Offsets.m_bInitialized, false);

            mem.WriteInt(itemView, Offsets.m_iItemIDHigh, ForceFallbackItemId);
            mem.WriteInt(itemView, Offsets.m_iItemIDLow, ForceFallbackItemId);

            if (Offsets.m_iAccountID != 0)
                mem.WriteInt(itemView, Offsets.m_iAccountID, 0);

            if (Offsets.m_iEntityQuality != 0)
                mem.WriteInt(itemView, Offsets.m_iEntityQuality, targetQuality);

            if (Offsets.m_bDisallowSOC != 0)
                mem.WriteBool(itemView, Offsets.m_bDisallowSOC, true);

            if (Offsets.m_bRestoreCustomMaterialAfterPrecache != 0)
                mem.WriteBool(itemView, Offsets.m_bRestoreCustomMaterialAfterPrecache, true);

            if (Offsets.m_bClientside != 0)
                mem.WriteBool(weapon, Offsets.m_bClientside, true);

            mem.WriteInt(weapon, Offsets.m_nFallbackPaintKit, config.PaintKit);
            mem.WriteInt(weapon, Offsets.m_nFallbackSeed, targetSeed);
            mem.WriteFloat(weapon, Offsets.m_flFallbackWear, targetWear);

            if (config.StatTrakEnabled)
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, Math.Max(0, config.StatTrak));
            else
                mem.WriteInt(weapon, Offsets.m_nFallbackStatTrak, -1);

            if (Offsets.m_bInitialized != 0)
                mem.WriteBool(itemView, Offsets.m_bInitialized, true);

            if (Offsets.m_bAttributesInitialized != 0)
                mem.WriteBool(weapon, Offsets.m_bAttributesInitialized, true);

            ApplyMeshMask(mem, weapon, meshMask);
            ApplyHudViewModelMesh(mem, pawn, entitySystem, weapon, meshMask);
            ApplyViewmodelAttachmentMesh(mem, entitySystem, weapon, meshMask);

            return changed;
        }

        private static void MaybeBumpEconReload(GameMemory mem, IntPtr pawn)
        {
            if (Offsets.m_nCustomEconReloadEventId == 0)
                return;

            long now = Environment.TickCount64;
            if (_lastEconPawn == pawn && now - _lastEconReloadMs < EconReloadIntervalMs)
                return;

            _lastEconPawn = pawn;
            _lastEconReloadMs = now;
            int eventId = mem.ReadInt(pawn, Offsets.m_nCustomEconReloadEventId);
            mem.WriteInt(pawn, Offsets.m_nCustomEconReloadEventId, eventId + 1);
        }

        private static void ApplyViewmodelAttachmentMesh(
            GameMemory mem,
            IntPtr entitySystem,
            IntPtr weapon,
            ulong meshMask)
        {
            if (Offsets.m_hViewmodelAttachment == 0)
                return;

            int handle = mem.ReadInt(weapon, Offsets.m_hViewmodelAttachment);
            IntPtr attachment = EntityList.ResolveWeaponHandle(mem, entitySystem, handle);
            if (!IsValidAddress(attachment))
                return;

            ApplyMeshMask(mem, attachment, meshMask);
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

            WalkSceneChildren(mem, armsSceneNode, weapon, meshMask);
        }

        private static void WalkSceneChildren(
            GameMemory mem,
            IntPtr sceneNode,
            IntPtr weapon,
            ulong meshMask)
        {
            IntPtr child = mem.ReadPtr(sceneNode, Offsets.m_pChild);
            int guard = 0;
            while (IsValidAddress(child) && guard++ < 48)
            {
                IntPtr owner = mem.ReadPtr(child, Offsets.m_pOwner);
                if (owner == weapon)
                    ApplyMeshMaskToSceneNode(mem, child, meshMask);

                WalkSceneChildren(mem, child, weapon, meshMask);
                child = mem.ReadPtr(child, Offsets.m_pNextSibling);
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

