using ImGuiNET;
using System.Numerics;

namespace External_Aimbot
{
    internal static class UtilityHud
    {
        private static long _lastOriginSampleMs;
        private static Vector3 _lastOrigin;
        private static float _smoothedVelocity;

        public static void Draw(UtilityStore store, in UtilityHudContext ctx)
        {
            var draw = ImGui.GetBackgroundDrawList();
            var io = ImGui.GetIO();
            float w = io.DisplaySize.X;
            float h = io.DisplaySize.Y;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            bool compact = store.IsOn("st_compact") || store.IsOn("st_small_font");
            float fontSize = store.IsOn("st_small_font") ? 13f : 15f;
            uint text = ToU32(UiTheme.TextPrimary);
            uint muted = ToU32(UiTheme.TextMuted);
            uint accent = UiTheme.AccentU32;
            uint danger = ToU32(UiTheme.TextDanger);
            uint warn = ToU32(UiTheme.TextWarning);

            UpdateVelocity(ctx);

            DrawScreenFx(draw, store, ctx, w, h, center, accent, danger, warn);
            DrawCrosshair(draw, store, center, accent);
            DrawMarkers(draw, store, center, accent);

            float yTop = 12f;
            float yBottom = h - (compact ? 18f : 24f);

            if (store.IsOn("hud_fps"))
                DrawLabel(draw, new Vector2(12f, yTop), $"FPS {io.Framerate:0}", text, fontSize);
            yTop += Row(compact);

            if (store.IsOn("hud_ms"))
            {
                float ms = io.Framerate > 0 ? 1000f / io.Framerate : 0;
                DrawLabel(draw, new Vector2(12f, yTop), $"Frame {ms:0.1} ms", muted, fontSize);
                yTop += Row(compact);
            }

            if (store.IsOn("hud_clock"))
            {
                DrawLabel(draw, new Vector2(12f, yTop), DateTime.Now.ToString("HH:mm:ss"), text, fontSize);
                yTop += Row(compact);
            }

            if (store.IsOn("hud_date"))
            {
                DrawLabel(draw, new Vector2(12f, yTop), DateTime.Now.ToString("ddd dd MMM"), muted, fontSize);
                yTop += Row(compact);
            }

            if (store.IsOn("hud_session"))
            {
                double sec = (Environment.TickCount64 - ctx.SessionStartTicks) / 1000.0;
                DrawLabel(draw, new Vector2(12f, yTop), $"Session {sec:0}s", muted, fontSize);
                yTop += Row(compact);
            }

            if (store.IsOn("hud_watermark"))
            {
                string wm = store.IsOn("st_minimal_wm") ? "CS2" : "CS2 Combined";
                DrawLabel(draw, new Vector2(w - 120f, 12f), wm, accent, fontSize);
            }

            if (store.IsOn("hud_build"))
                DrawLabel(draw, new Vector2(w - 120f, 28f), "overlay", muted, fontSize - 2f);

            if (store.IsOn("hud_map") && !string.IsNullOrEmpty(ctx.MapName))
                DrawLabel(draw, new Vector2(12f, yTop), ctx.MapName, accent, fontSize);

            if (store.IsOn("hud_ping_est"))
            {
                float est = io.Framerate > 0 ? 1000f / io.Framerate : 0;
                DrawLabel(draw, new Vector2(w - 130f, 44f), $"Est {est:0} ms", muted, fontSize);
            }

            if (!ctx.InGame)
                return;

            if (store.IsOn("hud_health"))
                DrawLabel(draw, new Vector2(12f, yTop), $"HP {ctx.LocalHealth}", ctx.LocalHealth < 30 ? danger : text, fontSize);

            if (store.IsOn("hud_armor") && ctx.LocalArmor > 0)
                DrawLabel(draw, new Vector2(12f, yTop + Row(compact)), $"AP {ctx.LocalArmor}", text, fontSize);

            if (store.IsOn("hud_fov") && ctx.GameFov > 0)
                DrawLabel(draw, new Vector2(12f, yTop + Row(compact) * 2), $"FOV {ctx.GameFov}", muted, fontSize);

            if (store.IsOn("hud_weapon") && ctx.Weapon.IsValid)
                DrawLabel(draw, new Vector2(12f, h * 0.5f - 40f), ctx.Weapon.Name, accent, fontSize);

            if (store.IsOn("hud_shots") && ctx.ShotsFired > 0)
                DrawLabel(draw, new Vector2(12f, yTop + Row(compact) * 3), $"Shots {ctx.ShotsFired}", muted, fontSize);

            if (store.IsOn("hud_velocity"))
                DrawLabel(draw, new Vector2(12f, yTop + Row(compact) * 4), $"Speed {_smoothedVelocity:0} u/s", muted, fontSize);

            if (store.IsOn("hud_pitch_yaw"))
                DrawLabel(draw, new Vector2(12f, yTop + Row(compact) * 5), $"Pitch {ctx.ViewAngles.X:0.0}  Yaw {ctx.ViewAngles.Y:0.0}", muted, fontSize);

            if (store.IsOn("hud_coords") && !store.IsOn("st_hide_coords"))
                DrawLabel(draw, new Vector2(12f, yBottom), $"X {ctx.LocalOrigin.X:0}  Y {ctx.LocalOrigin.Y:0}  Z {ctx.LocalOrigin.Z:0}", muted, fontSize - 1f);

            if (store.IsOn("hud_team"))
                DrawLabel(draw, new Vector2(w - 90f, yTop), ctx.LocalTeam == 2 ? "T" : ctx.LocalTeam == 3 ? "CT" : "—", text, fontSize);

            if (store.IsOn("hud_enemy_alive"))
                DrawLabel(draw, new Vector2(w - 110f, yTop + Row(compact)), $"Enemies {ctx.EnemyAlive}", warn, fontSize);

            if (store.IsOn("hud_team_alive"))
                DrawLabel(draw, new Vector2(w - 110f, yTop + Row(compact) * 2), $"Team {ctx.TeamAlive}", text, fontSize);

            if (store.IsOn("hud_spectators"))
            {
                if (store.IsOn("st_no_spectator_names") || ctx.Misc.Spectators == null)
                    DrawLabel(draw, new Vector2(w - 140f, 70f), $"Specs {ctx.Misc.SpectatorCount}", warn, fontSize);
                else if (ctx.Misc.SpectatorCount > 0)
                    DrawLabel(draw, new Vector2(w - 180f, 70f), string.Join(", ", ctx.Misc.Spectators), warn, fontSize - 1f);
            }

            if (store.IsOn("hud_bomb") && ctx.Misc.BombPlanted)
                DrawLabel(draw, new Vector2(w * 0.5f - 50f, 56f), $"BOMB {ctx.Misc.BombSite} {ctx.Misc.BombTimeLeft:0.0}s", danger, fontSize + 1f);

            if (store.IsOn("hud_defuse") && ctx.Misc.BombBeingDefused)
                DrawLabel(draw, new Vector2(w * 0.5f - 60f, 76f), $"DEFUSE {ctx.Misc.DefuseTimeLeft:0.0}s", accent, fontSize);

            if (store.IsOn("hud_status_bar"))
                DrawLabel(draw, new Vector2(12f, yBottom - Row(compact)), "Overlay active", accent, fontSize - 1f);

            if (store.IsOn("hud_hint_menu"))
                DrawLabel(draw, new Vector2(12f, yBottom), "Menu: compact panel · scroll Tools tab", muted, fontSize - 2f);

            if (store.IsOn("hud_weapon_recoil") && !string.IsNullOrEmpty(ctx.RecoilPreset))
                DrawLabel(draw, new Vector2(12f, yBottom - Row(compact) * 2), ctx.RecoilPreset, muted, fontSize - 1f);

            if (store.IsOn("hud_esp_count"))
                DrawLabel(draw, new Vector2(w - 100f, h - 40f), $"ESP {ctx.EspCount}", muted, fontSize);

            if (store.IsOn("hud_radar_count"))
                DrawLabel(draw, new Vector2(w - 100f, h - 56f), $"Radar {ctx.RadarBlipCount}", muted, fontSize);

            if (store.IsOn("pr_entity_id") && ctx.CrosshairEntityId > 0)
                DrawLabel(draw, new Vector2(w * 0.5f - 30f, h * 0.5f + 24f), $"Ent {ctx.CrosshairEntityId}", muted, fontSize - 1f);

            DrawAlerts(draw, store, ctx, w, h, center, danger, warn, accent, fontSize);
        }

