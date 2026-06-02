using System.Numerics;
using ImGuiNET;

namespace External_Aimbot
{
    public readonly struct RadarBlip
    {
        public float NormalizedX { get; init; }
        public float NormalizedY { get; init; }
        public bool IsEnemy { get; init; }
        public bool IsVisible { get; init; }
    }

    internal static class RadarOverlay
    {
        public static List<RadarBlip> BuildBlips(
            Entity localPlayer,
            IReadOnlyList<Entity> entities,
            float viewYawDegrees,
            float range)
        {
            var blips = new List<RadarBlip>(entities.Count);
            float yawRad = viewYawDegrees * MathF.PI / 180f;
            float sin = MathF.Sin(yawRad);
            float cos = MathF.Cos(yawRad);
            float clampedRange = Math.Max(range, 500f);

            foreach (Entity entity in entities)
            {
                Vector3 delta = entity.origin - localPlayer.origin;
                float forward = delta.X * cos + delta.Y * sin;
                float right = -delta.X * sin + delta.Y * cos;

                blips.Add(new RadarBlip
                {
                    NormalizedX = Math.Clamp(right / clampedRange, -1f, 1f),
                    NormalizedY = Math.Clamp(-forward / clampedRange, -1f, 1f),
                    IsEnemy = entity.team != localPlayer.team,
                    IsVisible = entity.isVisible,
                });
            }

            return blips;
        }

        public static void Draw(
            IReadOnlyList<RadarBlip> blips,
            float size,
            float margin)
        {
            if (blips.Count == 0 && size <= 0f)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;

            float half = size * 0.5f;
            float centerX = displaySize.X - margin - half;
            float centerY = margin + half;

            uint bg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.05f, 0.75f));
            uint border = ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.35f, 0.35f, 0.95f));
            uint cross = ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.25f, 0.25f, 0.8f));
            uint localColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.85f, 1f, 1f));

            drawList.AddRectFilled(
                new Vector2(centerX - half, centerY - half),
                new Vector2(centerX + half, centerY + half),
                bg,
                4f);
            drawList.AddRect(
                new Vector2(centerX - half, centerY - half),
                new Vector2(centerX + half, centerY + half),
                border,
                4f,
                ImDrawFlags.None,
                1.5f);
            drawList.AddLine(
                new Vector2(centerX, centerY - half + 4f),
                new Vector2(centerX, centerY + half - 4f),
                cross,
                1f);
            drawList.AddLine(
                new Vector2(centerX - half + 4f, centerY),
                new Vector2(centerX + half - 4f, centerY),
                cross,
                1f);
            drawList.AddTriangleFilled(
                new Vector2(centerX, centerY - half + 8f),
                new Vector2(centerX - 5f, centerY - half + 16f),
                new Vector2(centerX + 5f, centerY - half + 16f),
                cross);
            drawList.AddCircleFilled(new Vector2(centerX, centerY), 3f, localColor);

            float plotRadius = half - 10f;
            foreach (RadarBlip blip in blips)
            {
                var pos = new Vector2(
                    centerX + blip.NormalizedX * plotRadius,
                    centerY + blip.NormalizedY * plotRadius);

                Vector4 colorVec = blip.IsEnemy
                    ? (blip.IsVisible
                        ? new Vector4(1f, 0.25f, 0.25f, 1f)
                        : new Vector4(1f, 0.55f, 0.2f, 1f))
                    : new Vector4(0.25f, 0.55f, 1f, 1f);

                drawList.AddCircleFilled(pos, 3.5f, ImGui.ColorConvertFloat4ToU32(colorVec));
            }
        }
    }
}
