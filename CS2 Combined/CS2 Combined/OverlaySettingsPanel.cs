using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class OverlaySettingsPanel
    {
        private const float FabWidth = 52f;
        private const float FabHeight = 28f;
        private const float FabMargin = 14f;

        public static void DrawFab(OverlaySettings settings)
        {
            if (!settings.ShowSettingsButton)
                return;

            OverlayLayout.AnchorWindow(settings.SettingsButtonCorner, new Vector2(FabMargin, FabMargin));
            ImGui.SetNextWindowSize(new Vector2(FabWidth, FabHeight));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f, 3f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.07f, 0.08f, 0.10f, 0.94f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(UiTheme.Accent.X, UiTheme.Accent.Y, UiTheme.Accent.Z, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(UiTheme.Accent.X * 0.35f, UiTheme.Accent.Y * 0.35f, UiTheme.Accent.Z * 0.35f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(UiTheme.Accent.X * 0.55f, UiTheme.Accent.Y * 0.55f, UiTheme.Accent.Z * 0.55f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(UiTheme.Accent.X * 0.75f, UiTheme.Accent.Y * 0.75f, UiTheme.Accent.Z * 0.75f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.TextPrimary);

            ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.AlwaysAutoResize;

            ImGui.Begin("##overlay_settings_fab", flags);

            if (ImGui.Button("SET", new Vector2(FabWidth - 8f, FabHeight - 6f)))
                settings.SettingsPopupOpen = !settings.SettingsPopupOpen;

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Overlay settings");

            ImGui.End();
            ImGui.PopStyleColor(6);
            ImGui.PopStyleVar(3);
        }

        public static void DrawMenuShortcut(OverlaySettings settings)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.AccentSoft);
            if (ImGui.SmallButton("Settings"))
                settings.SettingsPopupOpen = !settings.SettingsPopupOpen;
            ImGui.PopStyleColor();
        }

        public static void DrawWindow(OverlaySettings settings)
        {
            if (!settings.SettingsPopupOpen)
                return;

            Vector2 popupMargin = new(FabMargin, FabMargin + FabHeight + 8f);
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
            ImGui.Checkbox("Show SET corner button", ref settings.ShowSettingsButton);
            OverlayLayout.CornerCombo("Button corner##set_fab", ref settings.SettingsButtonCorner);
            UiTheme.HintMuted("Look for the teal SET pill in that corner.");

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