        private static void DrawAlerts(
            ImDrawListPtr draw,
            UtilityStore store,
            in UtilityHudContext ctx,
            float w,
            float h,
            Vector2 center,
            uint danger,
            uint warn,
            uint accent,
            float fontSize)
        {
            if (store.IsOn("al_bomb_planted") && ctx.Misc.BombPlanted)
                DrawLabel(draw, new Vector2(center.X - 70f, 100f), "BOMB PLANTED", danger, fontSize + 2f);

            if (store.IsOn("al_defusing") && ctx.Misc.BombBeingDefused)
                DrawLabel(draw, new Vector2(center.X - 50f, 120f), "DEFUSING", accent, fontSize + 1f);

            if (store.IsOn("al_low_hp") && ctx.LocalHealth > 0 && ctx.LocalHealth < 25)
                DrawLabel(draw, new Vector2(center.X - 45f, 140f), "LOW HP", danger, fontSize + 1f);

            if (store.IsOn("al_spectated") && ctx.Misc.SpectatorCount > 0)
                DrawLabel(draw, new Vector2(center.X - 70f, 160f), "SPECTATED", warn, fontSize);

            if (store.IsOn("al_flash_warn") && ctx.FlashAlpha > 120f)
                DrawLabel(draw, new Vector2(center.X - 55f, 180f), "FLASHED", warn, fontSize);
        }

