using System.Reflection;

namespace External_Aimbot
{
    public enum OverlayCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    public enum MenuDensity
    {
        Compact,
        Comfortable,
    }

    public enum MenuFontChoice
    {
        Default,
        Fredoka,
    }

    public enum AccentPreset
    {
        Teal,
        Blue,
        Violet,
    }

    public enum PerformancePreset
    {
        Fast,
        Balanced,
        LowCpu,
    }

    public sealed class OverlaySettings
    {
        public bool ShowSettingsButton = true;
        public OverlayCorner SettingsButtonCorner = OverlayCorner.BottomLeft;
        public bool SettingsPopupOpen;

        public bool ShowMainMenu = true;
        public OverlayCorner MenuCorner = OverlayCorner.TopLeft;
        public MenuDensity MenuDensity = MenuDensity.Compact;
        public MenuFontChoice MenuFont = MenuFontChoice.Fredoka;
        public float MenuFontSize = 14f;
        public float MenuFontScale = 1f;
        public float MenuWidthFraction = 0.24f;
        public float MenuHeightFraction = 0.32f;
        public AccentPreset Accent = AccentPreset.Teal;
        public PerformancePreset Performance = PerformancePreset.Balanced;

        public bool MenuLayoutInitialized;
        public bool PendingMenuLayoutReset;
        public float MenuPosX;
        public float MenuPosY;
        public float MenuWidth;
        public float MenuHeight;

        public int GameLoopSleepMs => Performance switch
        {
            PerformancePreset.Fast => 2,
            PerformancePreset.LowCpu => 10,
            _ => 5,
        };

        public static string VersionLabel
        {
            get
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Version? ver = asm.GetName().Version;
                string verText = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
                return $"CS2 Combined v{verText}";
            }
        }

        public static string RuntimeLabel =>
            $".NET {Environment.Version} · {Environment.OSVersion.Platform}";
    }
}
