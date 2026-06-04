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
        private const int MinIntervalMs = 85;
        private const int ReleaseHoldMs = 16;
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
        private static bool _attackPulsing;
        private static bool _wasEnabled;

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            bool overlayBlockingInput,
            out AllGunsAutoDebug debug)
        {
            debug = new AllGunsAutoDebug { Status = enabled ? "Active" : "Disabled" };

            if (!enabled && _wasEnabled)
            {
                StopPulsing(mem);
                ResetState();
            }

            _wasEnabled = enabled;

            if (!enabled)
                return;

            if (overlayBlockingInput)
            {
                StopPulsing(mem);
                ResetState();
                debug = debug with { Phase = "Idle", Status = "Using menu" };
                return;
            }

            if (pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
            {
                StopPulsing(mem);
                ResetState();
                debug = debug with { Phase = "Idle", Status = "Not in game" };
                return;
            }

            if (_lastPawn != pawn)
            {
                StopPulsing(mem);
                _lastPawn = pawn;
                ResetState();
            }

            if (Offsets.dwAttack == 0)
            {
                debug = debug with { Phase = "Idle", Status = "Attack offset missing" };
                return;
            }

            if (!IsPlayerAlive(mem, pawn))
            {
                StopPulsing(mem);
                ResetState();
                debug = debug with { Phase = "Idle", Status = "Dead / spectating" };
                return;
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int defIndex = IsValidWeapon(activeWeapon)
                ? WeaponInventory.ReadDefinitionIndex(mem, activeWeapon)
                : 0;
            string weaponName = defIndex > 0 ? WeaponCatalog.GetName(defIndex) : "none";
            bool attackHeld = InputState.IsAttackHeld(mem);
            bool isSemiAuto = defIndex > 0 && WeaponCatalog.IsSemiAuto(defIndex);

            if (!attackHeld || !IsValidWeapon(activeWeapon) || defIndex <= 0)
            {
                StopPulsing(mem);
                ResetState();
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = attackHeld,
                    IsSemiAuto = isSemiAuto,
                    Phase = "Idle",
                    Status = !IsValidWeapon(activeWeapon) ? "No active weapon" : "Hold LMB in CS2",
                };
                return;
            }

            if (Offsets.m_bInReload != 0 && mem.ReadBool(activeWeapon, Offsets.m_bInReload))
            {
                StopPulsing(mem);
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
                StopPulsing(mem);
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

        private static void ResetFireDelays(GameMemory mem, IntPtr weapon)
        {
            if (!IsValidWeapon(weapon))
                return;

            if (Offsets.m_nNextPrimaryAttackTick != 0)
            {
                mem.WriteInt(weapon, Offsets.m_nNextPrimaryAttackTick, 0);
                mem.WriteFloat(weapon, Offsets.m_flNextPrimaryAttackTickRatio, 0f);
            }

            if (Offsets.m_flNextClientFireBulletTime != 0)
            {
                mem.WriteFloat(weapon, Offsets.m_flNextClientFireBulletTime, 0f);
                mem.WriteFloat(weapon, Offsets.m_flNextClientFireBulletTime_Repredict, 0f);
            }
        }

        private static void StopPulsing(GameMemory mem)
        {
            if (!_attackPulsing)
                return;

            ReleaseAttack(mem);
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
            9 or 32 or 61 => 120,
            40 or 36 => 100,
            1 => 260,
            64 => 200,
            35 or 25 or 29 or 27 => 280,
            _ => MinIntervalMs,
        };

        private static bool IsPlayerAlive(GameMemory mem, IntPtr pawn)
        {
            if (Offsets.m_lifeState == 0)
                return true;

            return mem.ReadByte(pawn + Offsets.m_lifeState) == 0;
        }

        private static void PressAttack(GameMemory mem)
        {
            mem.WriteInt(mem.Client, Offsets.dwAttack, AttackPress);
            _attackPulsing = true;
        }

        private static void ReleaseAttack(GameMemory mem)
        {
            mem.WriteInt(mem.Client, Offsets.dwAttack, AttackRelease);
            _attackPulsing = false;
        }

        private static bool IsValidWeapon(IntPtr weapon)
        {
            long value = weapon.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }
    }
}
