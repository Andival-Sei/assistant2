package com.assistant.app.finance

data class FinanceOverview(
    val onboardingCompleted: Boolean,
    val defaultCurrency: String?,
    val overviewCards: List<String>,
    val totalBalanceMinor: Long,
    val cardBalanceMinor: Long,
    val cashBalanceMinor: Long,
    val monthIncomeMinor: Long,
    val monthExpenseMinor: Long,
    val monthNetMinor: Long,
    val accounts: List<FinanceAccount>,
    val recentTransactions: List<FinanceTransaction>,
    val categoriesCount: Int
)

data class FinanceAccount(
    val id: String,
    val kind: String,
    val name: String,
    val bankName: String?,
    val providerCode: String,
    val currency: String,
    val balanceMinor: Long,
    val isPrimary: Boolean,
    val transactionCount: Int,
    val balanceEditable: Boolean
)

data class FinanceTransaction(
    val id: String,
    val accountId: String,
    val accountName: String,
    val direction: String,
    val transactionKind: String,
    val title: String,
    val merchantName: String?,
    val note: String?,
    val amountMinor: Long,
    val currency: String,
    val happenedAt: String,
    val destinationAccountId: String?,
    val destinationAccountName: String?,
    val categoryId: String?,
    val categoryName: String?,
    val itemCount: Int,
    val sourceType: String,
    val receiptStoragePath: String?,
    val items: List<FinanceTransactionItem>
)

data class FinanceTransactionItem(
    val id: String,
    val title: String,
    val amountMinor: Long,
    val categoryId: String?,
    val categoryName: String?,
    val categoryCode: String?,
    val displayOrder: Int
)

data class FinanceTransactionItemDraft(
    val title: String,
    val amountMinor: Long,
    val categoryId: String?
)

data class FinanceCreateTransactionRequest(
    val accountId: String,
    val direction: String,
    val title: String?,
    val note: String?,
    val amountMinor: Long?,
    val currency: String?,
    val happenedAt: String?,
    val categoryId: String?,
    val items: List<FinanceTransactionItemDraft>,
    val destinationAccountId: String?,
    val sourceType: String,
    val merchantName: String?
)

data class FinanceCategory(
    val id: String,
    val parentId: String?,
    val direction: String,
    val code: String,
    val name: String,
    val icon: String?,
    val color: String?,
    val displayOrder: Int
)

data class FinanceTransactionsMonth(
    val month: String,
    val availableMonths: List<String>,
    val transactions: List<FinanceTransaction>
)

data class FinanceImportDraftItem(
    val title: String,
    val amountMinor: Long,
    val suggestedCategoryCode: String?,
    val suggestedCategoryId: String?,
    val suggestedCategoryName: String?,
    val suggestedCategoryPath: String?
)

data class FinanceImportDraft(
    val title: String,
    val merchantName: String?,
    val note: String?,
    val direction: String,
    val transactionKind: String,
    val amountMinor: Long,
    val currency: String,
    val happenedAt: String?,
    val sourceType: String,
    val documentKind: String,
    val items: List<FinanceImportDraftItem>
)

data class FinanceImportResult(
    val drafts: List<FinanceImportDraft>,
    val warnings: List<String>,
    val documentKind: String,
    val sourceType: String,
    val fileName: String,
    val storagePath: String
)
