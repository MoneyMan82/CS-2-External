using System.Numerics;

namespace External_Aimbot
{
    internal static class SprayPatterns
    {
        // Cumulative bullet drift from first shot (yaw, pitch) in degrees.
        private static readonly Dictionary<int, Vector2[]> Patterns = BuildPatterns();

        public static Vector2 GetCumulativeOffset(int weaponId, int shotIndex)
        {
            if (!Patterns.TryGetValue(weaponId, out Vector2[]? pattern))
                return Vector2.Zero;

            if (shotIndex <= 0)
                return Vector2.Zero;

            int idx = Math.Clamp(shotIndex, 0, pattern.Length - 1);
            return pattern[idx];
        }

        public static Vector2 GetNextShotOffset(int weaponId, int shotIndex)
        {
            Vector2 current = GetCumulativeOffset(weaponId, shotIndex);
            Vector2 previous = GetCumulativeOffset(weaponId, shotIndex - 1);
            return current - previous;
        }

        public static float GetWeaponScale(WeaponClass weaponClass) => weaponClass switch
        {
            WeaponClass.Rifle => 1f,
            WeaponClass.Smg => 0.85f,
            WeaponClass.Lmg => 1.1f,
            WeaponClass.Sniper => 0.35f,
            WeaponClass.Pistol => 0.55f,
            WeaponClass.Shotgun => 0.45f,
            _ => 0.75f,
        };

        private static Dictionary<int, Vector2[]> BuildPatterns()
        {
            return new Dictionary<int, Vector2[]>
            {
                [7] = BuildFromDeltas(Ak47Deltas),
                [16] = BuildFromDeltas(M4A4Deltas),
                [60] = BuildFromDeltas(M4A1Deltas),
                [10] = ScalePattern(BuildFromDeltas(M4A4Deltas), 0.95f, 0.9f),
                [13] = ScalePattern(BuildFromDeltas(Ak47Deltas), 0.9f, 0.85f),
                [8] = ScalePattern(BuildFromDeltas(M4A4Deltas), 0.92f, 0.88f),
                [39] = ScalePattern(BuildFromDeltas(Ak47Deltas), 0.93f, 0.9f),
                [17] = ScalePattern(BuildFromDeltas(SmgDeltas), 1f, 1f),
                [34] = ScalePattern(BuildFromDeltas(SmgDeltas), 0.95f, 0.95f),
                [19] = ScalePattern(BuildFromDeltas(SmgDeltas), 1.05f, 1f),
            };
        }

        private static Vector2[] BuildFromDeltas(IReadOnlyList<Vector2> deltas)
        {
            var cumulative = new Vector2[deltas.Count];
            cumulative[0] = deltas[0];
            for (int i = 1; i < deltas.Count; i++)
                cumulative[i] = cumulative[i - 1] + deltas[i];
            return cumulative;
        }

        private static Vector2[] ScalePattern(Vector2[] source, float yawScale, float pitchScale)
        {
            var scaled = new Vector2[source.Length];
            for (int i = 0; i < source.Length; i++)
                scaled[i] = new Vector2(source[i].X * yawScale, source[i].Y * pitchScale);
            return scaled;
        }

        // Per-shot drift (yaw, pitch). Values tuned for CS2-style spray compensation.
        private static readonly Vector2[] Ak47Deltas =
        [
            new(0f, 0f), new(0f, 0.55f), new(0.02f, 0.58f), new(0.04f, 0.6f), new(0.06f, 0.62f),
            new(0.1f, 0.62f), new(0.15f, 0.6f), new(0.2f, 0.58f), new(0.22f, 0.55f), new(0.18f, 0.52f),
            new(0.1f, 0.48f), new(0f, 0.45f), new(-0.12f, 0.42f), new(-0.24f, 0.4f), new(-0.3f, 0.38f),
            new(-0.28f, 0.36f), new(-0.2f, 0.34f), new(-0.1f, 0.32f), new(0.05f, 0.3f), new(0.18f, 0.28f),
            new(0.28f, 0.26f), new(0.32f, 0.24f), new(0.26f, 0.22f), new(0.12f, 0.2f), new(-0.05f, 0.18f),
            new(-0.2f, 0.16f), new(-0.28f, 0.14f), new(-0.22f, 0.12f), new(-0.1f, 0.1f), new(0.05f, 0.08f),
        ];

        private static readonly Vector2[] M4A4Deltas =
        [
            new(0f, 0f), new(0f, 0.45f), new(0.01f, 0.48f), new(0.03f, 0.5f), new(0.05f, 0.5f),
            new(0.08f, 0.48f), new(0.12f, 0.46f), new(0.14f, 0.44f), new(0.12f, 0.42f), new(0.08f, 0.4f),
            new(0.02f, 0.38f), new(-0.05f, 0.36f), new(-0.12f, 0.34f), new(-0.18f, 0.32f), new(-0.2f, 0.3f),
            new(-0.16f, 0.28f), new(-0.08f, 0.26f), new(0.02f, 0.24f), new(0.12f, 0.22f), new(0.18f, 0.2f),
            new(0.16f, 0.18f), new(0.08f, 0.16f), new(-0.02f, 0.14f), new(-0.12f, 0.12f), new(-0.16f, 0.1f),
            new(-0.12f, 0.08f), new(-0.04f, 0.06f), new(0.06f, 0.05f), new(0.12f, 0.04f), new(0.1f, 0.03f),
        ];

        private static readonly Vector2[] M4A1Deltas =
        [
            new(0f, 0f), new(0f, 0.4f), new(0.01f, 0.42f), new(0.02f, 0.44f), new(0.04f, 0.44f),
            new(0.06f, 0.42f), new(0.1f, 0.4f), new(0.12f, 0.38f), new(0.1f, 0.36f), new(0.06f, 0.34f),
            new(0f, 0.32f), new(-0.06f, 0.3f), new(-0.12f, 0.28f), new(-0.16f, 0.26f), new(-0.18f, 0.24f),
            new(-0.14f, 0.22f), new(-0.06f, 0.2f), new(0.04f, 0.18f), new(0.12f, 0.16f), new(0.14f, 0.14f),
            new(0.1f, 0.12f), new(0.02f, 0.1f), new(-0.08f, 0.08f), new(-0.14f, 0.07f), new(-0.12f, 0.06f),
        ];

        private static readonly Vector2[] SmgDeltas =
        [
            new(0f, 0f), new(0f, 0.35f), new(0.04f, 0.36f), new(0.08f, 0.36f), new(0.1f, 0.35f),
            new(0.08f, 0.34f), new(0.02f, 0.32f), new(-0.06f, 0.3f), new(-0.12f, 0.28f), new(-0.14f, 0.26f),
            new(-0.1f, 0.24f), new(-0.02f, 0.22f), new(0.08f, 0.2f), new(0.14f, 0.18f), new(0.12f, 0.16f),
            new(0.04f, 0.14f), new(-0.06f, 0.12f), new(-0.12f, 0.1f), new(-0.1f, 0.08f), new(0f, 0.06f),
        ];
    }
}
