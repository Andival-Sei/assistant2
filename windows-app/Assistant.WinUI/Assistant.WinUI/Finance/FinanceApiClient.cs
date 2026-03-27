using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Assistant.WinUI.Auth;

namespace Assistant.WinUI.Finance
{
    internal sealed class FinanceApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public FinanceApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(AppConfig.SupabaseUrl.TrimEnd('/') + "/")
            };
        }

        public Task<FinanceOverview> GetOverviewAsync(string accessToken) =>
            PostAsync("finance_get_overview", accessToken, new { });

        public Task<FinanceOverview> CompleteOnboardingAsync(
            string accessToken,
            string? currency,
            string? bank,
            long? cashMinor,
            long? primaryAccountBalanceMinor) =>
            PostAsync(
                "finance_complete_onboarding",
                accessToken,
                new
                {
                    p_currency = currency,
                    p_bank = bank,
                    p_cash_minor = cashMinor,
                    p_primary_account_balance_minor = primaryAccountBalanceMinor
                });

        private async Task<FinanceOverview> PostAsync(string endpoint, string accessToken, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"rest/v1/rpc/{endpoint}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("apikey", AppConfig.SupabaseAnonKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload) ? "Finance request failed." : payload);
            }

            return JsonSerializer.Deserialize<FinanceOverview>(payload, JsonOptions)
                ?? new FinanceOverview();
        }
    }
}
