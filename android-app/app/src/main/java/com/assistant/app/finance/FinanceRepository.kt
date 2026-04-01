package com.assistant.app.finance

import com.assistant.app.auth.SupabaseProvider
import io.github.jan.supabase.auth.auth
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONException
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedReader
import java.io.DataOutputStream
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.URL

class FinanceRepository {
    suspend fun getOverview(): FinanceOverview = requestOverview(
        endpoint = "finance_get_overview",
        body = JSONObject()
    )

    suspend fun getTransactions(month: String? = null): FinanceTransactionsMonth {
        val body = JSONObject()
        if (month != null) body.put("p_month", month) else body.put("p_month", JSONObject.NULL)
        return requestTransactions("finance_get_transactions", body)
    }

    suspend fun getCategories(): List<FinanceCategory> = requestCategories(
        endpoint = "finance_get_categories",
        body = JSONObject()
    )

    suspend fun updateOverviewCards(cards: List<String>): FinanceOverview {
        val body = JSONObject()
        body.put("p_cards", JSONArray(cards))
        return requestOverview("finance_update_overview_cards", body)
    }

    suspend fun createTransaction(
        accountId: String,
        direction: String,
        title: String?,
        note: String?,
        amountMinor: Long?,
        currency: String?,
        happenedAt: String?,
        categoryId: String?,
        items: List<FinanceTransactionItemDraft>,
        destinationAccountId: String?,
        sourceType: String,
        receiptStoragePath: String?,
        merchantName: String?
    ): FinanceOverview {
        val transaction = JSONObject()
        transaction.put("accountId", accountId)
        transaction.put("direction", direction)
        transaction.put("title", title ?: JSONObject.NULL)
        transaction.put("note", note ?: JSONObject.NULL)
        transaction.put("amountMinor", amountMinor ?: JSONObject.NULL)
        transaction.put("currency", currency ?: JSONObject.NULL)
        transaction.put("happenedAt", happenedAt ?: JSONObject.NULL)
        transaction.put("categoryId", categoryId ?: JSONObject.NULL)
        transaction.put("destinationAccountId", destinationAccountId ?: JSONObject.NULL)
        transaction.put("sourceType", sourceType)
        transaction.put("merchantName", merchantName ?: JSONObject.NULL)
        val itemsArray = JSONArray()
        items.forEach { item ->
            itemsArray.put(
                JSONObject()
                    .put("title", item.title)
                    .put("amountMinor", item.amountMinor)
                    .put("categoryId", item.categoryId ?: JSONObject.NULL)
            )
        }
        transaction.put("items", itemsArray)
        val body = JSONObject()
        body.put("p_transactions", JSONArray().put(transaction))
        executeRequest("finance_create_transactions", body)
        return getOverview()
    }

    suspend fun createTransactions(
        transactions: List<FinanceCreateTransactionRequest>
    ): FinanceOverview {
        val body = JSONObject()
        val transactionsArray = JSONArray()
        transactions.forEach { transaction ->
            val itemsArray = JSONArray()
            transaction.items.forEach { item ->
                itemsArray.put(
                    JSONObject()
                        .put("title", item.title)
                        .put("amountMinor", item.amountMinor)
                        .put("categoryId", item.categoryId ?: JSONObject.NULL)
                )
            }

            transactionsArray.put(
                JSONObject()
                    .put("accountId", transaction.accountId)
                    .put("direction", transaction.direction)
                    .put("title", transaction.title ?: JSONObject.NULL)
                    .put("note", transaction.note ?: JSONObject.NULL)
                    .put("amountMinor", transaction.amountMinor ?: JSONObject.NULL)
                    .put("currency", transaction.currency ?: JSONObject.NULL)
                    .put("happenedAt", transaction.happenedAt ?: JSONObject.NULL)
                    .put("categoryId", transaction.categoryId ?: JSONObject.NULL)
                    .put("destinationAccountId", transaction.destinationAccountId ?: JSONObject.NULL)
                    .put("sourceType", transaction.sourceType)
                    .put("merchantName", transaction.merchantName ?: JSONObject.NULL)
                    .put("items", itemsArray)
            )
        }
        body.put("p_transactions", transactionsArray)
        executeRequest("finance_create_transactions", body)
        return getOverview()
    }

