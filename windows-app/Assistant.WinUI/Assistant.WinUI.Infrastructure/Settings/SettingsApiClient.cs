using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Assistant.WinUI.Auth;
using System.Collections.Generic;

namespace Assistant.WinUI.Settings
{
    internal sealed class SettingsApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public SettingsApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(AppConfig.SupabaseUrl.TrimEnd('/') + "/")
            };
        }

        public async Task<SettingsSnapshot> GetSnapshotAsync(
            string accessToken,
            string userId,
            string fallbackEmail)
        {
            var profilePayload = string.Empty;
            try
            {
                profilePayload = await SendReadWithRetryAsync(
                    $"rest/v1/profiles?select=display_name,email&id=eq.{Uri.EscapeDataString(userId)}",
                    accessToken);
            }
            catch (InvalidOperationException ex) when (IsMissingProfilesContract(ex.Message) || IsPermissionOrShapeIssue(ex.Message) || IsTransientReadIssue(ex.Message))
            {
                profilePayload = string.Empty;
            }

            var settingsPayload = string.Empty;
            try
            {
                settingsPayload = await SendReadWithRetryAsync(
                    $"rest/v1/user_settings?select=gemini_api_key,ai_enhancements_enabled&user_id=eq.{Uri.EscapeDataString(userId)}&limit=1",
                    accessToken);

                if (IsEmptyJsonArray(settingsPayload))
                {
                    settingsPayload = await SendReadWithRetryAsync(
                        "rest/v1/user_settings?select=gemini_api_key,ai_enhancements_enabled&order=updated_at.desc&limit=1",
                        accessToken);
                }
            }
            catch (InvalidOperationException ex) when (IsPermissionOrShapeIssue(ex.Message) || IsTransientReadIssue(ex.Message))
            {
                settingsPayload = string.Empty;
            }

            string displayName = string.Empty;
            string email = fallbackEmail;
            if (!string.IsNullOrWhiteSpace(profilePayload))
            {
                using var profileDoc = JsonDocument.Parse(profilePayload);
                if (profileDoc.RootElement.ValueKind == JsonValueKind.Array &&
                    profileDoc.RootElement.GetArrayLength() > 0)
                {
                    var profile = profileDoc.RootElement[0];
                    displayName = profile.GetPropertyOrDefault("display_name");
                    var profileEmail = profile.GetPropertyOrDefault("email");
                    if (!string.IsNullOrWhiteSpace(profileEmail))
                    {
                        email = profileEmail;
                    }
                }
            }

            string geminiApiKey = string.Empty;
            var aiEnhancementsEnabled = true;
            if (!string.IsNullOrWhiteSpace(settingsPayload))
            {
                using var settingsDoc = JsonDocument.Parse(settingsPayload);
                if (settingsDoc.RootElement.ValueKind == JsonValueKind.Array &&
                    settingsDoc.RootElement.GetArrayLength() > 0)
                {
                    geminiApiKey = settingsDoc.RootElement[0].GetPropertyOrDefault("gemini_api_key");
                    var enabled = settingsDoc.RootElement[0].GetPropertyOrDefault("ai_enhancements_enabled");
                    if (bool.TryParse(enabled, out var parsedEnabled))
                    {
                        aiEnhancementsEnabled = parsedEnabled;
                    }
                }
            }

            return new SettingsSnapshot
            {
                UserId = userId,
                Email = email,
                DisplayName = displayName,
                GeminiApiKey = geminiApiKey,
                AiEnhancementsEnabled = aiEnhancementsEnabled
            };
        }

        private static bool IsMissingProfilesContract(string? message)
        {
            var normalized = message?.ToLowerInvariant() ?? string.Empty;
            return normalized.Contains("profiles") &&
                (normalized.Contains("relation") ||
                 normalized.Contains("could not find") ||
                 normalized.Contains("does not exist") ||
                 normalized.Contains("schema cache"));
        }

        private static bool IsPermissionOrShapeIssue(string? message)
        {
            var normalized = message?.ToLowerInvariant() ?? string.Empty;
            return normalized.Contains("permission denied") ||
                   normalized.Contains("forbidden") ||
                   normalized.Contains("schema cache") ||
                   normalized.Contains("column") && normalized.Contains("does not exist");
        }

        private static bool IsTransientReadIssue(string? message)
        {
            var normalized = message?.ToLowerInvariant() ?? string.Empty;
            return normalized.Contains("bad gateway") ||
                   normalized.Contains("gateway") ||
                   normalized.Contains("timeout") ||
                   normalized.Contains("timed out") ||
                   normalized.Contains("http 502") ||
                   normalized.Contains("http 503") ||
                   normalized.Contains("http 504");
        }

        private static bool IsEmptyJsonArray(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return true;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.ValueKind == JsonValueKind.Array &&
                       doc.RootElement.GetArrayLength() == 0;
            }
            catch
            {
                return false;
            }
        }

        public Task SaveDisplayNameAsync(string accessToken, string userId, string email, string displayName) =>
            SendAsync(
                HttpMethod.Post,
                "rest/v1/profiles?on_conflict=id",
                accessToken,
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        id = userId,
                        email,
                        display_name = displayName
                    }
                }),
                prefer: "resolution=merge-duplicates");

        public Task SaveGeminiSettingsAsync(string accessToken, string userId, string geminiApiKey, bool aiEnhancementsEnabled) =>
            SendAsync(
                HttpMethod.Post,
                "rest/v1/user_settings?on_conflict=user_id",
                accessToken,
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        user_id = userId,
                        gemini_api_key = geminiApiKey,
                        ai_enhancements_enabled = aiEnhancementsEnabled
                    }
                }),
                prefer: "resolution=merge-duplicates");

        public Task DeleteAccountAsync(string accessToken) =>
            SendAsync(
                HttpMethod.Post,
                "functions/v1/delete-account",
                accessToken,
                "{}");

        public async Task<List<LinkedIdentity>> GetLinkedIdentitiesAsync(string accessToken)
        {
            var payload = await SendAsync(
                HttpMethod.Post,
                "rest/v1/rpc/settings_get_linked_identities",
                accessToken,
                "{}");

            return JsonSerializer.Deserialize<List<LinkedIdentity>>(payload, JsonOptions) ?? new List<LinkedIdentity>();
        }

        public Task UnlinkGoogleIdentityAsync(string accessToken) =>
            SendAsync(
                HttpMethod.Post,
                "rest/v1/rpc/settings_unlink_google_identity",
                accessToken,
                "{}");

        private async Task<string> SendReadWithRetryAsync(string path, string accessToken, int maxAttempts = 3)
        {
            InvalidOperationException? lastError = null;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return await SendAsync(HttpMethod.Get, path, accessToken);
                }
                catch (InvalidOperationException ex) when (IsTransientReadIssue(ex.Message) && attempt < maxAttempts - 1)
                {
                    lastError = ex;
                    await Task.Delay(220 * (attempt + 1));
                }
            }

            throw lastError ?? new InvalidOperationException("Read request failed.");
        }

        private async Task<string> SendAsync(
            HttpMethod method,
            string path,
            string accessToken,
            string? body = null,
            string? prefer = null)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("apikey", AppConfig.SupabaseAnonKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(prefer))
            {
                request.Headers.TryAddWithoutValidation("Prefer", prefer);
            }

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload) ? "Settings request failed." : payload);
            }

            return payload;
        }
    }
}
