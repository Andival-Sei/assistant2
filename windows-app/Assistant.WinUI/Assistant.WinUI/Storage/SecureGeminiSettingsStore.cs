using System.Text.Json;
using Windows.Security.Credentials;

namespace Assistant.WinUI.Storage
{
    internal sealed class SecureGeminiSettingsStore
    {
        private const string ResourceName = "AssistantGeminiSettings";

        private sealed class StoredGeminiSettings
        {
            public string GeminiApiKey { get; set; } = string.Empty;

            public bool AiEnhancementsEnabled { get; set; }
        }

        public (string GeminiApiKey, bool AiEnhancementsEnabled)? Load(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var vault = new PasswordVault();
            try
            {
                var credential = vault.Retrieve(ResourceName, userId);
                credential.RetrievePassword();
                var payload = JsonSerializer.Deserialize<StoredGeminiSettings>(credential.Password);
                if (payload == null)
                {
                    return null;
                }

                return (payload.GeminiApiKey, payload.AiEnhancementsEnabled);
            }
            catch
            {
                return null;
            }
        }

        public void Save(string userId, string geminiApiKey, bool aiEnhancementsEnabled)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var vault = new PasswordVault();
            RemoveIfExists(vault, userId);
            vault.Add(new PasswordCredential(
                ResourceName,
                userId,
                JsonSerializer.Serialize(new StoredGeminiSettings
                {
                    GeminiApiKey = geminiApiKey,
                    AiEnhancementsEnabled = aiEnhancementsEnabled
                })));
        }

        public void Clear(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var vault = new PasswordVault();
            RemoveIfExists(vault, userId);
        }

        private static void RemoveIfExists(PasswordVault vault, string userId)
        {
            try
            {
                var credential = vault.Retrieve(ResourceName, userId);
                vault.Remove(credential);
            }
            catch
            {
                // ignore
            }
        }
    }
}
