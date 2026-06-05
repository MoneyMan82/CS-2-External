namespace External_Aimbot
{
    internal readonly struct SkinFunctionAddresses
    {
        public IntPtr RegenerateWeaponSkins { get; init; }
        public IntPtr UpdateSubClass { get; init; }
        public string RegenerateSource { get; init; }
    }

    internal static class SkinFunctionResolver
    {
        private static readonly string[] RegeneratePatterns =
        [
            "48 83 EC ? E8 ? ? ? ? 48 85 C0 0F 84 ? ? ? ? 48 8B 10",
            "48 83 EC 28 48 8B 05",
            "48 83 EC 20 48 8B 05",
            "48 83 EC 30 48 8B 05",
        ];

        private const string UpdateSubClassPattern =
            "40 53 48 83 EC ? 48 8B D9 4C 8B C2 48 8B 0D ? ? ? ? 48 8D 54 24";

        private static SkinFunctionAddresses _cached;
        private static bool _scanDone;

        public static void ResetCache() => _scanDone = false;

        public static SkinFunctionAddresses Resolve(GameMemory mem)
        {
            if (_scanDone)
                return _cached;

            _scanDone = true;
            IntPtr regenerate = IntPtr.Zero;
            string source = "";

            foreach (string pattern in RegeneratePatterns)
            {
                IntPtr hit = mem.ScanClient(pattern);
                if (hit == IntPtr.Zero)
                    continue;

                IntPtr callee = TryResolveE8CallTarget(mem, hit);
                if (callee != IntPtr.Zero)
                {
                    regenerate = callee;
                    source = $"E8 target ({pattern[..Math.Min(12, pattern.Length)]}…)";
                    break;
                }

                regenerate = hit;
                source = $"direct ({pattern[..Math.Min(12, pattern.Length)]}…)";
                break;
            }

            IntPtr updateSubClass = mem.ScanClient(UpdateSubClassPattern);
            _cached = new SkinFunctionAddresses
            {
                RegenerateWeaponSkins = regenerate,
                UpdateSubClass = updateSubClass,
                RegenerateSource = source,
            };

            return _cached;
        }

        private static IntPtr TryResolveE8CallTarget(GameMemory mem, IntPtr hit)
        {
            for (int i = 0; i < 16; i++)
            {
                if (mem.ReadByte(hit + i) != 0xE8)
                    continue;

                int rel = mem.ReadInt(hit, i + 1);
                return hit + i + 5 + rel;
            }

            return IntPtr.Zero;
        }
    }
}
