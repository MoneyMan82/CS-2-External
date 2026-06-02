using System.Numerics;

namespace External_Aimbot
{
    internal static class ViewMatrix
    {
        public static float[] Read(GameMemory mem) =>
            mem.ReadMatrix(mem.Client + Offsets.dwViewMatrix);

        public static bool WorldToScreen(
            Vector3 world,
            float[] matrix,
            float screenWidth,
            float screenHeight,
            out Vector2 screen)
        {
            screen = Vector2.Zero;

            if (matrix.Length < 16 || screenWidth <= 0f || screenHeight <= 0f)
                return false;

            float w = matrix[12] * world.X + matrix[13] * world.Y + matrix[14] * world.Z + matrix[15];
            if (w < 0.001f)
                return false;

            float x = matrix[0] * world.X + matrix[1] * world.Y + matrix[2] * world.Z + matrix[3];
            float y = matrix[4] * world.X + matrix[5] * world.Y + matrix[6] * world.Z + matrix[7];

            screen = new Vector2(
                (screenWidth * 0.5f) * (1f + x / w),
                (screenHeight * 0.5f) * (1f - y / w)
            );

            return screen.X >= 0f && screen.X <= screenWidth && screen.Y >= 0f && screen.Y <= screenHeight;
        }

        public static bool TryWorldToScreen(
            Vector3 world,
            float[] matrix,
            float screenWidth,
            float screenHeight,
            out Vector2 screen)
        {
            screen = Vector2.Zero;

            if (matrix.Length < 16 || screenWidth <= 0f || screenHeight <= 0f)
                return false;

            float w = matrix[12] * world.X + matrix[13] * world.Y + matrix[14] * world.Z + matrix[15];
            if (w < 0.001f)
                return false;

            float x = matrix[0] * world.X + matrix[1] * world.Y + matrix[2] * world.Z + matrix[3];
            float y = matrix[4] * world.X + matrix[5] * world.Y + matrix[6] * world.Z + matrix[7];

            screen = new Vector2(
                (screenWidth * 0.5f) * (1f + x / w),
                (screenHeight * 0.5f) * (1f - y / w)
            );

            return true;
        }
    }
}
