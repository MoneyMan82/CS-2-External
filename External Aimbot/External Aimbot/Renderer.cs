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
        private const string OverlayTitle = "External Aimbot Overlay";

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
        public AimbotGameMode gameMode = AimbotGameMode.Casual;
        public float fov = 90f;
        public float smoothness = 1f;
        public float recoilStrength = 1f;
        public bool HasPredictionPoint;
        public Vector2 PredictionPoint;

        private readonly object _overlayLock = new();
        private List<OverlayLine> _overlayLines = [];

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

            ImGui.Begin("menu", ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.Checkbox("aimbot", ref aimbot);
            ImGui.Checkbox("recoil control", ref recoilControl);
            if (recoilControl)
                ImGui.SliderFloat("Recoil strength", ref recoilStrength, 0.5f, 2f);
            ImGui.Checkbox("recoil predictor", ref recoilPredictor);
            ImGui.Checkbox("visibility check", ref visibilityCheck);
            if (visibilityCheck)
                ImGui.Checkbox("map raytracing", ref mapRaytracing);
            ImGui.Checkbox("target lines", ref drawTargetLines);

            ImGui.Text("Game mode");
            if (ImGui.RadioButton("Casual", gameMode == AimbotGameMode.Casual))
                gameMode = AimbotGameMode.Casual;
            ImGui.SameLine();
            if (ImGui.RadioButton("Deathmatch", gameMode == AimbotGameMode.Deathmatch))
                gameMode = AimbotGameMode.Deathmatch;

            if (gameMode == AimbotGameMode.Casual)
                ImGui.Checkbox("aim on teammates, aswell", ref aimOnTeam);

            ImGui.SliderFloat("FOV", ref fov, 1f, 180f);
            ImGui.SliderFloat("Smoothness", ref smoothness, 1f, 20f);
            ImGui.Text("Hold mouse 4 or 5 to aim");

            DrawDisplayModeHelp();

            ImGui.End();

            DrawFovCircle();
            DrawTargetLines();
            DrawRecoilPrediction();
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
            if (!aimbot)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var displaySize = ImGui.GetIO().DisplaySize;
            var center = new Vector2(displaySize.X / 2f, displaySize.Y / 2f);
            float radius = displaySize.Y / 2f * (fov / 90f);

            drawList.AddCircle(
                center,
                radius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                64,
                1.5f
            );
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
