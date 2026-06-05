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
                IntPtr weapon = EntityList.ResolveWeaponHandle(mem, entitySystem, handle);
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
            if (handle <= 0)
                return IntPtr.Zero;

            return EntityList.ResolveWeaponHandle(mem, entitySystem, handle);
        }

        public static int ReadDefinitionIndex(GameMemory mem, IntPtr weapon)
        {
            if (weapon == IntPtr.Zero)
                return 0;

            int raw = mem.ReadInt(
                weapon,
                Offsets.m_AttributeManager + Offsets.m_Item + Offsets.m_iItemDefinitionIndex);

            return raw & 0xFFFF;
        }

        private static IEnumerable<int> ReadWeaponHandles(GameMemory mem, IntPtr weaponServices)
        {
            var handles = new HashSet<int>();

            void AddHandle(int handle)
            {
                if (handle > 0 && handle != unchecked((int)0xFFFFFFFF))
                    handles.Add(handle);
            }

            AddHandle(mem.ReadInt(weaponServices, Offsets.m_hActiveWeapon));

            TryReadVectorHandles(mem, weaponServices, Offsets.m_hMyWeapons, AddHandle);
            TryReadVectorHandles(mem, weaponServices, Offsets.m_hMyWeapons + 0x10, AddHandle);

            return handles;
        }

        private static void TryReadVectorHandles(
            GameMemory mem,
            IntPtr weaponServices,
            int vectorOffset,
            Action<int> addHandle)
        {
            int count = mem.ReadInt(weaponServices, vectorOffset);
            IntPtr data = mem.ReadPtr(weaponServices, vectorOffset + 0x8);
            if (count <= 0 || count > MaxWeapons || data == IntPtr.Zero)
                return;

            for (int i = 0; i < count; i++)
                addHandle(mem.ReadInt(data, i * 4));
        }
    }
}
