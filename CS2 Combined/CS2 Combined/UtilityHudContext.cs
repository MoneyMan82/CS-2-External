using System.Numerics;

namespace External_Aimbot
{
    public readonly struct UtilityHudContext
    {
        public bool InGame { get; init; }
        public int LocalHealth { get; init; }
        public int LocalArmor { get; init; }
        public int LocalTeam { get; init; }
        public int ShotsFired { get; init; }
        public int GameFov { get; init; }
        public int EnemyAlive { get; init; }
        public int TeamAlive { get; init; }
        public int EspCount { get; init; }
        public int RadarBlipCount { get; init; }
        public int CrosshairEntityId { get; init; }
        public float FlashAlpha { get; init; }
        public Vector2 ViewAngles { get; init; }
        public Vector3 LocalOrigin { get; init; }
        public Vector3 LastOrigin { get; init; }
        public string MapName { get; init; }
        public string WeaponName { get; init; }
        public string RecoilPreset { get; init; }
        public WeaponContext Weapon { get; init; }
        public MiscDebug Misc { get; init; }
        public long SessionStartTicks { get; init; }
    }
}
