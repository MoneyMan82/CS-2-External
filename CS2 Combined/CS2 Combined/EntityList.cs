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

        public static IntPtr ResolveHandle(GameMemory mem, IntPtr entitySystem, int handle) =>
            ResolveEntityIndex(mem, entitySystem, handle);

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

        public static bool LooksLikePlayerController(GameMemory mem, IntPtr entity)
        {
            if (!IsValidUserAddress(entity))
                return false;

            int pawnHandle = mem.ReadInt(entity, Offsets.m_hPlayerPawn);
            return pawnHandle > 0;
        }

        public static bool LooksLikePlayerPawn(GameMemory mem, IntPtr pawn, IntPtr controller)
        {
            if (!IsValidUserAddress(pawn))
                return false;

            if (IsAlive(mem, pawn, controller))
                return PlayerValidation.HasUsableOrigin(mem, pawn, controller);

            return false;
        }

        public static bool LooksLikePlayerPawn(GameMemory mem, IntPtr pawn) =>
            LooksLikePlayerPawn(mem, pawn, IntPtr.Zero);

        private static bool IsAlive(GameMemory mem, IntPtr pawn, IntPtr controller)
        {
            if (Offsets.m_lifeState != 0 && mem.ReadByte(pawn + Offsets.m_lifeState) != 0)
                return false;

            int health = mem.ReadInt(pawn, Offsets.m_iHealth);
            if (health is >= 1 and <= 100)
                return true;

            if (controller != IntPtr.Zero)
            {
                health = mem.ReadInt(controller, Offsets.m_iPawnHealth);
                if (health is >= 1 and <= 100)
                    return true;
            }

            return Offsets.m_lifeState == 0 || mem.ReadByte(pawn + Offsets.m_lifeState) == 0;
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

        private static bool IsValidUserAddress(IntPtr address)
        {
            long value = address.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }
    }
}
