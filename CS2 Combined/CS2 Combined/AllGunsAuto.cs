using System.Runtime.InteropServices;

namespace External_Aimbot
{
    public readonly struct AllGunsAutoDebug
    {
        public string ActiveWeapon { get; init; }
        public bool Shooting { get; init; }
        public bool AttackHeld { get; init; }
        public bool IsSemiAuto { get; init; }
        public int WeaponsUpdated { get; init; }
        public string Phase { get; init; }
        public string Status { get; init; }
    }

    internal static class AllGunsAuto
    {
        private const int AttackPress = 65537;
        private const int AttackRelease = 256;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const int MinPulseIntervalMs = 16;
        private const int ReleaseMs = 12;
        private const int PressMs = 4;

        private enum SemiClickPhase
        {
            Idle,
            Release,
            Press,
        }

        private static long _lastShotMs;
        private static long _attackStartMs;
        private static bool _attackArmed;
        private static SemiClickPhase _semiPhase = SemiClickPhase.Idle;
        private static long _semiPhaseMs;

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
                ResetSemiAutoState();
                return;
            }

            if (Offsets.m_bBurstMode == 0)
            {
                ResetSemiAutoState();
                debug = debug with { Status = "Weapon offsets missing" };
                return;
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int defIndex = WeaponInventory.ReadDefinitionIndex(mem, activeWeapon);
            string weaponName = defIndex > 0 ? WeaponCatalog.GetName(defIndex) : "none";
            bool attackHeld = InputState.IsAttackHeld();
            bool isSemiAuto = defIndex > 0 && WeaponCatalog.IsSemiAuto(defIndex);

            if (!attackHeld || activeWeapon == IntPtr.Zero || defIndex <= 0)
            {
                _attackArmed = false;
                ResetSemiAutoState();
                if (isSemiAuto)
                    ReleaseAttack(mem);

                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = attackHeld,
                    IsSemiAuto = isSemiAuto,
                    Phase = "Idle",
                    Status = activeWeapon == IntPtr.Zero ? "No active weapon" : "Ready (hold LMB)",
                };
                return;
            }

            int updated = DisableBurstOnLoadout(mem, pawn, entitySystem);

            if (mem.ReadBool(activeWeapon, Offsets.m_bInReload))
            {
                ResetSemiAutoState();
                ReleaseAttack(mem);
                SimulateMouseUp();
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = true,
                    IsSemiAuto = isSemiAuto,
                    WeaponsUpdated = updated,
                    Phase = "Idle",
                    Status = "Reloading",
                };
                return;
            }

            if (!isSemiAuto)
            {
                _attackArmed = false;
                ResetSemiAutoState();
                debug = new AllGunsAutoDebug
                {
                    ActiveWeapon = weaponName,
                    Shooting = true,
                    AttackHeld = true,
                    IsSemiAuto = false,
                    WeaponsUpdated = updated,
                    Phase = "Full auto",
                    Status = "Full auto (unchanged)",
                };
                return;
            }

            ResetFireDelays(mem, activeWeapon);
            ClearBoltAction(mem, activeWeapon);

            if (!_attackArmed)
            {
                _attackStartMs = Environment.TickCount64;
                _attackArmed = true;
            }

            (string status, string phase) = RunSemiAutoClicker(mem, defIndex);
            debug = new AllGunsAutoDebug
            {
                ActiveWeapon = weaponName,
                Shooting = true,
                AttackHeld = true,
                IsSemiAuto = true,
                WeaponsUpdated = updated,
                Phase = phase,
                Status = status,
            };
        }

        private static int DisableBurstOnLoadout(GameMemory mem, IntPtr pawn, IntPtr entitySystem)
        {
            int updated = 0;
            foreach (IntPtr weapon in WeaponInventory.EnumerateWeapons(mem, pawn, entitySystem))
            {
                mem.WriteInt(weapon, Offsets.m_bBurstMode, 0);
                updated++;
            }

            return updated;
        }

        private static (string Status, string Phase) RunSemiAutoClicker(GameMemory mem, int defIndex)
        {
            long now = Environment.TickCount64;
            int intervalMs = Math.Max(MinPulseIntervalMs, GetFireIntervalMs(defIndex));
            long waitBase = _lastShotMs > 0 ? _lastShotMs : _attackStartMs;

            switch (_semiPhase)
            {
                case SemiClickPhase.Idle:
                    if (now - waitBase < intervalMs)
                        return ($"Wait {intervalMs - (now - waitBase)}ms", "Wait");

                    _semiPhase = SemiClickPhase.Release;
                    _semiPhaseMs = now;
                    ReleaseAttack(mem);
                    SimulateMouseUp();
                    return ("Release", "Release");

                case SemiClickPhase.Release:
                    ReleaseAttack(mem);
                    SimulateMouseUp();
                    if (now - _semiPhaseMs < ReleaseMs)
                        return ("Release", "Release");

                    _semiPhase = SemiClickPhase.Press;
                    _semiPhaseMs = now;
                    PressAttack(mem);
                    SimulateMouseDown();
                    return ("Press", "Press");

                case SemiClickPhase.Press:
                    PressAttack(mem);
                    SimulateMouseDown();
                    if (now - _semiPhaseMs < PressMs)
                        return ("Press", "Press");

                    ReleaseAttack(mem);
                    _semiPhase = SemiClickPhase.Idle;
                    _lastShotMs = now;
                    return ("Auto shot", "Shot");

                default:
                    ResetSemiAutoState();
                    return ("Reset", "Idle");
            }
        }

        private static void ResetSemiAutoState()
        {
            _semiPhase = SemiClickPhase.Idle;
            _semiPhaseMs = 0;
            _lastShotMs = 0;
        }

        private static int GetFireIntervalMs(int defIndex) => defIndex switch
        {
            9 => 110,
            40 => 90,
            1 => 220,
            64 => 180,
            35 or 25 or 29 or 27 => 250,
            _ => 100,
        };

        private static void ResetFireDelays(GameMemory mem, IntPtr weapon)
        {
            if (Offsets.m_flNextClientFireBulletTime != 0)
            {
                mem.WriteFloat(weapon, Offsets.m_flNextClientFireBulletTime, 0f);
                mem.WriteFloat(weapon, Offsets.m_flNextClientFireBulletTime_Repredict, 0f);
            }

            if (Offsets.m_flPostponeFireReadyFrac != 0)
            {
                mem.WriteFloat(weapon, Offsets.m_flPostponeFireReadyFrac, 0f);
                mem.WriteInt(weapon, Offsets.m_nPostponeFireReadyTicks, 0);
            }

            if (Offsets.m_fLastShotTime != 0)
                mem.WriteFloat(weapon, Offsets.m_fLastShotTime, 0f);

            if (Offsets.m_nNextPrimaryAttackTick != 0)
            {
                mem.WriteInt(weapon, Offsets.m_nNextPrimaryAttackTick, 0);
                mem.WriteFloat(weapon, Offsets.m_flNextPrimaryAttackTickRatio, 0f);
            }

            if (Offsets.m_iBurstShotsRemaining != 0)
                mem.WriteInt(weapon, Offsets.m_iBurstShotsRemaining, 0);
        }

        private static void ClearBoltAction(GameMemory mem, IntPtr weapon)
        {
            if (Offsets.m_bNeedsBoltAction != 0)
                mem.WriteInt(weapon, Offsets.m_bNeedsBoltAction, 0);
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

        private static void SimulateMouseDown() =>
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);

        private static void SimulateMouseUp() =>
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    }
}
