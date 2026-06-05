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
        public bool recoilControl = false;
        public bool recoilPredictor = true;
        public float recoilPunchScale = 2f;
        public RecoilCompensationMode recoilMode = RecoilCompensationMode.PerWeapon;
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
        private WeaponContext _currentWeapon;

        public WeaponContext CurrentWeapon
        {
            get
            {
                lock (_overlayLock)
                    return _currentWeapon;
            }
            set
            {
                lock (_overlayLock)
                    _currentWeapon = value;
            }
        }

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

        private volatile bool _overlayBlockingInput;

        public bool IsOverlayBlockingInput => _overlayBlockingInput;

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

        public bool intelEnabled = false;
        public bool intelShowTrails = true;
        public float intelDecaySeconds = 8f;
        public float intelRadarSize = 112f;
        public float intelRadarRange = 2500f;
        public bool intelBottomLeft = true;
        public AimbotGameMode intelGameMode = AimbotGameMode.Casual;
        private bool _intelTeamPlaceholder;
        private IntelState _intelState;

        public readonly OverlaySettings Settings = new();
        private MenuFontChoice _appliedMenuFont = (MenuFontChoice)(-1);
        private float _appliedMenuFontSize = -1f;

        private readonly UtilityStore utilityStore = UtilityStore.CreateWithDefaults();
        public long utilitySessionStartTicks = Environment.TickCount64;
        private string _utilitySearch = "";
        private UtilityHudContext _utilityHudContext;

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
            LoadUiFont();
            return Task.CompletedTask;
        }

        private void LoadUiFont()
        {
            if (Settings.MenuFont == MenuFontChoice.Fredoka)
                TryLoadFredokaFont();
        }

        private void TryLoadFredokaFont()
        {
            string fontPath = Path.Combine(AppContext.BaseDirectory, "fonts", "FredokaOne-Regular.ttf");
            if (File.Exists(fontPath))
                ReplaceFont(fontPath, (int)Settings.MenuFontSize, FontGlyphRangeType.English);
        }

        private void ApplyMenuFontIfNeeded()
        {
            if (_appliedMenuFont == Settings.MenuFont &&
                MathF.Abs(_appliedMenuFontSize - Settings.MenuFontSize) < 0.01f)
            {
                return;
            }

            _appliedMenuFont = Settings.MenuFont;
            _appliedMenuFontSize = Settings.MenuFontSize;

            if (Settings.MenuFont == MenuFontChoice.Fredoka)
                TryLoadFredokaFont();
        }

        public int GetGameLoopSleepMs()
        {
            if (bhopEnabled)
                return bhopSubtick ? 1 : 2;

            return Settings.GameLoopSleepMs;
        }

        protected override void Render()
        {
            SyncOverlayToGameWindow();
            KeepOverlayOnTop();

            UiTheme.ApplyForSettings(Settings);
            ApplyMenuFontIfNeeded();

            if (Settings.ShowMainMenu)
                DrawMainMenuWindow();

            bool drawFov = showFovCircle || utilityStore.IsOn("pr_show_fov_circle");
            if (drawFov)
                DrawFovCircle();

            if (utilityStore.IsOn("pr_show_prediction") || (recoilPredictor && HasPredictionPoint))
                DrawRecoilPrediction();

            if (utilityStore.IsOn("pr_show_target_lines") || drawTargetLines)
                DrawTargetLines();

            DrawEsp();
            DrawOverlayRadar();
            DrawIntel();
            UtilityHud.Draw(utilityStore, _utilityHudContext);

            OverlaySettingsPanel.DrawFab(Settings);
            OverlaySettingsPanel.DrawWindow(Settings);

            ApplyPassThroughInput();
        }

        /// <summary>
        /// Only eat mouse clicks when the user is clicking/dragging UI — not when the cursor merely hovers a menu.
        /// ClickableTransparentOverlay uses io.WantCaptureMouse to toggle WS_EX_TRANSPARENT.
        /// </summary>
        private static void ApplyPassThroughInput()
        {
            ImGuiIOPtr io = ImGui.GetIO();

            bool clickingUi =
                ImGui.IsAnyItemActive() ||
                (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) &&
                 (ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
                  ImGui.IsMouseDown(ImGuiMouseButton.Right) ||
                  ImGui.IsMouseDown(ImGuiMouseButton.Middle)));

            io.WantCaptureMouse = clickingUi;
            io.WantCaptureKeyboard = ImGui.IsAnyItemActive();
        }

        public void SetUtilityHudContext(UtilityHudContext ctx) => _utilityHudContext = ctx;

        private void DrawMainMenuWindow()
        {
            var io = ImGui.GetIO();
            float defaultW = Math.Clamp(io.DisplaySize.X * Settings.MenuWidthFraction, 240f, 380f);
            float defaultH = Math.Clamp(io.DisplaySize.Y * Settings.MenuHeightFraction, 180f, 340f);
            var defaultSize = new Vector2(defaultW, defaultH);
            var margin = new Vector2(8f, 8f);

            if (Settings.PendingMenuLayoutReset)
            {
                OverlayLayout.SetInitialWindowPlacement(
                    Settings.MenuCorner,
                    margin,
                    defaultSize,
                    ImGuiCond.Always);
                Settings.PendingMenuLayoutReset = false;
                Settings.MenuLayoutInitialized = false;
            }
            else if (!Settings.MenuLayoutInitialized)
            {
                OverlayLayout.SetInitialWindowPlacement(
                    Settings.MenuCorner,
                    margin,
                    defaultSize,
                    ImGuiCond.FirstUseEver);
            }

            ImGui.SetNextWindowSizeConstraints(new Vector2(240f, 180f), new Vector2(560f, 520f));

            ImGui.Begin("CS2 Combined", ImGuiWindowFlags.NoCollapse);
            UpdateOverlayInputBlock();

            Vector2 windowSize = ImGui.GetWindowSize();
            if (windowSize.X >= 240f && windowSize.Y >= 180f)
            {
                Vector2 windowPos = ImGui.GetWindowPos();
                Settings.MenuPosX = windowPos.X;
                Settings.MenuPosY = windowPos.Y;
                Settings.MenuWidth = windowSize.X;
                Settings.MenuHeight = windowSize.Y;
                Settings.MenuLayoutInitialized = true;
            }

            ImGui.SetWindowFontScale(Settings.MenuFontScale);

            float settingsX = ImGui.GetWindowWidth() - 78f;
            ImGui.SetCursorPos(new Vector2(settingsX, 8f));
            OverlaySettingsPanel.DrawMenuShortcut(Settings);
            ImGui.SetCursorPos(new Vector2(10f, 8f));

            UiTheme.DrawMenuHeader();

            if (ImGui.BeginTabBar("MainTabs", ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
            {
                if (ImGui.BeginTabItem("Tools"))
                {
                    DrawToolsTab();
                    ImGui.EndTabItem();
                }

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

                if (ImGui.BeginTabItem("Intel"))
                {
                    DrawIntelTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            DrawDisplayModeHelp();
            ImGui.SetWindowFontScale(1f);
            UpdateOverlayInputBlock();
            ImGui.End();
        }

        private void DrawToolsTab()
        {
            int total = UtilityCatalog.All.Count;
            int enabled = utilityStore.EnabledCount;
            ImGui.TextColored(UiTheme.TextMuted, $"{enabled} / {total} on");
            ImGui.SameLine();
            if (ImGui.SmallButton("All off"))
            {
                foreach (UtilityEntry entry in UtilityCatalog.All)
                {
                    if (entry.Kind == UtilityKind.Toggle)
                        utilityStore.SetOn(entry.Id, false);
                }
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Reset"))
            {
                UtilityStore fresh = UtilityStore.CreateWithDefaults();
                foreach (UtilityEntry entry in UtilityCatalog.All)
                {
                    if (entry.Kind == UtilityKind.Toggle)
                        utilityStore.SetOn(entry.Id, fresh.IsOn(entry.Id));
                    else
                        utilityStore.SetFloat(entry.Id, fresh.GetFloat(entry.Id, entry.DefaultFloat));
                }
            }

            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##util_search", "Search...", ref _utilitySearch, 64);
            string filter = _utilitySearch.Trim();

            float scrollH = Math.Max(100f, ImGui.GetContentRegionAvail().Y);
            ImGui.BeginChild("##tools_scroll", new Vector2(-1f, scrollH), ImGuiChildFlags.None);

            foreach (string category in UtilityCatalog.Categories)
            {
                List<UtilityEntry> entries = UtilityCatalog.All
                    .Where(e => e.Category == category)
                    .Where(e => filter.Length == 0
                        || e.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || e.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (entries.Count == 0)
                    continue;

                if (!ImGui.CollapsingHeader($"{category} ({entries.Count})##util_cat", ImGuiTreeNodeFlags.DefaultOpen))
                    continue;

                foreach (UtilityEntry entry in entries)
                {
                    if (entry.Kind == UtilityKind.Toggle)
                    {
                        bool value = utilityStore.IsOn(entry.Id);
                        if (ImGui.Checkbox(entry.Label, ref value))
                        {
                            if (entry.Id.StartsWith("ch_style_", StringComparison.Ordinal))
                                utilityStore.SetCrosshairStyle(int.Parse(entry.Id["ch_style_".Length..]));
                            else
                                utilityStore.SetOn(entry.Id, value);
                        }
                    }
                    else
                    {
                        float value = utilityStore.GetFloat(entry.Id, entry.DefaultFloat);
                        if (ImGui.SliderFloat(entry.Label, ref value, entry.Min, entry.Max))
                            utilityStore.SetFloat(entry.Id, value);
                    }
                }
            }

            ImGui.EndChild();
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
                espName && !utilityStore.IsOn("st_hide_names"),
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
            ImGui.Checkbox("Landing dot", ref recoilPredictor);
            UiTheme.HintMuted("Spray landing marker (configure RCS in Misc)");

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

            UiTheme.BeginStatusPanel("trigger");
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

            UiTheme.BeginStatusPanel("bhop");
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
            float scrollH = Math.Max(100f, ImGui.GetContentRegionAvail().Y);
            ImGui.BeginChild("##misc_scroll", new Vector2(-1f, scrollH), ImGuiChildFlags.None);

            UiTheme.Section("Visual");
            ImGui.Checkbox("Anti flash", ref antiFlashEnabled);
            ImGui.Checkbox("FOV changer", ref miscFovChanger);
            if (miscFovChanger)
                ImGui.SliderInt("Game FOV", ref miscFovValue, 60, 140);

            UiTheme.Section("Combat");
            ImGui.Checkbox("Recoil control (RCS)", ref recoilControl);
            UiTheme.HintMuted("Per-gun spray compensation while shooting (Misc only, not aimbot)");
            if (recoilControl)
            {
                ImGui.SliderFloat("RCS strength", ref recoilStrength, 0.25f, 1.5f);
                ImGui.SliderFloat("Punch scale", ref recoilPunchScale, 1f, 2.5f);
                string modeLabel = recoilMode switch
                {
                    RecoilCompensationMode.Memory => "Memory only",
                    RecoilCompensationMode.PatternOnly => "Pattern table only",
                    RecoilCompensationMode.PerWeapon => "Per weapon (recommended)",
                    _ => recoilMode.ToString(),
                };
                if (ImGui.BeginCombo("RCS mode##misc_rcs_mode", modeLabel))
                {
                    if (ImGui.Selectable("Per weapon (recommended)", recoilMode == RecoilCompensationMode.PerWeapon))
                        recoilMode = RecoilCompensationMode.PerWeapon;
                    if (ImGui.Selectable("Memory only", recoilMode == RecoilCompensationMode.Memory))
                        recoilMode = RecoilCompensationMode.Memory;
                    if (ImGui.Selectable("Pattern table only", recoilMode == RecoilCompensationMode.PatternOnly))
                        recoilMode = RecoilCompensationMode.PatternOnly;
                    ImGui.EndCombo();
                }

                WeaponContext weapon = CurrentWeapon;
                if (weapon.IsValid)
                {
                    string table = WeaponRecoilPresets.GetPresetLabel(weapon.DefinitionIndex);
                    UiTheme.StatusRow("Weapon", weapon.Name, UiTheme.TextPrimary);
                    UiTheme.StatusRow("Spray", $"bullet {weapon.SprayIndex + 1} · idx {weapon.RecoilIndexFloat:F1}", UiTheme.TextInfo);
                    UiTheme.StatusRow("Pattern", table, weapon.HasRecoilPreset ? UiTheme.TextSuccess : UiTheme.TextMuted);
                }
            }

            ImGui.Checkbox("No recoil", ref miscNoRecoilEnabled);
            UiTheme.HintMuted("Zeros visual punch (separate from RCS above)");
            ImGui.Checkbox("All guns auto", ref miscAllGunsAutoEnabled);
            UiTheme.HintMuted("Semi-auto only. Hold LMB in CS2 (not on menu). Status shows Release/Press/Shot.");
            UiTheme.HintMuted("Enable it, click the game, then hold fire with Deagle/P2000/Glock/etc.");

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

            UiTheme.BeginStatusPanel("misc");
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
                AllGunsAutoDebug auto;
                lock (_overlayLock)
                {
                    auto = AllGunsAutoState;
                }

                UiTheme.StatusRow("Auto", auto.Status, UiTheme.TextPrimary);
                UiTheme.StatusRow("Phase", string.IsNullOrEmpty(auto.Phase) ? "Idle" : auto.Phase, UiTheme.TextInfo);
                UiTheme.StatusRow("Weapon", auto.ActiveWeapon, UiTheme.TextMuted);
                UiTheme.StatusRow("Shots", auto.ShotCount.ToString(),
                    auto.ShotCount > 0 ? UiTheme.TextSuccess : UiTheme.TextMuted);
            }

            MiscDebug misc = GetMiscDebugSnapshot();
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
                if (misc.SpectatorCount > 0 && misc.Spectators is { Length: > 0 })
                {
                    UiTheme.StatusRow("Spectators", misc.SpectatorCount.ToString(), UiTheme.TextWarning);
                    foreach (string name in misc.Spectators)
                        ImGui.BulletText(string.IsNullOrEmpty(name) ? "?" : name);
                }
                else
                {
                    UiTheme.StatusRow("Spectators", "none", UiTheme.TextMuted);
                }
            }

            UiTheme.EndStatusPanel();
            ImGui.EndChild();
        }

        private void DrawIntelTab()
        {
            float scrollH = Math.Max(100f, ImGui.GetContentRegionAvail().Y);
            ImGui.BeginChild("##intel_scroll", new Vector2(-1f, scrollH), ImGuiChildFlags.None);

            UiTheme.Section("Core");
            ImGui.Checkbox("Intel radar", ref intelEnabled);
            UiTheme.HintMuted("Remembers last seen enemy positions after they duck or break LOS.");

            UiTheme.Section("Radar");
            ImGui.Checkbox("Movement trails", ref intelShowTrails);
            ImGui.Checkbox("Bottom-left (off = top-right)", ref intelBottomLeft);
            ImGui.SliderFloat("Memory (sec)", ref intelDecaySeconds, 3f, 20f);
            ImGui.SliderFloat("Radar size", ref intelRadarSize, 80f, 180f);
            ImGui.SliderFloat("Radar range", ref intelRadarRange, 1000f, 5000f);

            UiTheme.Section("Targets");
            UiTheme.GameModeRadio("##intel", ref intelGameMode, showTeamOption: false, ref _intelTeamPlaceholder);

            UiTheme.BeginStatusPanel("intel");
            IntelState intel = GetIntelSnapshot();

            if (!intelEnabled)
            {
                UiTheme.StatusRow("State", "Off", UiTheme.TextMuted);
            }
            else
            {
                UiTheme.StatusRow("Live", intel.LiveCount.ToString(), UiTheme.TextDanger);
                UiTheme.StatusRow("Ghosts", intel.GhostCount.ToString(), UiTheme.TextWarning);
                UiTheme.StatusRow("Memory", $"{intelDecaySeconds:0.#}s", UiTheme.TextInfo);
            }

            if (intel.Blips is { Length: > 0 })
            {
                ImGui.Spacing();
                ImGui.TextColored(UiTheme.TextMuted, "TRACKING");
                foreach (IntelGhostBlip blip in intel.Blips.Take(6))
                {
                    string tag = blip.IsLive ? "live" : $"{blip.AgeSeconds:0.#}s ago";
                    Vector4 color = blip.IsLive ? UiTheme.TextDanger : UiTheme.TextWarning;
                    ImGui.TextColored(color, $"  ·  {tag}");
                }
            }

            UiTheme.EndStatusPanel();
            ImGui.EndChild();
        }

        public void SetIntelState(IntelState state)
        {
            lock (_overlayLock)
            {
                _intelState = state;
            }
        }

        private IntelState GetIntelSnapshot()
        {
            lock (_overlayLock)
                return _intelState;
        }

        private void DrawIntel()
        {
            if (!intelEnabled)
                return;

            IntelState state = GetIntelSnapshot();
            IntelOverlay.Draw(
                state,
                intelRadarSize,
                16f,
                intelShowTrails,
                intelBottomLeft);
        }

        public void SetAntiFlashDebug(AntiFlashDebug debug) => AntiFlashState = debug;

        public void SetNoRecoilDebug(NoRecoilDebug debug) => NoRecoilState = debug;

        public void SetAllGunsAutoDebug(AllGunsAutoDebug debug)
        {
            lock (_overlayLock)
            {
                AllGunsAutoState = debug;
            }
        }

        private void UpdateOverlayInputBlock()
        {
            // Only block game features while interacting with a widget (checkbox, slider, etc.).
            // WantCaptureMouse / AnyWindow hover is true over the full-screen overlay and would block forever.
            _overlayBlockingInput = ImGui.IsAnyItemActive();
        }

        public void SetMiscDebug(MiscDebug debug)
        {
            lock (_overlayLock)
            {
                MiscState = debug;
            }
        }

        private MiscDebug GetMiscDebugSnapshot()
        {
            lock (_overlayLock)
            {
                MiscDebug misc = MiscState;
                if (misc.Spectators == null || misc.Spectators.Length == 0)
                    return misc;

                return misc with { Spectators = (string[])misc.Spectators.Clone() };
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

        private void DrawMapRaytracingStatus(bool alwaysShow = false)
        {
            if (!alwaysShow && !mapRaytracing)
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
            var center = new Vector2(
                MathF.Floor(displaySize.X * 0.5f) + 0.5f,
                MathF.Floor(displaySize.Y * 0.5f) + 0.5f);

            float gameFov = MiscState.CurrentGameFov > 0
                ? MiscState.CurrentGameFov
                : miscFovChanger ? miscFovValue : 90f;

            float radius = Calculate.GetFovCircleRadius(fov, displaySize.X, displaySize.Y, gameFov);
            if (radius <= 1f)
                return;

            int segments = Math.Clamp((int)MathF.Ceiling(2f * MathF.PI * radius), 384, 2048);
            Vector4 accent = UiTheme.Accent;

            DrawCircleOutline(drawList, center, radius, segments, ToColor(accent, 0.18f), 3f);
            DrawCircleOutline(drawList, center, radius, segments, ToColor(Vector4.Zero, 0.55f), 2f);
            DrawCircleOutline(drawList, center, radius, segments, ToColor(accent, 1f), 1.25f);

            drawList.AddCircleFilled(center, 1.5f, ToColor(accent, 1f), 16);
        }

        private static void DrawCircleOutline(
            ImDrawListPtr drawList,
            Vector2 center,
            float radius,
            int segments,
            uint color,
            float thickness)
        {
            drawList.PathClear();
            drawList.PathArcTo(center, radius, 0f, MathF.PI * 2f, segments);
            drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
        }

        private static uint ToColor(Vector4 rgb, float alpha) =>
            ImGui.ColorConvertFloat4ToU32(new Vector4(rgb.X, rgb.Y, rgb.Z, alpha));

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
