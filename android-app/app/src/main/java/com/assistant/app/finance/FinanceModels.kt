package com.assistant.app.finance

data class FinanceOverview(
    val onboardingCompleted: Boolean,
    val defaultCurrency: String?,
    val totalBalanceMinor: Long,
    val accounts: List<FinanceAccount>,
    val recentTransactions: List<FinanceTransaction>
)

data class FinanceAccount(
    val id: String,
    val kind: String,
    val name: String,
    val bankName: String?,
    val currency: String,
    val balanceMinor: Long,
    val isPrimary: Boolean
)

data class FinanceTransaction(
    val id: String,
    val accountId: String,
    val direction: String,
    val title: String,
    val note: String?,
    val amountMinor: Long,
    val currency: String,
    val happenedAt: String
)
