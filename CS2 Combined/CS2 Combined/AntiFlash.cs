namespace External_Aimbot
{
    public readonly struct AntiFlashDebug
    {
        public float FlashAlpha { get; init; }
        public string Status { get; init; }
    }

    internal static class AntiFlash
    {
        public static void Process(
            GameMemory mem,
            IntPtr localPawn,
            bool enabled,
            out AntiFlashDebug debug)
        {
            if (!enabled)
            {
                debug = new AntiFlashDebug { Status = "Disabled" };
                return;
            }

            if (Offsets.m_flFlashMaxAlpha == 0)
            {
                debug = new AntiFlashDebug { Status = "Flash offsets missing" };
                return;
            }

            if (localPawn == IntPtr.Zero)
            {
                debug = new AntiFlashDebug { Status = "Not in game" };
                return;
            }

            float alpha = mem.ReadFloat(localPawn, Offsets.m_flFlashMaxAlpha);

            if (alpha > 0f)
            {
                mem.WriteFloat(localPawn, Offsets.m_flFlashMaxAlpha, 0f);
                mem.WriteFloat(localPawn, Offsets.m_flFlashDuration, 0f);
                mem.WriteFloat(localPawn, Offsets.m_flFlashOverlayAlpha, 0f);
                mem.WriteFloat(localPawn, Offsets.m_flFlashBangTime, 0f);
            }

            debug = new AntiFlashDebug
            {
                FlashAlpha = alpha,
                Status = alpha > 0f ? "Flash cleared" : "Active",
            };
        }
    }
}
