using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace External_Aimbot
{
    internal sealed class GameMemory : IDisposable
    {
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessVmWrite = 0x0020;
        private const uint ProcessQueryInformation = 0x0400;

        private readonly IntPtr _handle;
        private Process? _process;

        public IntPtr Client { get; private set; }
        public IntPtr Engine { get; private set; }

        public GameMemory()
        {
            _process = Process.GetProcessesByName("cs2").FirstOrDefault()
                ?? throw new InvalidOperationException("cs2.exe is not running.");

            _handle = OpenProcess(ProcessVmRead | ProcessVmWrite | ProcessQueryInformation, false, _process.Id);
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to open cs2 process. Try running as administrator.");

            RefreshModuleBases();

            if (Client == IntPtr.Zero)
                throw new InvalidOperationException("client.dll not found.");
        }

        public bool IsAttached =>
            _process != null && !_process.HasExited && _handle != IntPtr.Zero;

        public bool TryRefreshAttachment()
        {
            if (_process != null && !_process.HasExited && Client != IntPtr.Zero)
            {
                IntPtr entitySystem = ReadPtr(Client, Offsets.dwGameEntitySystem);
                if (entitySystem != IntPtr.Zero)
                    return true;
            }

            _process = Process.GetProcessesByName("cs2").FirstOrDefault();
            if (_process == null || _process.HasExited)
                return false;

            RefreshModuleBases();
            return Client != IntPtr.Zero;
        }

        private void RefreshModuleBases()
        {
            if (_process == null || _process.HasExited)
                return;

            Client = GetModuleBase(_process, "client.dll");
            Engine = GetModuleBase(_process, "engine2.dll");
        }

        public IntPtr ReadPtr(IntPtr address)
        {
            if (ReadProcessMemory(_handle, address, _ptrBuffer, 8, out int read) && read == 8)
                return new IntPtr(BitConverter.ToInt64(_ptrBuffer, 0));

            return IntPtr.Zero;
        }

        public IntPtr ReadPtr(IntPtr baseAddress, int offset) => ReadPtr(baseAddress + offset);

        public int ReadInt(IntPtr baseAddress, int offset)
        {
            if (ReadProcessMemory(_handle, baseAddress + offset, _intBuffer, 4, out int read) && read == 4)
                return BitConverter.ToInt32(_intBuffer, 0);

            return 0;
        }

        public float ReadFloat(IntPtr baseAddress, int offset)
        {
            if (ReadProcessMemory(_handle, baseAddress + offset, _intBuffer, 4, out int read) && read == 4)
                return BitConverter.ToSingle(_intBuffer, 0);

            return 0f;
        }

        public byte ReadByte(IntPtr address)
        {
            if (ReadProcessMemory(_handle, address, _boolBuffer, 1, out int read) && read == 1)
                return _boolBuffer[0];

            return 0;
        }

        public string ReadString(IntPtr address, int maxLength = 128)
        {
            if (address == IntPtr.Zero || maxLength <= 0)
                return "";

            int size = Math.Min(maxLength, 256);
            byte[] buffer = size <= 16 ? _structBuffer : new byte[size];
            if (!ReadProcessMemory(_handle, address, buffer, size, out int read) || read == 0)
                return "";

            int length = Array.IndexOf(buffer, (byte)0, 0, read);
            if (length < 0)
                length = read;

            return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
        }

        public bool ReadBool(IntPtr baseAddress, int offset) =>
            ReadProcessMemory(_handle, baseAddress + offset, _boolBuffer, 1, out int read) && read == 1 && _boolBuffer[0] != 0;

        public Vector3 ReadVec(IntPtr baseAddress, int offset)
        {
            if (ReadProcessMemory(_handle, baseAddress + offset, _vecBuffer, 12, out int read) && read == 12)
                return new Vector3(
                    BitConverter.ToSingle(_vecBuffer, 0),
                    BitConverter.ToSingle(_vecBuffer, 4),
                    BitConverter.ToSingle(_vecBuffer, 8));

            return Vector3.Zero;
        }

        public bool TryReadStruct<T>(IntPtr address, out T value) where T : struct
        {
            value = default;
            int size = Marshal.SizeOf<T>();
            if (size <= 0 || size > 256)
                return false;

            byte[] buffer = size <= 16 ? _structBuffer : new byte[size];
            if (!ReadProcessMemory(_handle, address, buffer, size, out int read) || read != size)
                return false;

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                value = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
                return true;
            }
            finally
            {
                handle.Free();
            }
        }

        public float[] ReadMatrix(IntPtr address)
        {
            var matrix = new float[16];
            byte[] buffer = new byte[64];
            if (!ReadProcessMemory(_handle, address, buffer, 64, out int read) || read != 64)
                return matrix;

            Buffer.BlockCopy(buffer, 0, matrix, 0, 64);
            return matrix;
        }

        public void WriteVec(IntPtr baseAddress, int offset, Vector3 value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value.X), 0, _vecBuffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(value.Y), 0, _vecBuffer, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(value.Z), 0, _vecBuffer, 8, 4);
            WriteProcessMemory(_handle, baseAddress + offset, _vecBuffer, 12, out _);
        }

        public void WriteInt(IntPtr baseAddress, int offset, int value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _intBuffer, 0, 4);
            WriteProcessMemory(_handle, baseAddress + offset, _intBuffer, 4, out _);
        }

        public void WriteFloat(IntPtr baseAddress, int offset, float value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _intBuffer, 0, 4);
            WriteProcessMemory(_handle, baseAddress + offset, _intBuffer, 4, out _);
        }

        public void WriteBool(IntPtr baseAddress, int offset, bool value)
        {
            _boolBuffer[0] = value ? (byte)1 : (byte)0;
            WriteProcessMemory(_handle, baseAddress + offset, _boolBuffer, 1, out _);
        }

        public void WriteULong(IntPtr baseAddress, int offset, ulong value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _ptrBuffer, 0, 8);
            WriteProcessMemory(_handle, baseAddress + offset, _ptrBuffer, 8, out _);
        }

        public bool TryReadBytes(IntPtr address, Span<byte> destination)
        {
            if (address == IntPtr.Zero || destination.IsEmpty)
                return false;

            byte[] buffer = destination.Length <= 256 ? _structBuffer : new byte[destination.Length];
            if (!ReadProcessMemory(_handle, address, buffer, destination.Length, out int read) || read != destination.Length)
                return false;

            buffer.AsSpan(0, destination.Length).CopyTo(destination);
            return true;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
                CloseHandle(_handle);
        }

        private static IntPtr GetModuleBase(Process process, string moduleName)
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    return module.BaseAddress;
            }

            return IntPtr.Zero;
        }

        private readonly byte[] _ptrBuffer = new byte[8];
        private readonly byte[] _intBuffer = new byte[4];
        private readonly byte[] _boolBuffer = new byte[1];
        private readonly byte[] _vecBuffer = new byte[12];
        private readonly byte[] _structBuffer = new byte[16];

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr process,
            IntPtr address,
            byte[] buffer,
            int size,
            out int bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr process,
            IntPtr address,
            byte[] buffer,
            int size,
            out int bytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
