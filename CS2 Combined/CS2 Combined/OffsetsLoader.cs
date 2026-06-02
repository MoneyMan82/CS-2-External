using System.Text.Json;

namespace External_Aimbot
{
    internal static class OffsetsLoader
    {
        public static bool TryLoadFromFile(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);

                if (!doc.RootElement.TryGetProperty("client.dll", out JsonElement client))
                    return false;

                SetIfPresent(client, "dwEntityList", v => Offsets.dwEntityList = v);
                SetIfPresent(client, "dwGameEntitySystem", v => Offsets.dwGameEntitySystem = v);
                SetIfPresent(client, "dwGameEntitySystem_highestEntityIndex", v => Offsets.dwGameEntitySystem_highestEntityIndex = v);
                SetIfPresent(client, "dwLocalPlayerPawn", v => Offsets.dwLocalPlayerPawn = v);
                SetIfPresent(client, "dwLocalPlayerController", v => Offsets.dwLocalPlayerController = v);
                SetIfPresent(client, "dwGlobalVars", v => Offsets.dwGlobalVars = v);
                SetIfPresent(client, "dwViewAngles", v => Offsets.dwViewAngles = v);
                SetIfPresent(client, "dwViewMatrix", v => Offsets.dwViewMatrix = v);
                SetIfPresent(client, "dwCSGOInput", v => Offsets.dwCSGOInput = v);

                if (doc.RootElement.TryGetProperty("engine2.dll", out JsonElement engine))
                {
                    SetIfPresent(engine, "dwNetworkGameClient", v => Offsets.dwNetworkGameClient = v);
                    SetIfPresent(engine, "dwNetworkGameClient_signOnState", v => Offsets.dwNetworkGameClient_signOnState = v);
                    SetIfPresent(engine, "dwBuildNumber", v => Offsets.dwBuildNumber = v);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetIfPresent(JsonElement client, string name, Action<int> setter)
        {
            if (client.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int offset))
                setter(offset);
        }
    }
}
