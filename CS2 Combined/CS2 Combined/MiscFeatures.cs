namespace External_Aimbot
{
    public readonly struct MiscDebug
    {
        public bool BombPlanted { get; init; }
        public float BombTimeLeft { get; init; }
        public string BombSite { get; init; }
        public bool BombBeingDefused { get; init; }
        public float DefuseTimeLeft { get; init; }
        public int RadarRevealedCount { get; init; }
        public int SpectatorCount { get; init; }
        public string[] Spectators { get; init; }
        public int AppliedFov { get; init; }
    }

    internal static class MiscFeatures
    {
        private const int GlobalCurTimeOffset = 0x10;

        public static void Process(
            GameMemory mem,
            IntPtr entitySystem,
            IntPtr localPawn,
            IntPtr localController,
            Entity localPlayer,
            bool radarReveal,
            bool fovChanger,
            int fovValue,
            bool bombTimer,
            bool spectatorList,
            out MiscDebug debug)
        {
            debug = default;

            if (localPawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return;

            if (radarReveal)
            {
                int revealed = RadarReveal.Apply(mem, localPlayer, entitySystem);
                debug = debug with { RadarRevealedCount = revealed };
            }

            if (fovChanger)
            {
                int applied = ApplyFov(mem, localPawn, fovValue);
                debug = debug with { AppliedFov = applied };
            }

            if (bombTimer)
            {
                MiscDebug bomb = BombTracker.Read(mem, entitySystem);
                debug = debug with
                {
                    BombPlanted = bomb.BombPlanted,
                    BombTimeLeft = bomb.BombTimeLeft,
                    BombSite = bomb.BombSite,
                    BombBeingDefused = bomb.BombBeingDefused,
                    DefuseTimeLeft = bomb.DefuseTimeLeft,
                };
            }

            if (spectatorList)
            {
                var spectators = SpectatorList.GetWatchingLocal(
                    mem,
                    entitySystem,
                    localPawn,
                    localController);
                debug = debug with
                {
                    SpectatorCount = spectators.Count,
                    Spectators = spectators.ToArray(),
                };
            }
        }

        private static int ApplyFov(GameMemory mem, IntPtr localPawn, int fovValue)
        {
            if (Offsets.m_pCameraServices == 0 || Offsets.m_iFOV == 0)
                return 0;

            IntPtr cameraServices = mem.ReadPtr(localPawn, Offsets.m_pCameraServices);
            if (cameraServices == IntPtr.Zero)
                return 0;

            int clamped = Math.Clamp(fovValue, 60, 140);
            mem.WriteInt(cameraServices, Offsets.m_iFOV, clamped);
            return clamped;
        }

        private static class RadarReveal
        {
            public static int Apply(GameMemory mem, Entity localPlayer, IntPtr entitySystem)
            {
                List<Entity> enemies = EntityScanner.Scan(
                    mem,
                    localPlayer,
                    AimbotGameMode.Casual,
                    aimOnTeam: false);

                int count = 0;
                foreach (Entity entity in enemies)
                {
                    if (entity.pawnAddress == IntPtr.Zero)
                        continue;

                    int spottedOffset = Offsets.m_entitySpottedState + Offsets.m_bSpotted;
                    if (mem.ReadByte(entity.pawnAddress + spottedOffset) == 0)
                    {
                        mem.WriteInt(entity.pawnAddress, spottedOffset, 1);
                        count++;
                    }
                }

                return count;
            }
        }

        private static class BombTracker
        {
            public static MiscDebug Read(GameMemory mem, IntPtr entitySystem)
            {
                float curTime = ReadCurTime(mem);
                int highest = mem.ReadInt(entitySystem, Offsets.dwGameEntitySystem_highestEntityIndex);
                int maxIndex = Math.Clamp(highest, 0, 8192);

                for (int i = 0; i <= maxIndex; i++)
                {
                    IntPtr entity = EntityList.GetEntityFromIndex(mem, entitySystem, i);
                    if (entity == IntPtr.Zero)
                        continue;

                    if (!mem.ReadBool(entity, Offsets.m_bBombTicking))
                        continue;

                    if (mem.ReadBool(entity, Offsets.m_bHasExploded))
                        continue;

                    float blowTime = mem.ReadFloat(entity, Offsets.m_flC4Blow);
                    float timeLeft = Math.Max(0f, blowTime - curTime);
                    int siteIndex = mem.ReadInt(entity, Offsets.m_nBombSite);
                    bool defusing = mem.ReadBool(entity, Offsets.m_bBeingDefused);
                    float defuseLeft = 0f;
                    if (defusing)
                    {
                        float defuseEnd = mem.ReadFloat(entity, Offsets.m_flDefuseCountDown);
                        defuseLeft = Math.Max(0f, defuseEnd - curTime);
                    }

                    return new MiscDebug
                    {
                        BombPlanted = true,
                        BombTimeLeft = timeLeft,
                        BombSite = siteIndex == 1 ? "B" : "A",
                        BombBeingDefused = defusing,
                        DefuseTimeLeft = defuseLeft,
                    };
                }

                return default;
            }
        }

        private static class SpectatorList
        {
            public static List<string> GetWatchingLocal(
                GameMemory mem,
                IntPtr entitySystem,
                IntPtr localPawn,
                IntPtr localController)
            {
                var names = new List<string>();
                IntPtr listEntry = mem.ReadPtr(entitySystem, 0x10);
                if (listEntry == IntPtr.Zero)
                    return names;

                for (int i = 0; i < 64; i++)
                {
                    IntPtr controller = EntityList.ReadControllerAtSlot(mem, listEntry, i);
                    if (controller == IntPtr.Zero || controller == localController)
                        continue;

                    if (mem.ReadBool(controller, Offsets.m_bIsLocalPlayerController))
                        continue;

                    if (!IsWatchingPawn(mem, entitySystem, controller, localPawn))
                        continue;

                    string name = ReadControllerName(mem, controller);
                    if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                        names.Add(name);
                }

                return names;
            }

            private static bool IsWatchingPawn(
                GameMemory mem,
                IntPtr entitySystem,
                IntPtr controller,
                IntPtr targetPawn)
            {
                foreach (IntPtr pawn in GetObserverPawns(mem, entitySystem, controller))
                {
                    if (pawn == IntPtr.Zero)
                        continue;

                    IntPtr observerServices = mem.ReadPtr(pawn, Offsets.m_pObserverServices);
                    if (observerServices == IntPtr.Zero)
                        continue;

                    if (mem.ReadInt(observerServices, Offsets.m_iObserverMode) == 0)
                        continue;

                    int targetHandle = mem.ReadInt(observerServices, Offsets.m_hObserverTarget);
                    IntPtr watchedPawn = EntityList.ResolveHandle(mem, entitySystem, targetHandle);
                    if (watchedPawn == targetPawn)
                        return true;
                }

                return false;
            }

            private static IEnumerable<IntPtr> GetObserverPawns(
                GameMemory mem,
                IntPtr entitySystem,
                IntPtr controller)
            {
                int pawnHandle = mem.ReadInt(controller, Offsets.m_hPlayerPawn);
                if (pawnHandle == 0)
                    pawnHandle = mem.ReadInt(controller, Offsets.m_hPawn);

                if (pawnHandle != 0)
                    yield return EntityList.ResolveHandle(mem, entitySystem, pawnHandle);

                int observerHandle = mem.ReadInt(controller, Offsets.m_hObserverPawn);
                if (observerHandle != 0)
                    yield return EntityList.ResolveHandle(mem, entitySystem, observerHandle);
            }

            private static string ReadControllerName(GameMemory mem, IntPtr controller)
            {
                if (Offsets.m_sSanitizedPlayerName != 0)
                {
                    string sanitized = mem.ReadString(controller + Offsets.m_sSanitizedPlayerName, 64);
                    if (!string.IsNullOrWhiteSpace(sanitized))
                        return sanitized;
                }

                return mem.ReadString(controller + Offsets.m_iszPlayerName, 64);
            }
        }

        private static float ReadCurTime(GameMemory mem)
        {
            IntPtr globalVars = mem.ReadPtr(mem.Client, Offsets.dwGlobalVars);
            if (globalVars == IntPtr.Zero)
                return 0f;

            return mem.ReadFloat(globalVars, GlobalCurTimeOffset);
        }
    }
}
