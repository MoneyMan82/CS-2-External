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
            int jumpValue = onGround ? JumpPress : JumpRelease;
            mem.WriteInt(mem.Client, Offsets.dwJump, jumpValue);

            debug = new BhopDebug
            {
                SpaceHeld = spaceHeld,
                OnGround = onGround,
                Flags = flags,
                Status = onGround ? "Jumping" : "In air",
            };
        }

        private static void ReleaseJump(GameMemory mem)
        {
            if (Offsets.dwJump != 0)
                mem.WriteInt(mem.Client, Offsets.dwJump, JumpRelease);
        }
    }
}
