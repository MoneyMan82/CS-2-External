using System.Numerics;

namespace External_Aimbot
{
    internal static class BoneReader
    {
        private const int BoneStride = 32;

        // CS2 player skeleton (cs2-dumper community bone indices)
        public static readonly (int From, int To)[] Connections =
        [
            (7, 6), (6, 4), (4, 2), (2, 1),
            (6, 9), (9, 10), (10, 11),
            (6, 13), (13, 14), (14, 15),
            (1, 17), (17, 18), (18, 19),
            (1, 20), (20, 21), (21, 22),
        ];

        public static bool TryGetBonePosition(GameMemory mem, IntPtr pawn, int boneId, out Vector3 position)
        {
            position = Vector3.Zero;

            if (!TryGetBoneArray(mem, pawn, out IntPtr boneArray))
                return false;

            return TryReadBoneFromArray(mem, boneArray, boneId, out position);
        }

        private static bool TryGetBoneArray(GameMemory mem, IntPtr pawn, out IntPtr boneArray)
        {
            boneArray = IntPtr.Zero;

            IntPtr sceneNode = mem.ReadPtr(pawn, Offsets.m_pGameSceneNode);
            if (sceneNode == IntPtr.Zero)
                return false;

            boneArray = mem.ReadPtr(sceneNode, Offsets.m_modelState + Offsets.m_boneArray);
            if (IsValidPointer(boneArray))
                return true;

            boneArray = mem.ReadPtr(sceneNode, Offsets.m_boneArrayLegacy);
            return IsValidPointer(boneArray);
        }

        private static bool TryReadBoneFromArray(GameMemory mem, IntPtr boneArray, int boneId, out Vector3 position)
        {
            int baseOffset = boneId * BoneStride;

            position = mem.ReadVec(boneArray, baseOffset);
            if (IsValidBonePosition(position))
                return true;

            position = mem.ReadVec(boneArray, baseOffset + 0x18);
            if (IsValidBonePosition(position))
                return true;

            position = mem.ReadVec(boneArray, boneId * 48 + 0x30);
            return IsValidBonePosition(position);
        }

        private static bool IsValidPointer(IntPtr ptr)
        {
            long value = ptr.ToInt64();
            return value > 0x10000 && value < 0x7FFFFFFFFFFF;
        }

        private static bool IsValidBonePosition(Vector3 pos)
        {
            if (pos == Vector3.Zero)
                return false;

            if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z))
                return false;

            if (float.IsInfinity(pos.X) || float.IsInfinity(pos.Y) || float.IsInfinity(pos.Z))
                return false;

            return MathF.Abs(pos.X) < 50000f &&
                   MathF.Abs(pos.Y) < 50000f &&
                   MathF.Abs(pos.Z) < 50000f;
        }
    }
}
