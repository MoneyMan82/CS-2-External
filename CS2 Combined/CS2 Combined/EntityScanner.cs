using System.Numerics;

namespace External_Aimbot
{
    internal static class EntityScanner
    {
        private const int QuickIndexScanCap = 640;
        private const int DeepIndexScanCap = 2048;
        private const int DeepScanIntervalMs = 400;

        private static long _lastDeepIndexScanMs;

        public static List<Entity> ScanAllPlayers(GameMemory mem, Entity localPlayer)
        {
            var entities = new List<Entity>();
            var seenPawns = new HashSet<IntPtr>();

            IntPtr entitySystem = mem.ReadPtr(mem.Client, Offsets.dwGameEntitySystem);
            if (entitySystem == IntPtr.Zero)
                return entities;

            int highestEntityIndex = mem.ReadInt(entitySystem, Offsets.dwGameEntitySystem_highestEntityIndex);

            ScanControllerSlots(mem, entitySystem, localPlayer, seenPawns, entities);
            ScanIndexedControllers(mem, entitySystem, localPlayer, seenPawns, entities);
            ScanEntityIndices(mem, entitySystem, localPlayer, highestEntityIndex, seenPawns, entities);

            return entities;
        }

        public static List<Entity> Scan(
            GameMemory mem,
            Entity localPlayer,
            AimbotGameMode gameMode,
            bool aimOnTeam) =>
            FilterByTeam(ScanAllPlayers(mem, localPlayer), localPlayer, gameMode, aimOnTeam);

        public static List<Entity> FilterByTeam(
            List<Entity> entities,
            Entity localPlayer,
            AimbotGameMode gameMode,
            bool aimOnTeam)
        {
            var filtered = new List<Entity>(entities.Count);
            foreach (Entity entity in entities)
            {
                if (PlayerValidation.PassesTeamFilter(entity.team, localPlayer, gameMode, aimOnTeam))
                    filtered.Add(entity);
            }

            return filtered;
        }

        private static void ScanControllerSlots(
            GameMemory mem,
            IntPtr entitySystem,
            Entity localPlayer,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            IntPtr listEntry = mem.ReadPtr(entitySystem, 0x10);
            if (listEntry == IntPtr.Zero)
                return;

            for (int i = 0; i < 64; i++)
            {
                IntPtr controller = EntityList.ReadControllerAtSlot(mem, listEntry, i);
                if (controller == IntPtr.Zero)
                    continue;

                TryAddFromController(mem, entitySystem, controller, localPlayer, seenPawns, entities);
            }
        }

        private static void ScanIndexedControllers(
            GameMemory mem,
            IntPtr entitySystem,
            Entity localPlayer,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            for (int i = 1; i <= 64; i++)
            {
                IntPtr controller = EntityList.GetEntityFromIndex(mem, entitySystem, i);
                if (!EntityList.LooksLikePlayerController(mem, controller))
                    continue;

                TryAddFromController(mem, entitySystem, controller, localPlayer, seenPawns, entities);
            }
        }

        private static void ScanEntityIndices(
            GameMemory mem,
            IntPtr entitySystem,
            Entity localPlayer,
            int highestEntityIndex,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            long now = Environment.TickCount64;
            int scanCap = QuickIndexScanCap;
            if (now - _lastDeepIndexScanMs >= DeepScanIntervalMs)
            {
                scanCap = DeepIndexScanCap;
                _lastDeepIndexScanMs = now;
            }

            int maxIndex = Math.Clamp(highestEntityIndex, 0, scanCap);

            for (int i = 0; i <= maxIndex; i++)
            {
                IntPtr pawn = EntityList.GetEntityFromIndex(mem, entitySystem, i);
                if (pawn == IntPtr.Zero || pawn == localPlayer.pawnAddress)
                    continue;

                if (seenPawns.Contains(pawn))
                    continue;

                if (!EntityList.LooksLikePlayerPawn(mem, pawn))
                    continue;

                TryAddEntity(mem, IntPtr.Zero, pawn, localPlayer, seenPawns, entities);
            }
        }

        private static void TryAddFromController(
            GameMemory mem,
            IntPtr entitySystem,
            IntPtr controller,
            Entity localPlayer,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            foreach (int handle in GetPawnHandles(mem, controller))
            {
                IntPtr pawn = EntityList.ResolveEntityIndex(mem, entitySystem, handle);
                if (pawn == IntPtr.Zero || pawn == localPlayer.pawnAddress)
                    continue;

                TryAddEntity(mem, controller, pawn, localPlayer, seenPawns, entities);
            }
        }

        private static IEnumerable<int> GetPawnHandles(GameMemory mem, IntPtr controller)
        {
            int playerPawn = mem.ReadInt(controller, Offsets.m_hPlayerPawn);
            if (playerPawn > 0)
                yield return playerPawn;

            int pawn = mem.ReadInt(controller, Offsets.m_hPawn);
            if (pawn > 0 && pawn != playerPawn)
                yield return pawn;
        }

        private static void TryAddEntity(
            GameMemory mem,
            IntPtr controller,
            IntPtr pawn,
            Entity localPlayer,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            if (!seenPawns.Add(pawn))
                return;

            if (!PlayerValidation.TryGetPlayerData(mem, controller, pawn, out int health, out int team))
                return;

            var entity = new Entity
            {
                pawnAddress = pawn,
                controllerAddress = controller,
                health = health,
                team = team,
                origin = PlayerValidation.ReadPawnOrigin(mem, pawn),
                view = mem.ReadVec(pawn, Offsets.m_vecViewOffset),
            };
            entity.distance = Vector3.Distance(entity.origin, localPlayer.origin);

            entities.Add(entity);
        }
    }
}
