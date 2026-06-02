using System.Numerics;

namespace External_Aimbot
{
    internal static class RecoilPredictor
    {
        private const float AimPunchScale = 2f;

        public static Vector2 PredictLandingAngles(Vector2 viewAngles, Vector3 aimPunch, WeaponContext weapon)
        {
            if (!weapon.IsValid)
                return viewAngles;

            float scale = SprayPatterns.GetWeaponScale(weapon.Class);
            Vector2 spray = SprayPatterns.GetCumulativeOffset(weapon.DefinitionIndex, weapon.SprayIndex) * scale;

            if (weapon.Class == WeaponClass.Sniper)
                spray += new Vector2(0f, weapon.AccuracyPenalty * 0.35f);

            return new Vector2(
                viewAngles.X + aimPunch.Y * AimPunchScale + spray.X,
                viewAngles.Y + aimPunch.X * AimPunchScale + spray.Y
            );
        }

        public static Vector2 CompensateForHit(
            Vector2 targetAngles,
            Vector3 aimPunch,
            WeaponContext weapon,
            float strength)
        {
            if (!weapon.IsValid || strength <= 0f)
                return targetAngles;

            float scale = AimPunchScale * strength;
            float weaponScale = SprayPatterns.GetWeaponScale(weapon.Class);

            Vector2 spray = SprayPatterns.GetCumulativeOffset(weapon.DefinitionIndex, weapon.SprayIndex) * weaponScale * strength;

            if (weapon.Class == WeaponClass.Sniper)
                spray += new Vector2(0f, weapon.AccuracyPenalty * 0.35f * strength);

            return new Vector2(
                targetAngles.X - aimPunch.Y * scale - spray.X,
                targetAngles.Y - aimPunch.X * scale - spray.Y
            );
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

            Vector2 landingAngles = PredictLandingAngles(viewAngles, aimPunch, weapon);
            Vector3 direction = Calculate.DirectionFromAngles(landingAngles.Y, landingAngles.X);
            Vector3 worldPoint = eyePosition + direction * 8192f;

            return ViewMatrix.WorldToScreen(worldPoint, viewMatrix, screenWidth, screenHeight, out screenPoint);
        }
    }
}
