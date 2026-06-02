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

            int updated = 0;
            foreach (IntPtr weapon in WeaponInventory.EnumerateWeapons(mem, pawn, entitySystem))
            {
                if (DisableBurstMode(mem, weapon))
                    updated++;
            }

            IntPtr activeWeapon = WeaponInventory.GetActiveWeapon(mem, pawn, entitySystem);
            int defIndex = WeaponInventory.ReadDefinitionIndex(mem, activeWeapon);
            string weaponName = defIndex > 0 ? WeaponCatalog.GetName(defIndex) : "none";

            if (!InputState.IsAttackHeld() || activeWeapon == IntPtr.Zero)
            {
                debug = debug with
                {
                    ActiveWeapon = weaponName,
                    WeaponsUpdated = updated,
                    Status = "Ready",
                };
                return;
            }

            ResetFireDelays(mem, activeWeapon);
            ClearBoltAction(mem, activeWeapon);

            if (WeaponCatalog.IsSemiAuto(defIndex))
                PulseAttack(mem);

            debug = new AllGunsAutoDebug
            {
                ActiveWeapon = weaponName,
                Shooting = true,
                WeaponsUpdated = updated,
                Status = WeaponCatalog.IsSemiAuto(defIndex) ? "Auto firing" : "Fire delays cleared",
            };
        }

        private static bool DisableBurstMode(GameMemory mem, IntPtr weapon)
        {
            if (Offsets.m_bBurstMode == 0 || mem.ReadBool(weapon, Offsets.m_bBurstMode))
            {
                mem.WriteInt(weapon, Offsets.m_bBurstMode, 0);
                return true;
            }

            return false;
        }

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

            if (Offsets.m_iBurstShotsRemaining != 0)
                mem.WriteInt(weapon, Offsets.m_iBurstShotsRemaining, 0);
        }

        private static void ClearBoltAction(GameMemory mem, IntPtr weapon)
        {
            if (Offsets.m_bNeedsBoltAction == 0)
                return;

            if (mem.ReadBool(weapon, Offsets.m_bNeedsBoltAction))
                mem.WriteInt(weapon, Offsets.m_bNeedsBoltAction, 0);
        }

        private static void PulseAttack(GameMemory mem)
        {
            if (Offsets.dwAttack == 0)
                return;

            mem.WriteInt(mem.Client, Offsets.dwAttack, AttackPress);
            mem.WriteInt(mem.Client, Offsets.dwAttack, AttackRelease);
        }
    }
}
