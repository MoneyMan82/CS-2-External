using System.Numerics;
using System.Runtime.InteropServices;

namespace External_Aimbot
{
    internal static class RecoilControl
    {
        private const float DefaultRecoilMultiplier = 2f;
        private static Vector3 _lastPunch;

        [StructLayout(LayoutKind.Sequential)]
        private struct UtlVector
        {
            public int Count;
            public int Pad;
            public IntPtr Data;
        }

        public static void Reset() => _lastPunch = Vector3.Zero;

        public static Vector3 GetAimPunch(GameMemory mem, IntPtr pawn)
        {
            if (pawn == IntPtr.Zero)
                return Vector3.Zero;

            IntPtr aimPunchServices = mem.ReadPtr(pawn, Offsets.m_pAimPunchServices);
            if (aimPunchServices == IntPtr.Zero)
                return Vector3.Zero;

            if (TryReadLastVector3(mem, aimPunchServices + Offsets.m_aimPunchCache, out Vector3 punch))
                return punch;

            return mem.ReadVec(aimPunchServices, Offsets.m_predictableBaseAngle);
        }

        public static Vector2 ApplyDeltaCompensation(Vector2 angles, Vector3 currentPunch, float strength)
        {
            if (strength <= 0f)
                return angles;

            Vector3 delta = currentPunch - _lastPunch;
            _lastPunch = currentPunch;

            if (delta == Vector3.Zero)
                return angles;

            float scale = DefaultRecoilMultiplier * strength;

            return new Vector2(
                angles.X - delta.Y * scale,
                angles.Y - delta.X * scale
            );
        }

        public static Vector2 ApplyAbsoluteCompensation(Vector2 angles, Vector3 aimPunch, float strength)
        {
            if (aimPunch == Vector3.Zero || strength <= 0f)
                return angles;

            float scale = DefaultRecoilMultiplier * strength;

            return new Vector2(
                angles.X - aimPunch.Y * scale,
                angles.Y - aimPunch.X * scale
            );
        }

        public static bool ShouldCompensate(GameMemory mem, IntPtr pawn) =>
            pawn != IntPtr.Zero && mem.ReadInt(pawn, Offsets.m_iShotsFired) > 1;

        private static bool TryReadLastVector3(GameMemory mem, IntPtr vectorAddress, out Vector3 value)
        {
            value = Vector3.Zero;

            if (!mem.TryReadStruct(vectorAddress, out UtlVector vector))
                return false;

            if (vector.Count <= 0 || vector.Count > 0xFFFF || vector.Data == IntPtr.Zero)
                return false;

            IntPtr lastEntry = vector.Data + (vector.Count - 1) * 12;
            value = mem.ReadVec(lastEntry, 0);
            return true;
        }
    }
}
