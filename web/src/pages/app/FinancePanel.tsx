import { startTransition, useEffect, useMemo, useState } from "react";

import { supabase } from "../../lib/supabaseClient";
import {
  Currency,
  FinanceAccount,
  FinanceCategory,
  FinanceOnboardingState,
  FinanceOverview,
  FinanceOverviewCardId,
  FinanceTab,
  FinanceTransaction,
  FinanceTransactionsMonth,
} from "../../types";
import { FinanceTransactionFlow } from "./FinanceTransactionFlow";
import "./finance.css";

type Lang = "ru" | "en";

type FinanceCopy = {
  sectionLabel: string;
  onboardingTitle: string;
  onboardingSubtitle: string;
  back: string;
  continue: string;
  finish: string;
  skipAll: string;
  skipStep: string;
  tabs: Record<FinanceTab, string>;
  stepTitle: string;
  overviewTitle: string;
  overviewHint: string;
  accountsTitle: string;
  transactionsTitle: string;
  categoriesTitle: string;
  analyticsTitle: string;
  addAccount: string;
  editAccount: string;
  addTransaction: string;
  configureOverview: string;
  save: string;
  cancel: string;
  close: string;
  amount: string;
  balance: string;
  accountsCount: string;
  allAccounts: string;
  totalBalance: string;
  cardBalance: string;
  cashBalance: string;
  monthIncome: string;
  monthExpense: string;
  monthResult: string;
  recentTransactions: string;
  noAccounts: string;
  noTransactions: string;
  noCategories: string;
  monthPlaceholder: string;
  overviewSettingsTitle: string;
  overviewSettingsHint: string;
  accountModalCreateTitle: string;
  accountModalEditTitle: string;
  accountProviderLabel: string;
  accountNameLabel: string;
  accountPrimaryLabel: string;
  accountBalanceLocked: string;
  transactionStubTitle: string;
  transactionStubBody: string;
  transactionTypesTitle: string;
  transactionInputTitle: string;
  analyticsStub: string;
  categoriesExpense: string;
  categoriesIncome: string;
  onboardingSteps: Array<{
    title: string;
    body: string;
  }>;
  cardLabels: Record<FinanceOverviewCardId, string>;
  accountProviders: Array<{
    code: string;
    label: string;
    description: string;
  }>;
  currencyChoices: Array<{ code: Currency; label: string }>;
  transactionTypes: Array<{
    id: string;
    title: string;
    body: string;
  }>;
  transactionInputs: Array<{
    id: string;
    title: string;
    body: string;
  }>;
};

