using System.Runtime.InteropServices;

namespace External_Aimbot
{
    public readonly struct AllGunsAutoDebug
    {
        public string ActiveWeapon { get; init; }
        public bool Shooting { get; init; }
        public int WeaponsUpdated { get; init; }
        public string Status { get; init; }
    }

    internal static class AllGunsAuto
    {
        private const int AttackPress = 65537;
        private const int AttackRelease = 256;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;

        private static long _lastShotMs;

        public static void Process(
            GameMemory mem,
            IntPtr pawn,
            IntPtr entitySystem,
            bool enabled,
            out AllGunsAutoDebug debug)
        {
            debug = new AllGunsAutoDebug { Status = enabled ? "Active" : "Disabled" };

            if (!enabled || pawn == IntPtr.Zero || entitySystem == IntPtr.Zero)
                return;

            if (Offsets.m_bBurstMode == 0)
            {
                debug = debug with { Status = "Weapon offsets missing" };
                return;
            }

            int updated = 0;
            foreach (IntPtr weapon in WeaponInventory.EnumerateWeapons(mem, pawn, entitySystem))
            {
                mem.WriteInt(weapon, Offsets.m_bBurstMode, 0);
                updated++;
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int defIndex = WeaponInventory.ReadDefinitionIndex(mem, activeWeapon);
            string weaponName = defIndex > 0 ? WeaponCatalog.GetName(defIndex) : "none";

            if (!InputState.IsAttackHeld() || activeWeapon == IntPtr.Zero || defIndex <= 0)
            {
                ReleaseAttack(mem);
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    WeaponsUpdated = updated,
                    Status = activeWeapon == IntPtr.Zero ? "No active weapon" : "Ready",
                };
                return;
            }

            if (mem.ReadBool(activeWeapon, Offsets.m_bInReload))
            {
                ReleaseAttack(mem);
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    WeaponsUpdated = updated,
                    Status = "Reloading",
                };
                return;
            }

            ResetFireDelays(mem, activeWeapon);
            ClearBoltAction(mem, activeWeapon);

            if (WeaponCatalog.IsSemiAuto(defIndex))
            {
                string status = RunSemiAutoClicker(mem, defIndex);
                debug = new AllGunsAutoDebug
                {
                    ActiveWeapon = weaponName,
                    Shooting = true,
                    WeaponsUpdated = updated,
                    Status = status,
                };
                return;
            }

            HoldAttack(mem);
            debug = new AllGunsAutoDebug
            {
                ActiveWeapon = weaponName,
                Shooting = true,
                WeaponsUpdated = updated,
                Status = "Full auto delays cleared",
            };
        }

        private static string RunSemiAutoClicker(GameMemory mem, int defIndex)
        {
            long now = Environment.TickCount64;
            int intervalMs = GetFireIntervalMs(defIndex);

            if (now - _lastShotMs < intervalMs)
                return "Waiting";

            // User keeps LMB held; synthetic up-then-down registers as a new semi-auto click.
            SimulateMouseUp();
            Thread.Sleep(1);
            SimulateMouseDown();

            PressAttack(mem);
            Thread.Sleep(1);
            ReleaseAttack(mem);

            _lastShotMs = now;
            return "Auto shot";
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

        private static void HoldAttack(GameMemory mem)
        {
            if (Offsets.dwAttack == 0)
                return;

            mem.WriteInt(mem.Client, Offsets.dwAttack, AttackPress);
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
