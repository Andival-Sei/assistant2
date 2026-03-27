import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { User } from "@supabase/supabase-js";
import { useNavigate } from "react-router-dom";

import { TopNav } from "../../components/top/TopNav";
import { signOutEverywhere, supabase } from "../../lib/supabaseClient";
import {
  DashboardSection,
  DashboardSubsection,
  FinanceOverview,
  FinanceTab,
  FinanceOnboardingState,
} from "../../types";
import { FinancePanel, parseAmountToMinor } from './FinancePanel';
import { getDashboardShellConfig, getDefaultSubsection } from "./shellConfig";

function formatUserName(user: User | null, fallback: string) {
  const email = user?.email?.trim();
  if (!email) return fallback;
  const head = email.split("@")[0] ?? email;
  return head.charAt(0).toUpperCase() + head.slice(1);
}

function SectionIcon({ section }: { section: DashboardSection }) {
  const stroke = {
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.8,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
  };

  switch (section) {
    case "home":
      return (
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path {...stroke} d="M3.5 9.2 10 4l6.5 5.2v6.3a1 1 0 0 1-1 1H4.5a1 1 0 0 1-1-1Z" />
          <path {...stroke} d="M8 16.5v-4.2h4v4.2" />
        </svg>
      );
    case "finance":
      return (
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <rect {...stroke} x="2.8" y="4.8" width="14.4" height="10.4" rx="2.2" />
          <path {...stroke} d="M2.8 8.3h14.4" />
          <path {...stroke} d="M6.4 12.1h2.2" />
        </svg>
      );
    case "health":
      return (
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path
            {...stroke}
            d="M10 16.6 4.6 11.3A3.6 3.6 0 0 1 9.7 6l.3.3.3-.3a3.6 3.6 0 0 1 5.1 5.1Z"
          />
        </svg>
      );
    case "tasks":
      return (
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path {...stroke} d="M6.4 6.1h8.1M6.4 10h8.1M6.4 13.9h8.1" />
          <path {...stroke} d="m3.8 6.2.9.9 1.6-1.8M3.8 10.1l.9.9 1.6-1.8M3.8 14l.9.9 1.6-1.8" />
        </svg>
      );
    case "chat":
      return (
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path {...stroke} d="M5.1 4.4h9.8a1.8 1.8 0 0 1 1.8 1.8v5.6a1.8 1.8 0 0 1-1.8 1.8H10l-3.5 2.3v-2.3H5.1a1.8 1.8 0 0 1-1.8-1.8V6.2a1.8 1.8 0 0 1 1.8-1.8Z" />
        </svg>
      );
    case "settings":
      return (
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <circle {...stroke} cx="10" cy="10" r="2.7" />
          <path {...stroke} d="M10 2.7v2.1M10 15.2v2.1M17.3 10h-2.1M4.8 10H2.7" />
          <path {...stroke} d="m15.2 4.8-1.5 1.5M6.3 13.7l-1.5 1.5M15.2 15.2l-1.5-1.5M6.3 6.3 4.8 4.8" />
        </svg>
      );
  }
}

