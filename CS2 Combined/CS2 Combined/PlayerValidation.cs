using System.Numerics;

namespace External_Aimbot
{
    internal static class PlayerValidation
    {
        public static bool TryGetPlayerData(
            GameMemory mem,
            IntPtr controller,
            IntPtr pawn,
            out int health,
            out int team)
        {
            health = mem.ReadInt(pawn, Offsets.m_iHealth);
            team = ReadTeam(mem, pawn, controller);

            if (health is < 1 or > 100 && controller != IntPtr.Zero)
                health = mem.ReadInt(controller, Offsets.m_iPawnHealth);

            if (health is < 1 or > 100 && !IsAlivePawn(mem, pawn))
                return false;

            if (health is < 1 or > 100)
                health = Math.Clamp(health, 1, 100);

            return HasUsableOrigin(mem, pawn, controller);
        }

        public static bool PassesTeamFilter(int team, Entity localPlayer, AimbotGameMode gameMode, bool aimOnTeam)
        {
            if (gameMode == AimbotGameMode.Deathmatch)
                return true;

            int localTeam = localPlayer.team;

            if (aimOnTeam)
                return team is 2 or 3 || team == 0;

            if (localTeam == 0)
                return true;

            if (team == 0)
                return true;

            if (localTeam is 2 or 3)
                return team is 2 or 3 && team != localTeam;

            return team is 2 or 3;
        }

        public static Vector3 ReadPawnOrigin(GameMemory mem, IntPtr pawn)
        {
            Vector3 origin = mem.ReadVec(pawn, Offsets.m_vOldOrigin);
            if (HasNonZeroHorizontal(origin))
                return origin;

            if (Offsets.m_pGameSceneNode != 0)
            {
                IntPtr sceneNode = mem.ReadPtr(pawn, Offsets.m_pGameSceneNode);
                if (sceneNode != IntPtr.Zero && Offsets.m_vecAbsOrigin != 0)
                {
                    origin = mem.ReadVec(sceneNode, Offsets.m_vecAbsOrigin);
                    if (HasNonZeroHorizontal(origin))
                        return origin;
                }
            }

            return origin;
        }

        public static bool HasUsableOrigin(GameMemory mem, IntPtr pawn, IntPtr controller)
        {
            if (HasNonZeroHorizontal(ReadPawnOrigin(mem, pawn)))
                return true;

            return controller != IntPtr.Zero;
        }

        private static bool IsAlivePawn(GameMemory mem, IntPtr pawn)
        {
            if (Offsets.m_lifeState == 0)
                return true;

            return mem.ReadByte(pawn + Offsets.m_lifeState) == 0;
        }

        private static int ReadTeam(GameMemory mem, IntPtr pawn, IntPtr controller)
        {
            int team = mem.ReadByte(pawn + Offsets.m_iTeamNum);
            if (team is 2 or 3)
                return team;

            if (controller != IntPtr.Zero)
            {
                team = mem.ReadByte(controller + Offsets.m_iTeamNum);
                if (team is 2 or 3)
                    return team;

                int pendingTeam = mem.ReadByte(controller + Offsets.m_iPendingTeamNum);
                if (pendingTeam is 2 or 3)
                    return pendingTeam;
            }

            return team;
        }

        private static bool HasNonZeroHorizontal(Vector3 origin) =>
            origin.X != 0f || origin.Y != 0f;
    }
}