        private static void DrawScreenFx(
            ImDrawListPtr draw,
            UtilityStore store,
            in UtilityHudContext ctx,
            float w,
            float h,
            Vector2 center,
            uint accent,
            uint danger,
            uint warn)
        {
            if (store.IsOn("col_accent_bar"))
            {
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w, 3f), accent);
            }

            if (store.IsOn("fx_vignette"))
            {
                float a = store.GetFloat("fx_vignette_str", 0.35f);
                uint c = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, a * 0.5f));
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w, h * 0.12f), c);
                draw.AddRectFilled(new Vector2(0, h * 0.88f), new Vector2(w, h), c);
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w * 0.08f, h), c);
                draw.AddRectFilled(new Vector2(w * 0.92f, 0), new Vector2(w, h), c);
            }

            if (store.IsOn("fx_border"))
                draw.AddRect(new Vector2(8, 8), new Vector2(w - 8, h - 8), accent, 0f, 0, 2f);

            if (store.IsOn("fx_grid"))
            {
                float step = store.GetFloat("fx_grid_step", 64f);
                uint gc = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.04f));
                for (float x = 0; x < w; x += step)
                    draw.AddLine(new Vector2(x, 0), new Vector2(x, h), gc, 1f);
                for (float y = 0; y < h; y += step)
                    draw.AddLine(new Vector2(0, y), new Vector2(w, y), gc, 1f);
            }

            if (store.IsOn("fx_scanlines"))
            {
                uint sc = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.06f));
                for (float y = 0; y < h; y += 3f)
                    draw.AddLine(new Vector2(0, y), new Vector2(w, y), sc, 1f);
            }

            float tintA = store.GetFloat("fx_tint_alpha", 0.08f);
            if (store.IsOn("fx_warm"))
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.5f, 0.2f, tintA)));
            if (store.IsOn("fx_cool"))
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.5f, 1f, tintA)));
            if (store.IsOn("fx_dim"))
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.15f)));

            if (store.IsOn("fx_lowhp") && ctx.LocalHealth > 0 && ctx.LocalHealth < 35)
            {
                float pulse = 0.15f + 0.1f * MathF.Sin(Environment.TickCount64 * 0.01f);
                draw.AddRectFilled(new Vector2(0, 0), new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.1f, 0.1f, pulse)));
            }

            if (store.IsOn("fx_bomb_border") && ctx.Misc.BombPlanted)
                draw.AddRect(new Vector2(4, 4), new Vector2(w - 4, h - 4), danger, 0f, 0, 4f);

            if (store.IsOn("fx_defuse_glow") && ctx.Misc.BombBeingDefused)
                draw.AddRect(new Vector2(12, 12), new Vector2(w - 12, h - 12), accent, 0f, 0, 3f);

            if (store.IsOn("fx_safe_area"))
                draw.AddRect(new Vector2(w * 0.05f, h * 0.05f), new Vector2(w * 0.95f, h * 0.95f), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.12f)), 0f, 0, 1f);

            if (store.IsOn("fx_horizon"))
                draw.AddLine(new Vector2(0, h * 0.5f), new Vector2(w, h * 0.5f), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.08f)), 1f);

            if (store.IsOn("fx_cross_axis"))
            {
                draw.AddLine(new Vector2(center.X, 0), new Vector2(center.X, h), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.05f)), 1f);
                draw.AddLine(new Vector2(0, center.Y), new Vector2(w, center.Y), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.05f)), 1f);
            }

            if (store.IsOn("fx_corner_brackets"))
                DrawCornerBrackets(draw, new Vector2(24, 24), new Vector2(w - 24, h - 24), accent);
        }

        private static void DrawCrosshair(ImDrawListPtr draw, UtilityStore store, Vector2 center, uint color)
        {
            int style = store.ActiveCrosshairStyle();
            if (!store.IsOn($"ch_style_{style}"))
                return;

            float size = store.GetFloat("ch_size", 6f);
            float gap = store.GetFloat("ch_gap", 4f);
            float thick = store.GetFloat("ch_thick", 2f);
            uint outline = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.85f));

            if (store.IsOn("ch_circle"))
                draw.AddCircle(center, gap + size, color, 32, thick);

            if (store.IsOn("ch_t"))
            {
                DrawLineOutlined(draw, new Vector2(center.X - size, center.Y), new Vector2(center.X + size, center.Y), color, outline, thick);
                DrawLineOutlined(draw, new Vector2(center.X, center.Y - gap - size), new Vector2(center.X, center.Y - gap), color, outline, thick);
            }
            else
            {
                DrawLineOutlined(draw, new Vector2(center.X - gap - size, center.Y), new Vector2(center.X - gap, center.Y), color, outline, thick);
                DrawLineOutlined(draw, new Vector2(center.X + gap, center.Y), new Vector2(center.X + gap + size, center.Y), color, outline, thick);
                DrawLineOutlined(draw, new Vector2(center.X, center.Y - gap - size), new Vector2(center.X, center.Y - gap), color, outline, thick);
                DrawLineOutlined(draw, new Vector2(center.X, center.Y + gap), new Vector2(center.X, center.Y + gap + size), color, outline, thick);
            }

            if (store.IsOn("ch_dot"))
                draw.AddCircleFilled(center, thick + 0.5f, color);

            if (style == 2)
                draw.AddCircle(center, gap + size * 0.5f, color, 24, 1f);
            else if (style == 3)
                draw.AddQuadFilled(center + new Vector2(0, -size), center + new Vector2(size, 0), center + new Vector2(0, size), center + new Vector2(-size, 0), color);
            else if (style >= 4 && style <= 6)
                draw.AddCircle(center, gap + (style - 3) * 3f, color, 24, 1f);
        }

        private static void DrawMarkers(ImDrawListPtr draw, UtilityStore store, Vector2 center, uint color)
        {
            if (store.IsOn("fx_center_mark"))
                draw.AddCircleFilled(center, 2f, color);

            for (int i = 1; i <= 10; i++)
            {
                if (!store.IsOn($"mk_dot_{i}"))
                    continue;

                float angle = i / 10f * MathF.PI * 2f;
                float r = 40f + i * 6f;
                var p = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
                draw.AddCircleFilled(p, 2f, color);
            }

            if (store.IsOn("mk_distance_ring"))
                draw.AddCircle(center, 120f, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.15f)), 64, 1f);
        }

        private static void UpdateVelocity(in UtilityHudContext ctx)
        {
            long now = Environment.TickCount64;
            if (_lastOriginSampleMs == 0)
            {
                _lastOrigin = ctx.LocalOrigin;
                _lastOriginSampleMs = now;
                return;
            }

            float dt = (now - _lastOriginSampleMs) / 1000f;
            if (dt < 0.02f)
                return;

            float dist = Vector3.Distance(ctx.LocalOrigin, _lastOrigin);
            _smoothedVelocity = dist / dt;
            _lastOrigin = ctx.LocalOrigin;
            _lastOriginSampleMs = now;
        }

        private static void DrawLabel(ImDrawListPtr draw, Vector2 pos, string text, uint color, float size)
        {
            if (string.IsNullOrEmpty(text))
                return;

            draw.AddText(ImGui.GetFont(), size, pos + new Vector2(1, 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.8f)), text);
            draw.AddText(ImGui.GetFont(), size, pos, color, text);
        }

        private static void DrawLineOutlined(ImDrawListPtr draw, Vector2 a, Vector2 b, uint color, uint outline, float thick)
        {
            draw.AddLine(a, b, outline, thick + 2f);
            draw.AddLine(a, b, color, thick);
        }

        private static void DrawCornerBrackets(ImDrawListPtr draw, Vector2 min, Vector2 max, uint color)
        {
            float c = 18f;
            draw.AddLine(min, min + new Vector2(c, 0), color, 2f);
            draw.AddLine(min, min + new Vector2(0, c), color, 2f);
            draw.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X - c, min.Y), color, 2f);
            draw.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X, min.Y + c), color, 2f);
            draw.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X + c, max.Y), color, 2f);
            draw.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X, max.Y - c), color, 2f);
            draw.AddLine(max, max - new Vector2(c, 0), color, 2f);
            draw.AddLine(max, max - new Vector2(0, c), color, 2f);
        }

        private static float Row(bool compact) => compact ? 14f : 18f;

        private static uint ToU32(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);
    }
}
