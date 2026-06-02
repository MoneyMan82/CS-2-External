using System.Numerics;

namespace External_Aimbot
{
    internal static class EntityScanner
    {
        public static List<Entity> Scan(
            GameMemory mem,
            Entity localPlayer,
            AimbotGameMode gameMode,
            bool aimOnTeam)
        {
            var entities = new List<Entity>();
            var seenPawns = new HashSet<IntPtr>();

            IntPtr entitySystem = mem.ReadPtr(mem.Client, Offsets.dwGameEntitySystem);
            if (entitySystem == IntPtr.Zero)
                return entities;

            int highestEntityIndex = mem.ReadInt(entitySystem, Offsets.dwGameEntitySystem_highestEntityIndex);

            ScanControllers(mem, entitySystem, localPlayer, gameMode, aimOnTeam, seenPawns, entities);
            ScanEntityIndices(mem, entitySystem, localPlayer, highestEntityIndex, gameMode, aimOnTeam, seenPawns, entities);

            return entities;
        }

        private static void ScanControllers(
            GameMemory mem,
            IntPtr entitySystem,
            Entity localPlayer,
            AimbotGameMode gameMode,
            bool aimOnTeam,
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

                if (!TryResolvePawn(mem, entitySystem, controller, localPlayer.pawnAddress, out IntPtr pawn))
                    continue;

                TryAddEntity(mem, controller, pawn, localPlayer, gameMode, aimOnTeam, seenPawns, entities);
            }
        }

        private static void ScanEntityIndices(
            GameMemory mem,
            IntPtr entitySystem,
            Entity localPlayer,
            int highestEntityIndex,
            AimbotGameMode gameMode,
            bool aimOnTeam,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            int maxIndex = Math.Clamp(highestEntityIndex, 0, 8192);

            for (int i = 0; i <= maxIndex; i++)
            {
                IntPtr pawn = EntityList.GetEntityFromIndex(mem, entitySystem, i);
                if (pawn == IntPtr.Zero || pawn == localPlayer.pawnAddress)
                    continue;

                if (seenPawns.Contains(pawn))
                    continue;

                if (!EntityList.LooksLikePlayerPawn(mem, pawn))
                    continue;

                TryAddEntity(mem, IntPtr.Zero, pawn, localPlayer, gameMode, aimOnTeam, seenPawns, entities);
            }
        }

        private static bool TryResolvePawn(
            GameMemory mem,
            IntPtr entitySystem,
            IntPtr controller,
            IntPtr localPawn,
            out IntPtr pawn)
        {
            pawn = IntPtr.Zero;

            int pawnHandle = mem.ReadInt(controller, Offsets.m_hPlayerPawn);
            if (pawnHandle == 0)
                pawnHandle = mem.ReadInt(controller, Offsets.m_hPawn);

            if (pawnHandle == 0)
                return false;

            pawn = EntityList.ResolveHandle(mem, entitySystem, pawnHandle);
            return pawn != IntPtr.Zero && pawn != localPawn;
        }

        private static void TryAddEntity(
            GameMemory mem,
            IntPtr controller,
            IntPtr pawn,
            Entity localPlayer,
            AimbotGameMode gameMode,
            bool aimOnTeam,
            HashSet<IntPtr> seenPawns,
            List<Entity> entities)
        {
            if (!seenPawns.Add(pawn))
                return;

            if (!PlayerValidation.TryGetPlayerData(mem, controller, pawn, out int health, out int team))
                return;

            if (!PlayerValidation.PassesTeamFilter(team, localPlayer, gameMode, aimOnTeam))
                return;

            var entity = new Entity
            {
                pawnAddress = pawn,
                controllerAddress = controller,
                health = health,
                team = team,
                origin = mem.ReadVec(pawn, Offsets.m_vOldOrigin),
                view = mem.ReadVec(pawn, Offsets.m_vecViewOffset),
            };
            entity.distance = Vector3.Distance(entity.origin, localPlayer.origin);

            entities.Add(entity);
        }
    }
}
