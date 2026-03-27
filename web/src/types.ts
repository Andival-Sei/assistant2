export type DashboardSection = "home" | "finance" | "health" | "tasks" | "settings";

export type FinanceTab = "overview" | "accounts" | "transactions" | "settings";
export type Currency = "RUB" | "USD" | "EUR";

export type SectionCopy = {
  label: string;
  title: string;
  note: string;
  mobileIcon: string;
};

export type FinanceAccount = {
  id: string;
  kind: "bank_card" | "cash";
  name: string;
  bankName: string | null;
  currency: Currency;
  balanceMinor: number;
  isPrimary: boolean;
};

export type FinanceTransaction = {
  id: string;
  accountId: string;
  direction: "income" | "expense" | "transfer";
  title: string;
  note: string | null;
  amountMinor: number;
  currency: Currency;
  happenedAt: string;
};

export type FinanceOverview = {
  onboardingCompleted: boolean;
  defaultCurrency: Currency | null;
  totalBalanceMinor: number;
  accounts: FinanceAccount[];
  recentTransactions: FinanceTransaction[];
};

export type FinanceOnboardingState = {
  currency: Currency | null;
  bank: string | null;
  primaryBalance: string;
  cash: string;
};
