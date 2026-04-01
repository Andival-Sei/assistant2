using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Assistant.WinUI.Settings
{
    internal sealed class LinkedIdentity
    {
        [JsonPropertyName("identity_id")]
        public string? IdentityId { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;
    }

    internal sealed class SettingsSnapshot
    {
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string GeminiApiKey { get; set; } = string.Empty;

        public bool HasGeminiApiKey => !string.IsNullOrWhiteSpace(GeminiApiKey);

        public bool AiEnhancementsEnabled { get; set; } = true;

        public List<LinkedIdentity> Identities { get; set; } = new();
    }
}
