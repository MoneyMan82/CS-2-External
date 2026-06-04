using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class EspRenderer
    {
        public static void Draw(
            IReadOnlyList<EspPlayerData> players,
            bool box,
            bool bones,
            bool name,
            bool health,
            bool weapon,
            bool distance,
            bool snaplines,
            bool headDot,
            bool armor,
            bool colorByVisibility)
        {
            if (players.Count == 0)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            var screenBottom = new Vector2(displaySize.X / 2f, displaySize.Y);

            foreach (EspPlayerData player in players)
            {
                if (!player.Valid)
                    continue;

                uint color = GetColor(player, colorByVisibility);
                uint outline = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.85f));

                if (box)
                    DrawBox(drawList, player, color, outline);

                if (bones)
                    DrawBones(drawList, player, color);

                if (snaplines)
                    drawList.AddLine(screenBottom, player.FeetScreen, color, 1f);

                if (headDot)
                    drawList.AddCircleFilled(player.HeadScreen, 3f, color);

                float centerX = (player.HeadScreen.X + player.FeetScreen.X) * 0.5f;
                float top = MathF.Min(player.HeadScreen.Y, player.FeetScreen.Y);
                float bottom = MathF.Max(player.HeadScreen.Y, player.FeetScreen.Y);
                float textY = top - 18f;

                if (weapon && !string.IsNullOrEmpty(player.WeaponName))
                {
                    DrawCenteredText(drawList, centerX, textY, player.WeaponName, color, outline);
                    textY -= 14f;
                }

                if (name && !string.IsNullOrEmpty(player.Name))
                {
                    DrawCenteredText(drawList, centerX, textY, player.Name, color, outline);
                    textY -= 14f;
                }

                if (health)
                {
                    string hpText = $"{player.Health} HP";
                    if (armor && player.Armor > 0)
                        hpText += $" | {player.Armor} AP";
                    DrawCenteredText(drawList, centerX, textY, hpText, color, outline);
                    DrawHealthBar(drawList, player);
                }

                if (distance)
                {
                    DrawCenteredText(
                        drawList,
                        centerX,
                        bottom + 4f,
                        $"{player.Distance * 0.0254f:0}m",
                        color,
                        outline);
                }
            }
        }

        private static uint GetColor(EspPlayerData player, bool colorByVisibility)
        {
            if (colorByVisibility)
            {
                return ImGui.ColorConvertFloat4ToU32(player.IsVisible
                    ? new Vector4(0.2f, 1f, 0.35f, 1f)
                    : new Vector4(1f, 0.35f, 0.35f, 1f));
            }

            return player.Team switch
            {
                2 => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.78f, 0.2f, 1f)),
                3 => ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.65f, 1f, 1f)),
                _ => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
            };
        }

        private static void DrawBox(ImDrawListPtr drawList, EspPlayerData player, uint color, uint outline)
        {
            float height = MathF.Abs(player.FeetScreen.Y - player.HeadScreen.Y);
            float width = Math.Clamp(height * 0.45f, 18f, 220f);
            float centerX = (player.HeadScreen.X + player.FeetScreen.X) * 0.5f;
            float top = MathF.Min(player.HeadScreen.Y, player.FeetScreen.Y);
            float bottom = MathF.Max(player.HeadScreen.Y, player.FeetScreen.Y);

            var min = new Vector2(centerX - width * 0.5f, top);
            var max = new Vector2(centerX + width * 0.5f, bottom);

            DrawCornerBox(drawList, min, max, color, outline);
        }

        private static void DrawCornerBox(ImDrawListPtr drawList, Vector2 min, Vector2 max, uint color, uint outline)
        {
            float w = max.X - min.X;
            float h = max.Y - min.Y;
            float corner = Math.Clamp(MathF.Min(w, h) * 0.22f, 6f, 24f);

            DrawLineOutline(drawList, min, new Vector2(min.X + corner, min.Y), color, outline);
            DrawLineOutline(drawList, min, new Vector2(min.X, min.Y + corner), color, outline);

            var topRight = new Vector2(max.X, min.Y);
            DrawLineOutline(drawList, topRight, new Vector2(max.X - corner, min.Y), color, outline);
            DrawLineOutline(drawList, topRight, new Vector2(max.X, min.Y + corner), color, outline);

            var bottomLeft = new Vector2(min.X, max.Y);
            DrawLineOutline(drawList, bottomLeft, new Vector2(min.X + corner, max.Y), color, outline);
            DrawLineOutline(drawList, bottomLeft, new Vector2(min.X, max.Y - corner), color, outline);

            DrawLineOutline(drawList, max, new Vector2(max.X - corner, max.Y), color, outline);
            DrawLineOutline(drawList, max, new Vector2(max.X, max.Y - corner), color, outline);
        }

        private static void DrawHealthBar(ImDrawListPtr drawList, EspPlayerData player)
        {
            float height = MathF.Abs(player.FeetScreen.Y - player.HeadScreen.Y);
            float width = Math.Clamp(height * 0.45f, 18f, 220f);
            float centerX = (player.HeadScreen.X + player.FeetScreen.X) * 0.5f;
            float top = MathF.Min(player.HeadScreen.Y, player.FeetScreen.Y);
            float bottom = MathF.Max(player.HeadScreen.Y, player.FeetScreen.Y);

            float barX = centerX - width * 0.5f - 6f;
            float hpPct = Math.Clamp(player.Health / 100f, 0f, 1f);
            float filledTop = bottom - height * hpPct;

            uint bg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.85f));
            uint hpColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f - hpPct, hpPct, 0.15f, 1f));

            drawList.AddRectFilled(new Vector2(barX - 2f, top), new Vector2(barX + 2f, bottom), bg);
            drawList.AddRectFilled(new Vector2(barX - 2f, filledTop), new Vector2(barX + 2f, bottom), hpColor);
        }

        private static void DrawBones(ImDrawListPtr drawList, EspPlayerData player, uint color)
        {
            foreach (EspBoneLine line in player.BoneLines)
                drawList.AddLine(line.From, line.To, color, 1.4f);
        }

        private static void DrawCenteredText(
            ImDrawListPtr drawList,
            float centerX,
            float y,
            string text,
            uint color,
            uint outline)
        {
            Vector2 size = ImGui.CalcTextSize(text);
            var pos = new Vector2(centerX - size.X * 0.5f, y);
            drawList.AddText(new Vector2(pos.X + 1f, pos.Y + 1f), outline, text);
            drawList.AddText(pos, color, text);
        }

        private static void DrawLineOutline(
            ImDrawListPtr drawList,
            Vector2 a,
            Vector2 b,
            uint color,
            uint outline)
        {
            drawList.AddLine(a, b, outline, 2.4f);
            drawList.AddLine(a, b, color, 1.4f);
        }
    }
}
