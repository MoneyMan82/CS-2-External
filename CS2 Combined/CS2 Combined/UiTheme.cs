using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class UiTheme
    {
        private static bool _applied;

        public static readonly Vector4 Accent = new(0.18f, 0.83f, 0.72f, 1f);
        public static readonly Vector4 AccentSoft = new(0.18f, 0.83f, 0.72f, 0.18f);
        public static readonly Vector4 TextPrimary = new(0.93f, 0.95f, 0.97f, 1f);
        public static readonly Vector4 TextMuted = new(0.55f, 0.60f, 0.67f, 1f);
        public static readonly Vector4 TextSuccess = new(0.35f, 0.95f, 0.55f, 1f);
        public static readonly Vector4 TextWarning = new(1f, 0.78f, 0.28f, 1f);
        public static readonly Vector4 TextDanger = new(1f, 0.42f, 0.42f, 1f);
        public static readonly Vector4 TextInfo = new(0.45f, 0.78f, 1f, 1f);

        public static uint AccentU32 => ImGui.ColorConvertFloat4ToU32(Accent);

        public static void Apply()
        {
            if (_applied)
                return;

            _applied = true;

            ImGuiStylePtr style = ImGui.GetStyle();
            style.WindowRounding = 12f;
            style.ChildRounding = 10f;
            style.FrameRounding = 8f;
            style.PopupRounding = 8f;
            style.ScrollbarRounding = 8f;
            style.GrabRounding = 6f;
            style.TabRounding = 8f;
            style.WindowBorderSize = 1f;
            style.FrameBorderSize = 0f;
            style.PopupBorderSize = 1f;
            style.WindowPadding = new Vector2(16f, 14f);
            style.FramePadding = new Vector2(10f, 6f);
            style.ItemSpacing = new Vector2(10f, 8f);
            style.ItemInnerSpacing = new Vector2(8f, 6f);
            style.ScrollbarSize = 12f;
            style.GrabMinSize = 12f;
            style.TabBarBorderSize = 0f;
            style.TabBorderSize = 0f;
            style.AntiAliasedLines = true;
            style.AntiAliasedFill = true;
            style.AntiAliasedLinesUseTex = true;
            style.CircleTessellationMaxError = 0.01f;

            style.Colors[(int)ImGuiCol.Text] = TextPrimary;
            style.Colors[(int)ImGuiCol.TextDisabled] = TextMuted;
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.07f, 0.08f, 0.10f, 0.97f);
            style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.09f, 0.10f, 0.13f, 1f);
            style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.09f, 0.10f, 0.13f, 0.98f);
            style.Colors[(int)ImGuiCol.Border] = new Vector4(0.20f, 0.24f, 0.30f, 0.55f);
            style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0f, 0f, 0f, 0f);
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.13f, 0.15f, 0.19f, 1f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.17f, 0.20f, 0.25f, 1f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.20f, 0.24f, 0.30f, 1f);
            style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.07f, 0.08f, 0.10f, 1f);
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.08f, 0.09f, 0.12f, 1f);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.07f, 0.08f, 0.10f, 0.75f);
            style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.09f, 0.10f, 0.13f, 1f);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.07f, 0.08f, 0.10f, 0.6f);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.22f, 0.26f, 0.32f, 1f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.28f, 0.33f, 0.40f, 1f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = Accent;
            style.Colors[(int)ImGuiCol.CheckMark] = Accent;
            style.Colors[(int)ImGuiCol.SliderGrab] = Accent;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.22f, 0.92f, 0.80f, 1f);
            style.Colors[(int)ImGuiCol.Button] = new Vector4(0.15f, 0.18f, 0.22f, 1f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.20f, 0.24f, 0.30f, 1f);
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.14f, 0.40f, 0.36f, 1f);
            style.Colors[(int)ImGuiCol.Header] = AccentSoft;
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.18f, 0.83f, 0.72f, 0.28f);
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.18f, 0.83f, 0.72f, 0.40f);
            style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.20f, 0.24f, 0.30f, 0.65f);
            style.Colors[(int)ImGuiCol.SeparatorHovered] = Accent;
            style.Colors[(int)ImGuiCol.SeparatorActive] = Accent;
            style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.18f, 0.83f, 0.72f, 0.20f);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.18f, 0.83f, 0.72f, 0.45f);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = Accent;
            style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.11f, 0.13f, 0.17f, 1f);
            style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.18f, 0.83f, 0.72f, 0.25f);
            style.Colors[(int)ImGuiCol.TabSelected] = new Vector4(0.14f, 0.36f, 0.32f, 1f);
            style.Colors[(int)ImGuiCol.TabSelectedOverline] = Accent;
            style.Colors[(int)ImGuiCol.PlotLines] = Accent;
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = TextSuccess;
            style.Colors[(int)ImGuiCol.PlotHistogram] = Accent;
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = TextSuccess;
            style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.18f, 0.83f, 0.72f, 0.35f);
        }

        public static void ApplyCompact()
        {
            Apply();
            ImGuiStylePtr style = ImGui.GetStyle();
            style.WindowPadding = new Vector2(10f, 8f);
            style.FramePadding = new Vector2(6f, 4f);
            style.ItemSpacing = new Vector2(6f, 4f);
            style.ItemInnerSpacing = new Vector2(4f, 3f);
            style.ScrollbarSize = 10f;
        }

        public static void DrawMenuHeader()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            Vector2 pos = ImGui.GetCursorScreenPos();
            float width = ImGui.GetContentRegionAvail().X;

            ImGui.GetWindowDrawList().AddRectFilled(
                pos,
                pos + new Vector2(width, 2f),
                AccentU32,
                2f);

            ImGui.Dummy(new Vector2(0f, 6f));

            ImGui.PushStyleColor(ImGuiCol.Text, Accent);
            ImGui.SetWindowFontScale(1.02f);
            ImGui.Text("CS2 COMBINED");
            ImGui.SetWindowFontScale(1f);
            ImGui.PopStyleColor();

            ImGui.TextColored(TextMuted, $"Compact menu · {UtilityCatalog.All.Count} tools");
        }

        public static void Section(string title)
        {
            ImGui.Spacing();
            ImGui.TextColored(Accent, title.ToUpperInvariant());
            ImGui.Separator();
            ImGui.Spacing();
        }

        public static void Hint(string text) =>
            ImGui.TextWrapped(text);

        public static void HintMuted(string text) =>
            ImGui.TextColored(TextMuted, text);

        public static void BeginStatusPanel(string panelId)
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.11f, 0.13f, 0.17f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
            ImGui.BeginChild($"##status_{panelId}", new Vector2(-1f, 0f), ImGuiChildFlags.None);
            ImGui.TextColored(Accent, "STATUS");
            ImGui.Spacing();
        }

        public static void EndStatusPanel()
        {
            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        public static void StatusRow(string label, string value, Vector4 valueColor)
        {
            ImGui.TextColored(TextMuted, label);
            ImGui.SameLine(130f);
            ImGui.TextColored(valueColor, value);
        }

        public static void GameModeRadio(string id, ref AimbotGameMode mode, bool showTeamOption, ref bool showTeam)
        {
            ImGui.TextColored(TextMuted, "Game mode");
            if (RadioPill($"Casual{id}", mode == AimbotGameMode.Casual))
                mode = AimbotGameMode.Casual;
            ImGui.SameLine();
            if (RadioPill($"Deathmatch{id}", mode == AimbotGameMode.Deathmatch))
                mode = AimbotGameMode.Deathmatch;

            if (mode == AimbotGameMode.Casual && showTeamOption)
                ImGui.Checkbox($"Show teammates{id}", ref showTeam);
        }

        private static bool RadioPill(string label, bool selected)
        {
            if (selected)
                ImGui.PushStyleColor(ImGuiCol.Button, AccentSoft);

            bool clicked = ImGui.Button(label);
            if (selected)
                ImGui.PopStyleColor();

            return clicked;
        }

        public static void DrawFooterWarning(string message, Vector4 color)
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(color.X, color.Y, color.Z, 0.12f));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
            ImGui.BeginChild("##footer_warn", new Vector2(-1f, 0f), ImGuiChildFlags.None);
            ImGui.TextColored(color, message);
            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
    }
}
