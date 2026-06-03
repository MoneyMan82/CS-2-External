using ImGuiNET;
using System.Runtime.InteropServices;

namespace External_Aimbot
{
    public enum Hotkey
    {
        Mouse4 = 0x05,
        Mouse5 = 0x06,
        LeftAlt = 0x12,
        LeftShift = 0x10,
        LeftCtrl = 0x11,
        CapsLock = 0x14,
    }

    internal static class HotkeyInput
    {
        public static bool IsHeld(Hotkey key) =>
            (GetAsyncKeyState((int)key) & 0x8000) != 0;

        public static string Label(Hotkey key) => key switch
        {
            Hotkey.Mouse4 => "Mouse 4",
            Hotkey.Mouse5 => "Mouse 5",
            Hotkey.LeftAlt => "Left Alt",
            Hotkey.LeftShift => "Left Shift",
            Hotkey.LeftCtrl => "Left Ctrl",
            Hotkey.CapsLock => "Caps Lock",
            _ => key.ToString(),
        };

        public static void DrawSelector(string label, ref Hotkey hotkey)
        {
            ImGui.PushItemWidth(160f);
            if (ImGui.BeginCombo(label, Label(hotkey)))
            {
                foreach (Hotkey option in Enum.GetValues<Hotkey>())
                {
                    bool selected = hotkey == option;
                    if (ImGui.Selectable(Label(option), selected))
                        hotkey = option;
                }

                ImGui.EndCombo();
            }

            ImGui.PopItemWidth();
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
