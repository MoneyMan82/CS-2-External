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

            if (health is < 1 or > 100)
                return false;

            return HasValidOrigin(mem, pawn, controller);
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

        private static bool HasValidOrigin(GameMemory mem, IntPtr pawn, IntPtr controller)
        {
            Vector3 origin = mem.ReadVec(pawn, Offsets.m_vOldOrigin);
            if (origin.X != 0f || origin.Y != 0f || origin.Z != 0f)
                return true;

            return controller != IntPtr.Zero;
        }
    }
}
