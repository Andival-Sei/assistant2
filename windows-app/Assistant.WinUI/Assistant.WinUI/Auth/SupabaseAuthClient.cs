using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Assistant.WinUI.Auth
{
    internal sealed class SupabaseAuthClient
    {
        private readonly HttpClient _http;
        private readonly string _url;
        private readonly string _anonKey;

        public SupabaseAuthClient(string url, string anonKey)
        {
            _url = url.TrimEnd('/');
            _anonKey = anonKey;
            _http = new HttpClient
            {
                BaseAddress = new Uri(_url),
            };
            _http.DefaultRequestHeaders.Add("apikey", _anonKey);
        }

        public async Task<AuthSession> SignInWithPasswordAsync(string email, string password)
        {
            var payload = JsonSerializer.Serialize(new { email, password });
            var response = await _http.PostAsync(
                "/auth/v1/token?grant_type=password",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            var json = await response.Content.ReadAsStringAsync();
            EnsureSuccess(response, json);
            return ParseSession(json);
        }

        public async Task<AuthSession?> SignUpAsync(string email, string password, string redirectTo)
        {
            var payload = JsonSerializer.Serialize(new
            {
                email,
                password,
                email_redirect_to = redirectTo
            });

            var response = await _http.PostAsync(
                "/auth/v1/signup",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            var json = await response.Content.ReadAsStringAsync();
            EnsureSuccess(response, json);
            return ParseSession(json, allowEmpty: true);
        }

        public async Task ResetPasswordAsync(string email, string redirectTo)
        {
            var payload = JsonSerializer.Serialize(new { email, redirect_to = redirectTo });
            var response = await _http.PostAsync(
                "/auth/v1/recover",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            var json = await response.Content.ReadAsStringAsync();
            EnsureSuccess(response, json);
        }

        public async Task UpdatePasswordAsync(string accessToken, string newPassword)
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, "/auth/v1/user");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = new StringContent(
                JsonSerializer.Serialize(new { password = newPassword }),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            EnsureSuccess(response, json);
        }

        public async Task SignOutAsync(string accessToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/logout");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            EnsureSuccess(response, json);
        }

        public string BuildGoogleAuthorizeUrl(string redirectTo, string codeChallenge, bool forceAccountSelection = false)
        {
            var builder = new UriBuilder($"{_url}/auth/v1/authorize");
            var query = new Dictionary<string, string?>
            {
                ["provider"] = "google",
                ["redirect_to"] = redirectTo,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "s256"
            };
            if (forceAccountSelection)
            {
                query["prompt"] = "select_account";
            }
            builder.Query = string.Join("&", query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
            return builder.ToString();
        }

        public async Task<AuthSession> ExchangeCodeForSessionAsync(string code, string codeVerifier)
        {
            var payload = JsonSerializer.Serialize(new
            {
                auth_code = code,
                code_verifier = codeVerifier
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/token?grant_type=pkce");
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            EnsureSuccess(response, json);
            return ParseSession(json);
        }

        private static void EnsureSuccess(HttpResponseMessage response, string? json)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var message = ExtractErrorMessage(json);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? $"HTTP {(int)response.StatusCode}"
                : message);
        }

        private static string? ExtractErrorMessage(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("msg", out var msg))
                {
                    return msg.GetString();
                }

                if (root.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }

                if (root.TryGetProperty("error_description", out var description))
                {
                    return description.GetString();
                }

                if (root.TryGetProperty("error", out var error))
                {
                    return error.GetString();
                }
            }
            catch
            {
                return json;
            }

            return json;
        }

        private static AuthSession ParseSession(string json, bool allowEmpty = false)
        {
            if (allowEmpty && string.IsNullOrWhiteSpace(json))
                return new AuthSession();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var session = new AuthSession
            {
                AccessToken = root.GetPropertyOrDefault("access_token"),
                RefreshToken = root.GetPropertyOrDefault("refresh_token"),
                TokenType = root.GetPropertyOrDefault("token_type", "bearer"),
                ExpiresIn = root.GetPropertyOrDefaultInt("expires_in"),
                ExpiresAt = root.GetPropertyOrDefaultLong("expires_at")
            };

            if (root.TryGetProperty("user", out var user) &&
                user.TryGetProperty("email", out var email))
            {
                session.UserEmail = email.GetString();
            }

            return session;
        }
    }

    internal static class JsonHelpers
    {
        public static string GetPropertyOrDefault(this JsonElement element, string name, string fallback = "")
        {
            return element.TryGetProperty(name, out var value) ? (value.GetString() ?? fallback) : fallback;
        }

        public static int GetPropertyOrDefaultInt(this JsonElement element, string name, int fallback = 0)
        {
            return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var i) ? i : fallback;
        }

        public static long? GetPropertyOrDefaultLong(this JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.TryGetInt64(out var i) ? i : null;
        }
    }
}
