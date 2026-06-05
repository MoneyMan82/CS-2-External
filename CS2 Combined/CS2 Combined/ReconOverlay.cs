using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class ReconOverlay
    {
        private const float LabelSize = 14f;

        public static void Draw(
            ReconState state,
            bool edgeGlow,
            bool threatArrows,
            bool exposureMeter)
        {
            if (!state.Enabled || state.ThreatCount == 0)
                return;

            var draw = ImGui.GetBackgroundDrawList();
            var io = ImGui.GetIO();
            float w = io.DisplaySize.X;
            float h = io.DisplaySize.Y;
            var center = new Vector2(w * 0.5f, h * 0.5f);

            if (edgeGlow)
                DrawExposureFrame(draw, w, h, state.WorstBand, state.ExposureScore);

            if (exposureMeter)
                DrawStatusLabel(draw, w, state);

            if (threatArrows)
            {
                foreach (ReconThreat threat in state.Threats)
                    DrawThreatTick(draw, center, w, h, threat);
            }
        }

        private static void DrawExposureFrame(
            ImDrawListPtr draw,
            float w,
            float h,
            ExposureBand worst,
            float score)
        {
            Vector4 accent = worst switch
            {
                ExposureBand.Wide => UiTheme.TextDanger,
                ExposureBand.Peeking => UiTheme.TextWarning,
                _ => UiTheme.Accent,
            };

            float pulse = 0.04f + 0.02f * MathF.Sin(Environment.TickCount64 * 0.008f);
            float vignetteAlpha = Math.Clamp(score * 0.1f + pulse, 0.03f, 0.14f);
            uint vignette = ToU32(accent with { W = vignetteAlpha });

            draw.AddRectFilled(new Vector2(0f, 0f), new Vector2(w, h * 0.1f), vignette);
            draw.AddRectFilled(new Vector2(0f, h * 0.9f), new Vector2(w, h), vignette);
            draw.AddRectFilled(new Vector2(0f, 0f), new Vector2(w * 0.07f, h), vignette);
            draw.AddRectFilled(new Vector2(w * 0.93f, 0f), new Vector2(w, h), vignette);

            float borderAlpha = worst == ExposureBand.Wide ? 0.75f : 0.5f;
            uint border = ToU32(accent with { W = borderAlpha });
            draw.AddRect(new Vector2(8f, 8f), new Vector2(w - 8f, h - 8f), border, 10f, ImDrawFlags.None, 1.8f);

            DrawCornerBrackets(draw, new Vector2(20f, 20f), new Vector2(w - 20f, h - 20f), border);
        }

        private static void DrawStatusLabel(ImDrawListPtr draw, float w, ReconState state)
        {
            string title = state.WorstBand switch
            {
                ExposureBand.Wide => "EXPOSED",
                ExposureBand.Peeking => "PEEKING",
                _ => "CLEAR",
            };

            string detail = state.ThreatCount == 1 ? "1 threat" : $"{state.ThreatCount} threats";
            uint titleColor = state.WorstBand switch
            {
                ExposureBand.Wide => ToU32(UiTheme.TextDanger),
                ExposureBand.Peeking => ToU32(UiTheme.TextWarning),
                _ => ToU32(UiTheme.TextSuccess),
            };

            float x = w * 0.5f - 42f;
            float y = 52f;
            DrawLabel(draw, new Vector2(x, y), title, titleColor, LabelSize + 1f);
            DrawLabel(draw, new Vector2(x, y + 18f), detail, ToU32(UiTheme.TextMuted), LabelSize - 1f);
        }

        private static void DrawThreatTick(
            ImDrawListPtr draw,
            Vector2 center,
            float w,
            float h,
            ReconThreat threat)
        {
            Vector2 dir = threat.ScreenPos - center;
            if (dir.LengthSquared() < 1f)
                dir = new Vector2(0f, -1f);
            else
                dir = Vector2.Normalize(dir);

            float margin = 32f;
            float halfW = w * 0.5f - margin;
            float halfH = h * 0.5f - margin;
            float scale = MathF.Min(
                MathF.Abs(halfW / MathF.Max(MathF.Abs(dir.X), 0.001f)),
                MathF.Abs(halfH / MathF.Max(MathF.Abs(dir.Y), 0.001f)));
            var edge = center + dir * scale;

            Vector4 color = threat.Band switch
            {
                ExposureBand.Wide => UiTheme.TextDanger,
                ExposureBand.Peeking => UiTheme.TextWarning,
                _ => UiTheme.TextMuted,
            };

            uint line = ToU32(color with { W = 0.85f });
            uint dot = ToU32(color);
            var inner = edge - dir * 16f;

            draw.AddLine(inner, edge, line, 1.4f);
            draw.AddCircleFilled(edge, 2.5f, dot);
        }

        private static void DrawLabel(ImDrawListPtr draw, Vector2 pos, string text, uint color, float size)
        {
            if (string.IsNullOrEmpty(text))
                return;

            uint shadow = ToU32(new Vector4(0f, 0f, 0f, 0.75f));
            draw.AddText(ImGui.GetFont(), size, pos + new Vector2(1f, 1f), shadow, text);
            draw.AddText(ImGui.GetFont(), size, pos, color, text);
        }

        private static void DrawCornerBrackets(ImDrawListPtr draw, Vector2 min, Vector2 max, uint color)
        {
            float len = 14f;
            draw.AddLine(min, min + new Vector2(len, 0f), color, 1.6f);
            draw.AddLine(min, min + new Vector2(0f, len), color, 1.6f);
            draw.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X - len, min.Y), color, 1.6f);
            draw.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X, min.Y + len), color, 1.6f);
            draw.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X + len, max.Y), color, 1.6f);
            draw.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X, max.Y - len), color, 1.6f);
            draw.AddLine(max, max - new Vector2(len, 0f), color, 1.6f);
            draw.AddLine(max, max - new Vector2(0f, len), color, 1.6f);
        }

        private static uint ToU32(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);
    }
}
