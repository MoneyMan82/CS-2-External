using System.Runtime.InteropServices;

namespace External_Aimbot
{
    public readonly struct AllGunsAutoDebug
    {
        public string ActiveWeapon { get; init; }
        public bool Shooting { get; init; }
        public bool AttackHeld { get; init; }
        public bool IsSemiAuto { get; init; }
        public int ShotCount { get; init; }
        public string Phase { get; init; }
        public string Status { get; init; }
    }

    internal static class AllGunsAuto
    {
        private const uint InputMouse = 0;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const int MinIntervalMs = 100;
        private const int ReleaseHoldMs = 20;
        private const int PressHoldMs = 16;

        private enum ClickPhase
        {
            Idle,
            Released,
            Pressed,
        }

        private static ClickPhase _phase;
        private static long _phaseStartMs;
        private static long _lastShotMs;
        private static long _attackStartMs;
        private static int _shotCount;
        private static IntPtr _lastPawn;

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            out AllGunsAutoDebug debug)
        {
            debug = new AllGunsAutoDebug { Status = enabled ? "Active" : "Disabled" };

            if (!enabled)
            {
                ResetState();
                return;
            }

            if (pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
            {
                ResetState();
                debug = debug with { Status = "Not in game" };
                return;
            }

            if (_lastPawn != pawn)
            {
                _lastPawn = pawn;
                ResetState();
            }

            if (!IsCs2Foreground())
            {
                ResetState();
                debug = debug with { Status = "Focus CS2 window" };
                return;
            }

            if (!IsPlayerAlive(mem, pawn))
            {
                ResetState();
                debug = debug with { Status = "Dead / spectating" };
                return;
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int defIndex = IsValidWeapon(activeWeapon)
                ? WeaponInventory.ReadDefinitionIndex(mem, activeWeapon)
                : 0;
            string weaponName = defIndex > 0 ? WeaponCatalog.GetName(defIndex) : "none";
            bool attackHeld = InputState.IsAttackHeld(mem);
            bool isSemiAuto = defIndex > 0 && WeaponCatalog.IsSemiAuto(defIndex);
            bool ownedWeapon = IsOwnedWeapon(mem, pawn, entitySystem, activeWeapon);

            if (!attackHeld || !ownedWeapon || defIndex <= 0)
            {
                ResetState();
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = attackHeld,
                    IsSemiAuto = isSemiAuto,
                    Phase = "Idle",
                    Status = !ownedWeapon ? "No active weapon" : "Ready (hold LMB in CS2)",
                };
                return;
            }

            if (Offsets.m_bInReload != 0 && mem.ReadBool(activeWeapon, Offsets.m_bInReload))
            {
                ResetState();
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = true,
                    IsSemiAuto = isSemiAuto,
                    Phase = "Idle",
                    Status = "Reloading",
                };
                return;
            }

            if (!isSemiAuto)
            {
                ResetState();
                debug = new AllGunsAutoDebug
                {
                    ActiveWeapon = weaponName,
                    Shooting = true,
                    AttackHeld = true,
                    IsSemiAuto = false,
                    Phase = "Full auto",
                    Status = "Full auto (unchanged)",
                };
                return;
            }

            if (_attackStartMs == 0)
                _attackStartMs = Environment.TickCount64;

            (string status, string phase) = RunSemiAutoClick(defIndex);
            debug = new AllGunsAutoDebug
            {
                ActiveWeapon = weaponName,
                Shooting = true,
                AttackHeld = true,
                IsSemiAuto = true,
                ShotCount = _shotCount,
                Phase = phase,
                Status = status,
            };
        }

        private static (string Status, string Phase) RunSemiAutoClick(int defIndex)
        {
            long now = Environment.TickCount64;
            int intervalMs = Math.Max(MinIntervalMs, GetFireIntervalMs(defIndex));

            switch (_phase)
            {
                case ClickPhase.Idle:
                    if (_lastShotMs != 0 && now - _lastShotMs < intervalMs)
                        return ($"Wait {intervalMs - (now - _lastShotMs)}ms", "Wait");

                    if (_lastShotMs == 0 && now - _attackStartMs < MinIntervalMs)
                        return ($"Arming {MinIntervalMs - (now - _attackStartMs)}ms", "Wait");

                    SendMouse(MouseEventLeftUp);
                    _phase = ClickPhase.Released;
                    _phaseStartMs = now;
                    return ("Mouse up", "Release");

                case ClickPhase.Released:
                    if (now - _phaseStartMs < ReleaseHoldMs)
                        return ("Release hold", "Release");

                    SendMouse(MouseEventLeftDown);
                    _phase = ClickPhase.Pressed;
                    _phaseStartMs = now;
                    return ("Mouse down", "Press");

                case ClickPhase.Pressed:
                    if (now - _phaseStartMs < PressHoldMs)
                        return ("Press hold", "Press");

                    SendMouse(MouseEventLeftUp);
                    _phase = ClickPhase.Idle;
                    _lastShotMs = now;
                    _shotCount++;
                    return ($"Shot #{_shotCount}", "Shot");

                default:
                    _phase = ClickPhase.Idle;
                    return ("Idle", "Idle");
            }
        }

        private static void ResetState()
        {
            _phase = ClickPhase.Idle;
            _phaseStartMs = 0;
            _lastShotMs = 0;
            _attackStartMs = 0;
            _shotCount = 0;
        }

        private static int GetFireIntervalMs(int defIndex) => defIndex switch
        {
            9 => 140,
            40 => 120,
            1 => 300,
            64 => 240,
            35 or 25 or 29 or 27 => 320,
            _ => MinIntervalMs,
        };

        private static bool IsOwnedWeapon(GameMemory mem, IntPtr pawn, IntPtr entitySystem, IntPtr weapon)
        {
            if (!IsValidWeapon(weapon))
                return false;

            foreach (IntPtr owned in WeaponInventory.EnumerateWeapons(mem, pawn, entitySystem))
            {
                if (owned == weapon)
                    return true;
            }

            return false;
        }

        private static bool IsPlayerAlive(GameMemory mem, IntPtr pawn)
        {
            if (Offsets.m_lifeState == 0)
                return true;

            return mem.ReadByte(pawn + Offsets.m_lifeState) == 0;
        }

        private static bool IsCs2Foreground()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;

            foreach (System.Diagnostics.Process cs2 in System.Diagnostics.Process.GetProcessesByName("cs2"))
            {
                if (cs2.MainWindowHandle == foreground)
                    return true;
            }

            return false;
        }

        private static void SendMouse(uint flags)
        {
            Input input = new()
            {
                type = InputMouse,
                mi = new MouseInput { dwFlags = flags },
            };

            SendInput(1, [input], Marshal.SizeOf<Input>());
        }

        private static bool IsValidWeapon(IntPtr weapon)
        {
            long value = weapon.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint type;
            public MouseInput mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
    }
}
