using System.Runtime.InteropServices;

namespace External_Aimbot
{
    internal static class InputState
    {
        private const int VkLButton = 0x01;
        private const int VkSpace = 0x20;

        private const int AttackPress = 65537;

        public static bool IsAttackHeld() =>
            (GetAsyncKeyState(VkLButton) & 0x8000) != 0;

        public static bool IsAttackHeld(GameMemory mem)
        {
            if ((GetAsyncKeyState(VkLButton) & 0x8000) != 0)
                return true;

            if (mem == null || Offsets.dwAttack == 0)
                return false;

            return mem.ReadInt(mem.Client, Offsets.dwAttack) == AttackPress;
        }

        public static bool IsSpaceHeld() =>
            (GetAsyncKeyState(VkSpace) & 0x8000) != 0;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
