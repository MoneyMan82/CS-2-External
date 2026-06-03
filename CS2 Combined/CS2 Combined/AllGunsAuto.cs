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
        public int ShotCount { get; init; }
        public string Phase { get; init; }
        public string Status { get; init; }
    }

    internal static class AllGunsAuto
    {
        private const int AttackPress = 65537;
        private const int AttackRelease = 256;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint WmLButtonUp = 0x0202;
        private const uint WmLButtonDown = 0x0201;
        private const int MkLButton = 0x0001;
        private const int MinPulseIntervalMs = 16;
        private const int ReleaseHoldMs = 12;
        private const int PressHoldMs = 10;

        private static long _lastShotMs;
        private static long _attackStartMs;
        private static int _shotCount;

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
                ResetSemiAutoState();
                if (isSemiAuto)
                    ReleaseAttack(mem);

                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = attackHeld,
                    IsSemiAuto = isSemiAuto,
                    ShotCount = _shotCount,
                    Phase = "Idle",
                    Status = activeWeapon == IntPtr.Zero ? "No active weapon" : "Ready (hold LMB in CS2)",
                };
                return;
            }

            int updated = DisableBurstOnLoadout(mem, pawn, entitySystem);

            if (mem.ReadBool(activeWeapon, Offsets.m_bInReload))
            {
                ResetSemiAutoState();
                ReleaseAttack(mem);
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    AttackHeld = true,
                    IsSemiAuto = isSemiAuto,
                    WeaponsUpdated = updated,
                    ShotCount = _shotCount,
                    Phase = "Idle",
                    Status = "Reloading",
                };
                return;
            }

            if (!isSemiAuto)
            {
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

            if (_attackStartMs == 0)
                _attackStartMs = Environment.TickCount64;

            ResetFireDelays(mem, activeWeapon);
            ClearBoltAction(mem, activeWeapon);

            (string status, string phase) = RunSemiAutoClicker(mem, defIndex);
            debug = new AllGunsAutoDebug
            {
                ActiveWeapon = weaponName,
                Shooting = true,
                AttackHeld = true,
                IsSemiAuto = true,
                WeaponsUpdated = updated,
                ShotCount = _shotCount,
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

            if (_lastShotMs == 0)
            {
                if (now - _attackStartMs < MinPulseIntervalMs)
                    return ($"Arming {MinPulseIntervalMs - (now - _attackStartMs)}ms", "Wait");
            }
            else if (now - _lastShotMs < intervalMs)
            {
                return ($"Wait {intervalMs - (now - _lastShotMs)}ms", "Wait");
            }

            string method = FireSemiAutoClick(mem);
            _lastShotMs = Environment.TickCount64;
            _shotCount++;
            return ($"Auto shot #{_shotCount} ({method})", "Shot");
        }

        private static string FireSemiAutoClick(GameMemory mem)
        {
            ReleaseAttack(mem);

            if (TryPostCs2MouseClick(mem))
                return "window";

            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(ReleaseHoldMs);
            PressAttack(mem);
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(PressHoldMs);
            ReleaseAttack(mem);
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            return "mouse";
        }

        private static bool TryPostCs2MouseClick(GameMemory mem)
        {
            IntPtr hwnd = GetCs2WindowHandle();
            if (hwnd == IntPtr.Zero)
                return false;

            ReleaseAttack(mem);
            PostMessage(hwnd, WmLButtonUp, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(ReleaseHoldMs);
            PressAttack(mem);
            PostMessage(hwnd, WmLButtonDown, (IntPtr)MkLButton, IntPtr.Zero);
            Thread.Sleep(PressHoldMs);
            ReleaseAttack(mem);
            PostMessage(hwnd, WmLButtonUp, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        private static IntPtr GetCs2WindowHandle()
        {
            foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName("cs2"))
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process.MainWindowHandle;
            }

            return IntPtr.Zero;
        }

        private static void ResetSemiAutoState()
        {
            _lastShotMs = 0;
            _attackStartMs = 0;
            _shotCount = 0;
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

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
