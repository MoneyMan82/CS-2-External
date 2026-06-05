using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class OverlayLayout
    {
        public static Vector2 GetPivot(OverlayCorner corner) =>
            corner switch
            {
                OverlayCorner.TopRight => new Vector2(1f, 0f),
                OverlayCorner.BottomLeft => new Vector2(0f, 1f),
                OverlayCorner.BottomRight => new Vector2(1f, 1f),
                _ => Vector2.Zero,
            };

        public static Vector2 GetScreenCorner(Vector2 displaySize, OverlayCorner corner, Vector2 margin) =>
            corner switch
            {
                OverlayCorner.TopRight => new Vector2(displaySize.X - margin.X, margin.Y),
                OverlayCorner.BottomLeft => new Vector2(margin.X, displaySize.Y - margin.Y),
                OverlayCorner.BottomRight => new Vector2(displaySize.X - margin.X, displaySize.Y - margin.Y),
                _ => margin,
            };

        public static void AnchorWindow(OverlayCorner corner, Vector2 margin)
        {
            Vector2 display = ImGui.GetIO().DisplaySize;
            ImGui.SetNextWindowPos(GetScreenCorner(display, corner, margin), ImGuiCond.Always, GetPivot(corner));
        }

        public static bool CornerCombo(string id, ref OverlayCorner corner)
        {
            bool changed = false;
            string label = FormatCorner(corner);
            if (ImGui.BeginCombo(id, label))
            {
                foreach (OverlayCorner value in Enum.GetValues<OverlayCorner>())
                {
                    if (ImGui.Selectable(FormatCorner(value), corner == value))
                    {
                        corner = value;
                        changed = true;
                    }
                }

                ImGui.EndCombo();
            }

            return changed;
        }

        private static string FormatCorner(OverlayCorner corner) =>
            corner switch
            {
                OverlayCorner.TopLeft => "Top left",
                OverlayCorner.TopRight => "Top right",
                OverlayCorner.BottomLeft => "Bottom left",
                OverlayCorner.BottomRight => "Bottom right",
                _ => corner.ToString(),
            };
    }
}