    suspend fun processReceiptImport(
        fileName: String,
        mimeType: String,
        bytes: ByteArray,
        sourceType: String
    ): FinanceImportResult = withContext(Dispatchers.IO) {
        val session = SupabaseProvider.client.auth.currentSessionOrNull()
            ?: error("No active Supabase session.")
        val boundary = "----AssistantBoundary${System.currentTimeMillis()}"
        val connection = URL("${SupabaseProvider.supabaseUrl}/functions/v1/process-finance-import")
            .openConnection() as HttpURLConnection

        try {
            connection.requestMethod = "POST"
            connection.doOutput = true
            connection.setRequestProperty("apikey", SupabaseProvider.supabaseAnonKey)
            connection.setRequestProperty("Authorization", "Bearer ${session.accessToken}")
            connection.setRequestProperty("Content-Type", "multipart/form-data; boundary=$boundary")
            connection.setRequestProperty("Accept", "application/json")

            DataOutputStream(connection.outputStream).use { output ->
                output.writeBytes("--$boundary\r\n")
                output.writeBytes("Content-Disposition: form-data; name=\"sourceKind\"\r\n\r\n")
                output.writeBytes("${if (sourceType == "photo") "camera" else "file"}\r\n")
                output.writeBytes("--$boundary\r\n")
                output.writeBytes("Content-Disposition: form-data; name=\"file\"; filename=\"$fileName\"\r\n")
                output.writeBytes("Content-Type: $mimeType\r\n\r\n")
                output.write(bytes)
                output.writeBytes("\r\n--$boundary--\r\n")
                output.flush()
            }

            val stream = if (connection.responseCode in 200..299) {
                connection.inputStream
            } else {
                connection.errorStream
            }

            val payload = stream.bufferedReader().use(BufferedReader::readText)
            if (connection.responseCode !in 200..299) {
                error(payload.ifBlank { "Receipt import request failed." })
            }

            parseImportResult(JSONObject(payload))
        } finally {
            connection.disconnect()
        }
    }

