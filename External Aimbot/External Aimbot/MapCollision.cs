using System.Numerics;

namespace External_Aimbot
{
    internal static class MapCollision
    {
        private static readonly object Sync = new();
        private static MapRayTracer? _tracer;
        private static string _loadedMap = "";
        private static string _loadingMap = "";
        private static bool _isLoading;

        public static string CurrentMap { get; private set; } = "";
        public static bool IsLoaded => _tracer != null;
        public static int TriangleCount => _tracer?.TriangleCount ?? 0;
        public static bool IsLoading => _isLoading;

        public static void Update(GameMemory mem)
        {
            string mapName = MapNameReader.ReadCurrentMap(mem);
            CurrentMap = mapName;

            if (string.IsNullOrEmpty(mapName))
                return;

            lock (Sync)
            {
                if (_loadedMap == mapName && _tracer != null)
                    return;

                if (_loadingMap == mapName && _isLoading)
                    return;
            }

            RequestLoad(mapName);
        }

        public static bool IsVisible(Vector3 from, Vector3 head, Vector3 chest)
        {
            MapRayTracer? tracer = _tracer;
            if (tracer == null)
                return true;

            return tracer.IsVisible(from, head, chest);
        }

        public static string? FindTriFile(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return null;

            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            [
                Path.Combine(baseDir, "maps", $"{mapName}.tri"),
                Path.Combine(baseDir, $"{mapName}.tri"),
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static void RequestLoad(string mapName)
        {
            string? triPath = FindTriFile(mapName);
            if (triPath == null)
            {
                lock (Sync)
                {
                    _tracer = null;
                    _loadedMap = "";
                    _loadingMap = "";
                    _isLoading = false;
                }

                return;
            }

            lock (Sync)
            {
                _loadingMap = mapName;
                _isLoading = true;
            }

            Task.Run(() => LoadMapAsync(mapName, triPath));
        }

        private static void LoadMapAsync(string mapName, string triPath)
        {
            try
            {
                MapRayTracer? tracer = MapRayTracer.LoadFromTriFile(triPath);

                lock (Sync)
                {
                    if (_loadingMap != mapName)
                        return;

                    _tracer = tracer;
                    _loadedMap = tracer != null ? mapName : "";
                    _isLoading = false;
                }
            }
            catch
            {
                lock (Sync)
                {
                    if (_loadingMap != mapName)
                        return;

                    _tracer = null;
                    _loadedMap = "";
                    _isLoading = false;
                }
            }
        }
    }
}
