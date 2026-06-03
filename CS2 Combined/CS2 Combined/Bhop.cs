namespace External_Aimbot
{
    public readonly struct BhopDebug
    {
        public bool SpaceHeld { get; init; }
        public bool OnGround { get; init; }
        public int Flags { get; init; }
        public string Status { get; init; }
    }

    internal static class Bhop
    {
        private const int JumpPress = 65537;
        private const int JumpRelease = 256;
        private const int FlOnGround = 1;

        public static void Process(
            GameMemory mem,
            IntPtr localPawn,
            bool enabled,
            bool holdSpace,
            bool subtick,
            out BhopDebug debug)
        {
            bool spaceHeld = InputState.IsSpaceHeld();

            if (!enabled)
            {
                ReleaseJump(mem);
                debug = new BhopDebug { SpaceHeld = spaceHeld, Status = "Disabled" };
                return;
            }

            if (Offsets.dwJump == 0)
            {
                debug = new BhopDebug { SpaceHeld = spaceHeld, Status = "Jump offset missing" };
                return;
            }

            if (localPawn == IntPtr.Zero)
            {
                ReleaseJump(mem);
                debug = new BhopDebug { SpaceHeld = spaceHeld, Status = "Not in game" };
                return;
            }

            if (holdSpace && !spaceHeld)
            {
                ReleaseJump(mem);
                debug = new BhopDebug { SpaceHeld = false, Status = "Hold Space to bhop" };
                return;
            }

            int flags = mem.ReadInt(localPawn, Offsets.m_fFlags);
            bool onGround = (flags & FlOnGround) != 0;

            if (onGround)
                WriteFastJump(mem, subtick ? 2 : 1);
            else
                ReleaseJump(mem);

            debug = new BhopDebug
            {
                SpaceHeld = spaceHeld,
                OnGround = onGround,
                Flags = flags,
                Status = onGround
                    ? (subtick ? "Jumping (fast subtick)" : "Jumping")
                    : "In air",
            };
        }

        private static void WriteFastJump(GameMemory mem, int pulseCount)
        {
            pulseCount = Math.Clamp(pulseCount, 1, 3);

            for (int i = 0; i < pulseCount; i++)
            {
                mem.WriteInt(mem.Client, Offsets.dwJump, JumpPress);
                mem.WriteInt(mem.Client, Offsets.dwJump, JumpRelease);
                mem.WriteInt(mem.Client, Offsets.dwJump, JumpPress);
            }
        }

        private static void ReleaseJump(GameMemory mem)
        {
            if (Offsets.dwJump != 0)
                mem.WriteInt(mem.Client, Offsets.dwJump, JumpRelease);
        }
    }
}
