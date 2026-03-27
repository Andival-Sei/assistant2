package com.assistant.app.finance

import com.assistant.app.auth.SupabaseProvider
import io.github.jan.supabase.auth.auth
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedReader
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.URL

class FinanceRepository {
    suspend fun getOverview(): FinanceOverview = requestOverview(
        endpoint = "finance_get_overview",
        body = JSONObject()
    )

    suspend fun completeOnboarding(
        currency: String?,
        bank: String?,
        cashMinor: Long?,
        primaryAccountBalanceMinor: Long?
    ): FinanceOverview {
        val body = JSONObject()
        if (currency != null) body.put("p_currency", currency) else body.put("p_currency", JSONObject.NULL)
        if (bank != null) body.put("p_bank", bank) else body.put("p_bank", JSONObject.NULL)
        if (cashMinor != null) body.put("p_cash_minor", cashMinor) else body.put("p_cash_minor", JSONObject.NULL)
        if (primaryAccountBalanceMinor != null) {
            body.put("p_primary_account_balance_minor", primaryAccountBalanceMinor)
        } else {
            body.put("p_primary_account_balance_minor", JSONObject.NULL)
        }
        return requestOverview("finance_complete_onboarding", body)
    }

    private suspend fun requestOverview(endpoint: String, body: JSONObject): FinanceOverview =
        withContext(Dispatchers.IO) {
            val session = SupabaseProvider.client.auth.currentSessionOrNull()
                ?: error("No active Supabase session.")
            val connection = URL("${SupabaseProvider.supabaseUrl}/rest/v1/rpc/$endpoint")
                .openConnection() as HttpURLConnection

            try {
                connection.requestMethod = "POST"
                connection.doOutput = true
                connection.setRequestProperty("apikey", SupabaseProvider.supabaseAnonKey)
                connection.setRequestProperty("Authorization", "Bearer ${session.accessToken}")
                connection.setRequestProperty("Content-Type", "application/json")
                connection.setRequestProperty("Accept", "application/json")

                OutputStreamWriter(connection.outputStream).use { writer ->
                    writer.write(body.toString())
                }

                val stream = if (connection.responseCode in 200..299) {
                    connection.inputStream
                } else {
                    connection.errorStream
                }

                val payload = stream.bufferedReader().use(BufferedReader::readText)
                if (connection.responseCode !in 200..299) {
                    error(payload.ifBlank { "Finance request failed." })
                }

                parseOverview(JSONObject(payload))
            } finally {
                connection.disconnect()
            }
        }

    private fun parseOverview(json: JSONObject): FinanceOverview {
        return FinanceOverview(
            onboardingCompleted = json.optBoolean("onboardingCompleted", false),
            defaultCurrency = json.optString("defaultCurrency").ifBlank { null },
            totalBalanceMinor = json.optLong("totalBalanceMinor", 0),
            accounts = parseAccounts(json.optJSONArray("accounts")),
            recentTransactions = parseTransactions(json.optJSONArray("recentTransactions"))
        )
    }

    private fun parseAccounts(array: JSONArray?): List<FinanceAccount> {
        if (array == null) return emptyList()
        return buildList {
            for (index in 0 until array.length()) {
                val item = array.optJSONObject(index) ?: continue
                add(
                    FinanceAccount(
                        id = item.optString("id"),
                        kind = item.optString("kind"),
                        name = item.optString("name"),
                        bankName = item.optString("bankName").ifBlank { null },
                        currency = item.optString("currency"),
                        balanceMinor = item.optLong("balanceMinor", 0),
                        isPrimary = item.optBoolean("isPrimary", false)
                    )
                )
            }
        }
    }

    private fun parseTransactions(array: JSONArray?): List<FinanceTransaction> {
        if (array == null) return emptyList()
        return buildList {
            for (index in 0 until array.length()) {
                val item = array.optJSONObject(index) ?: continue
                add(
                    FinanceTransaction(
                        id = item.optString("id"),
                        accountId = item.optString("accountId"),
                        direction = item.optString("direction"),
                        title = item.optString("title"),
                        note = item.optString("note").ifBlank { null },
                        amountMinor = item.optLong("amountMinor", 0),
                        currency = item.optString("currency"),
                        happenedAt = item.optString("happenedAt")
                    )
                )
            }
        }
    }
}
