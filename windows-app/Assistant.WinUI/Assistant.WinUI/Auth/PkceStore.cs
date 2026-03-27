using Windows.Storage;

namespace Assistant.WinUI.Auth
{
    internal static class PkceStore
    {
        private const string Key = "assistant_pkce_verifier";

        public static void Save(string verifier)
        {
            ApplicationData.Current.LocalSettings.Values[Key] = verifier;
        }

        public static string? Load()
        {
            return ApplicationData.Current.LocalSettings.Values[Key] as string;
        }

        public static void Clear()
        {
            ApplicationData.Current.LocalSettings.Values.Remove(Key);
        }
    }
}
