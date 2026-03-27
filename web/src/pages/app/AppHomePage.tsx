import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { User } from "@supabase/supabase-js";
import { useNavigate } from "react-router-dom";

import { TopNav } from "../../components/top/TopNav";
import { signOutEverywhere, supabase } from "../../lib/supabaseClient";

import { DashboardSection, FinanceOverview, FinanceTab, SectionCopy, FinanceOnboardingState } from '../../types';
import { FinancePanel, parseAmountToMinor } from './FinancePanel';

const sections: DashboardSection[] = [
  "home",
  "finance",
  "health",
  "tasks",
  "settings",
];

function formatUserName(user: User | null, fallback: string) {
  const email = user?.email?.trim();
  if (!email) return fallback;
  const head = email.split("@")[0] ?? email;
  return head.charAt(0).toUpperCase() + head.slice(1);
}

function getCopy(lang: "ru" | "en"): Record<DashboardSection, SectionCopy> {
  if (lang === "ru") {
    return {
      home: {
        label: "Главная",
        title: "Главная",
        note: "Раздел в разработке. Здесь появится главный обзор проекта, быстрые действия и персональная сводка.",
        mobileIcon: "GL",
      },
      finance: {
        label: "Финансы",
        title: "Финансы",
        note: "Центр финансового контроля: баланс, счета, транзакции и настройки.",
        mobileIcon: "FN",
      },
      health: {
        label: "Здоровье",
        title: "Здоровье",
        note: "Раздел в разработке. Здесь появятся трекинг самочувствия, метрики и история состояния.",
        mobileIcon: "HL",
      },
      tasks: {
        label: "Задачи",
        title: "Задачи",
        note: "Раздел в разработке. Здесь будут списки задач, статусы, приоритеты и рабочие потоки.",
        mobileIcon: "TK",
      },
      settings: {
        label: "Настройки",
        title: "Настройки",
        note: "Раздел в разработке. Здесь будут параметры приложения, профиля и подключённых сервисов.",
        mobileIcon: "NS",
      },
    };
  }

  return {
    home: {
      label: "Home",
      title: "Home",
      note: "This section is in development. It will contain the main project overview, quick actions, and personal summary.",
      mobileIcon: "HM",
    },
    finance: {
      label: "Finance",
      title: "Finance",
      note: "Your finance workspace: balance, accounts, transactions, and setup.",
      mobileIcon: "FN",
    },
    health: {
      label: "Health",
      title: "Health",
      note: "This section is in development. It will contain wellbeing tracking, metrics, and health history.",
      mobileIcon: "HL",
    },
    tasks: {
      label: "Tasks",
      title: "Tasks",
      note: "This section is in development. It will contain task lists, statuses, priorities, and work flows.",
      mobileIcon: "TS",
    },
    settings: {
      label: "Settings",
      title: "Settings",
      note: "This section is in development. It will contain app, profile, and connected service settings.",
      mobileIcon: "ST",
    },
  };
}