function getFinanceCopy(lang: Lang): FinanceCopy {
  if (lang === "ru") {
    return {
      sectionLabel: "Финансы",
      onboardingTitle: "Соберём финансовую базу",
      onboardingSubtitle:
        "Сначала фиксируем валюту, основной счёт и наличные. После этого откроется полноценный раздел обзора, счетов, транзакций и категорий.",
      back: "Назад",
      continue: "Далее",
      finish: "Открыть финансы",
      skipAll: "Пропустить всё",
      skipStep: "Пропустить шаг",
      tabs: {
        overview: "Обзор",
        accounts: "Счета",
        transactions: "Транзакции",
        categories: "Категории",
        analytics: "Аналитика",
      },
      stepTitle: "Первичная настройка",
      overviewTitle: "Обзор",
      overviewHint: "Показываем только ключевые показатели и короткий срез последних операций.",
      accountsTitle: "Счета",
      transactionsTitle: "Транзакции",
      categoriesTitle: "Категории",
      analyticsTitle: "Аналитика",
      addAccount: "Добавить счёт",
      editAccount: "Редактировать",
      addTransaction: "Добавить транзакцию",
      configureOverview: "Настроить обзор",
      save: "Сохранить",
      cancel: "Отмена",
      close: "Закрыть",
      amount: "Сумма",
      balance: "Баланс",
      accountsCount: "Количество счетов",
      allAccounts: "Все счета",
      totalBalance: "Общий баланс",
      cardBalance: "На картах",
      cashBalance: "Наличные",
      monthIncome: "Доходы за месяц",
      monthExpense: "Расходы за месяц",
      monthResult: "Результат месяца",
      recentTransactions: "Последние транзакции",
      noAccounts: "Счета ещё не добавлены. Начните с основного банка или наличных.",
      noTransactions: "Пока нет транзакций. Добавьте первую вручную, по фото или из файла.",
      noCategories: "Категории не заполнены.",
      monthPlaceholder: "Выберите месяц",
      overviewSettingsTitle: "Карточки обзора",
      overviewSettingsHint:
        "Выберите, какие карточки должны быть на главном экране финансов. Изменения сохраняются в Supabase.",
      accountModalCreateTitle: "Новый счёт",
      accountModalEditTitle: "Редактирование счёта",
      accountProviderLabel: "Счёт / банк",
      accountNameLabel: "Название на экране",
      accountPrimaryLabel: "Сделать основным карточным счётом",
      accountBalanceLocked:
        "Сумму нельзя менять после первой транзакции по счёту. Это ограничение уже работает на сервере.",
      transactionStubTitle: "Добавление транзакций",
      transactionStubBody:
        "Типы и сценарии уже подготовлены. На следующем шаге подключим полноценный ввод одинарных и множественных транзакций.",
      transactionTypesTitle: "Тип операции",
      transactionInputTitle: "Способ ввода",
      analyticsStub: "Раздел аналитики оставлен как заглушка. Основа под данные и навигацию уже готова.",
      categoriesExpense: "Расходы",
      categoriesIncome: "Доходы",
      onboardingSteps: [
        {
          title: "Основная валюта",
          body: "Она будет использоваться для баланса, счетов и будущих отчётов.",
        },
        {
          title: "Основной счёт",
          body: "Выберите банк из фиксированного списка и задайте стартовую сумму.",
        },
        {
          title: "Наличные",
          body: "Если наличные уже есть, зафиксируйте сумму сразу, чтобы общий баланс был корректным.",
        },
      ],
      cardLabels: {
        total_balance: "Общий баланс",
        card_balance: "На картах",
        cash_balance: "Наличные",
        month_income: "Доходы за месяц",
        month_expense: "Расходы за месяц",
        month_result: "Результат месяца",
        recent_transactions: "Краткий список транзакций",
      },
      accountProviders: [
        { code: "tbank", label: "Т-Банк", description: "Основная карта или счёт" },
        { code: "sber", label: "Сбер", description: "Карты и счета Сбера" },
        { code: "alfa", label: "Альфа", description: "Счета Альфа-Банка" },
        { code: "vtb", label: "ВТБ", description: "Карты и счета ВТБ" },
        { code: "gazprombank", label: "Газпромбанк", description: "Банковский счёт" },
        { code: "yandex", label: "Яндекс", description: "Яндекс Банк / карта" },
        { code: "ozon", label: "Ozon", description: "Ozon Банк / карта" },
        { code: "raiffeisen", label: "Райффайзен", description: "Банковский счёт" },
        { code: "rosselkhoz", label: "Россельхозбанк", description: "Банковский счёт" },
        { code: "other_bank", label: "Другой счёт", description: "Если банка нет в списке" },
        { code: "cash", label: "Наличные", description: "Физические деньги" },
      ],
      currencyChoices: [
        { code: "RUB", label: "Рубли" },
        { code: "USD", label: "Доллары" },
        { code: "EUR", label: "Евро" },
      ],
      transactionTypes: [
        { id: "income", title: "Доход", body: "Зарплата, возвраты, подарки" },
        { id: "expense", title: "Расход", body: "Покупки, услуги, списания" },
        { id: "transfer", title: "Перевод", body: "Между своими счетами" },
      ],
      transactionInputs: [
        { id: "receipt", title: "Загрузить чек", body: "PDF, JPEG, PNG или EML" },
        { id: "camera", title: "Сфотографировать", body: "Использовать камеру телефона" },
        { id: "manual", title: "Вручную", body: "Заполнить форму вручную" },
      ],
    };
  }

  return {
    sectionLabel: "Finance",
    onboardingTitle: "Set up the finance base",
    onboardingSubtitle:
      "We start with currency, primary account, and cash so the finance workspace can open with the right structure.",
    back: "Back",
    continue: "Continue",
    finish: "Open finance",
    skipAll: "Skip all",
    skipStep: "Skip step",
    tabs: {
      overview: "Overview",
      accounts: "Accounts",
      transactions: "Transactions",
      categories: "Categories",
      analytics: "Analytics",
    },
    stepTitle: "Initial setup",
    overviewTitle: "Overview",
    overviewHint: "Only key metrics and a short transaction slice stay on the main finance surface.",
    accountsTitle: "Accounts",
    transactionsTitle: "Transactions",
    categoriesTitle: "Categories",
    analyticsTitle: "Analytics",
    addAccount: "Add account",
    editAccount: "Edit",
    addTransaction: "Add transaction",
    configureOverview: "Overview settings",
    save: "Save",
    cancel: "Cancel",
    close: "Close",
    amount: "Amount",
    balance: "Balance",
    accountsCount: "Accounts count",
    allAccounts: "All accounts",
    totalBalance: "Total balance",
    cardBalance: "On cards",
    cashBalance: "Cash",
    monthIncome: "Month income",
    monthExpense: "Month expense",
    monthResult: "Month result",
    recentTransactions: "Recent transactions",
    noAccounts: "No accounts yet.",
    noTransactions: "No transactions yet. Add the first one manually, from a photo, or from a file.",
    noCategories: "Categories are empty.",
    monthPlaceholder: "Choose month",
    overviewSettingsTitle: "Overview cards",
    overviewSettingsHint: "Choose which cards stay on the finance overview. Changes are stored in Supabase.",
    accountModalCreateTitle: "New account",
    accountModalEditTitle: "Edit account",
    accountProviderLabel: "Account / bank",
    accountNameLabel: "Visible title",
    accountPrimaryLabel: "Make it the primary card account",
    accountBalanceLocked: "Balance is locked after the first transaction. This rule is enforced on the backend.",
    transactionStubTitle: "Transaction entry",
    transactionStubBody: "Types and flows are prepared. The next step will connect full single and multi-item entry.",
    transactionTypesTitle: "Operation type",
    transactionInputTitle: "Input mode",
    analyticsStub: "Analytics stays in development for now. Navigation and data hooks are ready.",
    categoriesExpense: "Expenses",
    categoriesIncome: "Income",
    onboardingSteps: [
      {
        title: "Base currency",
        body: "It will drive balances, accounts, and future reporting.",
      },
      {
        title: "Primary account",
        body: "Pick a bank from the fixed list and set the starting amount.",
      },
      {
        title: "Cash",
        body: "If you already keep cash, set it now so the total balance is correct.",
      },
    ],
    cardLabels: {
      total_balance: "Total balance",
      card_balance: "On cards",
      cash_balance: "Cash",
      month_income: "Month income",
      month_expense: "Month expense",
      month_result: "Month result",
      recent_transactions: "Short transaction list",
    },
    accountProviders: [
      { code: "tbank", label: "T-Bank", description: "Primary card or account" },
      { code: "sber", label: "Sber", description: "Sber cards and accounts" },
      { code: "alfa", label: "Alfa", description: "Alfa Bank accounts" },
      { code: "vtb", label: "VTB", description: "VTB cards and accounts" },
      { code: "gazprombank", label: "Gazprombank", description: "Bank account" },
      { code: "yandex", label: "Yandex", description: "Yandex Bank / card" },
      { code: "ozon", label: "Ozon", description: "Ozon Bank / card" },
      { code: "raiffeisen", label: "Raiffeisen", description: "Bank account" },
      { code: "rosselkhoz", label: "Rosselkhozbank", description: "Bank account" },
      { code: "other_bank", label: "Other account", description: "When the bank is missing" },
      { code: "cash", label: "Cash", description: "Physical money" },
    ],
    currencyChoices: [
      { code: "RUB", label: "Rubles" },
      { code: "USD", label: "US dollars" },
      { code: "EUR", label: "Euro" },
    ],
    transactionTypes: [
      { id: "income", title: "Income", body: "Salary, refunds, gifts" },
      { id: "expense", title: "Expense", body: "Shopping, services, charges" },
      { id: "transfer", title: "Transfer", body: "Between your own accounts" },
    ],
    transactionInputs: [
      { id: "receipt", title: "Upload receipt", body: "PDF, JPEG, PNG, or EML" },
      { id: "camera", title: "Use camera", body: "Take a photo on mobile" },
      { id: "manual", title: "Manual entry", body: "Fill in the form by hand" },
    ],
  };
}

