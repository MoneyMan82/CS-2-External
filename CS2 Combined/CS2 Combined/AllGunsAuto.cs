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
        private const int AttackPress = 65537;
        private const int AttackRelease = 256;
        private const int MinIntervalMs = 80;
        private const int ReleaseHoldMs = 14;
        private const int PressHoldMs = 12;

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
        private static readonly HashSet<IntPtr> _burstDisabled = [];

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            out AllGunsAutoDebug debug)
        {
            debug = new AllGunsAutoDebug { Status = enabled ? "Active" : "Disabled" };

            if (!enabled || pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
            {
                ResetState();
                return;
            }

            if (_lastPawn != pawn)
            {
                _burstDisabled.Clear();
                _lastPawn = pawn;
                ResetState();
            }

            if (Offsets.dwAttack == 0)
            {
                debug = debug with { Status = "Attack offset missing" };
                return;
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int defIndex = WeaponInventory.ReadDefinitionIndex(mem, activeWeapon);
            string weaponName = defIndex > 0 ? WeaponCatalog.GetName(defIndex) : "none";
            bool attackHeld = InputState.IsAttackHeld();
            bool isSemiAuto = defIndex > 0 && WeaponCatalog.IsSemiAuto(defIndex);

            if (!attackHeld || activeWeapon == IntPtr.Zero || defIndex <= 0)
            {
                ReleaseAttack(mem);
                ResetState();
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = attackHeld,
                    IsSemiAuto = isSemiAuto,
                    Phase = "Idle",
                    Status = activeWeapon == IntPtr.Zero ? "No active weapon" : "Ready (hold LMB in CS2)",
                };
                return;
            }

            EnsureBurstDisabled(mem, activeWeapon);

            if (mem.ReadBool(activeWeapon, Offsets.m_bInReload))
            {
                ReleaseAttack(mem);
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

            (string status, string phase) = RunSemiAutoPulse(mem, activeWeapon, defIndex);
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

        private static void EnsureBurstDisabled(GameMemory mem, IntPtr weapon)
        {
            if (Offsets.m_bBurstMode == 0 || !_burstDisabled.Add(weapon))
                return;

            mem.WriteInt(weapon, Offsets.m_bBurstMode, 0);
        }

        private static (string Status, string Phase) RunSemiAutoPulse(
            GameMemory mem,
            IntPtr weapon,
            int defIndex)
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

                    ReleaseAttack(mem);
                    _phase = ClickPhase.Released;
                    _phaseStartMs = now;
                    return ("Release", "Release");

                case ClickPhase.Released:
                    if (now - _phaseStartMs < ReleaseHoldMs)
                        return ("Release hold", "Release");

                    ResetFireDelays(mem, weapon);
                    PressAttack(mem);
                    _phase = ClickPhase.Pressed;
                    _phaseStartMs = now;
                    return ("Press", "Press");

                case ClickPhase.Pressed:
                    if (now - _phaseStartMs < PressHoldMs)
                        return ("Press hold", "Press");

                    ReleaseAttack(mem);
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
            9 => 120,
            40 => 100,
            1 => 250,
            64 => 200,
            35 or 25 or 29 or 27 => 280,
            _ => MinIntervalMs,
        };

        private static void ResetFireDelays(GameMemory mem, IntPtr weapon)
        {
            if (Offsets.m_flNextClientFireBulletTime != 0)
            {
                mem.WriteFloat(weapon, Offsets.m_flNextClientFireBulletTime, 0f);
                mem.WriteFloat(weapon, Offsets.m_flNextClientFireBulletTime_Repredict, 0f);
            }

            if (Offsets.m_nNextPrimaryAttackTick != 0)
            {
                mem.WriteInt(weapon, Offsets.m_nNextPrimaryAttackTick, 0);
                mem.WriteFloat(weapon, Offsets.m_flNextPrimaryAttackTickRatio, 0f);
            }
        }

        private static void PressAttack(GameMemory mem)
        {
            if (Offsets.dwAttack != 0)
                mem.WriteInt(mem.Client, Offsets.dwAttack, AttackPress);
        }

        private static void ReleaseAttack(GameMemory mem)
        {
            if (Offsets.dwAttack != 0)
                mem.WriteInt(mem.Client, Offsets.dwAttack, AttackRelease);
        }
    }
}
