using System.Numerics;

namespace External_Aimbot
{
    internal static class SprayPatterns
    {
        public static Vector2 GetCumulativeOffset(int weaponId, int shotIndex) =>
            WeaponRecoilPresets.GetCumulativeOffset(weaponId, shotIndex);

        public static bool HasPattern(int weaponId) =>
            WeaponRecoilPresets.HasPattern(weaponId);

        public static float GetWeaponScale(WeaponClass weaponClass) =>
            WeaponRecoilPresets.GetClassScale(weaponClass);

        public static Vector2 GetPerShotOffset(int weaponId, int shotIndex, WeaponClass weaponClass) =>
            WeaponRecoilPresets.GetPerShotOffset(weaponId, shotIndex) *
            GetWeaponScale(weaponClass);
    }
}
