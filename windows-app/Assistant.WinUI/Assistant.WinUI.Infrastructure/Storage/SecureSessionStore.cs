using System.Text.Json;
using Windows.Storage;
using Windows.Security.Credentials;

namespace Assistant.WinUI.Storage
{
    internal sealed class SecureSessionStore
    {
        private const string ResourceName = "AssistantAuth";
        private const string UserName = "session";
        private const string RememberKey = "assistant.auth.remember";

        public void Save(object session, bool rememberDevice)
        {
            SetRememberDevice(rememberDevice);
            if (!rememberDevice)
            {
                Clear();
                return;
            }

            var json = JsonSerializer.Serialize(session);
            var vault = new PasswordVault();
            RemoveIfExists(vault);
            vault.Add(new PasswordCredential(ResourceName, UserName, json));
        }

        public string? Load()
        {
            var vault = new PasswordVault();
            try
            {
                var cred = vault.Retrieve(ResourceName, UserName);
                cred.RetrievePassword();
                return cred.Password;
            }
            catch
            {
                return null;
            }
        }

        public void Clear()
        {
            var vault = new PasswordVault();
            RemoveIfExists(vault);
        }

        public bool LoadRememberDevice()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(RememberKey, out var value) &&
                value is bool rememberDevice)
            {
                return rememberDevice;
            }

            return false;
        }

        public void SetRememberDevice(bool rememberDevice)
        {
            ApplicationData.Current.LocalSettings.Values[RememberKey] = rememberDevice;
        }

        private static void RemoveIfExists(PasswordVault vault)
        {
            try
            {
                var cred = vault.Retrieve(ResourceName, UserName);
                vault.Remove(cred);
            }
            catch
            {
                // ignore
            }
        }
    }
}
