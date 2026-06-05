using System.Numerics;

namespace External_Aimbot
{
    public enum ExposureBand
    {
        Hidden,
        Peeking,
        Wide,
    }

    public readonly struct ReconThreat
    {
        public float Distance { get; init; }
        public ExposureBand Band { get; init; }
        public Vector2 ScreenPos { get; init; }
    }

    public readonly struct ReconState
    {
        public bool Enabled { get; init; }
        public ExposureBand WorstBand { get; init; }
        public float ExposureScore { get; init; }
        public int ThreatCount { get; init; }
        public bool MapMeshActive { get; init; }
        public string Method { get; init; }
        public ReconThreat[] Threats { get; init; }
    }

    internal static class ReconAnalyzer
    {
        private const float FallbackFacingFov = 42f;

        public static ReconState Analyze(
            GameMemory mem,
            IntPtr entitySystem,
            Entity localPlayer,
            IReadOnlyList<Entity> candidates,
            bool enabled,
            bool preferMapMesh,
            float[] viewMatrix,
            float screenWidth,
            float screenHeight)
        {
            if (!enabled || localPlayer.pawnAddress == IntPtr.Zero || candidates.Count == 0)
            {
                return new ReconState
                {
                    Enabled = enabled,
                    Method = preferMapMesh && MapCollision.IsLoaded ? "Map raytrace" : "Spotted + facing",
                };
            }

            bool mapMesh = preferMapMesh && MapCollision.IsLoaded;
            string method = mapMesh ? "Map raytrace" : "Spotted + facing";

            Vector3 localHead = AimTarget.GetHeadPosition(
                mem,
                localPlayer.pawnAddress,
                localPlayer.origin,
                localPlayer.view);
            Vector3 localChest = AimTarget.GetChestPosition(mem, localPlayer.pawnAddress, localPlayer.origin);

            var threats = new List<ReconThreat>(candidates.Count);
            ExposureBand worst = ExposureBand.Hidden;
            float maxScore = 0f;

            foreach (Entity enemy in candidates)
            {
                if (enemy.pawnAddress == localPlayer.pawnAddress || enemy.health <= 0)
                    continue;

                ExposureBand band = EvaluateExposure(
                    mem,
                    entitySystem,
                    enemy,
                    localPlayer.pawnAddress,
                    localHead,
                    localChest,
                    mapMesh);

                if (band == ExposureBand.Hidden)
                    continue;

                if (band > worst)
                    worst = band;

                maxScore = Math.Max(maxScore, BandScore(band));

                Vector3 markerWorld = enemy.GetAimPosition(mem);
                if (!ViewMatrix.TryWorldToScreen(
                        markerWorld,
                        viewMatrix,
                        screenWidth,
                        screenHeight,
                        out Vector2 screenPos))
                {
                    continue;
                }

                threats.Add(new ReconThreat
                {
                    Distance = enemy.distance,
                    Band = band,
                    ScreenPos = screenPos,
                });
            }

            return new ReconState
            {
                Enabled = true,
                WorstBand = worst,
                ExposureScore = maxScore,
                ThreatCount = threats.Count,
                MapMeshActive = mapMesh,
                Method = method,
                Threats = threats.ToArray(),
            };
        }

        private static ExposureBand EvaluateExposure(
            GameMemory mem,
            IntPtr entitySystem,
            Entity enemy,
            IntPtr localPawn,
            Vector3 localHead,
            Vector3 localChest,
            bool mapMesh)
        {
            Vector3 enemyEye = AimTarget.GetLocalEyePosition(enemy.origin, enemy.view);

            if (mapMesh)
            {
                bool chestClear = MapCollision.HasClearLine(enemyEye, localChest);
                if (chestClear)
                    return ExposureBand.Wide;

                bool headClear = MapCollision.HasClearLine(enemyEye, localHead);
                if (headClear)
                    return ExposureBand.Peeking;

                return ExposureBand.Hidden;
            }

            if (enemy.controllerAddress == IntPtr.Zero)
                return ExposureBand.Hidden;

            int enemyControllerIndex = LocalPlayer.ResolveControllerIndex(mem, entitySystem, enemy.controllerAddress);
            if (enemyControllerIndex < 0)
                return ExposureBand.Hidden;

            if (!Visibility.IsSpottedByController(mem, localPawn, enemyControllerIndex))
                return ExposureBand.Hidden;

            Vector2 enemyAngles = AimTarget.ReadViewAngles(mem, enemy.pawnAddress);
            Vector2 toLocal = Calculate.CalculateAngles(enemyEye, localChest);
            if (Calculate.GetFovDistance(enemyAngles, toLocal) > FallbackFacingFov)
                return ExposureBand.Hidden;

            bool peekOnly = Vector3.Distance(enemyEye, localHead) < Vector3.Distance(enemyEye, localChest) + 8f;
            return peekOnly ? ExposureBand.Peeking : ExposureBand.Wide;
        }

        private static float BandScore(ExposureBand band) =>
            band switch
            {
                ExposureBand.Wide => 1f,
                ExposureBand.Peeking => 0.55f,
                _ => 0f,
            };
    }
}
