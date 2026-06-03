using System.Numerics;

namespace External_Aimbot
{
    internal static class AimTarget
    {
        private const int HeadBone = 6;
        private const int HeadBoneAlt = 8;
        private const int ChestBone = 4;

        public static Vector3 GetHeadPosition(GameMemory mem, IntPtr pawn, Vector3 origin, Vector3 viewOffset)
        {
            if (pawn != IntPtr.Zero)
            {
                if (BoneReader.TryGetBonePosition(mem, pawn, HeadBone, out Vector3 head))
                    return head;

                if (BoneReader.TryGetBonePosition(mem, pawn, HeadBoneAlt, out head))
                    return head;
            }

            if (viewOffset.Z is > 10f and < 90f)
                return origin + viewOffset;

            return origin + new Vector3(0f, 0f, 64f);
        }

        public static Vector3 GetChestPosition(GameMemory mem, IntPtr pawn, Vector3 origin)
        {
            if (pawn != IntPtr.Zero && BoneReader.TryGetBonePosition(mem, pawn, ChestBone, out Vector3 chest))
                return chest;

            return origin + new Vector3(0f, 0f, 36f);
        }

        public static Vector3 GetLocalEyePosition(Vector3 origin, Vector3 viewOffset) =>
            origin + viewOffset;

        public static Vector2 ReadViewAngles(GameMemory mem, IntPtr localPawn)
        {
            if (localPawn != IntPtr.Zero && Offsets.m_angEyeAngles != 0)
            {
                Vector3 eye = mem.ReadVec(localPawn, Offsets.m_angEyeAngles);
                if (eye.X != 0f || eye.Y != 0f)
                    return new Vector2(eye.Y, eye.X);
            }

            Vector3 cmd = mem.ReadVec(mem.Client, Offsets.dwViewAngles);
            return new Vector2(cmd.Y, cmd.X);
        }
    }
}
