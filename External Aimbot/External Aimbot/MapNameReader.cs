namespace External_Aimbot
{
    internal static class MapNameReader
    {
        private const int MapNameOffset = 0x180;

        public static string ReadCurrentMap(GameMemory mem)
        {
            IntPtr globalVars = mem.ReadPtr(mem.Client, Offsets.dwGlobalVars);
            if (globalVars == IntPtr.Zero)
                return "";

            IntPtr mapNamePtr = mem.ReadPtr(globalVars, MapNameOffset);
            if (mapNamePtr == IntPtr.Zero)
                return "";

            return Normalize(mem.ReadString(mapNamePtr));
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            raw = raw.Trim().Trim('<', '>', '"', '\'');
            raw = raw.Replace('\\', '/');

            if (raw.Contains('/'))
                raw = raw.Split('/').Last();

            return Path.GetFileNameWithoutExtension(raw);
        }
    }
}