export function AppHomePage() {
  const { i18n } = useTranslation();
  const navigate = useNavigate();
  const [user, setUser] = useState<User | null>(null);
  const lang = i18n.language.startsWith("ru") ? "ru" : "en";
  const shellConfig = useMemo(() => getDashboardShellConfig(lang), [lang]);
  const [section, setSection] = useState<DashboardSection>(() => {
    return (localStorage.getItem("dashboard_section") as DashboardSection) || "home";
  });
  const [subsection, setSubsection] = useState<DashboardSubsection>(() => {
    return (localStorage.getItem("dashboard_subsection") as DashboardSubsection) || "summary";
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

  const userName = useMemo(
    () => formatUserName(user, lang === "ru" ? "пользователь" : "user"),
    [lang, user],
  );
  const active = shellConfig[section];
  const isChatSection = section === "chat";
  const availableSubsections = active.subsections;
  const activeSubsection =
    availableSubsections.find((item) => item.id === subsection) ?? availableSubsections[0];
  const primaryRailItems = (Object.keys(shellConfig) as DashboardSection[]).filter(
    (item) => item !== "settings",
  );

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
    localStorage.setItem("dashboard_subsection", subsection);
  }, [subsection]);

  useEffect(() => {
    const nextDefault = getDefaultSubsection(shellConfig, section);
    const isAvailable = shellConfig[section].subsections.some((item) => item.id === subsection);
    if (!isAvailable) {
      setSubsection(nextDefault);
    }
  }, [section, shellConfig, subsection]);

  useEffect(() => {
    if (section === "finance" && (
      subsection === "overview" ||
      subsection === "accounts" ||
      subsection === "transactions" ||
      subsection === "settings"
    )) {
      setFinanceTab(subsection);
    }
  }, [section, subsection]);

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
    <div className="dashboard-app-shell">
      <aside className="dashboard-left-rail">
        <div className="dashboard-left-rail-inner">
          <div className="dashboard-brand-block">
            <div className="dashboard-brand">A</div>
            <div>
              <p className="dashboard-sidebar-label">Workspace</p>
              <h1 className="dashboard-sidebar-title">Assistant</h1>
            </div>
          </div>

          <nav className="dashboard-primary-nav" aria-label="Dashboard navigation">
            {primaryRailItems.map((item) => (
              <button
                key={item}
                className={`dashboard-nav-item ${section === item ? "active" : ""}`}
                type="button"
                onClick={() => {
                  setSection(item);
                  setSubsection(getDefaultSubsection(shellConfig, item));
                }}
              >
                <span className="dashboard-nav-icon">
                  <SectionIcon section={item} />
                </span>
                <span className="dashboard-nav-copy">
                  <strong>{shellConfig[item].label}</strong>
                </span>
              </button>
            ))}
          </nav>

          <div className="dashboard-rail-footer">
            <button
              className={`dashboard-nav-item dashboard-nav-item-footer ${section === "settings" ? "active" : ""}`}
              type="button"
              onClick={() => {
                setSection("settings");
                setSubsection(getDefaultSubsection(shellConfig, "settings"));
              }}
            >
              <span className="dashboard-nav-icon">
                <SectionIcon section="settings" />
              </span>
              <span className="dashboard-nav-copy">
                <strong>{shellConfig.settings.label}</strong>
              </span>
            </button>
          </div>
        </div>
      </aside>

      <main className="dashboard-stage">
        <div className="dashboard-stage-topbar">
          <TopNav variant="auth" />
        </div>

        <section
          className={`dashboard-stage-shell ${
            isChatSection ? "dashboard-stage-shell--chat" : "dashboard-stage-shell--dashboard"
          }`}
        >
          <div
            className={`dashboard-stage-content ${
              isChatSection ? "dashboard-stage-content--chat" : "dashboard-stage-content--dashboard"
            }`}
          >
            <header
              className={`dashboard-shell-header ${
                isChatSection ? "dashboard-shell-header--chat" : "dashboard-shell-header--dashboard"
              }`}
            >
              <div className="dashboard-shell-heading">
                <span className="dashboard-shell-badge">{active.badge}</span>
                <p className="dashboard-eyebrow">{active.eyebrow}</p>
                <h2>{lang === "ru" ? `${active.title}, ${userName}.` : `${active.title}, ${userName}.`}</h2>
                <p className="dashboard-shell-note">{active.note}</p>
              </div>

              <nav className="dashboard-secondary-nav" aria-label="Section navigation">
                {availableSubsections.map((item) => (
                  <button
                    key={item.id}
                    className={`dashboard-secondary-item ${activeSubsection.id === item.id ? "active" : ""}`}
                    type="button"
                    onClick={() => {
                      setSubsection(item.id);
                      if (
                        section === "finance" &&
                        (item.id === "overview" ||
                          item.id === "accounts" ||
                          item.id === "transactions" ||
                          item.id === "settings")
                      ) {
                        onSetFinanceTab(item.id);
                      }
                    }}
                  >
                    {item.label}
                  </button>
                ))}
              </nav>
            </header>

            <div className="dashboard-content-stage" key={`${lang}-${section}-${activeSubsection.id}`}>
              {section === "finance" ? (
                <FinancePanel
                  lang={lang}
                  overview={financeOverview}
                  loading={financeLoading}
                  error={financeError}
                  financeTab={financeTab}
                  onTabChange={onSetFinanceTab}
                  onboarding={financeOnboarding}
                  onboardingStep={financeOnboardingStep}
                  onSetOnboarding={(patch) =>
                    setFinanceOnboarding((current) => ({ ...current, ...patch }))
                  }
                  onStepChange={setFinanceOnboardingStep}
                  onComplete={completeFinanceOnboarding}
                />
              ) : (
                <article className="dashboard-placeholder-card dashboard-placeholder-card-premium">
                  <div className="dashboard-placeholder-meta">
                    <span className="dashboard-placeholder-badge">{active.badge}</span>
                    <span className="dashboard-placeholder-subsection">{activeSubsection.label}</span>
                  </div>
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
          </div>
        </section>
      </main>

      <nav className="dashboard-bottom-nav" aria-label="Mobile dashboard navigation">
        {(Object.keys(shellConfig) as DashboardSection[]).map((item) => (
          <button
            key={item}
            className={`dashboard-bottom-item ${
              section === item ? "active" : ""
            }`}
            type="button"
            onClick={() => {
              setSection(item);
              setSubsection(getDefaultSubsection(shellConfig, item));
            }}
          >
            <span className="dashboard-bottom-item-inner">
              <span className="dashboard-bottom-item-icon">
                <SectionIcon section={item} />
              </span>
              <span className="dashboard-bottom-item-label">
                {shellConfig[item].label}
              </span>
            </span>
          </button>
        ))}
      </nav>
    </div>
  );

  function onSetFinanceTab(tab: FinanceTab) {
    setFinanceTab(tab);
    setSubsection(tab);
  }
}
