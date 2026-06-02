namespace External_Aimbot
{
    internal static class OffsetPresets
    {
        private sealed record Preset(string Name, int DwEntityList, int DwLocalPlayerPawn, int DwLocalPlayerController, int DwViewAngles, int DwGlobalVars, int HighestEntityIndex);

        private static readonly Preset[] Presets =
        [
            new("cs2-dumper build 14165", 0x24E6590, 0x2340698, 0x231F700, 0x23558B8, 0x20606D0, 0x2090),
            new("cs2-dumper 2026-05-29", 0x24E5590, 0x233F698, 0x231E700, 0x23548B8, 0x205F6D0, 0x2090),
            new("community 2026-02-26", 0x24AB1B8, 0x2065AF0, 0x22F8028, 0x23165E8, 0x205A580, 0x2090),
        ];

        public static bool IsValid(GameMemory mem) => Validate(mem);

        public static bool TryApplyWorkingPreset(GameMemory mem, out string presetName)
        {
            if (Validate(mem))
            {
                presetName = "loaded offsets";
                return true;
            }

            foreach (Preset preset in Presets)
            {
                Apply(preset);

                if (!Validate(mem))
                    continue;

                presetName = preset.Name;
                return true;
            }

            presetName = "";
            return false;
        }

        private static bool Validate(GameMemory mem)
        {
            IntPtr entitySystem = mem.ReadPtr(mem.Client, Offsets.dwGameEntitySystem);
            IntPtr globalVars = mem.ReadPtr(mem.Client, Offsets.dwGlobalVars);

            if (entitySystem == IntPtr.Zero || globalVars == IntPtr.Zero)
                return false;

            int highest = mem.ReadInt(entitySystem, Offsets.dwGameEntitySystem_highestEntityIndex);
            return highest >= 1 && highest <= 32768;
        }

        public static void ApplyLoadedOffsets()
        {
            Apply(new Preset(
                "offsets.json",
                Offsets.dwEntityList,
                Offsets.dwLocalPlayerPawn,
                Offsets.dwLocalPlayerController,
                Offsets.dwViewAngles,
                Offsets.dwGlobalVars,
                Offsets.dwGameEntitySystem_highestEntityIndex));
        }

        private static void Apply(Preset preset)
        {
            Offsets.dwEntityList = preset.DwEntityList;
            Offsets.dwGameEntitySystem = preset.DwEntityList;
            Offsets.dwLocalPlayerPawn = preset.DwLocalPlayerPawn;
            Offsets.dwLocalPlayerController = preset.DwLocalPlayerController;
            Offsets.dwViewAngles = preset.DwViewAngles;
            Offsets.dwGlobalVars = preset.DwGlobalVars;
            Offsets.dwGameEntitySystem_highestEntityIndex = preset.HighestEntityIndex;
        }
    }
}
