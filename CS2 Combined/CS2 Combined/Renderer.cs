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

            ImGui.Begin("CS2 Combined", ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("MainTabs"))
            {
                if (ImGui.BeginTabItem("Aimbot"))
                {
                    DrawAimbotTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Trigger Bot"))
                {
                    DrawTriggerBotTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("ESP"))
                {
                    DrawEspTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Auto Bhop"))
                {
                    DrawBhopTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Misc"))
                {
                    DrawMiscTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Skin Changer"))
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
            ImGui.Checkbox("enable ESP", ref espEnabled);
            ImGui.Checkbox("box ESP", ref espBox);
            ImGui.Checkbox("bone ESP", ref espBones);
            ImGui.Checkbox("name ESP", ref espName);
            ImGui.Checkbox("health ESP", ref espHealth);
            ImGui.Checkbox("weapon ESP", ref espWeapon);
            ImGui.Checkbox("distance ESP", ref espDistance);
            ImGui.Checkbox("snaplines", ref espSnaplines);
            ImGui.Checkbox("head dot", ref espHeadDot);
            ImGui.Checkbox("show armor", ref espArmor);
            ImGui.Checkbox("color by visibility", ref espColorByVisibility);

            ImGui.Text("Game mode");
            if (ImGui.RadioButton("Casual##esp", espGameMode == AimbotGameMode.Casual))
                espGameMode = AimbotGameMode.Casual;
            ImGui.SameLine();
            if (ImGui.RadioButton("Deathmatch##esp", espGameMode == AimbotGameMode.Deathmatch))
                espGameMode = AimbotGameMode.Deathmatch;

            if (espGameMode == AimbotGameMode.Casual)
                ImGui.Checkbox("show teammates", ref espShowTeam);
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
            ImGui.Checkbox("aimbot", ref aimbot);
            ImGui.Checkbox("recoil control", ref recoilControl);
            if (recoilControl)
                ImGui.SliderFloat("Recoil strength", ref recoilStrength, 0.5f, 2f);
            ImGui.Checkbox("recoil predictor", ref recoilPredictor);

            var weapon = CurrentWeapon;
            if (weapon.IsValid)
            {
                string preset = weapon.HasRecoilPreset ? "preset loaded" : "generic fallback";
                ImGui.Text($"Weapon: {weapon.Name}  |  {weapon.FireModeLabel}  |  Shots: {weapon.ShotsFired}  |  {preset}");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Weapon: none");
            }

            ImGui.Checkbox("visibility check", ref visibilityCheck);
            if (visibilityCheck)
            {
                ImGui.Checkbox("map raytracing", ref mapRaytracing);
                DrawMapRaytracingStatus();
            }
            ImGui.Checkbox("target lines", ref drawTargetLines);

            ImGui.Text("Game mode");
            if (ImGui.RadioButton("Casual", gameMode == AimbotGameMode.Casual))
                gameMode = AimbotGameMode.Casual;
            ImGui.SameLine();
            if (ImGui.RadioButton("Deathmatch", gameMode == AimbotGameMode.Deathmatch))
                gameMode = AimbotGameMode.Deathmatch;

            if (gameMode == AimbotGameMode.Casual)
                ImGui.Checkbox("aim on teammates, aswell", ref aimOnTeam);

            ImGui.SliderFloat("Aim FOV", ref fov, 1f, 90f);
            ImGui.Checkbox("show FOV circle", ref showFovCircle);
            ImGui.TextColored(new Vector4(0.75f, 0.75f, 0.75f, 1f), "Circle = lock range. Lower FOV = tighter aim.");
            ImGui.SliderFloat("Smoothness", ref smoothness, 1f, 20f);
            HotkeyInput.DrawSelector("Aim hotkey", ref aimbotHotkey);
            ImGui.Text($"Hold {HotkeyInput.Label(aimbotHotkey)} to aim");
        }

        private void DrawTriggerBotTab()
        {
            ImGui.Checkbox("trigger bot", ref triggerBot);

            ImGui.Text("Game mode");
            if (ImGui.RadioButton("Casual##trigger", triggerGameMode == AimbotGameMode.Casual))
                triggerGameMode = AimbotGameMode.Casual;
            ImGui.SameLine();
            if (ImGui.RadioButton("Deathmatch##trigger", triggerGameMode == AimbotGameMode.Deathmatch))
                triggerGameMode = AimbotGameMode.Deathmatch;

            ImGui.SliderInt("Click delay (ms)", ref triggerClickDelayMs, 1, 50);
            ImGui.SliderInt("Shot cooldown (ms)", ref triggerCooldownMs, 10, 500);
            HotkeyInput.DrawSelector("Trigger hotkey", ref triggerHotkey);
            ImGui.Text($"Hold {HotkeyInput.Label(triggerHotkey)} to trigger");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Status");

            var debug = TriggerDebug;
            ImGui.Text($"Hotkey: {(debug.HotkeyHeld ? "HELD" : "not held")}");
            ImGui.Text($"Crosshair entity ID: {debug.EntityId}");
            if (debug.EntityId > 0)
            {
                ImGui.Text($"Target team: {debug.TargetTeam}, your team: {debug.LocalTeam}");
                ImGui.Text($"Target HP: {debug.TargetHealth}");
            }

            ImGui.TextColored(
                debug.Status == "Shot fired"
                    ? new Vector4(0.2f, 1f, 0.2f, 1f)
                    : new Vector4(0.8f, 0.8f, 0.8f, 1f),
                debug.Status
            );
        }

        public void SetTriggerDebug(TriggerBotDebug debug) => TriggerDebug = debug;

        private void DrawBhopTab()
        {
            ImGui.Checkbox("auto bhop", ref bhopEnabled);
            ImGui.Checkbox("hold Space to bhop", ref bhopHoldSpace);
            ImGui.Checkbox("subtick bhop (fastest)", ref bhopSubtick);

            if (!bhopHoldSpace)
                ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f), "Jumps automatically when enabled");

            if (bhopSubtick)
                ImGui.TextColored(new Vector4(0.4f, 0.85f, 1f, 1f), "Fast subtick pulses + 1ms update rate");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Status");

            var debug = BhopState;
            ImGui.Text($"Space: {(debug.SpaceHeld ? "HELD" : "not held")}");
            ImGui.Text($"On ground: {(debug.OnGround ? "yes" : "no")}");
            ImGui.Text($"Flags: 0x{debug.Flags:X}");

            ImGui.TextColored(
                debug.Status is "Jumping" or "Jumping (subtick)" or "In air"
                    ? new Vector4(0.2f, 1f, 0.2f, 1f)
                    : new Vector4(0.8f, 0.8f, 0.8f, 1f),
                debug.Status
            );
        }

        public void SetBhopDebug(BhopDebug debug) => BhopState = debug;

        private void DrawMiscTab()
        {
            ImGui.Text("Visual");
            ImGui.Checkbox("anti flash", ref antiFlashEnabled);
            ImGui.Checkbox("FOV changer", ref miscFovChanger);
            if (miscFovChanger)
                ImGui.SliderInt("Game FOV", ref miscFovValue, 60, 140);

            ImGui.Spacing();
            ImGui.Text("Combat");
            ImGui.Checkbox("no recoil", ref miscNoRecoilEnabled);
            ImGui.TextColored(new Vector4(0.75f, 0.75f, 0.75f, 1f), "Removes visual recoil while shooting (no aimbot needed)");
            ImGui.Checkbox("all guns auto", ref miscAllGunsAutoEnabled);
            ImGui.TextColored(new Vector4(0.75f, 0.75f, 0.75f, 1f), "Hold LMB - only semi-auto guns (Deagle, AWP, pistols) fire repeatedly. Full-auto guns are unchanged.");

            ImGui.Spacing();
            ImGui.Text("Radar");
            ImGui.Checkbox("radar reveal", ref miscRadarReveal);
            ImGui.TextColored(new Vector4(0.75f, 0.75f, 0.75f, 1f), "Shows enemies on the in-game minimap");
            ImGui.Checkbox("overlay radar", ref miscOverlayRadar);
            if (miscOverlayRadar)
            {
                ImGui.Checkbox("show teammates##radar", ref miscRadarShowTeam);
                ImGui.SliderFloat("radar size", ref miscRadarSize, 80f, 220f);
                ImGui.SliderFloat("radar range", ref miscRadarRange, 1000f, 5000f);
            }

            ImGui.Spacing();
            ImGui.Text("Info");
            ImGui.Checkbox("bomb timer", ref miscBombTimer);
            ImGui.Checkbox("spectator list", ref miscSpectatorList);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Status");

            var flash = AntiFlashState;
            ImGui.Text($"Flash alpha: {flash.FlashAlpha:F0}  |  {flash.Status}");

            if (miscNoRecoilEnabled)
            {
                var recoil = NoRecoilState;
                ImGui.Text($"No recoil: {recoil.Status}");
            }

            if (miscAllGunsAutoEnabled)
            {
                var auto = AllGunsAutoState;
                ImGui.Text($"All guns auto: {auto.Status} ({auto.ActiveWeapon})");
            }

            var misc = MiscState;
            if (miscBombTimer)
            {
                if (misc.BombPlanted)
                {
                    ImGui.TextColored(new Vector4(1f, 0.45f, 0.2f, 1f),
                        $"Bomb site {misc.BombSite}: {misc.BombTimeLeft:0.0}s");
                    if (misc.BombBeingDefused)
                        ImGui.TextColored(new Vector4(0.2f, 1f, 0.35f, 1f),
                            $"Defusing: {misc.DefuseTimeLeft:0.0}s");
                }
                else
                {
                    ImGui.Text("Bomb: not planted");
                }
            }

            if (miscRadarReveal)
                ImGui.Text($"Radar reveal: {misc.RadarRevealedCount} spotted this tick");

            if (miscFovChanger && misc.AppliedFov > 0)
                ImGui.Text($"FOV: {misc.AppliedFov}");

            if (miscSpectatorList)
            {
                if (misc.SpectatorCount > 0 && misc.Spectators != null)
                {
                    ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f),
                        $"Spectators ({misc.SpectatorCount}):");
                    foreach (string name in misc.Spectators)
                        ImGui.BulletText(name);
                }
                else
                {
                    ImGui.Text("Spectators: none");
                }
            }
        }

        public void SetAntiFlashDebug(AntiFlashDebug debug) => AntiFlashState = debug;

        public void SetNoRecoilDebug(NoRecoilDebug debug) => NoRecoilState = debug;

        public void SetAllGunsAutoDebug(AllGunsAutoDebug debug) => AllGunsAutoState = debug;

        public void SetMiscDebug(MiscDebug debug) => MiscState = debug;

        public IReadOnlyDictionary<int, SkinConfig> GetSkinConfigs() => _skinConfigs;

        public void SetSkinChangerDebug(SkinChangerDebug debug) => SkinChangerState = debug;

        private void DrawSkinChangerTab()
        {
            ImGui.Checkbox("enable skin changer", ref skinChangerEnabled);
            ImGui.TextColored(new Vector4(0.75f, 0.75f, 0.75f, 1f),
                "Client-side only. Applies to weapons in your current loadout.");

            ImGui.Spacing();
            ImGui.Text("Configure weapon skin");

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

            if (ImGui.Button("Save for weapon"))
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
            if (ImGui.Button("Clear saved"))
                _skinConfigs.Remove(skinEditorWeaponDefIndex);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Saved skins");
            if (_skinConfigs.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "None saved yet");
            }
            else
            {
                foreach (var entry in _skinConfigs)
                {
                    ImGui.BulletText(
                        $"{WeaponCatalog.GetName(entry.Key)} -> kit {entry.Value.PaintKit}, wear {entry.Value.Wear:0.###}");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Loadout");

            var debug = SkinChangerState;
            ImGui.Text($"Status: {debug.Status}");

            if (debug.Loadout == null || debug.Loadout.Length == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Join a match to detect your weapons");
            }
            else
            {
                foreach (LoadoutWeaponInfo weapon in debug.Loadout)
                {
                    string tag = weapon.Configured ? "configured" : "default";
                    ImGui.BulletText($"{weapon.Name} (kit {weapon.CurrentPaintKit}) - {tag}");
                }
            }
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
                ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1f), "Map: unknown (not in a match yet)");
                return;
            }

            if (MapCollision.IsLoading)
            {
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), $"Map: {map} | Mesh: loading...");
                return;
            }

            if (MapCollision.IsLoaded)
            {
                ImGui.TextColored(
                    new Vector4(0.2f, 1f, 0.35f, 1f),
                    $"Map: {map} | Mesh: loaded ({MapCollision.TriangleCount:N0} tris)");
                return;
            }

            if (!mapRaytracing)
            {
                ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f), $"Map: {map} | Using spotted fallback (less accurate)");
                return;
            }

            ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), $"Map: {map} | Mesh: MISSING");
            ImGui.TextWrapped($"Put maps\\{map}.tri next to the exe. Run External Aimbot\\tools\\extract-maps.ps1 to generate meshes.");
            ImGui.TextWrapped("Wall check is OFF until mesh loads — lines stay red and aimbot won't lock through walls.");
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
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "CS2 window not found.");
                return;
            }

            if (IsExclusiveFullscreen(gameWindow))
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Use Borderless Windowed in CS2 video settings.");
            }
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
