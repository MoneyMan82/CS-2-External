namespace External_Aimbot
{
    internal static class PatternScanner
    {
        /// <summary>IDA-style pattern, e.g. "48 83 EC ? E8 ? ? ? ? 48 85 C0".</summary>
        public static IntPtr Scan(GameMemory mem, IntPtr moduleBase, int moduleSize, string pattern)
        {
            if (moduleBase == IntPtr.Zero || moduleSize <= 0 || string.IsNullOrWhiteSpace(pattern))
                return IntPtr.Zero;

            byte?[] tokens = ParsePattern(pattern);
            if (tokens.Length == 0)
                return IntPtr.Zero;

            const int chunkSize = 0x100000;
            var buffer = new byte[chunkSize + tokens.Length];
            int overlap = tokens.Length - 1;

            for (int offset = 0; offset < moduleSize; offset += chunkSize)
            {
                int readSize = Math.Min(chunkSize + overlap, moduleSize - offset);
                if (!mem.TryReadBytes(moduleBase + offset, buffer.AsSpan(0, readSize)))
                    continue;

                for (int i = 0; i <= readSize - tokens.Length; i++)
                {
                    if (!Match(buffer, i, tokens))
                        continue;

                    return moduleBase + offset + i;
                }
            }

            return IntPtr.Zero;
        }

        private static bool Match(byte[] data, int start, byte?[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                byte? token = tokens[i];
                if (token.HasValue && data[start + i] != token.Value)
                    return false;
            }

            return true;
        }

        private static byte?[] ParsePattern(string pattern)
        {
            string[] parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tokens = new byte?[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                tokens[i] = part is "?" or "??" ? null : Convert.ToByte(part, 16);
            }

            return tokens;
        }
    }
}
