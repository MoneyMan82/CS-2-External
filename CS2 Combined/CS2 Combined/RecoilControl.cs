using System.Numerics;

namespace External_Aimbot
{
    internal static class RecoilControl
    {
        private const float DefaultRecoilMultiplier = 2f;
        private static Vector3 _lastPunch;
        private static int _lastWeaponId = -1;

        public static void Reset() => _lastPunch = Vector3.Zero;

        public static void TrackWeapon(WeaponContext weapon)
        {
            if (!weapon.IsValid)
                return;

            if (weapon.DefinitionIndex != _lastWeaponId)
            {
                _lastWeaponId = weapon.DefinitionIndex;
                Reset();
            }
        }

        public static Vector3 GetAimPunch(GameMemory mem, IntPtr pawn)
        {
            if (pawn == IntPtr.Zero)
                return Vector3.Zero;

            Vector3 punch = Vector3.Zero;

            IntPtr aimPunchServices = mem.ReadPtr(pawn, Offsets.m_pAimPunchServices);
            if (aimPunchServices != IntPtr.Zero)
            {
                punch += mem.ReadVec(aimPunchServices, Offsets.m_predictableBaseAngle);
                punch += mem.ReadVec(aimPunchServices, Offsets.m_unpredictableBaseAngle);
            }

            IntPtr cameraServices = mem.ReadPtr(pawn, Offsets.m_pCameraServices);
            if (cameraServices != IntPtr.Zero)
                punch += mem.ReadVec(cameraServices, Offsets.m_vecCsViewPunchAngle);

            return punch;
        }

        public static Vector2 ApplyDeltaCompensation(Vector2 angles, Vector3 currentPunch, float strength)
        {
            if (strength <= 0f)
                return angles;

            Vector3 delta = currentPunch - _lastPunch;
            _lastPunch = currentPunch;

            if (delta == Vector3.Zero)
                return angles;

            return ApplyAbsoluteCompensation(angles, delta, strength);
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

        public static Vector2 Apply(
            Vector2 angles,
            Vector3 aimPunch,
            WeaponContext weapon,
            bool usePredictor,
            bool useControl,
            float strength)
        {
            if (strength <= 0f)
                return angles;

            if (usePredictor && weapon.SupportsRecoil)
                return RecoilPredictor.CompensateForHit(angles, aimPunch, weapon, strength);

            if (!useControl)
                return angles;

            float punchScale = DefaultRecoilMultiplier * strength;
            Vector2 spray = weapon.SupportsRecoil
                ? SprayPatterns.GetCumulativeOffset(weapon.DefinitionIndex, weapon.SprayIndex) *
                  SprayPatterns.GetWeaponScale(weapon.Class) * strength
                : Vector2.Zero;

            if (aimPunch == Vector3.Zero && spray == Vector2.Zero)
                return angles;

            return new Vector2(
                angles.X - aimPunch.Y * punchScale - spray.X,
                angles.Y - aimPunch.X * punchScale - spray.Y
            );
        }

        public static bool ShouldCompensate(GameMemory mem, IntPtr pawn, WeaponContext weapon)
        {
            if (pawn == IntPtr.Zero || !weapon.SupportsRecoil)
                return false;

            int shotsFired = mem.ReadInt(pawn, Offsets.m_iShotsFired);
            if (shotsFired < 1)
                return false;

            if (!weapon.IsAttacking)
                return false;

            return shotsFired >= 1;
        }
    }
}
