using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Assistant.WinUI.Auth;

namespace Assistant.WinUI.Finance
{
    internal sealed class FinanceImportRequestResult
    {
        public bool IsSuccessStatusCode { get; init; }
        public int StatusCode { get; init; }
        public string Payload { get; init; } = string.Empty;
        public FinanceImportResult? Result { get; init; }
    }

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
            string? cardType,
            string? lastFourDigits,
            long? cashMinor,
            long? primaryAccountBalanceMinor) =>
            PostAsync(
                "finance_complete_onboarding",
                accessToken,
                new
                {
                    p_currency = currency,
                    p_bank = bank,
                    p_card_type = cardType,
                    p_last_four_digits = lastFourDigits,
                    p_cash_minor = cashMinor,
                    p_primary_account_balance_minor = primaryAccountBalanceMinor
                });

        public Task<FinanceTransactionsMonth> GetTransactionsAsync(string accessToken, string? month = null) =>
            PostAsync<FinanceTransactionsMonth>(
                "finance_get_transactions",
                accessToken,
                new { p_month = month });

        public Task<List<FinanceCategory>> GetCategoriesAsync(string accessToken) =>
            PostAsync<List<FinanceCategory>>(
                "finance_get_categories",
                accessToken,
                new { });

        public Task ResetAllAsync(string accessToken) =>
            PostAsync<object>(
                "finance_reset_all",
                accessToken,
                new { });

        public Task<FinanceOverview> UpdateOverviewCardsAsync(string accessToken, IReadOnlyList<string> cards) =>
            PostAsync<FinanceOverview>(
                "finance_update_overview_cards",
                accessToken,
                new { p_cards = cards });

        public Task<FinanceOverview> UpsertAccountAsync(
            string accessToken,
            Guid? id,
            string kind,
            string providerCode,
            string? name,
            string? cardType,
            string? lastFourDigits,
            long balanceMinor,
            string currency,
            bool makePrimary,
            long? creditLimitMinor,
            long? creditDebtMinor,
            long? creditRequiredPaymentMinor,
            DateTimeOffset? creditPaymentDueDate,
            DateTimeOffset? creditGracePeriodEndDate,
            long? loanPrincipalMinor,
            long? loanCurrentDebtMinor,
            decimal? loanInterestPercent,
            long? loanPaymentAmountMinor,
            DateTimeOffset? loanPaymentDueDate,
            int? loanRemainingPaymentsCount,
            long? loanTotalPayableMinor,
            int? loanTotalPaymentsCount,
            long? loanFinalPaymentMinor) =>
            PostAsync<FinanceOverview>(
                "finance_upsert_account",
                accessToken,
                new
                {
                    p_id = id,
                    p_kind = kind,
                    p_provider_code = providerCode,
                    p_name = name,
                    p_card_type = cardType,
                    p_last_four_digits = lastFourDigits,
                    p_balance_minor = balanceMinor,
                    p_currency = currency,
                    p_make_primary = makePrimary,
                    p_credit_limit_minor = creditLimitMinor,
                    p_credit_debt_minor = creditDebtMinor,
                    p_credit_required_payment_minor = creditRequiredPaymentMinor,
                    p_credit_payment_due_date = creditPaymentDueDate?.Date,
                    p_credit_grace_period_end_date = creditGracePeriodEndDate?.Date,
                    p_loan_principal_minor = loanPrincipalMinor,
                    p_loan_current_debt_minor = loanCurrentDebtMinor,
                    p_loan_interest_percent = loanInterestPercent,
                    p_loan_payment_amount_minor = loanPaymentAmountMinor,
                    p_loan_payment_due_date = loanPaymentDueDate?.Date,
                    p_loan_remaining_payments_count = loanRemainingPaymentsCount,
                    p_loan_total_payable_minor = loanTotalPayableMinor,
                    p_loan_total_payments_count = loanTotalPaymentsCount,
                    p_loan_final_payment_minor = loanFinalPaymentMinor
                });

        public Task<FinanceOverview> RecordLoanPaymentAsync(
            string accessToken,
            FinanceRecordLoanPaymentRequest request) =>
            PostAsync<FinanceOverview>(
                "finance_record_loan_payment",
                accessToken,
                new
                {
                    p_source_account_id = request.SourceAccountId,
                    p_loan_account_id = request.LoanAccountId,
                    p_amount_minor = request.AmountMinor,
                    p_new_current_debt_minor = request.NewCurrentDebtMinor,
                    p_happened_at = request.HappenedAt,
                    p_title = request.Title,
                    p_note = request.Note,
                    p_source_type = request.SourceType
                });

        public Task<FinanceOverview> CreateTransactionsAsync(
            string accessToken,
            IReadOnlyList<FinanceCreateTransactionRequest> transactions) =>
            CreateTransactionsInternalAsync(accessToken, transactions);

        public async Task<FinanceImportResult> ProcessReceiptImportAsync(
            string accessToken,
            string filePath,
            string fileName,
            string contentType,
            string sourceType)
        {
            var response = await ProcessReceiptImportAttemptAsync(accessToken, filePath, fileName, contentType, sourceType);
            if (!response.IsSuccessStatusCode || response.Result == null)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Payload)
                    ? "Receipt import failed."
                    : response.Payload);
            }

            return response.Result;
        }

        internal async Task<FinanceImportRequestResult> ProcessReceiptImportAttemptAsync(
            string accessToken,
            string filePath,
            string fileName,
            string contentType,
            string sourceType)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(sourceType == "photo" ? "camera" : "file"), "sourceKind");
            using var stream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "functions/v1/process-finance-import");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("apikey", AppConfig.SupabaseAnonKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = form;

            using var response = await _httpClient.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();

            return new FinanceImportRequestResult
            {
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Payload = payload,
                Result = response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(payload)
                    ? JsonSerializer.Deserialize<FinanceImportResult>(payload, JsonOptions)
                    : null
            };
        }

        private async Task<FinanceOverview> CreateTransactionsInternalAsync(
            string accessToken,
            IReadOnlyList<FinanceCreateTransactionRequest> transactions)
        {
            await PostAsync<object>(
                "finance_create_transactions",
                accessToken,
                new
                {
                    p_transactions = transactions
                });

            return await GetOverviewAsync(accessToken);
        }

        private Task<FinanceOverview> PostAsync(string endpoint, string accessToken, object body) =>
            PostAsync<FinanceOverview>(endpoint, accessToken, body);

        private async Task<T> PostAsync<T>(string endpoint, string accessToken, object body)
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

            return JsonSerializer.Deserialize<T>(payload, JsonOptions)
                ?? throw new InvalidOperationException("Finance response payload is empty.");
        }
    }
}
