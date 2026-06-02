using System.Runtime.InteropServices;

namespace External_Aimbot
{
    public readonly struct TriggerBotDebug
    {
        public int EntityId { get; init; }
        public int TargetTeam { get; init; }
        public int LocalTeam { get; init; }
        public int TargetHealth { get; init; }
        public bool HotkeyHeld { get; init; }
        public string Status { get; init; }
    }

    internal static class TriggerBot
    {
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;

        public static void Process(
            GameMemory mem,
            IntPtr entitySystem,
            IntPtr localPawn,
            bool enabled,
            Hotkey hotkey,
            AimbotGameMode gameMode,
            int clickDelayMs,
            int cooldownMs,
            ref long lastShotTicks,
            out TriggerBotDebug debug)
        {
            bool hotkeyHeld = HotkeyInput.IsHeld(hotkey);

            if (!enabled)
            {
                debug = new TriggerBotDebug { HotkeyHeld = hotkeyHeld, Status = "Disabled" };
                return;
            }

            if (localPawn == IntPtr.Zero)
            {
                debug = new TriggerBotDebug { HotkeyHeld = hotkeyHeld, Status = "Not in game" };
                return;
            }

            int entityId = mem.ReadInt(localPawn, Offsets.m_iIDEntIndex);
            debug = new TriggerBotDebug
            {
                HotkeyHeld = hotkeyHeld,
                EntityId = entityId,
                Status = "Idle",
            };

            if (!hotkeyHeld)
            {
                debug = debug with { Status = $"Hold {HotkeyInput.Label(hotkey)}" };
                return;
            }

            if (entityId <= 0)
            {
                debug = debug with { Status = "No crosshair target" };
                return;
            }

            IntPtr entity = ResolveCrosshairEntity(mem, entitySystem, entityId);
            if (entity == IntPtr.Zero)
            {
                debug = debug with { Status = "Could not resolve entity" };
                return;
            }

            IntPtr targetPawn = ResolvePawn(mem, entitySystem, entity);
            int targetTeam = mem.ReadInt(targetPawn, Offsets.m_iTeamNum);
            int localTeam = mem.ReadInt(localPawn, Offsets.m_iTeamNum);
            int targetHealth = mem.ReadInt(targetPawn, Offsets.m_iHealth);
            if (targetHealth <= 0 && targetPawn != entity)
                targetHealth = mem.ReadInt(entity, Offsets.m_iPawnHealth);

            debug = debug with
            {
                TargetTeam = targetTeam,
                LocalTeam = localTeam,
                TargetHealth = targetHealth,
            };

            if (targetPawn == localPawn)
            {
                debug = debug with { Status = "Aiming at yourself" };
                return;
            }

            bool shouldShoot = gameMode == AimbotGameMode.Deathmatch
                ? entityId > 0
                : targetTeam != localTeam && targetTeam != 0 && localTeam != 0;

            if (!shouldShoot)
            {
                debug = debug with
                {
                    Status = gameMode == AimbotGameMode.Deathmatch
                        ? "Invalid target"
                        : "Same team — not shooting",
                };
                return;
            }

            long now = Environment.TickCount64;
            if (now - lastShotTicks < cooldownMs)
            {
                debug = debug with { Status = "Cooldown" };
                return;
            }

            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(Math.Clamp(clickDelayMs, 1, 100));
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);

            lastShotTicks = now;
            debug = debug with { Status = "Shot fired" };
        }

        private static IntPtr ResolveCrosshairEntity(GameMemory mem, IntPtr entitySystem, int index)
        {
            if (entitySystem == IntPtr.Zero)
                return IntPtr.Zero;

            foreach (int stride in new[] { EntityList.EntitySlotStride, EntityList.LegacyEntitySlotStride })
            {
                foreach (int idx in new[] { index & 0x7FFF, index })
                {
                    IntPtr entry = mem.ReadPtr(entitySystem, 0x8 * (idx >> 9) + 0x10);
                    if (!IsValidPointer(entry))
                        continue;

                    IntPtr entity = mem.ReadPtr(entry, stride * (idx & 0x1FF));
                    if (IsValidPointer(entity))
                        return entity;
                }
            }

            return IntPtr.Zero;
        }

        private static IntPtr ResolvePawn(GameMemory mem, IntPtr entitySystem, IntPtr entity)
        {
            int pawnHandle = mem.ReadInt(entity, Offsets.m_hPlayerPawn);
            if (pawnHandle <= 0)
                return entity;

            IntPtr pawn = ResolveCrosshairEntity(mem, entitySystem, pawnHandle);
            return pawn != IntPtr.Zero ? pawn : entity;
        }

        private static bool IsValidPointer(IntPtr ptr) =>
            ptr != IntPtr.Zero && ptr > (IntPtr)0x10000;

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    }
}
