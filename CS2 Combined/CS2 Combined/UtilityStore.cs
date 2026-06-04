namespace External_Aimbot
{
    internal sealed class UtilityStore
    {
        private readonly Dictionary<string, bool> _toggles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _sliders = new(StringComparer.Ordinal);

        public static UtilityStore CreateWithDefaults()
        {
            var store = new UtilityStore();
            foreach (UtilityEntry entry in UtilityCatalog.All)
            {
                if (entry.Kind == UtilityKind.Toggle)
                    store._toggles[entry.Id] = entry.DefaultOn;
                else
                    store._sliders[entry.Id] = entry.DefaultFloat;
            }

            return store;
        }

        public bool IsOn(string id) =>
            _toggles.TryGetValue(id, out bool on) && on;

        public void SetOn(string id, bool on) => _toggles[id] = on;

        public float GetFloat(string id, float fallback = 0f) =>
            _sliders.TryGetValue(id, out float v) ? v : fallback;

        public void SetFloat(string id, float value) => _sliders[id] = value;

        public int EnabledCount =>
            _toggles.Values.Count(v => v);

        public int ActiveCrosshairStyle()
        {
            for (int i = 1; i <= 12; i++)
            {
                if (IsOn($"ch_style_{i}"))
                    return i;
            }

            return 1;
        }

        public void SetCrosshairStyle(int style)
        {
            for (int i = 1; i <= 12; i++)
                SetOn($"ch_style_{i}", i == style);
        }
    }
}
