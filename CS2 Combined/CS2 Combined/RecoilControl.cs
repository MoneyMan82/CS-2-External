using System.Numerics;

namespace External_Aimbot
{
    public enum RecoilCompensationMode
    {
        /// <summary>Per-frame aim punch delta from game memory (recommended).</summary>
        Memory,
        /// <summary>Legacy cumulative spray table only — no live punch.</summary>
        PatternOnly,
        /// <summary>Live punch + that weapon's spray table (indexed by game recoil index).</summary>
        PerWeapon,
    }

    internal static class RecoilControl
    {
        /// <summary>CS2 weapon_recoil_angle_scale default; configurable in menu.</summary>
        public static float PunchAngleScale = 2f;

        public static RecoilCompensationMode Mode = RecoilCompensationMode.Memory;

        private static Vector3 _lastPunch;
        private static Vector2 _lastPatternCumulative;
        private static int _lastWeaponId = -1;
        private static int _lastShotsFired;
        private static int _lastSprayIndex = -1;

        public static void Reset()
        {
            _lastPunch = Vector3.Zero;
            _lastPatternCumulative = Vector2.Zero;
            _lastShotsFired = 0;
            _lastSprayIndex = -1;
        }

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

        private static void SyncPatternIndex(WeaponContext weapon)
        {
            int idx = weapon.SprayIndex;
            if (idx < _lastSprayIndex || weapon.ShotsFired <= 0)
                _lastPatternCumulative = Vector2.Zero;

            _lastSprayIndex = idx;
        }

        public static void SyncShotState(WeaponContext weapon)
        {
            if (!weapon.IsAttacking || weapon.ShotsFired <= 0)
            {
                if (_lastShotsFired > 0)
                    Reset();
                return;
            }

            if (weapon.ShotsFired < _lastShotsFired)
                _lastPunch = Vector3.Zero;

            _lastShotsFired = weapon.ShotsFired;
        }

        public static Vector3 GetAimPunch(GameMemory mem, IntPtr pawn) =>
            GetBulletPunch(mem, pawn);

        public static Vector3 GetBulletPunch(GameMemory mem, IntPtr pawn)
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

            return punch;
        }

        public static Vector2 PunchToAngleDelta(Vector3 punchDelta, float strength)
        {
            if (punchDelta == Vector3.Zero || strength <= 0f)
                return Vector2.Zero;

            float scale = PunchAngleScale * strength;
            return new Vector2(-punchDelta.Y * scale, -punchDelta.X * scale);
        }

        public static Vector2 ApplyDeltaCompensation(Vector2 angles, Vector3 currentPunch, float strength)
        {
            if (strength <= 0f)
                return angles;

            Vector3 delta = currentPunch - _lastPunch;
            _lastPunch = currentPunch;

            Vector2 correction = PunchToAngleDelta(delta, strength);
            if (correction == Vector2.Zero)
                return angles;

            return new Vector2(angles.X + correction.X, angles.Y + correction.Y);
        }

        /// <summary>One-shot offset from total punch (e.g. after zeroing punch in No Recoil).</summary>
        public static Vector2 ApplyInstantCompensation(Vector2 angles, Vector3 aimPunch, float strength)
        {
            Vector2 correction = PunchToAngleDelta(aimPunch, strength);
            if (correction == Vector2.Zero)
                return angles;

            return new Vector2(angles.X + correction.X, angles.Y + correction.Y);
        }

        public static Vector2 Apply(
            Vector2 angles,
            Vector3 aimPunch,
            WeaponContext weapon,
            float strength)
        {
            if (strength <= 0f || !weapon.IsValid)
                return angles;

            SyncPatternIndex(weapon);

            return Mode switch
            {
                RecoilCompensationMode.PatternOnly => ApplyPatternOffset(angles, weapon, strength),
                RecoilCompensationMode.PerWeapon => ApplyPerWeapon(angles, aimPunch, weapon, strength),
                _ => ApplyDeltaCompensation(angles, aimPunch, strength),
            };
        }

        private static Vector2 ApplyPerWeapon(Vector2 angles, Vector3 aimPunch, WeaponContext weapon, float strength)
        {
            Vector2 result = ApplyDeltaCompensation(angles, aimPunch, strength);
            if (!weapon.HasRecoilPreset)
                return result;

            return ApplyPatternCumulativeDelta(result, weapon, strength * 0.35f);
        }

        private static Vector2 ApplyPatternOffset(Vector2 angles, WeaponContext weapon, float strength) =>
            ApplyPatternCumulativeDelta(angles, weapon, strength);

        private static Vector2 ApplyPatternCumulativeDelta(Vector2 angles, WeaponContext weapon, float strength)
        {
            if (!weapon.SupportsRecoil || strength <= 0f || !weapon.HasRecoilPreset)
                return angles;

            Vector2 total = SprayPatterns.GetCumulativeOffset(weapon.DefinitionIndex, weapon.SprayIndex) * strength;

            Vector2 delta = total - _lastPatternCumulative;
            _lastPatternCumulative = total;

            if (delta == Vector2.Zero)
                return angles;

            return new Vector2(angles.X - delta.X, angles.Y - delta.Y);
        }

        public static bool ShouldCompensate(GameMemory mem, IntPtr pawn, WeaponContext weapon)
        {
            if (pawn == IntPtr.Zero || !weapon.IsAttacking)
                return false;

            return mem.ReadInt(pawn, Offsets.m_iShotsFired) >= 1;
        }
    }
}
