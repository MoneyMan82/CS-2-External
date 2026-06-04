using System.Numerics;

namespace External_Aimbot
{
    /// <summary>Visual "where bullets land" helper — uses live aim punch only (no legacy pixel tables).</summary>
    internal static class RecoilPredictor
    {
        public static Vector2 PredictLandingAngles(Vector2 viewAngles, Vector3 aimPunch)
        {
            if (aimPunch == Vector3.Zero)
                return viewAngles;

            float scale = RecoilControl.PunchAngleScale;
            return new Vector2(
                viewAngles.X + aimPunch.Y * scale,
                viewAngles.Y + aimPunch.X * scale);
        }

        public static Vector2 PredictLandingAngles(Vector2 viewAngles, Vector3 aimPunch, WeaponContext weapon)
        {
            Vector2 landing = PredictLandingAngles(viewAngles, aimPunch);

            if (weapon.Class == WeaponClass.Sniper && weapon.AccuracyPenalty > 0f)
                landing.X += weapon.AccuracyPenalty * 0.35f;

            return landing;
        }

        public static bool TryGetLandingScreenPoint(
            Vector2 viewAngles,
            Vector3 aimPunch,
            WeaponContext weapon,
            Vector3 eyePosition,
            float[] viewMatrix,
            float screenWidth,
            float screenHeight,
            out Vector2 screenPoint)
        {
            screenPoint = Vector2.Zero;

            if (aimPunch == Vector3.Zero && weapon.ShotsFired <= 0)
                return false;

            Vector2 landingAngles = PredictLandingAngles(viewAngles, aimPunch, weapon);
            Vector3 direction = Calculate.DirectionFromAngles(landingAngles.Y, landingAngles.X);
            Vector3 worldPoint = eyePosition + direction * 8192f;

            return ViewMatrix.WorldToScreen(worldPoint, viewMatrix, screenWidth, screenHeight, out screenPoint);
        }

        public static bool TryGetPatternScreenPoint(
            Vector2 viewAngles,
            WeaponContext weapon,
            Vector3 eyePosition,
            float[] viewMatrix,
            float screenWidth,
            float screenHeight,
            out Vector2 screenPoint)
        {
            screenPoint = Vector2.Zero;

            if (!weapon.SupportsRecoil || !weapon.HasRecoilPreset)
                return false;

            Vector2 spray = SprayPatterns.GetCumulativeOffset(weapon.DefinitionIndex, weapon.SprayIndex);
            Vector2 patternAngles = new Vector2(viewAngles.X + spray.X, viewAngles.Y + spray.Y);
            Vector3 direction = Calculate.DirectionFromAngles(patternAngles.Y, patternAngles.X);
            Vector3 worldPoint = eyePosition + direction * 8192f;

            return ViewMatrix.WorldToScreen(worldPoint, viewMatrix, screenWidth, screenHeight, out screenPoint);
        }
    }
}
