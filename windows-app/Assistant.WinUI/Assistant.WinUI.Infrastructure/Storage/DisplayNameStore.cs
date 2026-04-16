using Windows.Storage;

namespace Assistant.WinUI.Storage
{
    internal sealed class DisplayNameStore
    {
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

        public string Load(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return string.Empty;
            }

            return _settings.Values.TryGetValue(GetKey(userId), out var value)
                ? value as string ?? string.Empty
                : string.Empty;
        }

        public void Save(string userId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                _settings.Values.Remove(GetKey(userId));
                return;
            }

            _settings.Values[GetKey(userId)] = displayName;
        }

        private static string GetKey(string userId) => $"settings.display_name.{userId}";
    }
}
