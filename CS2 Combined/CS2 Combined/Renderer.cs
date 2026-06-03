using ClickableTransparentOverlay;
using ImGuiNET;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace External_Aimbot
{
    public class Renderer : Overlay
    {
        private const string OverlayTitle = "CS2 Combined Overlay";

        private static readonly IntPtr HwndTopmost = new(-1);

        private const uint SwpNomove = 0x0002;
        private const uint SwpNosize = 0x0001;
        private const uint SwpNoactivate = 0x0010;
        private const uint SwpShowwindow = 0x0040;

        public bool aimbot = true;
        public bool recoilControl = true;
        public bool recoilPredictor = true;
        public bool visibilityCheck = true;
        public bool mapRaytracing = true;
        public bool drawTargetLines = true;
        public bool aimOnTeam = false;
        public Hotkey aimbotHotkey = Hotkey.Mouse4;
        public AimbotGameMode gameMode = AimbotGameMode.Casual;
        public float fov = 20f;
        public bool showFovCircle = true;
        public float smoothness = 1f;
        public float recoilStrength = 1f;
        public bool HasPredictionPoint;
        public Vector2 PredictionPoint;
        public WeaponContext CurrentWeapon;

        public bool triggerBot = false;
        public Hotkey triggerHotkey = Hotkey.Mouse5;
        public AimbotGameMode triggerGameMode = AimbotGameMode.Casual;
        public int triggerClickDelayMs = 10;
        public int triggerCooldownMs = 50;
        public TriggerBotDebug TriggerDebug;

        public bool espEnabled = false;
        public bool espBox = true;
        public bool espBones = true;
        public bool espName = true;
        public bool espHealth = true;
        public bool espWeapon = true;
        public bool espDistance = true;
        public bool espSnaplines = false;
        public bool espHeadDot = false;
        public bool espArmor = true;
        public bool espColorByVisibility = true;
        public bool espShowTeam = false;
        public AimbotGameMode espGameMode = AimbotGameMode.Casual;

        public bool bhopEnabled = false;
        public bool bhopHoldSpace = true;
        public bool bhopSubtick = true;
        public BhopDebug BhopState;

        public bool antiFlashEnabled = false;
        public AntiFlashDebug AntiFlashState;

        public bool miscNoRecoilEnabled = false;
        public NoRecoilDebug NoRecoilState;

        public bool miscAllGunsAutoEnabled = false;
        public AllGunsAutoDebug AllGunsAutoState;

        public bool miscRadarReveal = false;
        public bool miscOverlayRadar = false;
        public bool miscRadarShowTeam = false;
        public float miscRadarSize = 120f;
        public float miscRadarRange = 2500f;
        public bool miscBombTimer = false;
        public bool miscSpectatorList = false;
        public bool miscFovChanger = false;
        public int miscFovValue = 90;
        public MiscDebug MiscState;

        public bool skinChangerEnabled = false;
        public int skinEditorWeaponDefIndex = 7;
        public int skinEditorSkinIndex = 1;
        public float skinEditorWear = 0.01f;
        public int skinEditorSeed = 0;
        public bool skinEditorStatTrak = false;
        public int skinEditorStatTrakValue = 1337;
        public SkinChangerDebug SkinChangerState;
        private readonly Dictionary<int, SkinConfig> _skinConfigs = new();

        private readonly object _overlayLock = new();
        private List<OverlayLine> _overlayLines = [];
        private List<EspPlayerData> _espPlayers = [];
        private List<RadarBlip> _radarBlips = [];

        private Rect? lastGameRect;
        private IntPtr cachedGameWindow;
        private long lastWindowLookupMs;

        public Renderer() : base(OverlayTitle, true, PrimaryScreenWidth, PrimaryScreenHeight)
        {
        }

        private static int PrimaryScreenWidth => GetSystemMetrics(0);
        private static int PrimaryScreenHeight => GetSystemMetrics(1);

        protected override Task PostInitialized()
        {
            Position = new Point(0, 0);
            SyncOverlayToGameWindow();
            return Task.CompletedTask;
        }

        protected override void Render()
        {
            SyncOverlayToGameWindow();
            KeepOverlayOnTop();

            UiTheme.Apply();
            ImGui.SetNextWindowSize(new Vector2(480f, 0f), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(420f, 320f), new Vector2(560f, 900f));

            ImGui.Begin("CS2 Combined", ImGuiWindowFlags.NoCollapse);

            UiTheme.DrawMenuHeader();

            if (ImGui.BeginTabBar("MainTabs", ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
            {
                if (ImGui.BeginTabItem("Aim"))
                {
                    DrawAimbotTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Trigger"))
                {
                    DrawTriggerBotTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("ESP"))
                {
                    DrawEspTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Bhop"))
                {
                    DrawBhopTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Misc"))
                {
                    DrawMiscTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Skins"))
                {
                    DrawSkinChangerTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            DrawDisplayModeHelp();

            ImGui.End();

            DrawFovCircle();
            DrawTargetLines();
            DrawRecoilPrediction();
            DrawEsp();
            DrawOverlayRadar();
        }

        private void DrawEspTab()
        {
            UiTheme.Section("Display");
            ImGui.Checkbox("Enable ESP", ref espEnabled);
            ImGui.Checkbox("Box", ref espBox);
            ImGui.Checkbox("Bones", ref espBones);
            ImGui.Checkbox("Name", ref espName);
            ImGui.Checkbox("Health", ref espHealth);
            ImGui.Checkbox("Weapon", ref espWeapon);
            ImGui.Checkbox("Distance", ref espDistance);
            ImGui.Checkbox("Snaplines", ref espSnaplines);
            ImGui.Checkbox("Head dot", ref espHeadDot);
            ImGui.Checkbox("Armor", ref espArmor);
            ImGui.Checkbox("Color by visibility", ref espColorByVisibility);

            UiTheme.Section("Filters");
            UiTheme.GameModeRadio("##esp", ref espGameMode, showTeamOption: true, ref espShowTeam);
        }

        private void DrawEsp()
        {
            if (!espEnabled)
                return;

            List<EspPlayerData> players;
            lock (_overlayLock)
            {
                players = _espPlayers;
            }

            EspRenderer.Draw(
                players,
                espBox,
                espBones,
                espName,
                espHealth,
                espWeapon,
                espDistance,
                espSnaplines,
                espHeadDot,
                espArmor,
                espColorByVisibility);
        }

        public void SetEspPlayers(IReadOnlyList<EspPlayerData> players)
        {
            lock (_overlayLock)
            {
                _espPlayers = players.ToList();
            }
        }

        private void DrawAimbotTab()
        {
            UiTheme.Section("Core");
            ImGui.Checkbox("Aimbot", ref aimbot);
            ImGui.Checkbox("Recoil control", ref recoilControl);
            if (recoilControl)
                ImGui.SliderFloat("Recoil strength", ref recoilStrength, 0.5f, 2f);
            ImGui.Checkbox("Recoil predictor", ref recoilPredictor);

            var weapon = CurrentWeapon;
            if (weapon.IsValid)
            {
                string preset = weapon.HasRecoilPreset ? "preset loaded" : "generic fallback";
                UiTheme.StatusRow("Weapon", weapon.Name, UiTheme.TextPrimary);
                UiTheme.StatusRow("Mode", $"{weapon.FireModeLabel} · {weapon.ShotsFired} shots", UiTheme.TextInfo);
                UiTheme.HintMuted(preset);
            }
            else
            {
                UiTheme.StatusRow("Weapon", "none", UiTheme.TextMuted);
            }

            UiTheme.Section("Targeting");
            ImGui.Checkbox("Visibility check", ref visibilityCheck);
            if (visibilityCheck)
            {
                ImGui.Checkbox("Map raytracing", ref mapRaytracing);
                DrawMapRaytracingStatus();
            }
            ImGui.Checkbox("Target lines", ref drawTargetLines);
            UiTheme.GameModeRadio("##aim", ref gameMode, showTeamOption: true, ref aimOnTeam);

            UiTheme.Section("Aim settings");
            ImGui.SliderFloat("Aim FOV", ref fov, 1f, 90f);
            ImGui.Checkbox("Show FOV circle", ref showFovCircle);
            UiTheme.HintMuted("Circle size matches lock range. Lower FOV = tighter aim.");
            ImGui.SliderFloat("Smoothness", ref smoothness, 1f, 20f);
            HotkeyInput.DrawSelector("Aim hotkey", ref aimbotHotkey);
            UiTheme.HintMuted($"Hold {HotkeyInput.Label(aimbotHotkey)} to aim");
        }

        private void DrawTriggerBotTab()
        {
            UiTheme.Section("Core");
            ImGui.Checkbox("Trigger bot", ref triggerBot);
            UiTheme.GameModeRadio("##trigger", ref triggerGameMode, showTeamOption: false, ref aimOnTeam);

            UiTheme.Section("Timing");
            ImGui.SliderInt("Click delay (ms)", ref triggerClickDelayMs, 1, 50);
            ImGui.SliderInt("Shot cooldown (ms)", ref triggerCooldownMs, 10, 500);
            HotkeyInput.DrawSelector("Trigger hotkey", ref triggerHotkey);
            UiTheme.HintMuted($"Hold {HotkeyInput.Label(triggerHotkey)} while aiming at a target");

            UiTheme.BeginStatusPanel();
            var debug = TriggerDebug;
            UiTheme.StatusRow("Hotkey", debug.HotkeyHeld ? "HELD" : "not held",
                debug.HotkeyHeld ? UiTheme.TextSuccess : UiTheme.TextMuted);
            UiTheme.StatusRow("Crosshair ID", debug.EntityId.ToString(), UiTheme.TextPrimary);
            if (debug.EntityId > 0)
            {
                UiTheme.StatusRow("Teams", $"{debug.TargetTeam} vs {debug.LocalTeam}", UiTheme.TextInfo);
                UiTheme.StatusRow("Target HP", debug.TargetHealth.ToString(), UiTheme.TextPrimary);
            }

            Vector4 statusColor = debug.Status == "Shot fired" ? UiTheme.TextSuccess : UiTheme.TextMuted;
            UiTheme.StatusRow("State", debug.Status, statusColor);
            UiTheme.EndStatusPanel();
        }

        public void SetTriggerDebug(TriggerBotDebug debug) => TriggerDebug = debug;

        private void DrawBhopTab()
        {
            UiTheme.Section("Core");
            ImGui.Checkbox("Auto bhop", ref bhopEnabled);
            ImGui.Checkbox("Hold Space to bhop", ref bhopHoldSpace);
            ImGui.Checkbox("Subtick bhop (fastest)", ref bhopSubtick);

            if (!bhopHoldSpace)
                UiTheme.DrawFooterWarning("Jumps automatically when enabled", UiTheme.TextWarning);
            else if (bhopSubtick)
                UiTheme.HintMuted("Fast subtick pulses with 1 ms update rate");

            UiTheme.BeginStatusPanel();
            var debug = BhopState;
            UiTheme.StatusRow("Space", debug.SpaceHeld ? "HELD" : "not held",
                debug.SpaceHeld ? UiTheme.TextSuccess : UiTheme.TextMuted);
            UiTheme.StatusRow("Ground", debug.OnGround ? "yes" : "no",
                debug.OnGround ? UiTheme.TextSuccess : UiTheme.TextInfo);
            UiTheme.StatusRow("Flags", $"0x{debug.Flags:X}", UiTheme.TextMuted);

            Vector4 statusColor = debug.Status is "Jumping" or "Jumping (subtick)" or "Jumping (fast subtick)" or "In air"
                ? UiTheme.TextSuccess
                : UiTheme.TextMuted;
            UiTheme.StatusRow("State", debug.Status, statusColor);
            UiTheme.EndStatusPanel();
        }

        public void SetBhopDebug(BhopDebug debug) => BhopState = debug;

        private void DrawMiscTab()
        {
            UiTheme.Section("Visual");
            ImGui.Checkbox("Anti flash", ref antiFlashEnabled);
            ImGui.Checkbox("FOV changer", ref miscFovChanger);
            if (miscFovChanger)
                ImGui.SliderInt("Game FOV", ref miscFovValue, 60, 140);

            UiTheme.Section("Combat");
            ImGui.Checkbox("No recoil", ref miscNoRecoilEnabled);
            UiTheme.HintMuted("Removes visual recoil while shooting (no aimbot needed)");
            ImGui.Checkbox("All guns auto", ref miscAllGunsAutoEnabled);
            UiTheme.HintMuted("Hold LMB — semi-auto guns fire repeatedly. Full-auto guns unchanged.");

            UiTheme.Section("Radar");
            ImGui.Checkbox("Radar reveal", ref miscRadarReveal);
            UiTheme.HintMuted("Shows enemies on the in-game minimap");
            ImGui.Checkbox("Overlay radar", ref miscOverlayRadar);
            if (miscOverlayRadar)
            {
                ImGui.Checkbox("Show teammates##radar", ref miscRadarShowTeam);
                ImGui.SliderFloat("Radar size", ref miscRadarSize, 80f, 220f);
                ImGui.SliderFloat("Radar range", ref miscRadarRange, 1000f, 5000f);
            }

            UiTheme.Section("Info");
            ImGui.Checkbox("Bomb timer", ref miscBombTimer);
            ImGui.Checkbox("Spectator list", ref miscSpectatorList);

            UiTheme.BeginStatusPanel();
            var flash = AntiFlashState;
            UiTheme.StatusRow("Flash", $"{flash.FlashAlpha:F0}", UiTheme.TextPrimary);
            UiTheme.StatusRow("Anti-flash", flash.Status, UiTheme.TextMuted);

            if (miscNoRecoilEnabled)
            {
                var recoil = NoRecoilState;
                UiTheme.StatusRow("No recoil", recoil.Status, UiTheme.TextInfo);
            }

            if (miscAllGunsAutoEnabled)
            {
                var auto = AllGunsAutoState;
                UiTheme.StatusRow("Auto", auto.Status, UiTheme.TextPrimary);
                UiTheme.StatusRow("Phase", auto.Phase, UiTheme.TextInfo);
                UiTheme.StatusRow("Weapon", auto.ActiveWeapon, UiTheme.TextMuted);
                UiTheme.StatusRow("Shots", auto.ShotCount.ToString(),
                    auto.ShotCount > 0 ? UiTheme.TextSuccess : UiTheme.TextMuted);
            }

            var misc = MiscState;
            if (miscBombTimer)
            {
                if (misc.BombPlanted)
                {
                    UiTheme.StatusRow("Bomb", $"Site {misc.BombSite} · {misc.BombTimeLeft:0.0}s", UiTheme.TextWarning);
                    if (misc.BombBeingDefused)
                        UiTheme.StatusRow("Defuse", $"{misc.DefuseTimeLeft:0.0}s", UiTheme.TextSuccess);
                }
                else
                {
                    UiTheme.StatusRow("Bomb", "not planted", UiTheme.TextMuted);
                }
            }

            if (miscRadarReveal)
                UiTheme.StatusRow("Radar reveal", $"{misc.RadarRevealedCount} spotted", UiTheme.TextInfo);

            if (miscFovChanger && misc.AppliedFov > 0)
                UiTheme.StatusRow("FOV", misc.AppliedFov.ToString(), UiTheme.TextPrimary);

            if (miscSpectatorList)
            {
                if (misc.SpectatorCount > 0 && misc.Spectators != null)
                {
                    UiTheme.StatusRow("Spectators", misc.SpectatorCount.ToString(), UiTheme.TextWarning);
                    foreach (string name in misc.Spectators)
                        ImGui.BulletText(name);
                }
                else
                {
                    UiTheme.StatusRow("Spectators", "none", UiTheme.TextMuted);
                }
            }

            UiTheme.EndStatusPanel();
        }

        public void SetAntiFlashDebug(AntiFlashDebug debug) => AntiFlashState = debug;

        public void SetNoRecoilDebug(NoRecoilDebug debug) => NoRecoilState = debug;

        public void SetAllGunsAutoDebug(AllGunsAutoDebug debug) => AllGunsAutoState = debug;

        public void SetMiscDebug(MiscDebug debug) => MiscState = debug;

        public IReadOnlyDictionary<int, SkinConfig> GetSkinConfigs() => _skinConfigs;

        public void SetSkinChangerDebug(SkinChangerDebug debug) => SkinChangerState = debug;

        private void DrawSkinChangerTab()
        {
            UiTheme.Section("Core");
            ImGui.Checkbox("Enable skin changer", ref skinChangerEnabled);
            UiTheme.HintMuted("Client-side only. Applies to weapons in your current loadout.");

            UiTheme.Section("Editor");
            string weaponLabel = WeaponCatalog.GetName(skinEditorWeaponDefIndex);
            if (ImGui.BeginCombo("Weapon", weaponLabel))
            {
                foreach (int weaponId in SkinCatalog.ConfigurableWeapons)
                {
                    string label = WeaponCatalog.GetName(weaponId);
                    if (ImGui.Selectable(label, skinEditorWeaponDefIndex == weaponId))
                        skinEditorWeaponDefIndex = weaponId;
                }

                ImGui.EndCombo();
            }

            SkinOption[] skins = SkinCatalog.GetSkins(skinEditorWeaponDefIndex);
            skinEditorSkinIndex = Math.Clamp(skinEditorSkinIndex, 0, Math.Max(0, skins.Length - 1));
            string skinLabel = skins[skinEditorSkinIndex].Name;

            if (ImGui.BeginCombo("Skin", skinLabel))
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    if (ImGui.Selectable(skins[i].Name, skinEditorSkinIndex == i))
                        skinEditorSkinIndex = i;
                }

                ImGui.EndCombo();
            }

            ImGui.SliderFloat("Wear", ref skinEditorWear, 0.001f, 1f);
            ImGui.InputInt("Seed", ref skinEditorSeed);
            skinEditorSeed = Math.Clamp(skinEditorSeed, 0, 999);
            ImGui.Checkbox("StatTrak", ref skinEditorStatTrak);
            if (skinEditorStatTrak)
                ImGui.InputInt("StatTrak value", ref skinEditorStatTrakValue);

            ImGui.Spacing();
            if (ImGui.Button("Save for weapon", new Vector2(140f, 0f)))
            {
                _skinConfigs[skinEditorWeaponDefIndex] = new SkinConfig
                {
                    PaintKit = skins[skinEditorSkinIndex].PaintKit,
                    Seed = skinEditorSeed,
                    Wear = skinEditorWear,
                    StatTrakEnabled = skinEditorStatTrak,
                    StatTrak = skinEditorStatTrakValue,
                };
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear saved", new Vector2(100f, 0f)))
                _skinConfigs.Remove(skinEditorWeaponDefIndex);

            UiTheme.Section("Saved");
            if (_skinConfigs.Count == 0)
            {
                UiTheme.HintMuted("None saved yet");
            }
            else
            {
                foreach (var entry in _skinConfigs)
                {
                    ImGui.BulletText(
                        $"{WeaponCatalog.GetName(entry.Key)} → kit {entry.Value.PaintKit}, wear {entry.Value.Wear:0.###}");
                }
            }

            UiTheme.BeginStatusPanel();
            var debug = SkinChangerState;
            UiTheme.StatusRow("State", debug.Status, UiTheme.TextPrimary);

            if (debug.Loadout == null || debug.Loadout.Length == 0)
            {
                UiTheme.HintMuted("Join a match to detect your weapons");
            }
            else
            {
                foreach (LoadoutWeaponInfo weapon in debug.Loadout)
                {
                    string tag = weapon.Configured ? "configured" : "default";
                    Vector4 color = weapon.Configured ? UiTheme.TextSuccess : UiTheme.TextMuted;
                    ImGui.TextColored(color, $"• {weapon.Name} (kit {weapon.CurrentPaintKit}) — {tag}");
                }
            }

            UiTheme.EndStatusPanel();
        }

        public void SetRadarBlips(IReadOnlyList<RadarBlip> blips)
        {
            lock (_overlayLock)
            {
                _radarBlips = blips.ToList();
            }
        }

        private void DrawOverlayRadar()
        {
            if (!miscOverlayRadar)
                return;

            List<RadarBlip> blips;
            lock (_overlayLock)
            {
                blips = _radarBlips;
            }

            RadarOverlay.Draw(blips, miscRadarSize, 16f);
        }

        private void DrawMapRaytracingStatus()
        {
            if (!mapRaytracing)
                return;

            string map = MapCollision.CurrentMap;
            if (string.IsNullOrEmpty(map))
            {
                UiTheme.StatusRow("Map", "unknown", UiTheme.TextWarning);
                return;
            }

            UiTheme.StatusRow("Map", map, UiTheme.TextPrimary);

            if (MapCollision.IsLoading)
            {
                UiTheme.StatusRow("Mesh", "loading...", UiTheme.TextWarning);
                return;
            }

            if (MapCollision.IsLoaded)
            {
                UiTheme.StatusRow("Mesh", $"loaded ({MapCollision.TriangleCount:N0} tris)", UiTheme.TextSuccess);
                return;
            }

            if (!mapRaytracing)
            {
                UiTheme.StatusRow("Mesh", "spotted fallback", UiTheme.TextWarning);
                return;
            }

            UiTheme.StatusRow("Mesh", "MISSING", UiTheme.TextDanger);
            UiTheme.HintMuted($"Add maps\\{map}.tri next to the exe.");
            UiTheme.HintMuted("Wall check is off until mesh loads.");
        }

        private void DrawRecoilPrediction()
        {
            if (!recoilPredictor || !HasPredictionPoint)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            var center = new Vector2(displaySize.X / 2f, displaySize.Y / 2f);

            uint lineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.65f, 0.1f, 0.95f));
            uint dotColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.2f, 1f));

            drawList.AddLine(center, PredictionPoint, lineColor, 1.8f);
            drawList.AddCircleFilled(PredictionPoint, 4f, dotColor);
            drawList.AddCircle(PredictionPoint, 7f, lineColor, 16, 1.2f);
        }

        public void SetOverlayLines(IReadOnlyList<OverlayLine> lines)
        {
            lock (_overlayLock)
            {
                _overlayLines = lines.ToList();
            }
        }

        public Vector2 GetScreenSize()
        {
            if (lastGameRect != null)
                return new Vector2(lastGameRect.Value.Width, lastGameRect.Value.Height);

            return new Vector2(PrimaryScreenWidth, PrimaryScreenHeight);
        }

        private void DrawTargetLines()
        {
            if (!drawTargetLines)
                return;

            List<OverlayLine> lines;
            lock (_overlayLock)
            {
                lines = _overlayLines;
            }

            if (lines.Count == 0)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            var center = new Vector2(displaySize.X / 2f, displaySize.Y / 2f);

            foreach (OverlayLine line in lines)
            {
                uint color = ImGui.ColorConvertFloat4ToU32(
                    line.Visible
                        ? new Vector4(0.2f, 1f, 0.2f, 0.9f)
                        : new Vector4(1f, 0.3f, 0.3f, 0.7f)
                );

                drawList.AddLine(center, line.ScreenPos, color, 1.2f);
            }
        }

        private void DrawFovCircle()
        {
            if (!aimbot || !showFovCircle)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            var center = new Vector2(displaySize.X / 2f, displaySize.Y / 2f);

            float gameFov = miscFovChanger ? miscFovValue : 90f;
            float radius = Calculate.GetFovCircleRadius(fov, displaySize.X, displaySize.Y, gameFov);
            if (radius <= 0f)
                return;

            uint circleColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.35f, 0.85f));
            uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.7f));

            drawList.AddCircle(center, radius + 1f, outlineColor, 64, 2.5f);
            drawList.AddCircle(center, radius, circleColor, 64, 1.5f);
            drawList.AddCircleFilled(center, 2.5f, circleColor);
        }

        private void DrawDisplayModeHelp()
        {
            IntPtr gameWindow = FindGameWindow();
            if (gameWindow == IntPtr.Zero)
            {
                UiTheme.DrawFooterWarning("CS2 window not found.", UiTheme.TextDanger);
                return;
            }

            if (IsExclusiveFullscreen(gameWindow))
                UiTheme.DrawFooterWarning("Use Borderless Windowed in CS2 video settings.", UiTheme.TextDanger);
        }

        private void SyncOverlayToGameWindow()
        {
            IntPtr gameWindow = FindGameWindow();
            if (gameWindow != IntPtr.Zero && GetWindowRect(gameWindow, out Rect gameRect))
            {
                if (lastGameRect == null || !SameRect(lastGameRect.Value, gameRect))
                {
                    Position = new Point(gameRect.Left, gameRect.Top);
                    Size = new Size(gameRect.Width, gameRect.Height);
                    lastGameRect = gameRect;
                }

                return;
            }

            if (lastGameRect != null)
            {
                Position = new Point(0, 0);
                Size = new Size(PrimaryScreenWidth, PrimaryScreenHeight);
                lastGameRect = null;
            }
        }

        private void KeepOverlayOnTop()
        {
            if (window == null)
                return;

            SetWindowPos(
                window.Handle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow
            );
        }

        private static bool SameRect(Rect a, Rect b) =>
            a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

        private IntPtr FindGameWindow()
        {
            long now = Environment.TickCount64;
            if (cachedGameWindow != IntPtr.Zero && now - lastWindowLookupMs < 500)
                return cachedGameWindow;

            lastWindowLookupMs = now;
            cachedGameWindow = FindGameWindowNow();
            return cachedGameWindow;
        }

        private static IntPtr FindGameWindowNow()
        {
            IntPtr gameWindow = FindWindow(null, "Counter-Strike 2");
            if (gameWindow != IntPtr.Zero)
                return gameWindow;

            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out uint processId);
                try
                {
                    using Process process = Process.GetProcessById((int)processId);
                    if (process.ProcessName.Equals("cs2", StringComparison.OrdinalIgnoreCase))
                    {
                        found = hWnd;
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static bool IsExclusiveFullscreen(IntPtr gameWindow)
        {
            if (!GetWindowRect(gameWindow, out Rect windowRect))
                return false;

            IntPtr monitor = MonitorFromWindow(gameWindow, MonitorDefaultTonearest);
            if (monitor == IntPtr.Zero)
                return false;

            var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo))
                return false;

            bool coversMonitor =
                windowRect.Left <= monitorInfo.Monitor.Left &&
                windowRect.Top <= monitorInfo.Monitor.Top &&
                windowRect.Right >= monitorInfo.Monitor.Right &&
                windowRect.Bottom >= monitorInfo.Monitor.Bottom;

            if (!coversMonitor)
                return false;

            long style = GetWindowLongPtr(gameWindow, GwlStyle).ToInt64();
            bool popup = (style & WsPopup) != 0;
            bool noCaption = (style & WsCaption) == 0;

            return popup && noCaption;
        }

        private const int GwlStyle = -16;
        private const int MonitorDefaultTonearest = 2;
        private const long WsPopup = 0x80000000;
        private const long WsCaption = 0x00C00000;

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags
        );

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    }
}
