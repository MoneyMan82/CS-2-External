using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class OverlaySettingsPanel
    {
        private const float FabSize = 30f;
        private const float FabMargin = 10f;

        public static void DrawFab(OverlaySettings settings)
        {
            if (!settings.ShowSettingsButton)
                return;

            OverlayLayout.AnchorWindow(settings.SettingsButtonCorner, new Vector2(FabMargin, FabMargin));
            ImGui.SetNextWindowSize(new Vector2(FabSize, FabSize));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.09f, 0.10f, 0.13f, 0.92f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(UiTheme.Accent.X, UiTheme.Accent.Y, UiTheme.Accent.Z, 0.45f));

            ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoSavedSettings;

            ImGui.Begin("##overlay_settings_fab", flags);

            if (ImGui.InvisibleButton("##fab_btn", new Vector2(FabSize, FabSize)))
                settings.SettingsPopupOpen = !settings.SettingsPopupOpen;

            bool hovered = ImGui.IsItemHovered();
            var draw = ImGui.GetWindowDrawList();
            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            Vector2 center = (min + max) * 0.5f;

            uint bg = ImGui.ColorConvertFloat4ToU32(
                hovered
                    ? new Vector4(UiTheme.Accent.X, UiTheme.Accent.Y, UiTheme.Accent.Z, 0.22f)
                    : new Vector4(0.13f, 0.15f, 0.19f, 0.95f));
            draw.AddRectFilled(min, max, bg, 8f);
            draw.AddRect(min, max, UiTheme.AccentU32, 8f, ImDrawFlags.None, hovered ? 1.6f : 1f);
            DrawGearIcon(draw, center, hovered ? 7.5f : 7f, UiTheme.AccentU32);

            ImGui.End();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }

        public static void DrawWindow(OverlaySettings settings)
        {
            if (!settings.SettingsPopupOpen)
                return;

            Vector2 popupMargin = new(FabMargin + FabSize + 6f, FabMargin);
            OverlayLayout.AnchorWindow(settings.SettingsButtonCorner, popupMargin);
            ImGui.SetNextWindowSize(new Vector2(268f, 360f), ImGuiCond.FirstUseEver);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
            ImGui.Begin(
                "Settings",
                ref settings.SettingsPopupOpen,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);

            float scrollH = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
            ImGui.BeginChild("##settings_scroll", new Vector2(-1f, scrollH), ImGuiChildFlags.None);

            UiTheme.Section("Menu");
            ImGui.Checkbox("Show feature menu", ref settings.ShowMainMenu);
            OverlayLayout.CornerCombo("Menu corner##set_menu", ref settings.MenuCorner);
            ImGui.SliderFloat("Menu width", ref settings.MenuWidthFraction, 0.18f, 0.36f);
            ImGui.SliderFloat("Menu height", ref settings.MenuHeightFraction, 0.22f, 0.45f);

            UiTheme.Section("Appearance");
            DrawDensityRadio(settings);
            DrawFontRadio(settings);
            ImGui.SliderFloat("Font size", ref settings.MenuFontSize, 11f, 20f);
            ImGui.SliderFloat("Font scale", ref settings.MenuFontScale, 0.85f, 1.25f);
            DrawAccentRadio(settings);

            UiTheme.Section("Button");
            ImGui.Checkbox("Show settings button", ref settings.ShowSettingsButton);
            OverlayLayout.CornerCombo("Button corner##set_fab", ref settings.SettingsButtonCorner);

            UiTheme.Section("Performance");
            DrawPerformanceRadio(settings);
            UiTheme.HintMuted($"Game loop sleep: {settings.GameLoopSleepMs} ms (bhop overrides when on)");

            UiTheme.Section("About");
            ImGui.TextColored(UiTheme.TextPrimary, OverlaySettings.VersionLabel);
            ImGui.TextColored(UiTheme.TextMuted, OverlaySettings.RuntimeLabel);
            ImGui.TextColored(UiTheme.TextMuted, $"Overlay FPS {ImGui.GetIO().Framerate:0}");

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.End();
        }

        private static void DrawGearIcon(ImDrawListPtr draw, Vector2 center, float radius, uint color)
        {
            draw.AddCircle(center, radius * 0.45f, color, 16, 1.4f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * MathF.PI * 0.25f;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var a = center + dir * (radius * 0.55f);
                var b = center + dir * radius;
                draw.AddLine(a, b, color, 1.6f);
            }
        }

        private static void DrawDensityRadio(OverlaySettings settings)
        {
            ImGui.TextColored(UiTheme.TextMuted, "Style");
            if (UiTheme.RadioPill("Compact##set_density", settings.MenuDensity == MenuDensity.Compact))
                settings.MenuDensity = MenuDensity.Compact;
            ImGui.SameLine();
            if (UiTheme.RadioPill("Comfort##set_density", settings.MenuDensity == MenuDensity.Comfortable))
                settings.MenuDensity = MenuDensity.Comfortable;
        }

        private static void DrawFontRadio(OverlaySettings settings)
        {
            ImGui.TextColored(UiTheme.TextMuted, "Font");
            if (UiTheme.RadioPill("Default##set_font", settings.MenuFont == MenuFontChoice.Default))
                settings.MenuFont = MenuFontChoice.Default;
            ImGui.SameLine();
            if (UiTheme.RadioPill("Fredoka##set_font", settings.MenuFont == MenuFontChoice.Fredoka))
                settings.MenuFont = MenuFontChoice.Fredoka;
        }

        private static void DrawAccentRadio(OverlaySettings settings)
        {
            ImGui.TextColored(UiTheme.TextMuted, "Accent");
            if (UiTheme.RadioPill("Teal##set_accent", settings.Accent == AccentPreset.Teal))
                settings.Accent = AccentPreset.Teal;
            ImGui.SameLine();
            if (UiTheme.RadioPill("Blue##set_accent", settings.Accent == AccentPreset.Blue))
                settings.Accent = AccentPreset.Blue;
            ImGui.SameLine();
            if (UiTheme.RadioPill("Violet##set_accent", settings.Accent == AccentPreset.Violet))
                settings.Accent = AccentPreset.Violet;
        }

        private static void DrawPerformanceRadio(OverlaySettings settings)
        {
            ImGui.TextColored(UiTheme.TextMuted, "CPU use");
            if (UiTheme.RadioPill("Fast##set_perf", settings.Performance == PerformancePreset.Fast))
                settings.Performance = PerformancePreset.Fast;
            ImGui.SameLine();
            if (UiTheme.RadioPill("Balanced##set_perf", settings.Performance == PerformancePreset.Balanced))
                settings.Performance = PerformancePreset.Balanced;
            ImGui.SameLine();
            if (UiTheme.RadioPill("Low##set_perf", settings.Performance == PerformancePreset.LowCpu))
                settings.Performance = PerformancePreset.LowCpu;
        }
    }
}
