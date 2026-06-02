namespace External_Aimbot
{
    internal static class OffsetsUpdater
    {
        private const string RemoteOffsetsUrl =
            "https://raw.githubusercontent.com/a2x/cs2-dumper/main/output/offsets.json";

        public static async Task<bool> TryUpdateValidatedAsync(string localPath, GameMemory mem)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string json = await client.GetStringAsync(RemoteOffsetsUrl);

                string tempPath = localPath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);

                if (!OffsetsLoader.TryLoadFromFile(tempPath))
                {
                    File.Delete(tempPath);
                    return false;
                }

                if (!OffsetPresets.IsValid(mem))
                {
                    OffsetsLoader.TryLoadFromFile(localPath);
                    File.Delete(tempPath);
                    return false;
                }

                File.Move(tempPath, localPath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