export function formatMoney(
  minor: number,
  currency: Currency | null,
  lang: Lang,
) {
  const locale = lang === "ru" ? "ru-RU" : "en-US";
  const amount = minor / 100;
  const fractionDigits = Number.isInteger(amount) ? 0 : 2;
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency: currency ?? "RUB",
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount);
}

export function parseAmountToMinor(value: string) {
  const normalized = value.replace(/\s/g, "").replace(",", ".");
  if (!normalized) return null;
  const amount = Number(normalized);
  if (Number.isNaN(amount) || amount < 0) return null;
  return Math.round(amount * 100);
}

function formatMonthLabel(month: string, lang: Lang) {
  return new Intl.DateTimeFormat(lang === "ru" ? "ru-RU" : "en-US", {
    month: "long",
    year: "numeric",
  }).format(new Date(`${month}-01T00:00:00`));
}

function formatTransactionDate(value: string, lang: Lang) {
  return new Intl.DateTimeFormat(lang === "ru" ? "ru-RU" : "en-US", {
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

function getTransactionSignedAmount(transaction: FinanceTransaction) {
  if (transaction.direction === "expense") return -Math.abs(transaction.amountMinor);
  if (transaction.direction === "income") return Math.abs(transaction.amountMinor);
  return transaction.amountMinor;
}

function buildCategoryGroups(categories: FinanceCategory[]) {
  const nodes = new Map<string, FinanceCategory & { children: FinanceCategory[] }>();
  for (const category of categories) {
    nodes.set(category.id, { ...category, children: [] });
  }

  const roots = {
    expense: [] as Array<FinanceCategory & { children: FinanceCategory[] }>,
    income: [] as Array<FinanceCategory & { children: FinanceCategory[] }>,
  };

  for (const node of nodes.values()) {
    if (node.parentId && nodes.has(node.parentId)) {
      nodes.get(node.parentId)!.children.push(node);
    } else {
      roots[node.direction].push(node);
    }
  }

  const sortTree = (items: Array<FinanceCategory & { children: FinanceCategory[] }>) => {
    items.sort((left, right) => left.displayOrder - right.displayOrder);
    for (const item of items) {
      sortTree(item.children as Array<FinanceCategory & { children: FinanceCategory[] }>);
    }
  };

  sortTree(roots.expense);
  sortTree(roots.income);

  return roots;
}

function cardTone(cardId: FinanceOverviewCardId) {
  switch (cardId) {
    case "total_balance":
      return "emerald";
    case "card_balance":
      return "blue";
    case "cash_balance":
      return "amber";
    case "month_income":
      return "mint";
    case "month_expense":
      return "rose";
    case "month_result":
      return "violet";
    case "recent_transactions":
      return "slate";
  }
}

function accountTone(providerCode: string) {
  switch (providerCode) {
    case "tbank":
      return "emerald";
    case "sber":
      return "mint";
    case "alfa":
      return "rose";
    case "vtb":
    case "ozon":
      return "blue";
    case "gazprombank":
      return "violet";
    case "yandex":
    case "raiffeisen":
    case "cash":
      return "amber";
    case "rosselkhoz":
      return "mint";
    default:
      return "slate";
  }
}

const OVERVIEW_CARD_IDS: FinanceOverviewCardId[] = [
  "total_balance",
  "card_balance",
  "cash_balance",
  "month_income",
  "month_expense",
  "month_result",
  "recent_transactions",
];

function getOverviewCardMetric(
  overview: FinanceOverview,
  cardId: FinanceOverviewCardId,
) {
  switch (cardId) {
    case "total_balance":
      return Math.abs(overview.totalBalanceMinor);
    case "card_balance":
      return Math.abs(overview.cardBalanceMinor);
    case "cash_balance":
      return Math.abs(overview.cashBalanceMinor);
    case "month_income":
      return Math.abs(overview.monthIncomeMinor);
    case "month_expense":
      return Math.abs(overview.monthExpenseMinor);
    case "month_result":
      return Math.abs(overview.monthNetMinor);
    case "recent_transactions":
      return overview.recentTransactions.length;
  }
}

function FinanceModal({
  title,
  subtitle,
  onClose,
  children,
}: {
  title: string;
  subtitle?: string;
  onClose: () => void;
  children: React.ReactNode;
}) {
  const [closing, setClosing] = useState(false);

  function requestClose() {
    if (closing) return;
    setClosing(true);
    window.setTimeout(() => onClose(), 180);
  }

  return (
    <div
      className={`finance-modal-backdrop ${closing ? "is-closing" : "is-open"}`}
      role="presentation"
      onClick={requestClose}
    >
      <div
        className={`finance-modal ${closing ? "is-closing" : "is-open"}`}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="finance-modal-head">
          <div>
            <h3>{title}</h3>
            {subtitle ? <p>{subtitle}</p> : null}
          </div>
          <button className="finance-icon-btn" type="button" onClick={requestClose} aria-label="close">
            ×
          </button>
        </div>
        <div className="finance-modal-body">{children}</div>
      </div>
    </div>
  );
}

function FinanceOverviewSettingsModal({
  lang,
  overview,
  onClose,
  onSaved,
}: {
  lang: Lang;
  overview: FinanceOverview;
  onClose: () => void;
  onSaved: (next: FinanceOverview) => void;
}) {
  const copy = getFinanceCopy(lang);
  const [orderedCards, setOrderedCards] = useState<FinanceOverviewCardId[]>(() => {
    const missing = OVERVIEW_CARD_IDS.filter((cardId) => !overview.overviewCards.includes(cardId));
    return [...overview.overviewCards, ...missing];
  });
  const [selectedCards, setSelectedCards] = useState<FinanceOverviewCardId[]>(overview.overviewCards);
  const [draggedCardId, setDraggedCardId] = useState<FinanceOverviewCardId | null>(null);
  const [dropTarget, setDropTarget] = useState<{
    id: FinanceOverviewCardId;
    placement: "before" | "after";
  } | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    setSaving(true);
    setError(null);
    const cardsToSave = orderedCards.filter((cardId) => selectedCards.includes(cardId));
    const { data, error: rpcError } = await supabase.rpc("finance_update_overview_cards", {
      p_cards: cardsToSave,
    });
    if (rpcError) {
      setSaving(false);
      setError(rpcError.message);
      return;
    }
    onSaved(data as FinanceOverview);
    setSaving(false);
    onClose();
  }

  return (
    <FinanceModal
      title={copy.overviewSettingsTitle}
      subtitle={copy.overviewSettingsHint}
      onClose={onClose}
    >
      <div className="finance-settings-order-list">
        {orderedCards.map((cardId) => {
          const active = selectedCards.includes(cardId);
          return (
            <div
              key={cardId}
              className={`finance-settings-card ${active ? "active" : ""} ${
                draggedCardId === cardId ? "dragging" : ""
              } ${
                dropTarget?.id === cardId ? `drop-${dropTarget.placement}` : ""
              }`}
              draggable
              onDragStart={() => setDraggedCardId(cardId)}
              onDragEnd={() => {
                setDraggedCardId(null);
                setDropTarget(null);
              }}
              onDragOver={(event) => {
                event.preventDefault();
                const bounds = event.currentTarget.getBoundingClientRect();
                const placement =
                  event.clientY - bounds.top < bounds.height / 2 ? "before" : "after";
                setDropTarget({ id: cardId, placement });
              }}
              onDragLeave={() => {
                if (dropTarget?.id === cardId) {
                  setDropTarget(null);
                }
              }}
              onDrop={() => {
                if (!draggedCardId || draggedCardId === cardId) return;
                setOrderedCards((current) => {
                  const next = [...current];
                  const fromIndex = next.indexOf(draggedCardId);
                  const toIndex = next.indexOf(cardId);
                  if (fromIndex === -1 || toIndex === -1) return current;
                  next.splice(fromIndex, 1);
                  const placement = dropTarget?.id === cardId ? dropTarget.placement : "before";
                  const insertIndex =
                    placement === "before"
                      ? toIndex
                      : toIndex + (fromIndex < toIndex ? 0 : 1);
                  next.splice(insertIndex, 0, draggedCardId);
                  return next;
                });
                setDraggedCardId(null);
                setDropTarget(null);
              }}
            >
              <div className="finance-settings-card-handle" aria-hidden="true">
                <span />
                <span />
              </div>
              <div className="finance-settings-card-copy">
                <span>{copy.cardLabels[cardId]}</span>
                <p>
                  {active
                    ? lang === "ru"
                      ? "Карточка включена и появится, когда значение будет больше нуля."
                      : "Card is enabled and will appear once the value becomes non-zero."
                    : lang === "ru"
                      ? "Карточка выключена и скрыта всегда."
                      : "Card is disabled and always hidden."}
                </p>
              </div>
              <button
                className={`finance-settings-toggle ${active ? "active" : ""}`}
                type="button"
                onClick={() =>
                  setSelectedCards((current) =>
                    active ? current.filter((item) => item !== cardId) : [...current, cardId],
                  )
                }
              >
                <strong>{active ? "ON" : "OFF"}</strong>
              </button>
            </div>
          );
        })}
      </div>
      {error ? <p className="finance-inline-error">{error}</p> : null}
      <div className="finance-modal-actions">
        <button className="secondary-btn" type="button" onClick={onClose}>
          {copy.cancel}
        </button>
        <button className="pill-btn" type="button" disabled={saving} onClick={handleSave}>
          {saving ? "..." : copy.save}
        </button>
      </div>
    </FinanceModal>
  );
}

function FinanceAccountModal({
  lang,
  overview,
  account,
  onClose,
  onSaved,
}: {
  lang: Lang;
  overview: FinanceOverview;
  account: FinanceAccount | null;
  onClose: () => void;
  onSaved: (next: FinanceOverview) => void;
}) {
  const copy = getFinanceCopy(lang);
  const [providerCode, setProviderCode] = useState(account?.providerCode ?? "tbank");
  const [name, setName] = useState(account?.name ?? "");
  const [amount, setAmount] = useState(
    account ? String((account.balanceMinor / 100).toFixed(2)).replace(".", ",") : "",
  );
  const [makePrimary, setMakePrimary] = useState(account?.isPrimary ?? !overview.accounts.some((item) => item.isPrimary));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isCash = providerCode === "cash";
  const provider = copy.accountProviders.find((item) => item.code === providerCode);

  useEffect(() => {
    if (isCash) {
      setName(lang === "ru" ? "Наличные" : "Cash");
      setMakePrimary(false);
    }
  }, [isCash, lang]);

  async function handleSubmit() {
    const balanceMinor = parseAmountToMinor(amount);
    if (balanceMinor === null) {
      setError(lang === "ru" ? "Введите корректную сумму" : "Enter a valid amount");
      return;
    }

    setSaving(true);
    setError(null);
    const { data, error: rpcError } = await supabase.rpc("finance_upsert_account", {
      p_id: account?.id ?? null,
      p_provider_code: providerCode,
      p_balance_minor: balanceMinor,
      p_currency: overview.defaultCurrency ?? "RUB",
      p_name: isCash ? null : name.trim() || null,
      p_make_primary: isCash ? false : makePrimary,
    });

    if (rpcError) {
      setSaving(false);
      setError(rpcError.message);
      return;
    }

    onSaved(data as FinanceOverview);
    setSaving(false);
    onClose();
  }

  return (
    <FinanceModal
      title={account ? copy.accountModalEditTitle : copy.accountModalCreateTitle}
      subtitle={provider?.description}
      onClose={onClose}
    >
      <div className="finance-form-grid finance-form-grid--modal">
        <label className="finance-field">
          <span>{copy.accountProviderLabel}</span>
          <select value={providerCode} onChange={(event) => setProviderCode(event.target.value)}>
            {copy.accountProviders.map((item) => (
              <option key={item.code} value={item.code}>
                {item.label}
              </option>
            ))}
          </select>
        </label>

        {!isCash ? (
          <label className="finance-field">
            <span>{copy.accountNameLabel}</span>
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={provider?.label}
            />
          </label>
        ) : null}

        <label className="finance-field">
          <span>{copy.amount}</span>
          <input
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            disabled={Boolean(account && !account.balanceEditable)}
            placeholder="0"
          />
        </label>

        {!isCash ? (
          <label className="finance-checkbox-row">
            <input
              type="checkbox"
              checked={makePrimary}
              onChange={(event) => setMakePrimary(event.target.checked)}
            />
            <span>{copy.accountPrimaryLabel}</span>
          </label>
        ) : null}
      </div>
      {account && !account.balanceEditable ? (
        <p className="finance-inline-note">{copy.accountBalanceLocked}</p>
      ) : null}
      {error ? <p className="finance-inline-error">{error}</p> : null}
      <div className="finance-modal-actions">
        <button className="secondary-btn" type="button" onClick={onClose}>
          {copy.cancel}
        </button>
        <button className="pill-btn" type="button" disabled={saving} onClick={handleSubmit}>
          {saving ? "..." : copy.save}
        </button>
      </div>
    </FinanceModal>
  );
}

function FinanceTransactionStubModal({
  lang,
  onClose,
}: {
  lang: Lang;
  onClose: () => void;
}) {
  const copy = getFinanceCopy(lang);

  return (
    <FinanceModal
      title={copy.transactionStubTitle}
      subtitle={copy.transactionStubBody}
      onClose={onClose}
    >
      <div className="finance-modal-stack">
        <section className="finance-choice-section">
          <div className="finance-choice-section-head">
            <span>{copy.transactionTypesTitle}</span>
          </div>
          <div className="finance-selection-grid">
            {copy.transactionTypes.map((item) => (
              <article key={item.id} className="finance-selection-card finance-selection-card--static">
                <span>{item.title}</span>
                <p>{item.body}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="finance-choice-section">
          <div className="finance-choice-section-head">
            <span>{copy.transactionInputTitle}</span>
          </div>
          <div className="finance-selection-grid">
            {copy.transactionInputs.map((item) => (
              <article key={item.id} className="finance-selection-card finance-selection-card--static">
                <span>{item.title}</span>
                <p>{item.body}</p>
              </article>
            ))}
          </div>
        </section>
      </div>
      <div className="finance-modal-actions">
        <button className="pill-btn" type="button" onClick={onClose}>
          {copy.close}
        </button>
      </div>
    </FinanceModal>
  );
}

function FinanceCard({
  title,
  value,
  tone,
  detail,
  emphasize,
}: {
  title: string;
  value: string;
  tone: string;
  detail: string;
  emphasize?: boolean;
}) {
  return (
    <article className={`finance-signal-card finance-signal-card--${tone} ${emphasize ? "finance-signal-card--wide" : ""}`}>
      <div className="finance-signal-copy">
        <span>{title}</span>
        <strong>{value}</strong>
        <p>{detail}</p>
      </div>
    </article>
  );
}

function FinanceTransactionList({
  lang,
  transactions,
  emptyText,
}: {
  lang: Lang;
  transactions: FinanceTransaction[];
  emptyText: string;
}) {
  if (!transactions.length) {
    return <p className="finance-empty-copy">{emptyText}</p>;
  }

  return (
    <div className="finance-transaction-list">
      {transactions.map((transaction) => {
        const amount = getTransactionSignedAmount(transaction);
        const amountClass =
          transaction.direction === "expense"
            ? "expense"
            : transaction.direction === "income"
              ? "income"
              : "transfer";

        return (
          <article key={transaction.id} className="finance-transaction-item">
            <div className="finance-transaction-copy">
              <strong>{transaction.title}</strong>
              <span>
                {transaction.accountName}
                {transaction.destinationAccountName ? ` → ${transaction.destinationAccountName}` : ""}
              </span>
              <small>{formatTransactionDate(transaction.happenedAt, lang)}</small>
            </div>
            <div className={`finance-transaction-amount finance-transaction-amount--${amountClass}`}>
              {formatMoney(amount, transaction.currency, lang)}
              {transaction.itemCount > 1 ? <small>{transaction.itemCount} поз.</small> : null}
            </div>
          </article>
        );
      })}
    </div>
  );
}

function FinanceCategoryTree({
  lang,
  title,
  items,
}: {
  lang: Lang;
  title: string;
  items: Array<FinanceCategory & { children: FinanceCategory[] }>;
}) {
  function renderNode(node: FinanceCategory & { children: FinanceCategory[] }, depth: number) {
    return (
      <div key={node.id} className="finance-category-node">
        <div
          className="finance-category-row"
          style={{ ["--finance-category-depth" as string]: depth } as React.CSSProperties}
        >
          <span className="finance-category-mark" />
          <div className="finance-category-copy">
            <strong>{node.name}</strong>
            <span>{node.code}</span>
          </div>
        </div>
        {node.children.length ? (
          <div className="finance-category-children">
            {node.children.map((child) =>
              renderNode(child as FinanceCategory & { children: FinanceCategory[] }, depth + 1),
            )}
          </div>
        ) : null}
      </div>
    );
  }

  return (
    <article className="finance-surface-card finance-surface-card--tree">
      <div className="finance-surface-head">
        <div>
          <span className="finance-surface-label">{lang === "ru" ? "Дерево" : "Tree"}</span>
          <h3>{title}</h3>
        </div>
        <strong>{items.length}</strong>
      </div>
      {items.length ? (
        <div className="finance-category-tree">{items.map((item) => renderNode(item, 0))}</div>
      ) : (
        <p className="finance-empty-copy">
          {lang === "ru" ? "Корневые категории отсутствуют." : "No root categories yet."}
        </p>
      )}
    </article>
  );
}

export function FinancePanel({
  lang,
  overview,
  loading,
  error,
  financeTab,
  onTabChange,
  onboarding,
  onboardingStep,
  onSetOnboarding,
  onStepChange,
  onComplete,
  onOverviewChange,
}: {
  lang: Lang;
  overview: FinanceOverview | null;
  loading: boolean;
  error: string | null;
  financeTab: FinanceTab;
  onTabChange: (tab: FinanceTab) => void;
  onboarding: FinanceOnboardingState;
  onboardingStep: number;
  onSetOnboarding: (patch: Partial<FinanceOnboardingState>) => void;
  onStepChange: (step: number) => void;
  onComplete: (skip: boolean) => void;
  onOverviewChange: (overview: FinanceOverview) => void;
}) {
  const copy = getFinanceCopy(lang);
  const [transactionsMonth, setTransactionsMonth] = useState<FinanceTransactionsMonth | null>(null);
  const [transactionsLoading, setTransactionsLoading] = useState(false);
  const [transactionsError, setTransactionsError] = useState<string | null>(null);
  const [selectedMonth, setSelectedMonth] = useState<string | null>(null);
  const [categories, setCategories] = useState<FinanceCategory[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [overviewSettingsOpen, setOverviewSettingsOpen] = useState(false);
  const [accountModalState, setAccountModalState] = useState<FinanceAccount | null | "new">(null);
  const [transactionFlowOpen, setTransactionFlowOpen] = useState(false);
  const [overviewRefreshNonce, setOverviewRefreshNonce] = useState(0);

  useEffect(() => {
    if (!overview?.onboardingCompleted) return;
    void loadTransactions(selectedMonth ?? undefined);
  }, [overview?.onboardingCompleted]);

  useEffect(() => {
    if (!overview?.onboardingCompleted) return;
    if (financeTab === "categories" && categories.length === 0 && !categoriesLoading) {
      void loadCategories();
    }
  }, [categories.length, categoriesLoading, financeTab, overview?.onboardingCompleted]);

  async function loadTransactions(month?: string) {
    setTransactionsLoading(true);
    setTransactionsError(null);
    const { data, error: rpcError } = await supabase.rpc("finance_get_transactions", {
      p_month: month ?? null,
    });
    if (rpcError) {
      setTransactionsLoading(false);
      setTransactionsError(rpcError.message);
      return;
    }
    const next = data as FinanceTransactionsMonth;
    setTransactionsMonth(next);
    setSelectedMonth(next.month);
    setTransactionsLoading(false);
  }

  async function loadCategories() {
    setCategoriesLoading(true);
    setCategoriesError(null);
    const { data, error: rpcError } = await supabase.rpc("finance_get_categories");
    if (rpcError) {
      setCategoriesLoading(false);
      setCategoriesError(rpcError.message);
      return;
    }
    setCategories((data as FinanceCategory[]) ?? []);
    setCategoriesLoading(false);
  }

  const displayCurrency = overview?.defaultCurrency ?? onboarding.currency ?? "RUB";
  const currentStep = copy.onboardingSteps[onboardingStep];
  const configuredCards = overview?.overviewCards ?? [];
  const summaryCards = useMemo(() => {
    if (!overview) return [];
    const detailMonth = transactionsMonth?.month ?? new Date().toISOString().slice(0, 7);

    const values: Record<FinanceOverviewCardId, { value: string; detail: string }> = {
      total_balance: {
        value: formatMoney(overview.totalBalanceMinor, displayCurrency, lang),
        detail: copy.overviewHint,
      },
      card_balance: {
        value: formatMoney(overview.cardBalanceMinor, displayCurrency, lang),
        detail: `${copy.accountsCount}: ${overview.accounts.filter((item) => item.kind === "bank_card").length}`,
      },
      cash_balance: {
        value: formatMoney(overview.cashBalanceMinor, displayCurrency, lang),
        detail: lang === "ru" ? "Все наличные счета" : "All cash accounts",
      },
      month_income: {
        value: formatMoney(overview.monthIncomeMinor, displayCurrency, lang),
        detail: formatMonthLabel(detailMonth, lang),
      },
      month_expense: {
        value: formatMoney(-overview.monthExpenseMinor, displayCurrency, lang),
        detail: formatMonthLabel(detailMonth, lang),
      },
      month_result: {
        value: formatMoney(overview.monthNetMinor, displayCurrency, lang),
        detail: formatMonthLabel(detailMonth, lang),
      },
      recent_transactions: {
        value: String(overview.recentTransactions.length),
        detail: copy.recentTransactions,
      },
    };

    return configuredCards
      .filter((cardId) => getOverviewCardMetric(overview, cardId) > 0)
      .map((cardId) => ({
        id: cardId,
        title: copy.cardLabels[cardId],
        tone: cardTone(cardId),
        value: values[cardId].value,
        detail: values[cardId].detail,
      }));
  }, [
    copy.accountsCount,
    copy.cardLabels,
    copy.overviewHint,
    copy.recentTransactions,
    displayCurrency,
    lang,
    overview,
    transactionsMonth?.month,
    configuredCards,
  ]);

  const categoryGroups = useMemo(() => buildCategoryGroups(categories), [categories]);

  if (loading) {
    return (
      <section className="finance-page">
        <div className="finance-shell-bar">
          <div className="finance-shell-copy">
            <span className="dashboard-placeholder-badge">{copy.sectionLabel}</span>
            <h2>{copy.overviewTitle}</h2>
          </div>
        </div>
        <div className="finance-summary-grid">
          {[1, 2, 3].map((item) => (
            <div key={item} className="finance-signal-card skeleton-block" />
          ))}
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <article className="dashboard-placeholder-card finance-page">
        <span className="dashboard-placeholder-badge">
          {lang === "ru" ? "Ошибка" : "Error"}
        </span>
        <h3>{lang === "ru" ? "Не удалось загрузить финансы" : "Failed to load finance"}</h3>
        <p>{error}</p>
      </article>
    );
  }

  if (!overview?.onboardingCompleted) {
    const canContinue = onboardingStep > 0 || onboarding.currency !== null;

    return (
      <section className="finance-page finance-onboarding-panel">
        <div className="finance-shell-bar finance-shell-bar--onboarding">
          <div className="finance-shell-copy">
            <span className="dashboard-placeholder-badge">{copy.stepTitle}</span>
            <h2>{copy.onboardingTitle}</h2>
            <p>{copy.onboardingSubtitle}</p>
          </div>
        </div>

        <div className="finance-stepper">
          {copy.onboardingSteps.map((step, index) => (
            <button
              key={step.title}
              className={`finance-step-card ${onboardingStep === index ? "active" : ""}`}
              type="button"
              onClick={() => onStepChange(index)}
            >
              <span>{index + 1}</span>
              <strong>{step.title}</strong>
            </button>
          ))}
        </div>

        <article className="finance-surface-card finance-surface-card--onboarding">
          <div className="finance-surface-head finance-surface-head--stack">
            <div>
              <span className="finance-surface-label">
                {lang === "ru" ? `Шаг ${onboardingStep + 1}` : `Step ${onboardingStep + 1}`}
              </span>
              <h3>{currentStep.title}</h3>
            </div>
            <p>{currentStep.body}</p>
          </div>

          {onboardingStep === 0 ? (
            <div className="finance-selection-grid">
              {copy.currencyChoices.map((currency) => (
                <button
                  key={currency.code}
                  className={`finance-selection-card ${onboarding.currency === currency.code ? "active" : ""}`}
                  type="button"
                  onClick={() => onSetOnboarding({ currency: currency.code })}
                >
                  <span>{currency.code}</span>
                  <strong>{currency.label}</strong>
                </button>
              ))}
            </div>
          ) : null}

          {onboardingStep === 1 ? (
            <div className="finance-selection-grid">
              {copy.accountProviders
                .filter((item) => item.code !== "cash")
                .map((provider) => (
                  <button
                    key={provider.code}
                    className={`finance-selection-card ${onboarding.bank === provider.code ? "active" : ""}`}
                    type="button"
                    onClick={() =>
                      onSetOnboarding({
                        bank: onboarding.bank === provider.code ? null : provider.code,
                      })
                    }
                  >
                    <span>{provider.label}</span>
                    <strong>{provider.description}</strong>
                  </button>
                ))}

              <label className="finance-field finance-field--wide">
                <span>{copy.amount}</span>
                <input
                  value={onboarding.primaryBalance}
                  onChange={(event) => onSetOnboarding({ primaryBalance: event.target.value })}
                  placeholder="0"
                />
              </label>
            </div>
          ) : null}

          {onboardingStep === 2 ? (
            <label className="finance-field finance-field--wide">
              <span>{copy.cashBalance}</span>
              <input
                value={onboarding.cash}
                onChange={(event) => onSetOnboarding({ cash: event.target.value })}
                placeholder="0"
              />
            </label>
          ) : null}

          <div className="finance-modal-actions">
            {onboardingStep > 0 ? (
              <button className="secondary-btn" type="button" onClick={() => onStepChange(onboardingStep - 1)}>
                {copy.back}
              </button>
            ) : (
              <button className="secondary-btn" type="button" onClick={() => onComplete(true)}>
                {copy.skipAll}
              </button>
            )}

            <button
              className="finance-text-link"
              type="button"
              onClick={() =>
                onboardingStep === 2 ? onComplete(true) : onStepChange(onboardingStep + 1)
              }
            >
              {copy.skipStep}
            </button>

            <button
              className="pill-btn"
              type="button"
              disabled={!canContinue}
              onClick={() =>
                onboardingStep === 2 ? onComplete(false) : onStepChange(onboardingStep + 1)
              }
            >
              {onboardingStep === 2 ? copy.finish : copy.continue}
            </button>
          </div>
        </article>
      </section>
    );
  }

  return (
    <section className="finance-page">
      <div className="finance-shell-bar finance-shell-bar--actions">
        <div className="finance-shell-actions">
          <button
            className="pill-btn"
            type="button"
            onClick={() => {
              void loadCategories();
              setTransactionFlowOpen(true);
            }}
          >
            {copy.addTransaction}
          </button>
          {financeTab === "accounts" ? (
            <button className="secondary-btn" type="button" onClick={() => setAccountModalState("new")}>
              {copy.addAccount}
            </button>
          ) : null}
          {financeTab === "overview" ? (
            <button
              className="secondary-btn finance-shell-settings-btn is-visible"
              type="button"
              onClick={() => setOverviewSettingsOpen(true)}
            >
              {copy.configureOverview}
            </button>
          ) : null}
        </div>
      </div>

      <div className="finance-tab-strip" role="tablist" aria-label={copy.sectionLabel}>
        {(Object.keys(copy.tabs) as FinanceTab[]).map((tab) => (
          <button
            key={tab}
            className={`finance-tab-pill ${financeTab === tab ? "active" : ""}`}
            type="button"
            onClick={() => onTabChange(tab)}
          >
            {copy.tabs[tab]}
          </button>
        ))}
      </div>

      <div className="finance-tab-content" key={financeTab}>
        {financeTab === "overview" ? (
          <div className="finance-overview-layout">
          <div key={`overview-grid-${overviewRefreshNonce}`} className="finance-summary-grid finance-summary-grid--animate">
            {summaryCards
              .filter((item) => item.id !== "recent_transactions")
              .map((item) => (
                <FinanceCard
                  key={item.id}
                  title={item.title}
                  value={item.value}
                  tone={item.tone}
                  detail={item.detail}
                  emphasize={item.id === "total_balance"}
                />
              ))}
          </div>

          {summaryCards.some((item) => item.id === "recent_transactions") ? (
            <article
              key={`overview-recent-${overviewRefreshNonce}`}
              className="finance-surface-card finance-surface-card--transactions finance-overview-block--animate"
            >
              <div className="finance-surface-head">
                <div>
                  <span className="finance-surface-label">{copy.overviewTitle}</span>
                  <h3>{copy.recentTransactions}</h3>
                </div>
                <button className="finance-text-link" type="button" onClick={() => onTabChange("transactions")}>
                  {copy.tabs.transactions}
                </button>
              </div>
              <FinanceTransactionList
                lang={lang}
                transactions={overview.recentTransactions}
                emptyText={copy.noTransactions}
              />
            </article>
          ) : null}
          </div>
        ) : null}

        {financeTab === "accounts" ? (
          <div className="finance-accounts-layout">
            <article className="finance-surface-card finance-surface-card--summary">
              <div className="finance-surface-head">
                <div>
                  <span className="finance-surface-label">{copy.accountsTitle}</span>
                  <h3>{copy.allAccounts}</h3>
                </div>
                <strong>{overview.accounts.length}</strong>
              </div>
              <p className="finance-surface-note">
                {copy.totalBalance}: {formatMoney(overview.totalBalanceMinor, displayCurrency, lang)}
              </p>
            </article>

            <div className="finance-account-grid">
              {overview.accounts.length ? (
                overview.accounts.map((account) => (
                  <article
                    key={account.id}
                    className={`finance-account-card finance-account-card--${accountTone(account.providerCode)}`}
                  >
                    <div className="finance-account-card-head">
                      <div>
                        <span>{account.bankName ?? account.name}</span>
                        <h3>{account.name}</h3>
                      </div>
                      <button
                        className="finance-icon-btn"
                        type="button"
                        onClick={() => setAccountModalState(account)}
                      >
                        •••
                      </button>
                    </div>
                    <strong>{formatMoney(account.balanceMinor, account.currency, lang)}</strong>
                    <div className="finance-account-meta">
                      <span>{account.kind === "cash" ? copy.cashBalance : copy.cardBalance}</span>
                      <span>
                        {lang === "ru" ? "Операций" : "Transactions"}: {account.transactionCount}
                      </span>
                      {account.isPrimary ? <span>Primary</span> : null}
                    </div>
                  </article>
                ))
              ) : (
                <article className="finance-surface-card">
                  <p className="finance-empty-copy">{copy.noAccounts}</p>
                </article>
              )}
            </div>
          </div>
        ) : null}

        {financeTab === "transactions" ? (
          <div className="finance-transactions-layout">
            <article className="finance-surface-card finance-surface-card--filters">
              <div className="finance-filters-row">
                <label className="finance-field finance-field--month">
                  <span>{copy.monthPlaceholder}</span>
                  <select
                    value={selectedMonth ?? ""}
                    onChange={(event) => {
                      const month = event.target.value;
                      startTransition(() => {
                        setSelectedMonth(month);
                      });
                      void loadTransactions(month);
                    }}
                  >
                    {transactionsMonth?.availableMonths.map((month) => (
                      <option key={month} value={month}>
                        {formatMonthLabel(month, lang)}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </article>

            <article className="finance-surface-card finance-surface-card--transactions">
              <div className="finance-surface-head">
                <div>
                  <span className="finance-surface-label">{copy.transactionsTitle}</span>
                  <h3>
                    {selectedMonth ? formatMonthLabel(selectedMonth, lang) : copy.monthPlaceholder}
                  </h3>
                </div>
                {transactionsLoading ? <strong>...</strong> : null}
              </div>

              {transactionsError ? (
                <p className="finance-inline-error">{transactionsError}</p>
              ) : (
                <FinanceTransactionList
                  lang={lang}
                  transactions={transactionsMonth?.transactions ?? []}
                  emptyText={copy.noTransactions}
                />
              )}
            </article>
          </div>
        ) : null}

        {financeTab === "categories" ? (
          <div className="finance-category-layout">
            {categoriesError ? <p className="finance-inline-error">{categoriesError}</p> : null}
            {categoriesLoading ? (
              <div className="finance-summary-grid">
                {[1, 2].map((item) => (
                  <div key={item} className="finance-surface-card skeleton-block" />
                ))}
              </div>
            ) : categories.length ? (
              <>
                <FinanceCategoryTree
                  lang={lang}
                  title={copy.categoriesExpense}
                  items={categoryGroups.expense}
                />
                <FinanceCategoryTree
                  lang={lang}
                  title={copy.categoriesIncome}
                  items={categoryGroups.income}
                />
              </>
            ) : (
              <article className="finance-surface-card">
                <p className="finance-empty-copy">{copy.noCategories}</p>
              </article>
            )}
          </div>
        ) : null}

        {financeTab === "analytics" ? (
          <article className="finance-surface-card finance-surface-card--placeholder">
            <div className="finance-surface-head">
              <div>
                <span className="finance-surface-label">{copy.analyticsTitle}</span>
                <h3>{copy.tabs.analytics}</h3>
              </div>
            </div>
            <p className="finance-empty-copy">{copy.analyticsStub}</p>
          </article>
        ) : null}
      </div>

      {overviewSettingsOpen ? (
        <FinanceOverviewSettingsModal
          lang={lang}
          overview={overview}
          onClose={() => setOverviewSettingsOpen(false)}
          onSaved={(next) => {
            onOverviewChange(next);
            setOverviewRefreshNonce((value) => value + 1);
          }}
        />
      ) : null}

      {accountModalState ? (
        <FinanceAccountModal
          lang={lang}
          overview={overview}
          account={accountModalState === "new" ? null : accountModalState}
          onClose={() => setAccountModalState(null)}
          onSaved={onOverviewChange}
        />
      ) : null}

      {transactionFlowOpen ? (
        <FinanceTransactionFlow
          lang={lang}
          overview={overview}
          categories={categories}
          categoriesLoading={categoriesLoading}
          onOverviewChange={onOverviewChange}
          onTransactionsRefresh={() => loadTransactions(selectedMonth ?? undefined)}
          onClose={() => setTransactionFlowOpen(false)}
        />
      ) : null}
    </section>
  );
}