export function AppHomePage() {
  const { i18n } = useTranslation();
  const navigate = useNavigate();
  const [user, setUser] = useState<User | null>(null);
  const [section, setSection] = useState<DashboardSection>(() => {
    return (localStorage.getItem("dashboard_section") as DashboardSection) || "home";
  });
  const [financeTab, setFinanceTab] = useState<FinanceTab>("overview");
  const [financeOverview, setFinanceOverview] = useState<FinanceOverview | null>(null);
  const [financeLoading, setFinanceLoading] = useState(false);
  const [financeError, setFinanceError] = useState<string | null>(null);
  const [financeOnboardingStep, setFinanceOnboardingStep] = useState(0);
  const [financeOnboarding, setFinanceOnboarding] = useState<FinanceOnboardingState>({
    currency: null,
    bank: null,
    primaryBalance: "",
    cash: "",
  });

  const lang = i18n.language.startsWith("ru") ? "ru" : "en";
  const copy = useMemo(() => getCopy(lang), [lang]);
  const userName = useMemo(
    () => formatUserName(user, lang === "ru" ? "пользователь" : "user"),
    [lang, user],
  );
  const userEmail = user?.email ?? "—";
  const active = copy[section];

  const loadFinanceOverview = async () => {
    setFinanceLoading(true);
    setFinanceError(null);
    const { data, error } = await supabase.rpc("finance_get_overview");
    if (error) {
      setFinanceError(error.message);
      setFinanceLoading(false);
      return;
    }
    setFinanceOverview(data as FinanceOverview);
    setFinanceLoading(false);
  };

  useEffect(() => {
    let mounted = true;
    void supabase.auth.getUser().then(({ data }) => {
      if (mounted) setUser(data.user);
    });

    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((_event, session) => {
      if (mounted) setUser(session?.user ?? null);
    });

    return () => {
      mounted = false;
      subscription.unsubscribe();
    };
  }, []);

  useEffect(() => {
    localStorage.setItem("dashboard_section", section);
  }, [section]);

  useEffect(() => {
    if (!user || section !== "finance") return;
    void loadFinanceOverview();
  }, [section, user]);

  const onLogout = async () => {
    await signOutEverywhere();
    navigate("/auth/login", { replace: true });
  };

  const completeFinanceOnboarding = async (skip: boolean) => {
    setFinanceLoading(true);
    setFinanceError(null);
    const { data, error } = await supabase.rpc("finance_complete_onboarding", {
      p_currency: skip ? null : financeOnboarding.currency,
      p_bank: skip ? null : financeOnboarding.bank,
      p_cash_minor: skip ? null : parseAmountToMinor(financeOnboarding.cash),
      p_primary_account_balance_minor: skip
        ? null
        : parseAmountToMinor(financeOnboarding.primaryBalance),
    });

    if (error) {
      setFinanceError(error.message);
      setFinanceLoading(false);
      return;
    }

    setFinanceOverview(data as FinanceOverview);
    setFinanceOnboardingStep(0);
    setFinanceLoading(false);
  };

  return (
    <div className="wrap dashboard-wrap">
      <TopNav variant="auth" />

      <section className="dashboard-shell dashboard-shell-clean">
        <aside className="dashboard-sidebar dashboard-sidebar-clean">
          <div className="dashboard-brand">A</div>
          <div>
            <p className="dashboard-sidebar-label">Workspace</p>
            <h1 className="dashboard-sidebar-title">Assistant</h1>
            <p className="dashboard-sidebar-copy">
              {lang === "ru"
                ? "Общий центр управления после авторизации."
                : "Shared control center after sign-in."}
            </p>
          </div>

          <nav className="dashboard-nav" aria-label="Dashboard navigation">
            {sections.map((item) => (
              <button
                key={item}
                className={`dashboard-nav-item ${
                  section === item ? "active" : ""
                }`}
                type="button"
                onClick={() => setSection(item)}
              >
                {copy[item].label}
              </button>
            ))}
          </nav>

          <div className="dashboard-profile">
            <p className="dashboard-sidebar-label">
              {lang === "ru" ? "Пользователь" : "User"}
            </p>
            <strong>{userEmail}</strong>
            <span>
              {lang === "ru"
                ? "Сессия активна и защищена через Supabase Auth."
                : "Session is active and protected with Supabase Auth."}
            </span>
          </div>

        </aside>

        <main className="dashboard-main dashboard-main-clean">
          <div className="dashboard-page-shell" key={`${lang}-${section}`}>
            {section !== "finance" ? (
              <header className="dashboard-header dashboard-header-clean">
                <div>
                  <p className="dashboard-eyebrow">{active.label}</p>
                  <h2>
                    {lang === "ru"
                      ? `${active.title}, ${userName}.`
                      : `${active.title}, ${userName}.`}
                  </h2>
                </div>
              </header>
            ) : null}

            {section === "finance" ? (
              <FinancePanel
                lang={lang}
                overview={financeOverview}
                loading={financeLoading}
                error={financeError}
                financeTab={financeTab}
                onTabChange={setFinanceTab}
                onboarding={financeOnboarding}
                onboardingStep={financeOnboardingStep}
                onSetOnboarding={(patch) =>
                  setFinanceOnboarding((current) => ({ ...current, ...patch }))
                }
                onStepChange={setFinanceOnboardingStep}
                onComplete={completeFinanceOnboarding}
              />
            ) : (
              <article className="dashboard-placeholder-card">
                <span className="dashboard-placeholder-badge">
                  {section === "settings"
                    ? lang === "ru"
                      ? "Безопасность"
                      : "Security"
                    : lang === "ru"
                      ? "В разработке"
                      : "In development"}
                </span>
                <h3>{active.title}</h3>
                <p>{active.note}</p>
                {section === "settings" ? (
                  <div className="settings-actions">
                    <button
                      className="dashboard-logout settings-logout"
                      type="button"
                      onClick={onLogout}
                    >
                      {lang === "ru" ? "Выйти из аккаунта" : "Sign out"}
                    </button>
                  </div>
                ) : null}
              </article>
            )}
          </div>
        </main>
      </section>

      <nav className="dashboard-bottom-nav" aria-label="Mobile dashboard navigation">
        {sections.map((item) => (
          <button
            key={item}
            className={`dashboard-bottom-item ${
              section === item ? "active" : ""
            }`}
            type="button"
            onClick={() => setSection(item)}
          >
            <span className="dashboard-bottom-item-inner">
              <span className="dashboard-bottom-item-icon">
                {copy[item].mobileIcon}
              </span>
              <span className="dashboard-bottom-item-label">
                {copy[item].label}
              </span>
            </span>
          </button>
        ))}
      </nav>
    </div>
  );
}
