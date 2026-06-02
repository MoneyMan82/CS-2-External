namespace External_Aimbot
{
    internal static class WeaponInventory
    {
        private const int MaxWeapons = 64;

        public static IEnumerable<IntPtr> EnumerateWeapons(GameMemory mem, IntPtr pawn, IntPtr entitySystem)
        {
            var seen = new HashSet<IntPtr>();
            IntPtr weaponServices = mem.ReadPtr(pawn, Offsets.m_pWeaponServices);
            if (weaponServices == IntPtr.Zero)
                yield break;

            foreach (int handle in ReadWeaponHandles(mem, weaponServices))
            {
                IntPtr weapon = EntityList.ResolveHandle(mem, entitySystem, handle);
                if (weapon != IntPtr.Zero && seen.Add(weapon))
                    yield return weapon;
            }
        }

        public static IntPtr GetActiveWeapon(GameMemory mem, IntPtr pawn, IntPtr entitySystem)
        {
            IntPtr weaponServices = mem.ReadPtr(pawn, Offsets.m_pWeaponServices);
            if (weaponServices == IntPtr.Zero)
                return IntPtr.Zero;

            int handle = mem.ReadInt(weaponServices, Offsets.m_hActiveWeapon);
            return EntityList.ResolveHandle(mem, entitySystem, handle);
        }

        public static int ReadDefinitionIndex(GameMemory mem, IntPtr weapon)
        {
            if (weapon == IntPtr.Zero)
                return 0;

            return mem.ReadInt(
                weapon,
                Offsets.m_AttributeManager + Offsets.m_Item + Offsets.m_iItemDefinitionIndex);
        }

        private static IEnumerable<int> ReadWeaponHandles(GameMemory mem, IntPtr weaponServices)
        {
            var handles = new HashSet<int>();

            void AddHandle(int handle)
            {
                if (handle > 0)
                    handles.Add(handle);
            }

            int vectorSize = mem.ReadInt(weaponServices, Offsets.m_hMyWeapons + 0x8);
            IntPtr vectorData = mem.ReadPtr(weaponServices, Offsets.m_hMyWeapons + 0x10);
            if (vectorSize > 0 && vectorSize <= MaxWeapons && vectorData != IntPtr.Zero)
            {
                for (int i = 0; i < vectorSize; i++)
                    AddHandle(mem.ReadInt(vectorData, i * 4));
            }

            vectorSize = mem.ReadInt(weaponServices, Offsets.m_hMyWeapons);
            vectorData = mem.ReadPtr(weaponServices, Offsets.m_hMyWeapons + 0x8);
            if (vectorSize > 0 && vectorSize <= MaxWeapons && vectorData != IntPtr.Zero)
            {
                for (int i = 0; i < vectorSize; i++)
                    AddHandle(mem.ReadInt(vectorData, i * 4));
            }

            for (int i = 0; i < MaxWeapons; i++)
                AddHandle(mem.ReadInt(weaponServices, Offsets.m_hMyWeapons + i * 4));

            AddHandle(mem.ReadInt(weaponServices, Offsets.m_hActiveWeapon));

            return handles;
        }
    }
}
