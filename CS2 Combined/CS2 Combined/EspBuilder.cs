using System.Numerics;

namespace External_Aimbot
{
    public readonly struct EspBoneLine
    {
        public Vector2 From { get; init; }
        public Vector2 To { get; init; }
    }

    public readonly struct EspPlayerData
    {
        public bool Valid { get; init; }
        public bool IsVisible { get; init; }
        public int Team { get; init; }
        public int Health { get; init; }
        public int Armor { get; init; }
        public float Distance { get; init; }
        public string Name { get; init; }
        public string WeaponName { get; init; }
        public Vector2 HeadScreen { get; init; }
        public Vector2 FeetScreen { get; init; }
        public EspBoneLine[] BoneLines { get; init; }
    }

    internal static class EspBuilder
    {
        public static EspPlayerData Build(
            GameMemory mem,
            IntPtr entitySystem,
            Entity entity,
            float[] viewMatrix,
            float screenWidth,
            float screenHeight)
        {
            Vector3 headWorld = entity.GetAimPosition(mem);
            Vector3 feetWorld = entity.origin;

            if (!ViewMatrix.TryWorldToScreen(headWorld, viewMatrix, screenWidth, screenHeight, out Vector2 headScreen) ||
                !ViewMatrix.TryWorldToScreen(feetWorld, viewMatrix, screenWidth, screenHeight, out Vector2 feetScreen))
            {
                return default;
            }

            string name = ReadPlayerName(mem, entity);
            string weapon = ReadWeaponName(mem, entitySystem, entity.pawnAddress);
            int armor = mem.ReadInt(entity.pawnAddress, Offsets.m_ArmorValue);
            var boneLines = BuildBoneLines(mem, entity.pawnAddress, viewMatrix, screenWidth, screenHeight);

            return new EspPlayerData
            {
                Valid = true,
                IsVisible = entity.isVisible,
                Team = entity.team,
                Health = entity.health,
                Armor = Math.Clamp(armor, 0, 100),
                Distance = entity.distance,
                Name = name,
                WeaponName = weapon,
                HeadScreen = headScreen,
                FeetScreen = feetScreen,
                BoneLines = boneLines ?? Array.Empty<EspBoneLine>(),
            };
        }

        private static string ReadPlayerName(GameMemory mem, Entity entity)
        {
            if (entity.controllerAddress != IntPtr.Zero)
            {
                string name = mem.ReadString(entity.controllerAddress + Offsets.m_iszPlayerName, 64);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return "Player";
        }

        private static string ReadWeaponName(GameMemory mem, IntPtr entitySystem, IntPtr pawn)
        {
            WeaponContext weapon = WeaponReader.Read(mem, pawn, entitySystem);
            return weapon.IsValid ? weapon.Name : "";
        }

        private static EspBoneLine[] BuildBoneLines(
            GameMemory mem,
            IntPtr pawn,
            float[] viewMatrix,
            float screenWidth,
            float screenHeight)
        {
            var lines = new List<EspBoneLine>(BoneReader.Connections.Length);

            foreach ((int from, int to) in BoneReader.Connections)
            {
                if (!BoneReader.TryGetBonePosition(mem, pawn, from, out Vector3 fromWorld) ||
                    !BoneReader.TryGetBonePosition(mem, pawn, to, out Vector3 toWorld))
                    continue;

                if (!ViewMatrix.TryWorldToScreen(fromWorld, viewMatrix, screenWidth, screenHeight, out Vector2 fromScreen) ||
                    !ViewMatrix.TryWorldToScreen(toWorld, viewMatrix, screenWidth, screenHeight, out Vector2 toScreen))
                    continue;

                lines.Add(new EspBoneLine { From = fromScreen, To = toScreen });
            }

            return lines.ToArray();
        }
    }
}
