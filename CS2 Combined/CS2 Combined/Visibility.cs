using System.Numerics;

namespace External_Aimbot
{
    internal static class Visibility
    {
        private const float MinForwardDot = 0.12f;

        public static bool CanSeeTarget(
            GameMemory mem,
            IntPtr targetPawn,
            int localControllerIndex,
            Vector3 localEye,
            Vector3 targetAim,
            Vector3 targetChest,
            Vector2 currentViewAngles,
            bool useMapRaytracing)
        {
            if (targetPawn == IntPtr.Zero)
                return false;

            if (!IsInFront(localEye, targetAim, currentViewAngles))
                return false;

            if (MapCollision.IsLoaded)
                return MapCollision.IsVisible(localEye, targetAim, targetChest);

            if (useMapRaytracing)
                return false;

            if (localControllerIndex < 0)
                return false;

            return IsSpottedByLocalPlayer(mem, targetPawn, localControllerIndex);
        }

        private static bool IsInFront(Vector3 localEye, Vector3 targetAim, Vector2 currentViewAngles)
        {
            Vector3 toTarget = targetAim - localEye;
            if (toTarget.LengthSquared() < 1f)
                return false;

            toTarget = Vector3.Normalize(toTarget);
            Vector3 viewDir = Calculate.DirectionFromAngles(currentViewAngles.Y, currentViewAngles.X);
            return Vector3.Dot(viewDir, toTarget) >= MinForwardDot;
        }

        private static bool IsSpottedByLocalPlayer(GameMemory mem, IntPtr targetPawn, int localControllerIndex)
        {
            int maskBase = Offsets.m_entitySpottedState + Offsets.m_bSpottedByMask;
            uint maskLow = (uint)mem.ReadInt(targetPawn, maskBase);
            uint maskHigh = (uint)mem.ReadInt(targetPawn, maskBase + 4);

            if (IsMaskBitSet(maskLow, maskHigh, localControllerIndex))
                return true;

            if (localControllerIndex > 0 && IsMaskBitSet(maskLow, maskHigh, localControllerIndex - 1))
                return true;

            return false;
        }

        private static bool IsMaskBitSet(uint maskLow, uint maskHigh, int controllerIndex)
        {
            if (controllerIndex < 0)
                return false;

            int arrayIndex = controllerIndex >> 5;
            int bitIndex = controllerIndex & 31;

            uint mask = arrayIndex switch
            {
                0 => maskLow,
                1 => maskHigh,
                _ => 0,
            };

            return (mask & (1u << bitIndex)) != 0;
        }
    }
}
