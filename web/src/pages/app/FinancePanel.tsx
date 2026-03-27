import "./finance.css";
import {
  Currency,
  FinanceAccount,
  FinanceOnboardingState,
  FinanceOverview,
  FinanceTab,
} from "../../types";

export const financeTabs: FinanceTab[] = [
  "overview",
  "accounts",
  "transactions",
  "settings",
];

export function formatMoney(
  minor: number,
  currency: Currency | null,
  lang: "ru" | "en"
) {
  const locale = lang === "ru" ? "ru-RU" : "en-US";
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency: currency ?? "RUB",
    maximumFractionDigits: 2,
  }).format(minor / 100);
}

export function parseAmountToMinor(value: string) {
  const normalized = value.replace(/\s/g, "").replace(",", ".");
  if (!normalized) return null;
  const amount = Number(normalized);
  if (Number.isNaN(amount) || amount < 0) return null;
  return Math.round(amount * 100);
}

export type FinanceCopy = {
  title: string;
  subtitle: string;
  onboardingTitle: string;
  onboardingSubtitle: string;
  skipAll: string;
  continue: string;
  back: string;
  finish: string;
  done: string;
  later: string;
  tabs: Record<FinanceTab, string>;
  steps: Array<{
    title: string;
    body: string;
  }>;
  totalBalance: string;
  emptyTitle: string;
  emptyBody: string;
  addAccount: string;
  addTransaction: string;
  accountsTitle: string;
  transactionsTitle: string;
  settingsTitle: string;
  noTransactions: string;
  noAccounts: string;
  summaryNote: string;
  primaryBank: string;
  cash: string;
  onboardingDone: string;
  currencyChoices: Array<{ code: Currency; label: string }>;
  bankChoices: string[];
  cardBalanceLabel: string;
  cashLabel: string;
  skipStep: string;
};

