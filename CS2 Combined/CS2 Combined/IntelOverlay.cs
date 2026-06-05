using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class IntelOverlay
    {
        public static void Draw(
            IntelState state,
            float size,
            float margin,
            bool showTrails,
            bool bottomLeft)
        {
            if (!state.Enabled || state.Blips.Length == 0 || size <= 0f)
                return;

            var draw = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            float half = size * 0.5f;

            float centerX = bottomLeft
                ? margin + half
                : displaySize.X - margin - half;
            float centerY = bottomLeft
                ? displaySize.Y - margin - half
                : margin + half;

            uint bg = ToU32(new Vector4(0.07f, 0.08f, 0.10f, 0.82f));
            uint border = ToU32(new Vector4(0.20f, 0.24f, 0.30f, 0.75f));
            uint cross = ToU32(new Vector4(0.25f, 0.28f, 0.34f, 0.55f));
            uint accent = UiTheme.AccentU32;
            uint muted = ToU32(UiTheme.TextMuted);

            var min = new Vector2(centerX - half, centerY - half);
            var max = new Vector2(centerX + half, centerY + half);

            draw.AddRectFilled(min, max, bg, 8f);
            draw.AddRect(min, max, border, 8f, ImDrawFlags.None, 1.2f);
            draw.AddLine(new Vector2(centerX, min.Y + 6f), new Vector2(centerX, max.Y - 6f), cross, 1f);
            draw.AddLine(new Vector2(min.X + 6f, centerY), new Vector2(max.X - 6f, centerY), cross, 1f);
            draw.AddTriangleFilled(
                new Vector2(centerX, min.Y + 8f),
                new Vector2(centerX - 4f, min.Y + 14f),
                new Vector2(centerX + 4f, min.Y + 14f),
                cross);
            draw.AddCircleFilled(new Vector2(centerX, centerY), 2.5f, accent);

            DrawLabel(draw, new Vector2(min.X + 6f, min.Y + 4f), "INTEL", accent, 11f);
            if (state.GhostCount > 0)
            {
                string ghosts = state.GhostCount == 1 ? "1 ghost" : $"{state.GhostCount} ghosts";
                DrawLabel(draw, new Vector2(max.X - 52f, min.Y + 4f), ghosts, muted, 10f);
            }

            float plotRadius = half - 16f;
            foreach (IntelGhostBlip blip in state.Blips)
            {
                var pos = new Vector2(
                    centerX + blip.NormalizedX * plotRadius,
                    centerY + blip.NormalizedY * plotRadius);

                float ageT = blip.DecaySeconds > 0f
                    ? Math.Clamp(blip.AgeSeconds / blip.DecaySeconds, 0f, 1f)
                    : 0f;
                float alpha = blip.IsLive ? 1f : Math.Clamp(1f - ageT * 0.85f, 0.2f, 0.9f);

                if (showTrails && (blip.TrailX != 0f || blip.TrailY != 0f))
                {
                    var trailEnd = pos + new Vector2(blip.TrailX, blip.TrailY) * plotRadius;
                    uint trailColor = ToU32(blip.IsLive
                        ? UiTheme.Accent with { W = 0.45f }
                        : UiTheme.TextWarning with { W = alpha * 0.5f });
                    draw.AddLine(pos, trailEnd, trailColor, 1f);
                }

                if (blip.IsLive)
                {
                    draw.AddCircleFilled(pos, 3.5f, ToU32(UiTheme.TextDanger with { W = alpha }));
                    draw.AddCircle(pos, 5f, accent, 12, 1f);
                }
                else
                {
                    uint ghost = ToU32(UiTheme.TextWarning with { W = alpha });
                    draw.AddCircle(pos, 4f, ghost, 12, 1.2f);
                    draw.AddCircleFilled(pos, 1.5f, ghost);
                }
            }
        }

        private static void DrawLabel(ImDrawListPtr draw, Vector2 pos, string text, uint color, float size)
        {
            uint shadow = ToU32(new Vector4(0f, 0f, 0f, 0.7f));
            draw.AddText(ImGui.GetFont(), size, pos + new Vector2(1f, 1f), shadow, text);
            draw.AddText(ImGui.GetFont(), size, pos, color, text);
        }

        private static uint ToU32(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);
    }
}