    suspend fun upsertAccount(
        id: String?,
        providerCode: String,
        balanceMinor: Long,
        currency: String,
        name: String?,
        makePrimary: Boolean
    ): FinanceOverview {
        val body = JSONObject()
        if (id != null) body.put("p_id", id) else body.put("p_id", JSONObject.NULL)
        body.put("p_provider_code", providerCode)
        body.put("p_balance_minor", balanceMinor)
        body.put("p_currency", currency)
        if (name != null) body.put("p_name", name) else body.put("p_name", JSONObject.NULL)
        body.put("p_make_primary", makePrimary)
        return requestOverview("finance_upsert_account", body)
    }

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
            overviewCards = parseStringArray(json.optJSONArray("overviewCards")),
            totalBalanceMinor = json.optLong("totalBalanceMinor", 0),
            cardBalanceMinor = json.optLong("cardBalanceMinor", 0),
            cashBalanceMinor = json.optLong("cashBalanceMinor", 0),
            monthIncomeMinor = json.optLong("monthIncomeMinor", 0),
            monthExpenseMinor = json.optLong("monthExpenseMinor", 0),
            monthNetMinor = json.optLong("monthNetMinor", 0),
            accounts = parseAccounts(json.optJSONArray("accounts")),
            recentTransactions = parseTransactions(json.optJSONArray("recentTransactions")),
            categoriesCount = json.optInt("categoriesCount", 0)
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
                        providerCode = item.optString("providerCode"),
                        currency = item.optString("currency"),
                        balanceMinor = item.optLong("balanceMinor", 0),
                        isPrimary = item.optBoolean("isPrimary", false),
                        transactionCount = item.optInt("transactionCount", 0),
                        balanceEditable = item.optBoolean("balanceEditable", true)
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
                        accountName = item.optString("accountName"),
                        direction = item.optString("direction"),
                        transactionKind = item.optString("transactionKind"),
                        title = item.optString("title"),
                        merchantName = item.optString("merchantName").ifBlank { null },
                        note = item.optString("note").ifBlank { null },
                        amountMinor = item.optLong("amountMinor", 0),
                        currency = item.optString("currency"),
                        happenedAt = item.optString("happenedAt"),
                        destinationAccountId = item.optString("destinationAccountId").ifBlank { null },
                        destinationAccountName = item.optString("destinationAccountName").ifBlank { null },
                        categoryId = item.optString("categoryId").ifBlank { null },
                        categoryName = item.optString("categoryName").ifBlank { null },
                        itemCount = item.optInt("itemCount", 1),
                        sourceType = item.optString("sourceType").ifBlank { "manual" },
                        receiptStoragePath = item.optString("receiptStoragePath").ifBlank { null },
                        items = parseTransactionItems(item.optJSONArray("items"))
                    )
                )
            }
        }
    }

    private fun parseTransactionItems(array: JSONArray?): List<FinanceTransactionItem> {
        if (array == null) return emptyList()
        return buildList {
            for (index in 0 until array.length()) {
                val item = array.optJSONObject(index) ?: continue
                add(
                    FinanceTransactionItem(
                        id = item.optString("id"),
                        title = item.optString("title"),
                        amountMinor = item.optLong("amountMinor", 0),
                        categoryId = item.optString("categoryId").ifBlank { null },
                        categoryName = item.optString("categoryName").ifBlank { null },
                        categoryCode = item.optString("categoryCode").ifBlank { null },
                        displayOrder = item.optInt("displayOrder", index)
                    )
                )
            }
        }
    }

    private fun parseCategories(array: JSONArray?): List<FinanceCategory> {
        if (array == null) return emptyList()
        return buildList {
            for (index in 0 until array.length()) {
                val item = array.optJSONObject(index) ?: continue
                add(
                    FinanceCategory(
                        id = item.optString("id"),
                        parentId = item.optString("parentId").ifBlank { null },
                        direction = item.optString("direction"),
                        code = item.optString("code"),
                        name = item.optString("name"),
                        icon = item.optString("icon").ifBlank { null },
                        color = item.optString("color").ifBlank { null },
                        displayOrder = item.optInt("displayOrder", 0)
                    )
                )
            }
        }
    }

    private fun parseStringArray(array: JSONArray?): List<String> {
        if (array == null) return emptyList()
        return buildList {
            for (index in 0 until array.length()) {
                val value = array.optString(index)
                if (value.isNotBlank()) add(value)
            }
        }
    }

    private fun parseTransactionsMonth(json: JSONObject): FinanceTransactionsMonth {
        return FinanceTransactionsMonth(
            month = json.optString("month"),
            availableMonths = parseStringArray(json.optJSONArray("availableMonths")),
            transactions = parseTransactions(json.optJSONArray("transactions"))
        )
    }

    private fun parseImportResult(json: JSONObject): FinanceImportResult {
        val draftsArray = json.optJSONArray("drafts")
        val drafts = buildList {
            if (draftsArray != null) {
                for (index in 0 until draftsArray.length()) {
                    val item = draftsArray.optJSONObject(index) ?: continue
                    val draftItems = item.optJSONArray("items")
                    add(
                        FinanceImportDraft(
                            title = item.optString("title"),
                            merchantName = item.optString("merchantName").ifBlank { null },
                            note = item.optString("note").ifBlank { null },
                            direction = item.optString("direction"),
                            transactionKind = item.optString("transactionKind"),
                            amountMinor = item.optLong("amountMinor", 0),
                            currency = item.optString("currency").ifBlank { "RUB" },
                            happenedAt = item.optString("happenedAt").ifBlank { null },
                            sourceType = item.optString("sourceType").ifBlank { "file" },
                            documentKind = item.optString("documentKind").ifBlank { "image" },
                            items = buildList {
                                if (draftItems != null) {
                                    for (itemIndex in 0 until draftItems.length()) {
                                        val draftItem = draftItems.optJSONObject(itemIndex) ?: continue
                                        add(
                                            FinanceImportDraftItem(
                                                title = draftItem.optString("title"),
                                                amountMinor = draftItem.optLong("amountMinor", 0),
                                                suggestedCategoryCode = draftItem.optString("suggestedCategoryCode").ifBlank { null },
                                                suggestedCategoryId = draftItem.optString("suggestedCategoryId").ifBlank { null },
                                                suggestedCategoryName = draftItem.optString("suggestedCategoryName").ifBlank { null },
                                                suggestedCategoryPath = draftItem.optString("suggestedCategoryPath").ifBlank { null }
                                            )
                                        )
                                    }
                                }
                            }
                        )
                    )
                }
            }
        }

        return FinanceImportResult(
            drafts = drafts,
            warnings = parseStringArray(json.optJSONArray("warnings")),
            documentKind = json.optString("documentKind").ifBlank { "image" },
            sourceType = json.optString("sourceType").ifBlank { "file" },
            fileName = json.optString("fileName"),
            storagePath = json.optString("storagePath")
        )
    }

    private suspend fun requestTransactions(endpoint: String, body: JSONObject): FinanceTransactionsMonth =
        withContext(Dispatchers.IO) {
            val payload = executeRequest(endpoint, body)
            parseTransactionsMonth(JSONObject(payload))
        }

    private suspend fun requestCategories(endpoint: String, body: JSONObject): List<FinanceCategory> =
        withContext(Dispatchers.IO) {
            val payload = executeRequest(endpoint, body)
            parseCategories(JSONArray(payload))
        }

    private suspend fun executeRequest(endpoint: String, body: JSONObject): String =
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

                payload
            } finally {
                connection.disconnect()
            }
        }
}