export function getFinanceCopy(lang: "ru" | "en"): FinanceCopy {
  if (lang === "ru") {
    return {
      title: "Финансы",
      subtitle: "Управляйте общим балансом, счетами и будущими транзакциями в одном рабочем пространстве.",
      onboardingTitle: "Настройте финансы за минуту",
      onboardingSubtitle:
        "Сначала фиксируем базу: валюта, основной счёт и наличные. Всё можно пропустить и настроить позже.",
      skipAll: "Пропустить онбординг",
      continue: "Далее",
      back: "Назад",
      finish: "Завершить",
      done: "Онбординг завершён",
      later: "Настроить позже",
      tabs: {
        overview: "Главная",
        accounts: "Счета",
        transactions: "Транзакции",
        settings: "Настройки",
      },
      steps: [
        {
          title: "Основная валюта",
          body: "Выберите валюту по умолчанию для баланса, карточек и будущих отчётов.",
        },
        {
          title: "Основной банк",
          body: "Добавьте главный карточный счёт и при желании укажите его стартовый баланс.",
        },
        {
          title: "Наличные",
          body: "Если хотите, сразу зафиксируйте сумму наличных. Это попадёт в общий баланс.",
        },
      ],
      totalBalance: "Текущий баланс",
      emptyTitle: "Финансовая база ещё не заполнена",
      emptyBody: "После онбординга здесь появятся баланс, список счетов и первые транзакции.",
      addAccount: "Добавить счёт",
      addTransaction: "Добавить транзакцию",
      accountsTitle: "Счета",
      transactionsTitle: "Последние транзакции",
      settingsTitle: "Настройки раздела",
      noTransactions: "Транзакций пока нет. Каркас готов, дальше можно подключать ввод операций.",
      noAccounts: "Счета пока не добавлены.",
      summaryNote: "Общий баланс считается из карточных счетов и наличных.",
      primaryBank: "Основной счёт",
      cash: "Наличные",
      onboardingDone: "Базовая финансовая структура создана и сохранена в Supabase.",
      currencyChoices: [
        { code: "RUB", label: "Рубли" },
        { code: "USD", label: "Доллары" },
        { code: "EUR", label: "Евро" },
      ],
      bankChoices: ["Т-Банк", "Сбер", "Альфа", "ВТБ"],
      cardBalanceLabel: "Стартовый баланс карты",
      cashLabel: "Сумма наличных",
      skipStep: "Пропустить шаг",
    };
  }

  return {
    title: "Finance",
    subtitle: "Manage your total balance, accounts, and future transaction flow in one workspace.",
    onboardingTitle: "Set up finance in under a minute",
    onboardingSubtitle:
      "Lock the basics first: currency, primary account, and cash. You can skip any step and finish later.",
    skipAll: "Skip onboarding",
    continue: "Continue",
    back: "Back",
    finish: "Finish",
    done: "Onboarding complete",
    later: "Set up later",
    tabs: {
      overview: "Overview",
      accounts: "Accounts",
      transactions: "Transactions",
      settings: "Settings",
    },
    steps: [
      {
        title: "Base currency",
        body: "Pick the default currency for balances, cards, and future reports.",
      },
      {
        title: "Primary bank",
        body: "Add your main card account and optionally set its starting balance.",
      },
      {
        title: "Cash",
        body: "If needed, set your cash amount now so it lands in the total balance.",
      },
    ],
    totalBalance: "Current balance",
    emptyTitle: "Your finance base is still empty",
    emptyBody: "After onboarding, this area will show balance, accounts, and first transactions.",
    addAccount: "Add account",
    addTransaction: "Add transaction",
    accountsTitle: "Accounts",
    transactionsTitle: "Recent transactions",
    settingsTitle: "Module settings",
    noTransactions: "No transactions yet. The shell is ready for the next data-entry stage.",
    noAccounts: "No accounts yet.",
    summaryNote: "Total balance is calculated from bank-card accounts and cash.",
    primaryBank: "Primary account",
    cash: "Cash",
    onboardingDone: "The finance base is now created and persisted in Supabase.",
    currencyChoices: [
      { code: "RUB", label: "Rubles" },
      { code: "USD", label: "US Dollars" },
      { code: "EUR", label: "Euro" },
    ],
    bankChoices: ["T-Bank", "Sber", "Alfa", "VTB"],
    cardBalanceLabel: "Starting card balance",
    cashLabel: "Cash amount",
    skipStep: "Skip step",
  };
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
}: {
  lang: "ru" | "en";
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
}) {
  const copy = getFinanceCopy(lang);
  const selectedCurrency = onboarding.currency ?? "RUB";

  if (loading) {
    return (
      <section className="finance-panel">
        <div className="finance-panel-hero">
          <div className="skeleton" style={{ height: "24px", width: "80px", marginBottom: "16px", borderRadius: "999px" }} />
          <div className="skeleton" style={{ height: "42px", width: "240px", marginBottom: "12px" }} />
          <div className="skeleton" style={{ height: "20px", width: "80%" }} />
        </div>
        <div className="finance-grid" style={{ marginTop: "32px" }}>
          <div className="finance-balance-card skeleton" style={{ height: "220px", border: "none", background: "none" }} />
          <div className="finance-side-card skeleton" style={{ height: "140px", border: "none" }} />
          <div className="finance-list-card skeleton" style={{ height: "300px", border: "none" }} />
          <div className="finance-list-card skeleton" style={{ height: "300px", border: "none" }} />
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <article className="dashboard-placeholder-card finance-panel">
        <span className="dashboard-placeholder-badge">
          {lang === "ru" ? "Ошибка" : "Error"}
        </span>
        <h3>{lang === "ru" ? "Не удалось открыть финансы" : "Finance failed to load"}</h3>
        <p>{error}</p>
      </article>
    );
  }

  if (!overview?.onboardingCompleted) {
    const step = copy.steps[onboardingStep]!;
    const canContinue =
      onboardingStep === 0
        ? onboarding.currency !== null
        : true;

    return (
      <section className="finance-panel finance-onboarding">
        <div className="finance-panel-hero">
          <span className="dashboard-placeholder-badge">
            {lang === "ru" ? "Онбординг" : "Onboarding"}
          </span>
          <h3>{copy.onboardingTitle}</h3>
          <p>{copy.onboardingSubtitle}</p>
        </div>

        <div className="finance-stepper">
          {copy.steps.map((item, index) => (
            <button
              key={item.title}
              className={`finance-step-dot ${
                onboardingStep === index ? "active" : ""
              }`}
              type="button"
              onClick={() => onStepChange(index)}
            >
              <span>{index + 1}</span>
              <strong>{item.title}</strong>
            </button>
          ))}
        </div>

        <article className="finance-onboarding-card">
          <div className="finance-onboarding-copy">
            <span className="finance-onboarding-index">
              {lang === "ru" ? `Шаг ${onboardingStep + 1}` : `Step ${onboardingStep + 1}`}
            </span>
            <h4>{step.title}</h4>
            <p>{step.body}</p>
          </div>

          {onboardingStep === 0 ? (
            <div className="finance-choice-grid">
              {copy.currencyChoices.map((currency) => (
                <button
                  key={currency.code}
                  className={`finance-choice-card ${
                    onboarding.currency === currency.code ? "active" : ""
                  }`}
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
            <div className="finance-form-grid">
              <div className="finance-choice-grid">
                {copy.bankChoices.map((bank) => (
                  <button
                    key={bank}
                    className={`finance-choice-card ${
                      onboarding.bank === bank ? "active" : ""
                    }`}
                    type="button"
                    onClick={() =>
                      onSetOnboarding({
                        bank: onboarding.bank === bank ? null : bank,
                      })
                    }
                  >
                    <span>{lang === "ru" ? "Банк" : "Bank"}</span>
                    <strong>{bank}</strong>
                  </button>
                ))}
              </div>

              <label className="finance-input-field">
                <span>{copy.cardBalanceLabel}</span>
                <input
                  value={onboarding.primaryBalance}
                  onChange={(event) =>
                    onSetOnboarding({ primaryBalance: event.target.value })
                  }
                  placeholder={lang === "ru" ? "Например, 120000" : "For example, 120000"}
                />
              </label>
            </div>
          ) : null}

          {onboardingStep === 2 ? (
            <label className="finance-input-field">
              <span>{copy.cashLabel}</span>
              <input
                value={onboarding.cash}
                onChange={(event) =>
                  onSetOnboarding({ cash: event.target.value })
                }
                placeholder={lang === "ru" ? "Например, 15000" : "For example, 15000"}
              />
            </label>
          ) : null}

          <div className="finance-onboarding-actions">
            <button
              className="secondary-btn"
              type="button"
              onClick={() =>
                onboardingStep === 2 ? onComplete(false) : onStepChange(onboardingStep + 1)
              }
              disabled={!canContinue}
            >
              {onboardingStep === 2 ? copy.finish : copy.continue}
            </button>

            {onboardingStep > 0 ? (
              <button
                className="finance-text-btn"
                type="button"
                onClick={() => onStepChange(onboardingStep - 1)}
              >
                {copy.back}
              </button>
            ) : null}

            <button
              className="finance-text-btn"
              type="button"
              onClick={() =>
                onboardingStep === 2 ? onComplete(true) : onStepChange(onboardingStep + 1)
              }
            >
              {onboardingStep === 2 ? copy.skipAll : copy.skipStep}
            </button>
          </div>
        </article>
      </section>
    );
  }

  const displayCurrency = overview.defaultCurrency ?? "RUB";

  return (
    <section className="finance-panel">
      <div className="finance-panel-hero">
        <span className="dashboard-placeholder-badge">
          {lang === "ru" ? "Финансы" : "Finance"}
        </span>
        <h3>{copy.title}</h3>
        <p>{copy.subtitle}</p>
      </div>

      <nav className="finance-subnav" aria-label="Finance navigation">
        {financeTabs.map((tab) => (
          <button
            key={tab}
            className={`finance-subnav-item ${financeTab === tab ? "active" : ""}`}
            type="button"
            onClick={() => onTabChange(tab)}
          >
            {copy.tabs[tab]}
          </button>
        ))}
      </nav>

      {financeTab === "overview" ? (
        <div className="finance-grid">
          <article className="finance-balance-card">
            <span>{copy.totalBalance}</span>
            <strong>
              {formatMoney(overview.totalBalanceMinor, displayCurrency, lang)}
            </strong>
            <p>{copy.summaryNote}</p>
            <div className="finance-action-row">
              <button className="pill-btn" type="button">
                {copy.addAccount}
              </button>
              <button className="secondary-btn" type="button">
                {copy.addTransaction}
              </button>
            </div>
          </article>

          <article className="finance-list-card">
            <div className="finance-card-head">
              <h4>{copy.accountsTitle}</h4>
            </div>
            {overview.accounts.length ? (
              <div className="finance-account-list">
                {overview.accounts.map((account) => (
                  <div key={account.id} className="finance-account-item">
                    <div>
                      <strong>{account.name}</strong>
                      <span>
                        {account.kind === "cash"
                          ? copy.cash
                          : account.isPrimary
                            ? copy.primaryBank
                            : account.bankName ?? account.name}
                      </span>
                    </div>
                    <b>
                      {formatMoney(
                        account.balanceMinor,
                        account.currency,
                        lang,
                      )}
                    </b>
                  </div>
                ))}
              </div>
            ) : (
              <p>{copy.noAccounts}</p>
            )}
          </article>

          <article className="finance-list-card">
            <div className="finance-card-head">
              <h4>{copy.transactionsTitle}</h4>
            </div>
            {overview.recentTransactions.length ? (
              <div className="finance-transaction-list">
                {overview.recentTransactions.map((transaction) => (
                  <div key={transaction.id} className="finance-transaction-item">
                    <div>
                      <strong>{transaction.title}</strong>
                      <span>
                        {new Intl.DateTimeFormat(
                          lang === "ru" ? "ru-RU" : "en-US",
                          { dateStyle: "medium", timeStyle: "short" },
                        ).format(new Date(transaction.happenedAt))}
                      </span>
                    </div>
                    <b>
                      {formatMoney(
                        transaction.amountMinor,
                        transaction.currency,
                        lang,
                      )}
                    </b>
                  </div>
                ))}
              </div>
            ) : (
              <p>{copy.noTransactions}</p>
            )}
          </article>
        </div>
      ) : null}

      {financeTab === "accounts" ? (
        <article className="finance-list-card">
          <div className="finance-card-head">
            <h4>{copy.accountsTitle}</h4>
          </div>
          {overview.accounts.length ? (
            <div className="finance-account-list finance-account-list-wide">
              {overview.accounts.map((account) => (
                <div key={account.id} className="finance-account-item finance-account-item-wide">
                  <div>
                    <strong>{account.name}</strong>
                    <span>{account.kind === "cash" ? copy.cash : account.bankName ?? account.name}</span>
                  </div>
                  <b>{formatMoney(account.balanceMinor, account.currency, lang)}</b>
                </div>
              ))}
            </div>
          ) : (
            <p>{copy.noAccounts}</p>
          )}
        </article>
      ) : null}

      {financeTab === "transactions" ? (
        <article className="finance-list-card">
          <div className="finance-card-head">
            <h4>{copy.transactionsTitle}</h4>
          </div>
          <p>{copy.noTransactions}</p>
        </article>
      ) : null}

      {financeTab === "settings" ? (
        <article className="finance-list-card">
          <div className="finance-card-head">
            <h4>{copy.settingsTitle}</h4>
          </div>
          <div className="finance-settings-list">
            <div className="finance-setting-row">
              <span>{lang === "ru" ? "Статус" : "Status"}</span>
              <strong>{copy.done}</strong>
            </div>
            <div className="finance-setting-row">
              <span>{lang === "ru" ? "Валюта по умолчанию" : "Default currency"}</span>
              <strong>{selectedCurrency}</strong>
            </div>
            <div className="finance-setting-row">
              <span>{lang === "ru" ? "Количество счетов" : "Accounts count"}</span>
              <strong>{overview.accounts.length}</strong>
            </div>
          </div>
        </article>
      ) : null}
    </section>
  );
}
