export type DashboardSection = "home" | "finance" | "health" | "tasks" | "chat" | "settings";
export type DashboardSubsection =
  | "summary"
  | "today"
  | "insights"
  | "chat"
  | "overview"
  | "accounts"
  | "transactions"
  | "categories"
  | "analytics"
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

export type FinanceTab =
  | "overview"
  | "accounts"
  | "transactions"
  | "categories"
  | "analytics";
export type Currency = "RUB" | "USD" | "EUR";
export type FinanceOverviewCardId =
  | "total_balance"
  | "card_balance"
  | "cash_balance"
  | "month_income"
  | "month_expense"
  | "month_result"
  | "recent_transactions";

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
  providerCode: string;
  currency: Currency;
  balanceMinor: number;
  isPrimary: boolean;
  transactionCount: number;
  balanceEditable: boolean;
};

export type FinanceTransaction = {
  id: string;
  accountId: string;
  accountName: string;
  direction: "income" | "expense" | "transfer";
  transactionKind: "single" | "split" | "transfer";
  title: string;
  merchantName: string | null;
  note: string | null;
  amountMinor: number;
  currency: Currency;
  happenedAt: string;
  destinationAccountId: string | null;
  destinationAccountName: string | null;
  categoryId: string | null;
  categoryName: string | null;
  itemCount: number;
  sourceType: "manual" | "photo" | "file";
  items: FinanceTransactionItem[];
};

export type FinanceTransactionItem = {
  id: string;
  title: string;
  amountMinor: number;
  categoryId: string | null;
  categoryName: string | null;
  categoryCode: string | null;
  displayOrder: number;
};

export type FinanceCategory = {
  id: string;
  parentId: string | null;
  direction: "income" | "expense";
  code: string;
  name: string;
  icon: string | null;
  color: string | null;
  displayOrder: number;
};

export type FinanceTransactionsMonth = {
  month: string;
  availableMonths: string[];
  transactions: FinanceTransaction[];
};

export type FinanceImportDraftItem = {
  title: string;
  amountMinor: number;
  suggestedCategoryCode: string | null;
  suggestedCategoryId: string | null;
  suggestedCategoryName: string | null;
  suggestedCategoryPath: string | null;
};

export type FinanceImportDraft = {
  title: string;
  merchantName: string | null;
  note: string | null;
  direction: "income" | "expense";
  transactionKind: "single" | "split";
  amountMinor: number;
  currency: Currency;
  happenedAt: string | null;
  sourceType: "photo" | "file";
  documentKind: "image" | "pdf" | "eml";
  items: FinanceImportDraftItem[];
};

export type FinanceImportResult = {
  drafts: FinanceImportDraft[];
  warnings: string[];
  documentKind: "image" | "pdf" | "eml";
  sourceType: "photo" | "file";
  fileName: string;
  storagePath?: string;
};

export type FinanceOverview = {
  onboardingCompleted: boolean;
  defaultCurrency: Currency | null;
  overviewCards: FinanceOverviewCardId[];
  totalBalanceMinor: number;
  cardBalanceMinor: number;
  cashBalanceMinor: number;
  monthIncomeMinor: number;
  monthExpenseMinor: number;
  monthNetMinor: number;
  accounts: FinanceAccount[];
  recentTransactions: FinanceTransaction[];
  categoriesCount: number;
};

export type FinanceOnboardingState = {
  currency: Currency | null;
  bank: string | null;
  primaryBalance: string;
  cash: string;
};
