namespace External_Aimbot
{
    internal static class SchemaUpdater
    {
        private const string RemoteSchemaUrl =
            "https://raw.githubusercontent.com/a2x/cs2-dumper/main/output/client_dll.json";

        public static async Task<bool> TryUpdateAsync(string localPath)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string json = await client.GetStringAsync(RemoteSchemaUrl);

                string tempPath = localPath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);

                if (!ClientSchemaLoader.TryLoad(tempPath))
                {
                    File.Delete(tempPath);
                    return false;
                }

                if (!ClientSchemaLoader.HasSkinOffsets)
                {
                    ClientSchemaLoader.TryLoad(localPath);
                    File.Delete(tempPath);
                    return false;
                }

                File.Move(tempPath, localPath, overwrite: true);
                ClientSchemaLoader.TryLoad(localPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
