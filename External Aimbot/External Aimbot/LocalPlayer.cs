namespace External_Aimbot
{
    internal readonly struct LocalPlayerDebug
    {
        public IntPtr DirectPawn { get; init; }
        public IntPtr Controller { get; init; }
        public int PawnHandle { get; init; }
        public int ControllerIndex { get; init; }
        public IntPtr ResolvedPawn { get; init; }
        public int SignOnState { get; init; }
        public string Source { get; init; }
    }

    internal static class LocalPlayer
    {
        public static IntPtr ResolvePawn(GameMemory mem, IntPtr entitySystem, out LocalPlayerDebug debug)
        {
            debug = new LocalPlayerDebug
            {
                DirectPawn = mem.ReadPtr(mem.Client, Offsets.dwLocalPlayerPawn),
                Controller = mem.ReadPtr(mem.Client, Offsets.dwLocalPlayerController),
                SignOnState = ReadSignOnState(mem),
            };

            if (debug.DirectPawn != IntPtr.Zero)
            {
                debug = FinalizeDebug(mem, entitySystem, debug with
                {
                    ResolvedPawn = debug.DirectPawn,
                    Source = "dwLocalPlayerPawn",
                });
                return debug.DirectPawn;
            }

            if (debug.Controller != IntPtr.Zero)
            {
                IntPtr pawn = ResolveFromController(mem, entitySystem, debug.Controller, ref debug);
                if (pawn != IntPtr.Zero)
                {
                    debug = FinalizeDebug(mem, entitySystem, debug with { Source = "dwLocalPlayerController" });
                    return pawn;
                }
            }

            IntPtr scanned = ScanForLocalController(mem, entitySystem, ref debug);
            if (scanned != IntPtr.Zero)
            {
                debug = FinalizeDebug(mem, entitySystem, debug with { ResolvedPawn = scanned, Source = "controller scan" });
                return scanned;
            }

            return IntPtr.Zero;
        }

        public static int ResolveControllerIndex(GameMemory mem, IntPtr entitySystem, IntPtr localController)
        {
            if (entitySystem == IntPtr.Zero || localController == IntPtr.Zero)
                return -1;

            IntPtr listEntry = mem.ReadPtr(entitySystem, 0x10);
            if (listEntry == IntPtr.Zero)
                return -1;

            for (int i = 0; i < 64; i++)
            {
                IntPtr controller = mem.ReadPtr(listEntry, EntityList.ControllerChunkStride * i);
                if (controller == IntPtr.Zero)
                    controller = mem.ReadPtr(listEntry, EntityList.EntitySlotStride * i);
                if (controller == IntPtr.Zero)
                    continue;

                if (controller == localController)
                    return i;

                if (mem.ReadBool(controller, Offsets.m_bIsLocalPlayerController))
                    return i;
            }

            return -1;
        }

        private static LocalPlayerDebug FinalizeDebug(GameMemory mem, IntPtr entitySystem, LocalPlayerDebug debug)
        {
            if (debug.Controller == IntPtr.Zero)
                debug = debug with { Controller = mem.ReadPtr(mem.Client, Offsets.dwLocalPlayerController) };

            if (debug.PawnHandle == 0 && debug.Controller != IntPtr.Zero)
                debug = debug with { PawnHandle = mem.ReadInt(debug.Controller, Offsets.m_hPlayerPawn) };

            return debug with
            {
                ControllerIndex = ResolveControllerIndex(mem, entitySystem, debug.Controller),
            };
        }

        private static IntPtr ScanForLocalController(GameMemory mem, IntPtr entitySystem, ref LocalPlayerDebug debug)
        {
            if (entitySystem == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr listEntry = mem.ReadPtr(entitySystem, 0x10);
            if (listEntry == IntPtr.Zero)
                return IntPtr.Zero;

            for (int i = 0; i < 64; i++)
            {
                IntPtr controller = mem.ReadPtr(listEntry, EntityList.ControllerChunkStride * i);
                if (controller == IntPtr.Zero)
                    controller = mem.ReadPtr(listEntry, EntityList.EntitySlotStride * i);
                if (controller == IntPtr.Zero)
                    continue;

                if (!mem.ReadBool(controller, Offsets.m_bIsLocalPlayerController))
                    continue;

                debug = debug with { Controller = controller };
                return ResolveFromController(mem, entitySystem, controller, ref debug);
            }

            return IntPtr.Zero;
        }

        private static IntPtr ResolveFromController(
            GameMemory mem,
            IntPtr entitySystem,
            IntPtr controller,
            ref LocalPlayerDebug debug)
        {
            int pawnHandle = mem.ReadInt(controller, Offsets.m_hPlayerPawn);
            if (pawnHandle == 0)
                pawnHandle = mem.ReadInt(controller, Offsets.m_hPawn);

            debug = debug with { PawnHandle = pawnHandle };

            if (pawnHandle == 0)
                return IntPtr.Zero;

            return EntityList.ResolveHandle(mem, entitySystem, pawnHandle);
        }

        public static IntPtr ResolvePawnFromHandle(GameMemory mem, IntPtr entitySystem, int pawnHandle) =>
            EntityList.ResolveHandle(mem, entitySystem, pawnHandle);

        private static int ReadSignOnState(GameMemory mem)
        {
            if (mem.Engine == IntPtr.Zero)
                return -1;

            IntPtr networkClient = mem.ReadPtr(mem.Engine, Offsets.dwNetworkGameClient);
            if (networkClient == IntPtr.Zero)
                return -1;

            return mem.ReadInt(networkClient, Offsets.dwNetworkGameClient_signOnState);
        }
    }
}
