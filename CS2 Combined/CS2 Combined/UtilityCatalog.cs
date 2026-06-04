namespace External_Aimbot
{
    public enum UtilityKind
    {
        Toggle,
        Slider,
    }

    public readonly struct UtilityEntry
    {
        public string Id { get; init; }
        public string Category { get; init; }
        public string Label { get; init; }
        public UtilityKind Kind { get; init; }
        public float DefaultFloat { get; init; }
        public float Min { get; init; }
        public float Max { get; init; }
        public bool DefaultOn { get; init; }
    }

    internal static class UtilityCatalog
    {
        public static IReadOnlyList<UtilityEntry> All { get; } = Build();

        public static IReadOnlyList<string> Categories { get; } =
            All.Select(e => e.Category).Distinct().OrderBy(c => c).ToArray();

        private static List<UtilityEntry> Build()
        {
            var list = new List<UtilityEntry>(128);

            void Toggle(string cat, string id, string label, bool on = false) =>
                list.Add(new UtilityEntry
                {
                    Id = id,
                    Category = cat,
                    Label = label,
                    Kind = UtilityKind.Toggle,
                    DefaultOn = on,
                });

            void Slider(string cat, string id, string label, float def, float min, float max) =>
                list.Add(new UtilityEntry
                {
                    Id = id,
                    Category = cat,
                    Label = label,
                    Kind = UtilityKind.Slider,
                    DefaultFloat = def,
                    Min = min,
                    Max = max,
                });

            for (int i = 1; i <= 12; i++)
                Toggle("Crosshair", $"ch_style_{i}", $"Style {i} preset", i == 1);

            Slider("Crosshair", "ch_size", "Crosshair size", 6f, 2f, 24f);
            Slider("Crosshair", "ch_gap", "Center gap", 4f, 0f, 20f);
            Slider("Crosshair", "ch_thick", "Line thickness", 2f, 1f, 6f);
            Toggle("Crosshair", "ch_dot", "Center dot", true);
            Toggle("Crosshair", "ch_outline", "Outline", true);
            Toggle("Crosshair", "ch_t", "T-shape", false);
            Toggle("Crosshair", "ch_circle", "Circle ring", false);

            Toggle("HUD · Top", "hud_fps", "FPS counter", true);
            Toggle("HUD · Top", "hud_ms", "Frame time (ms)", false);
            Toggle("HUD · Top", "hud_clock", "Local clock", true);
            Toggle("HUD · Top", "hud_session", "Session timer", true);
            Toggle("HUD · Top", "hud_date", "Date", false);
            Toggle("HUD · Top", "hud_watermark", "Watermark", true);
            Toggle("HUD · Top", "hud_build", "Build tag", false);
            Toggle("HUD · Top", "hud_map", "Map name", false);
            Toggle("HUD · Top", "hud_ping_est", "Latency estimate", false);
            Toggle("HUD · Top", "hud_tick", "Overlay tick", false);

            Toggle("HUD · Game", "hud_health", "Your HP", true);
            Toggle("HUD · Game", "hud_armor", "Your armor", true);
            Toggle("HUD · Game", "hud_fov", "Game FOV", true);
            Toggle("HUD · Game", "hud_weapon", "Active weapon", true);
            Toggle("HUD · Game", "hud_shots", "Shots fired", false);
            Toggle("HUD · Game", "hud_velocity", "Velocity", false);
            Toggle("HUD · Game", "hud_pitch_yaw", "View angles", false);
            Toggle("HUD · Game", "hud_coords", "World position", false);
            Toggle("HUD · Game", "hud_team", "Your team", false);
            Toggle("HUD · Game", "hud_enemy_alive", "Enemies alive", true);
            Toggle("HUD · Game", "hud_team_alive", "Teammates alive", false);
            Toggle("HUD · Game", "hud_spectators", "Spectator list", true);
            Toggle("HUD · Game", "hud_bomb", "Bomb timer", true);
            Toggle("HUD · Game", "hud_defuse", "Defuse timer", true);

            Toggle("HUD · Bottom", "hud_line_sep", "Separator line", false);
            Toggle("HUD · Bottom", "hud_hint_aim", "Aim hint", false);
            Toggle("HUD · Bottom", "hud_hint_menu", "Menu hint", true);
            Toggle("HUD · Bottom", "hud_status_bar", "Status bar", true);
            Toggle("HUD · Bottom", "hud_weapon_recoil", "Recoil preset name", false);
            Toggle("HUD · Bottom", "hud_target_line", "Target line count", false);
            Toggle("HUD · Bottom", "hud_esp_count", "ESP player count", false);
            Toggle("HUD · Bottom", "hud_radar_count", "Radar blip count", false);

            Toggle("Screen FX", "fx_vignette", "Vignette", false);
            Toggle("Screen FX", "fx_border", "Screen border", false);
            Toggle("Screen FX", "fx_grid", "Alignment grid", false);
            Toggle("Screen FX", "fx_scanlines", "Scanlines", false);
            Toggle("Screen FX", "fx_warm", "Warm tint", false);
            Toggle("Screen FX", "fx_cool", "Cool tint", false);
            Toggle("Screen FX", "fx_dim", "Dim overlay", false);
            Toggle("Screen FX", "fx_lowhp", "Low HP pulse", true);
            Toggle("Screen FX", "fx_flash_warn", "Flash warning", true);
            Toggle("Screen FX", "fx_bomb_border", "Bomb planted border", true);
            Toggle("Screen FX", "fx_defuse_glow", "Defuse glow", false);
            Toggle("Screen FX", "fx_center_mark", "Center marker", false);
            Toggle("Screen FX", "fx_corner_brackets", "Corner brackets", false);
            Toggle("Screen FX", "fx_safe_area", "Safe area box", false);
            Toggle("Screen FX", "fx_horizon", "Horizon line", false);
            Toggle("Screen FX", "fx_cross_axis", "Center axis", false);

            Slider("Screen FX", "fx_vignette_str", "Vignette strength", 0.35f, 0.1f, 0.8f);
            Slider("Screen FX", "fx_grid_step", "Grid spacing", 64f, 32f, 128f);
            Slider("Screen FX", "fx_tint_alpha", "Tint strength", 0.08f, 0.02f, 0.25f);

            for (int i = 1; i <= 10; i++)
                Toggle("Markers", $"mk_dot_{i}", $"Marker dot #{i}", false);

            Toggle("Markers", "mk_distance_ring", "Distance ring", false);
            Toggle("Markers", "mk_tick_marks", "Tick marks (top)", false);

            Toggle("Streamer", "st_compact", "Compact HUD text", true);
            Toggle("Streamer", "st_hide_coords", "Hide coordinates", false);
            Toggle("Streamer", "st_hide_names", "Anonymize names (ESP)", false);
            Toggle("Streamer", "st_minimal_wm", "Minimal watermark", false);
            Toggle("Streamer", "st_no_spectator_names", "Spectators: count only", false);
            Toggle("Streamer", "st_dark_bg", "Darken HUD panels", true);
            Toggle("Streamer", "st_small_font", "Smaller HUD font", false);
            Toggle("Streamer", "st_top_only", "HUD top corner only", false);

            Toggle("Alerts", "al_bomb_planted", "Bomb planted alert", true);
            Toggle("Alerts", "al_defusing", "Defusing alert", true);
            Toggle("Alerts", "al_low_hp", "Low HP alert", true);
            Toggle("Alerts", "al_spectated", "Being watched alert", true);
            Toggle("Alerts", "al_reload", "Reloading hint", false);
            Toggle("Alerts", "al_no_ammo", "No ammo hint", false);
            Toggle("Alerts", "al_round_freeze", "Warmup hint", false);

            Toggle("Practice", "pr_show_fov_circle", "FOV circle (aim)", false);
            Toggle("Practice", "pr_show_prediction", "Recoil prediction dot", false);
            Toggle("Practice", "pr_show_target_lines", "Target lines", false);
            Toggle("Practice", "pr_show_visibility", "Visibility debug", false);
            Toggle("Practice", "pr_show_map_ray", "Map ray trace flag", false);
            Toggle("Practice", "pr_delta_angle", "Angle delta readout", false);
            Toggle("Practice", "pr_punch_angle", "Aim punch readout", false);
            Toggle("Practice", "pr_entity_id", "Crosshair entity id", false);
            Toggle("Practice", "pr_handle_debug", "Weapon handle debug", false);
            Toggle("Practice", "pr_overlay_fps_graph", "FPS graph", false);

            Slider("Practice", "pr_graph_w", "FPS graph width", 120f, 60f, 240f);

            Toggle("Colors", "col_team_t", "T color accent", false);
            Toggle("Colors", "col_team_ct", "CT color accent", false);
            Toggle("Colors", "col_visible", "Visible color hint", false);
            Toggle("Colors", "col_hidden", "Hidden color hint", false);
            Toggle("Colors", "col_accent_bar", "Accent top bar", true);

            return list;
        }
    }
}
