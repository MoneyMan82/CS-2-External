using System.Numerics;

namespace External_Aimbot
{
    internal static class EntityList
    {
        public const int EntitySlotStride = 0x70;
        public const int LegacyEntitySlotStride = 0x78;
        public const int ControllerChunkStride = 0x78;

        public static IntPtr GetListEntry(GameMemory mem, IntPtr entitySystem, int index) =>
            mem.ReadPtr(entitySystem, 0x8 * ((index & 0x7FFF) >> 9) + 0x10);

        public static IntPtr GetEntityFromIndex(GameMemory mem, IntPtr entitySystem, int index)
        {
            IntPtr listEntry = GetListEntry(mem, entitySystem, index);
            if (listEntry == IntPtr.Zero)
                return IntPtr.Zero;

            return ReadEntityAtSlot(mem, listEntry, index & 0x1FF);
        }

        public static IntPtr ResolveHandle(GameMemory mem, IntPtr entitySystem, int handle)
        {
            if (entitySystem == IntPtr.Zero || handle == 0)
                return IntPtr.Zero;

            IntPtr listEntry = GetListEntry(mem, entitySystem, handle);
            if (listEntry == IntPtr.Zero)
                return IntPtr.Zero;

            return ReadPlayerPawnAtSlot(mem, listEntry, handle & 0x1FF);
        }

        public static IntPtr ResolveWeaponHandle(GameMemory mem, IntPtr entitySystem, int handle)
        {
            if (entitySystem == IntPtr.Zero || handle == 0)
                return IntPtr.Zero;

            IntPtr listEntry = GetListEntry(mem, entitySystem, handle);
            if (listEntry != IntPtr.Zero)
            {
                IntPtr entity = ReadEntityAtSlot(mem, listEntry, handle & 0x1FF);
                if (IsValidUserAddress(entity))
                    return entity;
            }

            return ResolveEntityIndex(mem, entitySystem, handle & 0x7FFF);
        }

        public static IntPtr ResolveEntityIndex(GameMemory mem, IntPtr entitySystem, int index)
        {
            if (entitySystem == IntPtr.Zero || index <= 0)
                return IntPtr.Zero;

            foreach (int stride in new[] { EntitySlotStride, LegacyEntitySlotStride })
            {
                foreach (int idx in new[] { index & 0x7FFF, index })
                {
                    IntPtr listEntry = mem.ReadPtr(entitySystem, 0x8 * (idx >> 9) + 0x10);
                    if (!IsValidUserAddress(listEntry))
                        continue;

                    IntPtr entity = mem.ReadPtr(listEntry, stride * (idx & 0x1FF));
                    if (IsValidUserAddress(entity))
                        return entity;
                }
            }

            return IntPtr.Zero;
        }

        public static IntPtr ReadControllerAtSlot(GameMemory mem, IntPtr listEntry, int slot)
        {
            IntPtr controller = mem.ReadPtr(listEntry, ControllerChunkStride * slot);
            if (IsValidUserAddress(controller))
                return controller;

            controller = mem.ReadPtr(listEntry, EntitySlotStride * slot);
            return IsValidUserAddress(controller) ? controller : IntPtr.Zero;
        }

        private static IntPtr ReadPlayerPawnAtSlot(GameMemory mem, IntPtr listEntry, int slot)
        {
            foreach (int stride in new[] { EntitySlotStride, LegacyEntitySlotStride })
            {
                IntPtr pawn = mem.ReadPtr(listEntry, stride * slot);
                if (!IsValidUserAddress(pawn))
                    continue;

                int health = mem.ReadInt(pawn, Offsets.m_iHealth);
                if (health is >= 1 and <= 100)
                    return pawn;
            }

            return IntPtr.Zero;
        }

        private static IntPtr ReadEntityAtSlot(GameMemory mem, IntPtr listEntry, int slot)
        {
            foreach (int stride in new[] { EntitySlotStride, LegacyEntitySlotStride })
            {
                IntPtr entity = mem.ReadPtr(listEntry, stride * slot);
                if (IsValidUserAddress(entity))
                    return entity;
            }

            return IntPtr.Zero;
        }

        public static bool LooksLikePlayerPawn(GameMemory mem, IntPtr pawn, IntPtr controller)
        {
            int health = mem.ReadInt(pawn, Offsets.m_iHealth);
            if (health is < 1 or > 100 && controller != IntPtr.Zero)
                health = mem.ReadInt(controller, Offsets.m_iPawnHealth);

            if (health is < 1 or > 100)
                return false;

            Vector3 origin = mem.ReadVec(pawn, Offsets.m_vOldOrigin);
            if (origin.X != 0f || origin.Y != 0f || origin.Z != 0f)
                return true;

            return controller != IntPtr.Zero;
        }

        public static bool LooksLikePlayerPawn(GameMemory mem, IntPtr pawn) =>
            LooksLikePlayerPawn(mem, pawn, IntPtr.Zero);

        private static bool IsValidUserAddress(IntPtr address)
        {
            long value = address.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }
    }
}
