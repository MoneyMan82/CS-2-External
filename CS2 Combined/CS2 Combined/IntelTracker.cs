using System.Numerics;

namespace External_Aimbot
{
    public readonly struct IntelGhostBlip
    {
        public float NormalizedX { get; init; }
        public float NormalizedY { get; init; }
        public float AgeSeconds { get; init; }
        public float DecaySeconds { get; init; }
        public bool IsLive { get; init; }
        public float TrailX { get; init; }
        public float TrailY { get; init; }
    }

    public readonly struct IntelState
    {
        public bool Enabled { get; init; }
        public int LiveCount { get; init; }
        public int GhostCount { get; init; }
        public IntelGhostBlip[] Blips { get; init; }
    }

    internal static class IntelTracker
    {
        private sealed class IntelEntry
        {
            public Vector3 LastOrigin;
            public Vector3 PrevOrigin;
            public long LastSeenMs;
            public bool WasLive;
        }

        private static readonly Dictionary<IntPtr, IntelEntry> Entries = new();
        private static string _lastMap = "";

        public static IntelState Update(
            GameMemory mem,
            Entity localPlayer,
            IReadOnlyList<Entity> candidates,
            int localControllerIndex,
            bool enabled,
            float decaySeconds,
            float range,
            float viewYawDegrees)
        {
            if (!enabled || localPlayer.pawnAddress == IntPtr.Zero)
            {
                Entries.Clear();
                return new IntelState { Enabled = enabled };
            }

            string map = MapCollision.CurrentMap;
            if (!string.IsNullOrEmpty(map) && map != _lastMap)
            {
                Entries.Clear();
                _lastMap = map;
            }

            long now = Environment.TickCount64;
            float decayMs = Math.Clamp(decaySeconds, 2f, 30f) * 1000f;
            var activePawns = new HashSet<IntPtr>();

            foreach (Entity entity in candidates)
            {
                if (entity.pawnAddress == IntPtr.Zero || entity.pawnAddress == localPlayer.pawnAddress)
                    continue;

                if (entity.health <= 0)
                    continue;

                activePawns.Add(entity.pawnAddress);

                bool live = entity.isVisible ||
                    (localControllerIndex >= 0 &&
                     Visibility.IsSpottedByController(mem, entity.pawnAddress, localControllerIndex));

                if (!live)
                    continue;

                if (!Entries.TryGetValue(entity.pawnAddress, out IntelEntry? entry))
                {
                    entry = new IntelEntry { PrevOrigin = entity.origin, LastOrigin = entity.origin };
                    Entries[entity.pawnAddress] = entry;
                }
                else
                {
                    entry.PrevOrigin = entry.LastOrigin;
                    entry.LastOrigin = entity.origin;
                }

                entry.LastSeenMs = now;
                entry.WasLive = true;
            }

            var remove = new List<IntPtr>();
            foreach (KeyValuePair<IntPtr, IntelEntry> pair in Entries)
            {
                if (!activePawns.Contains(pair.Key))
                {
                    remove.Add(pair.Key);
                    continue;
                }

                if (now - pair.Value.LastSeenMs > decayMs)
                    remove.Add(pair.Key);
            }

            foreach (IntPtr pawn in remove)
                Entries.Remove(pawn);

            float yawRad = viewYawDegrees * MathF.PI / 180f;
            float sin = MathF.Sin(yawRad);
            float cos = MathF.Cos(yawRad);
            float clampedRange = Math.Max(range, 500f);

            var blips = new List<IntelGhostBlip>(Entries.Count);
            int liveCount = 0;
            int ghostCount = 0;

            foreach (Entity entity in candidates)
            {
                if (!Entries.TryGetValue(entity.pawnAddress, out IntelEntry? entry))
                    continue;

                float ageSec = (now - entry.LastSeenMs) / 1000f;
                bool live = entity.isVisible ||
                    (localControllerIndex >= 0 &&
                     Visibility.IsSpottedByController(mem, entity.pawnAddress, localControllerIndex));

                if (live)
                    liveCount++;
                else
                    ghostCount++;

                Vector3 delta = entry.LastOrigin - localPlayer.origin;
                float forward = delta.X * cos + delta.Y * sin;
                float right = -delta.X * sin + delta.Y * cos;

                Vector3 move = entry.LastOrigin - entry.PrevOrigin;
                float trailForward = move.X * cos + move.Y * sin;
                float trailRight = -move.X * sin + move.Y * cos;
                float trailLen = MathF.Sqrt(trailForward * trailForward + trailRight * trailRight);
                if (trailLen > 0.01f)
                {
                    trailForward /= trailLen;
                    trailRight /= trailLen;
                }

                blips.Add(new IntelGhostBlip
                {
                    NormalizedX = Math.Clamp(right / clampedRange, -1f, 1f),
                    NormalizedY = Math.Clamp(-forward / clampedRange, -1f, 1f),
                    AgeSeconds = ageSec,
                    DecaySeconds = decaySeconds,
                    IsLive = live,
                    TrailX = trailRight * 0.15f,
                    TrailY = trailForward * 0.15f,
                });
            }

            return new IntelState
            {
                Enabled = true,
                LiveCount = liveCount,
                GhostCount = ghostCount,
                Blips = blips.ToArray(),
            };
        }
    }
}
