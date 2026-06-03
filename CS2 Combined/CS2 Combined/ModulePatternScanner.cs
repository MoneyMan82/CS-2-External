using System.Diagnostics;

namespace External_Aimbot
{
    internal static class ModulePatternScanner
    {
        public static IntPtr FindInModule(GameMemory mem, string moduleName, string pattern)
        {
            Process? process = Process.GetProcessesByName("cs2").FirstOrDefault();
            if (process == null || process.HasExited)
                return IntPtr.Zero;

            ProcessModule? module = process.Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

            if (module == null)
                return IntPtr.Zero;

            byte[] moduleBytes = ReadModuleBytes(mem, module.BaseAddress, module.ModuleMemorySize);
            if (moduleBytes.Length == 0)
                return IntPtr.Zero;

            byte?[] signature = ParsePattern(pattern);
            int index = IndexOfPattern(moduleBytes, signature);
            return index < 0 ? IntPtr.Zero : module.BaseAddress + index;
        }

        private static byte[] ReadModuleBytes(GameMemory mem, IntPtr baseAddress, int size)
        {
            const int chunkSize = 1024 * 1024;
            var buffer = new byte[size];
            int offset = 0;

            while (offset < size)
            {
                int readSize = Math.Min(chunkSize, size - offset);
                if (!mem.TryReadBytes(baseAddress + offset, buffer.AsSpan(offset, readSize)))
                    return [];

                offset += readSize;
            }

            return buffer;
        }

        private static byte?[] ParsePattern(string pattern)
        {
            string[] parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte?[parts.Length];

            for (int i = 0; i < parts.Length; i++)
                bytes[i] = parts[i] == "?" ? null : Convert.ToByte(parts[i], 16);

            return bytes;
        }

        private static int IndexOfPattern(byte[] data, byte?[] signature)
        {
            if (signature.Length == 0 || data.Length < signature.Length)
                return -1;

            for (int i = 0; i <= data.Length - signature.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < signature.Length; j++)
                {
                    if (signature[j].HasValue && data[i + j] != signature[j]!.Value)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }
    }
}
