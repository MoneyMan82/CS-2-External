using System.Numerics;

namespace External_Aimbot
{
    internal static class AimTarget
    {
        private const int HeadBone = 6;
        private const int HeadBoneAlt = 8;
        private const int ChestBone = 4;
        private const int LeftFootBone = 22;
        private const int RightFootBone = 19;
        private const int PelvisBone = 1;
        private const float HeadAimDrop = 3.5f;
        private const float EstimatedPlayerHeight = 72f;

        public static Vector3 ReadPawnOrigin(GameMemory mem, IntPtr pawn)
        {
            if (pawn == IntPtr.Zero)
                return Vector3.Zero;

            Vector3 origin = mem.ReadVec(pawn, Offsets.m_vOldOrigin);
            if (HasHorizontal(origin))
                return origin;

            if (Offsets.m_pGameSceneNode != 0)
            {
                IntPtr sceneNode = mem.ReadPtr(pawn, Offsets.m_pGameSceneNode);
                if (sceneNode != IntPtr.Zero && Offsets.m_vecAbsOrigin != 0)
                {
                    origin = mem.ReadVec(sceneNode, Offsets.m_vecAbsOrigin);
                    if (HasHorizontal(origin))
                        return origin;
                }
            }

            return origin;
        }

        public static Vector3 GetFeetPosition(GameMemory mem, IntPtr pawn, Vector3 origin)
        {
            if (pawn != IntPtr.Zero)
            {
                foreach (int bone in new[] { LeftFootBone, RightFootBone, PelvisBone })
                {
                    if (BoneReader.TryGetBonePosition(mem, pawn, bone, out Vector3 foot))
                        return foot;
                }
            }

            Vector3 resolvedOrigin = ReadPawnOrigin(mem, pawn);
            if (HasHorizontal(resolvedOrigin))
                return resolvedOrigin;

            if (HasHorizontal(origin))
                return origin;

            return Vector3.Zero;
        }

        public static bool TryGetEspBounds(
            GameMemory mem,
            Entity entity,
            float[] viewMatrix,
            float screenWidth,
            float screenHeight,
            out Vector2 headScreen,
            out Vector2 feetScreen)
        {
            headScreen = Vector2.Zero;
            feetScreen = Vector2.Zero;

            Vector3 headWorld = entity.GetAimPosition(mem);
            if (!ViewMatrix.TryWorldToScreen(headWorld, viewMatrix, screenWidth, screenHeight, out headScreen))
                return false;

            Vector3 feetWorld = GetFeetPosition(mem, entity.pawnAddress, entity.origin);
            if (ViewMatrix.TryWorldToScreen(feetWorld, viewMatrix, screenWidth, screenHeight, out feetScreen))
                return true;

            Vector3 estimatedFeet = headWorld + new Vector3(0f, 0f, -EstimatedPlayerHeight);
            if (ViewMatrix.TryWorldToScreen(estimatedFeet, viewMatrix, screenWidth, screenHeight, out feetScreen))
                return true;

            feetScreen = new Vector2(headScreen.X, headScreen.Y + 80f);
            return true;
        }

        private static bool HasHorizontal(Vector3 origin) =>
            origin.X != 0f || origin.Y != 0f;

        public static Vector3 GetHeadPosition(GameMemory mem, IntPtr pawn, Vector3 origin, Vector3 viewOffset)
        {
            if (pawn != IntPtr.Zero)
            {
                if (BoneReader.TryGetBonePosition(mem, pawn, HeadBone, out Vector3 head))
                    return DropHeadAim(head);

                if (BoneReader.TryGetBonePosition(mem, pawn, HeadBoneAlt, out head))
                    return DropHeadAim(head);
            }

            if (viewOffset.Z is > 10f and < 90f)
                return DropHeadAim(origin + viewOffset);

            return DropHeadAim(origin + new Vector3(0f, 0f, 64f));
        }

        private static Vector3 DropHeadAim(Vector3 position) =>
            position + new Vector3(0f, 0f, -HeadAimDrop);

        public static Vector3 GetChestPosition(GameMemory mem, IntPtr pawn, Vector3 origin)
        {
            if (pawn != IntPtr.Zero && BoneReader.TryGetBonePosition(mem, pawn, ChestBone, out Vector3 chest))
                return chest;

            return origin + new Vector3(0f, 0f, 36f);
        }

        public static Vector3 GetLocalEyePosition(Vector3 origin, Vector3 viewOffset) =>
            origin + viewOffset;

        public static Vector2 ReadCommandViewAngles(GameMemory mem)
        {
            Vector3 cmd = mem.ReadVec(mem.Client, Offsets.dwViewAngles);
            return new Vector2(cmd.Y, cmd.X);
        }

        public static Vector2 ReadViewAngles(GameMemory mem, IntPtr localPawn)
        {
            if (localPawn != IntPtr.Zero && Offsets.m_angEyeAngles != 0)
            {
                Vector3 eye = mem.ReadVec(localPawn, Offsets.m_angEyeAngles);
                if (eye.X != 0f || eye.Y != 0f)
                    return new Vector2(eye.Y, eye.X);
            }

            return ReadCommandViewAngles(mem);
        }
    }
}
