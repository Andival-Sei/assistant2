export type DashboardSection = "home" | "finance" | "health" | "tasks" | "chat" | "settings";
export type DashboardSubsection =
  | "summary"
  | "today"
  | "insights"
  | "chat"
  | "overview"
  | "accounts"
  | "transactions"
  | "settings"
  | "habits"
  | "metrics"
  | "records"
  | "focus"
  | "board"
  | "archive"
  | "profile"
  | "preferences"
  | "security";

export type DashboardNavItem = {
  id: DashboardSubsection;
  label: string;
};

export type DashboardSectionConfig = {
  id: DashboardSection;
  icon: string;
  mobileIcon: string;
  label: string;
  title: string;
  eyebrow: string;
  badge: string;
  note: string;
  defaultSubsection: DashboardSubsection;
  subsections: DashboardNavItem[];
};

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
