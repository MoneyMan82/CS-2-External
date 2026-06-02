using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace External_Aimbot
{
    internal class Calculate
    {
        public static Vector2 CalculateAngles(Vector3 from, Vector3 to)
        {
            float yaw;
            float pitch;

            // calculate yaw
            float deltaX = to.X - from.X;
            float deltaY = to.Y - from.Y;
            yaw = (float)(Math.Atan2(deltaY, deltaX) * 180 / Math.PI); // convert to degrees = * 180 / PI

            // calculate pitch

            float deltaZ = to.Z - from.Z;
            double distance = Math.Sqrt(Math.Pow(deltaX,2) + Math.Pow(deltaY,2));
            pitch = -(float)(Math.Atan2(deltaZ, distance) * 180 / Math.PI);

            return new Vector2(yaw, pitch);
        }

        public static Vector3 DirectionFromAngles(float pitch, float yaw)
        {
            float yawRad = yaw * MathF.PI / 180f;
            float pitchRad = pitch * MathF.PI / 180f;

            float cosPitch = MathF.Cos(pitchRad);
            return Vector3.Normalize(new Vector3(
                cosPitch * MathF.Cos(yawRad),
                cosPitch * MathF.Sin(yawRad),
                -MathF.Sin(pitchRad)
            ));
        }

        public static float GetFovDistance(Vector2 current, Vector2 target)
        {
            float deltaYaw = target.X - current.X;
            while (deltaYaw > 180) deltaYaw -= 360;
            while (deltaYaw < -180) deltaYaw += 360;

            float deltaPitch = target.Y - current.Y;
            return (float)Math.Sqrt(deltaPitch * deltaPitch + deltaYaw * deltaYaw);
        }

        public static float GetFovCircleRadius(
            float aimFovDegrees,
            float screenWidth,
            float screenHeight,
            float gameFovDegrees = 90f)
        {
            if (aimFovDegrees <= 0f || screenHeight <= 0f || screenWidth <= 0f)
                return 0f;

            aimFovDegrees = Math.Clamp(aimFovDegrees, 0.1f, 89f);
            gameFovDegrees = Math.Clamp(gameFovDegrees, 60f, 140f);

            float aimRad = aimFovDegrees * MathF.PI / 180f;
            float aspect = screenWidth / screenHeight;
            float verticalGameFovRad = 2f * MathF.Atan(MathF.Tan(gameFovDegrees * MathF.PI / 360f) / aspect);

            return screenHeight * 0.5f * MathF.Tan(aimRad) / MathF.Tan(verticalGameFovRad * 0.5f);
        }

        public static Vector2 SmoothAngles(Vector2 current, Vector2 target, float smoothness)
        {
            if (smoothness <= 1f)
                return target;

            float factor = 1f / smoothness;

            float deltaYaw = target.X - current.X;
            while (deltaYaw > 180) deltaYaw -= 360;
            while (deltaYaw < -180) deltaYaw += 360;

            float deltaPitch = target.Y - current.Y;

            return new Vector2(
                current.X + deltaYaw * factor,
                current.Y + deltaPitch * factor
            );
        }

        public static Vector2 NormalizeAngles(Vector2 angles)
        {
            float yaw = angles.X;
            float pitch = angles.Y;

            while (yaw > 180f) yaw -= 360f;
            while (yaw < -180f) yaw += 360f;

            pitch = Math.Clamp(pitch, -89f, 89f);

            return new Vector2(yaw, pitch);
        }
    }
}
