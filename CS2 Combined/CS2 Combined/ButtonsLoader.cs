using System.Text.Json;

namespace External_Aimbot
{
    internal static class ButtonsLoader
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

                SetIfPresent(client, "jump", v => Offsets.dwJump = v);

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
