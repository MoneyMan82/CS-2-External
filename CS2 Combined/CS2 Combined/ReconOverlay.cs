using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class ReconOverlay
    {
        public static void Draw(
            ReconState state,
            bool edgeGlow,
            bool threatArrows,
            bool exposureMeter)
        {
            if (!state.Enabled || state.ThreatCount == 0)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            var center = new Vector2(displaySize.X * 0.5f, displaySize.Y * 0.5f);

            if (edgeGlow)
                DrawEdgeGlow(drawList, displaySize, state.ExposureScore, state.WorstBand);

            if (exposureMeter)
                DrawExposureMeter(drawList, center, state);

            if (threatArrows)
            {
                foreach (ReconThreat threat in state.Threats)
                    DrawThreatMarker(drawList, center, displaySize, threat);
            }
        }

        private static void DrawEdgeGlow(
            ImDrawListPtr drawList,
            Vector2 displaySize,
            float score,
            ExposureBand worst)
        {
            float alpha = Math.Clamp(score * 0.55f, 0.08f, 0.45f);
            if (worst == ExposureBand.Wide)
                alpha = Math.Clamp(alpha + 0.12f, 0.15f, 0.55f);

            uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.18f, 0.22f, alpha));
            float thickness = Math.Clamp(displaySize.X * 0.035f, 18f, 72f);

            drawList.AddRectFilled(new Vector2(0f, 0f), new Vector2(displaySize.X, thickness), color);
            drawList.AddRectFilled(
                new Vector2(0f, displaySize.Y - thickness),
                new Vector2(displaySize.X, displaySize.Y),
                color);
            drawList.AddRectFilled(new Vector2(0f, 0f), new Vector2(thickness, displaySize.Y), color);
            drawList.AddRectFilled(
                new Vector2(displaySize.X - thickness, 0f),
                new Vector2(displaySize.X, displaySize.Y),
                color);
        }

        private static void DrawExposureMeter(ImDrawListPtr drawList, Vector2 center, ReconState state)
        {
            string label = state.WorstBand switch
            {
                ExposureBand.Wide => "WIDE — they see your body",
                ExposureBand.Peeking => "PEEK — head exposed",
                _ => "HIDDEN",
            };

            if (state.WorstBand == ExposureBand.Hidden)
                return;

            Vector4 textColor = state.WorstBand switch
            {
                ExposureBand.Wide => new Vector4(1f, 0.35f, 0.35f, 0.95f),
                ExposureBand.Peeking => new Vector4(1f, 0.75f, 0.25f, 0.95f),
                _ => new Vector4(0.7f, 0.9f, 0.7f, 0.9f),
            };

            string detail = state.ThreatCount == 1
                ? "1 player can see you"
                : $"{state.ThreatCount} players can see you";

            float y = 36f;
            Vector2 labelSize = ImGui.CalcTextSize(label);
            Vector2 detailSize = ImGui.CalcTextSize(detail);
            float padX = 10f;
            float padY = 6f;
            float boxW = MathF.Max(labelSize.X, detailSize.X) + padX * 2f;
            float boxH = labelSize.Y + detailSize.Y + padY * 3f;
            var boxMin = new Vector2(center.X - boxW * 0.5f, y);
            var boxMax = new Vector2(center.X + boxW * 0.5f, y + boxH);

            uint bg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.08f, 0.72f));
            uint border = ImGui.ColorConvertFloat4ToU32(textColor with { W = 0.85f });
            drawList.AddRectFilled(boxMin, boxMax, bg, 4f);
            drawList.AddRect(boxMin, boxMax, border, 4f, ImDrawFlags.None, 1.2f);
            drawList.AddText(new Vector2(boxMin.X + padX, boxMin.Y + padY), ImGui.ColorConvertFloat4ToU32(textColor), label);
            drawList.AddText(
                new Vector2(boxMin.X + padX, boxMin.Y + padY + labelSize.Y + padY * 0.5f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.85f, 0.9f, 0.85f)),
                detail);
        }

        private static void DrawThreatMarker(
            ImDrawListPtr drawList,
            Vector2 center,
            Vector2 displaySize,
            ReconThreat threat)
        {
            Vector2 dir = threat.ScreenPos - center;
            if (dir.LengthSquared() < 1f)
                dir = new Vector2(0f, -1f);
            else
                dir = Vector2.Normalize(dir);

            float margin = 28f;
            float halfW = displaySize.X * 0.5f - margin;
            float halfH = displaySize.Y * 0.5f - margin;
            float scale = MathF.Min(
                MathF.Abs(halfW / MathF.Max(MathF.Abs(dir.X), 0.001f)),
                MathF.Abs(halfH / MathF.Max(MathF.Abs(dir.Y), 0.001f)));
            var edgePos = center + dir * scale;

            Vector4 color = threat.Band switch
            {
                ExposureBand.Wide => new Vector4(1f, 0.25f, 0.25f, 0.95f),
                ExposureBand.Peeking => new Vector4(1f, 0.7f, 0.15f, 0.95f),
                _ => new Vector4(0.5f, 0.5f, 0.5f, 0.7f),
            };

            uint fill = ImGui.ColorConvertFloat4ToU32(color);
            drawList.AddCircleFilled(edgePos, 7f, fill);
            drawList.AddCircle(edgePos, 10f, ImGui.ColorConvertFloat4ToU32(color with { W = 0.5f }), 12, 1.5f);

            Vector2 tip = edgePos + dir * 10f;
            Vector2 left = edgePos + new Vector2(-dir.Y, dir.X) * 5f;
            Vector2 right = edgePos + new Vector2(dir.Y, -dir.X) * 5f;
            drawList.AddTriangleFilled(tip, left, right, fill);
        }
    }
}
