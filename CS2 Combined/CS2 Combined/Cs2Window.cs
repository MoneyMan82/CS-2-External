using System.Diagnostics;
using System.Runtime.InteropServices;

namespace External_Aimbot
{
    internal static class Cs2Window
    {
        public static IntPtr FindHandle()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                try
                {
                    using Process process = Process.GetProcessById((int)pid);
                    if (process.ProcessName.Equals("cs2", StringComparison.OrdinalIgnoreCase))
                    {
                        found = hwnd;
                        return false;
                    }
                }
                catch
                {
                    // Process exited between enum and open.
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        public static uint GetWindowThreadId(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out uint threadId);
            return threadId;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }
}
