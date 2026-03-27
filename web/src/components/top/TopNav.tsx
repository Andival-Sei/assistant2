import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";

type ThemeMode = "system" | "light" | "dark";
type Locale = "ru" | "en";

const THEME_KEY = "assistant-theme";

const languageOptions: Array<{
  code: Locale;
  flag: string;
  label: { ru: string; en: string };
}> = [
  {
    code: "ru",
    flag: "RU",
    label: { ru: "Русский", en: "Russian" },
  },
  {
    code: "en",
    flag: "EN",
    label: { ru: "English", en: "English" },
  },
];

const themeOptions: Array<{
  value: ThemeMode;
  icon: "system" | "light" | "dark";
  labelKey: "common.themeSystem" | "common.themeLight" | "common.themeDark";
}> = [
  { value: "system", icon: "system", labelKey: "common.themeSystem" },
  { value: "light", icon: "light", labelKey: "common.themeLight" },
  { value: "dark", icon: "dark", labelKey: "common.themeDark" },
];

function computeTheme(mode: ThemeMode) {
  if (mode === "system") {
    return window.matchMedia("(prefers-color-scheme: dark)").matches
      ? "dark"
      : "light";
  }
  return mode;
}

function normalizeTheme(mode: string | null): ThemeMode {
  if (mode === "light" || mode === "dark" || mode === "system") return mode;
  return "system";
}

function ThemeGlyph({ kind }: { kind: "system" | "light" | "dark" }) {
  if (kind === "light") {
    return (
      <svg viewBox="0 0 20 20" aria-hidden="true">
        <circle cx="10" cy="10" r="3.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
        <path
          d="M10 1.8v2.1M10 16.1v2.1M18.2 10h-2.1M3.9 10H1.8M15.8 4.2l-1.5 1.5M5.7 14.3l-1.5 1.5M15.8 15.8l-1.5-1.5M5.7 5.7 4.2 4.2"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
        />
      </svg>
    );
  }

  if (kind === "dark") {
    return (
      <svg viewBox="0 0 20 20" aria-hidden="true">
        <path
          d="M13.9 3.4a6.6 6.6 0 1 0 2.7 12.6A7.4 7.4 0 1 1 13.9 3.4Z"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinejoin="round"
        />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 20 20" aria-hidden="true">
      <rect x="3" y="4" width="14" height="10" rx="2.2" fill="none" stroke="currentColor" strokeWidth="1.6" />
      <path d="M7.2 16h5.6" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}

export function TopNav({ variant }: { variant: "landing" | "auth" }) {
  const location = useLocation();
  const { t, i18n } = useTranslation();
  const [themeMode, setThemeMode] = useState<ThemeMode>(() =>
    normalizeTheme(localStorage.getItem(THEME_KEY)),
  );
  const [isLanguageMenuOpen, setIsLanguageMenuOpen] = useState(false);
  const languageMenuRef = useRef<HTMLDivElement | null>(null);

  const lang = useMemo<Locale>(
    () => (i18n.language.startsWith("ru") ? "ru" : "en"),
    [i18n.language],
  );

  const activeLanguage = useMemo(
    () => languageOptions.find((option) => option.code === lang) ?? languageOptions[0],
    [lang],
  );

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const apply = () => {
      const computed = computeTheme(themeMode);
      document.documentElement.setAttribute("data-theme", computed);
    };
    apply();
    const onChange = () => {
      if (themeMode === "system") apply();
    };
    media.addEventListener("change", onChange);
    return () => {
      media.removeEventListener("change", onChange);
    };
  }, [themeMode]);

  useEffect(() => {
    if (!isLanguageMenuOpen) return;

    const onPointerDown = (event: PointerEvent) => {
      if (!languageMenuRef.current?.contains(event.target as Node)) {
        setIsLanguageMenuOpen(false);
      }
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsLanguageMenuOpen(false);
      }
    };

    window.addEventListener("pointerdown", onPointerDown);
    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("pointerdown", onPointerDown);
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [isLanguageMenuOpen]);

  const setTheme = (mode: ThemeMode) => {
    setThemeMode(mode);
    localStorage.setItem(THEME_KEY, mode);
  };

  const setLanguage = async (next: Locale) => {
    if (next !== lang) {
      await i18n.changeLanguage(next);
    }
    setIsLanguageMenuOpen(false);
  };

  return (
    <nav className="nav">
      <div className="brand">{t("brand")}</div>
      <div className="nav-actions">
        {variant === "landing" ? (
          <>
            <Link
              className="nav-link"
              to="/auth/login"
              state={{ from: location }}
            >
              {t("nav.login")}
            </Link>
            <Link
              className="pill-btn"
              to="/auth/register"
              state={{ from: location }}
            >
              {t("nav.register")}
            </Link>
          </>
        ) : (
          <Link className="nav-link" to="/">
            {t("common.backToLanding")}
          </Link>
        )}

        <div className="theme-switcher" aria-label={t("common.themeSystem")}>
          {themeOptions.map((option) => (
            <button
              key={option.value}
              className={`theme-switcher-btn ${
                themeMode === option.value ? "active" : ""
              }`}
              type="button"
              aria-pressed={themeMode === option.value}
              aria-label={t(option.labelKey)}
              title={t(option.labelKey)}
              onClick={() => setTheme(option.value)}
            >
              <ThemeGlyph kind={option.icon} />
            </button>
          ))}
        </div>

        <div
          ref={languageMenuRef}
          className={`language-menu ${isLanguageMenuOpen ? "open" : ""}`}
        >
          <button
            className="language-trigger"
            type="button"
            aria-haspopup="menu"
            aria-expanded={isLanguageMenuOpen}
            onClick={() => setIsLanguageMenuOpen((value) => !value)}
          >
            <span className="language-flag">{activeLanguage.flag}</span>
            <span className="language-chevron" aria-hidden="true">
              {isLanguageMenuOpen ? "^" : "v"}
            </span>
          </button>

          <div className="language-dropdown" role="menu" aria-hidden={!isLanguageMenuOpen}>
            {languageOptions.map((option) => (
              <button
                key={option.code}
                className={`language-option ${
                  option.code === lang ? "active" : ""
                }`}
                type="button"
                role="menuitemradio"
                aria-checked={option.code === lang}
                onClick={() => void setLanguage(option.code)}
              >
                <span className="language-option-flag">{option.flag}</span>
                <span className="language-option-label">
                  {option.label[lang]}
                </span>
                <span className="language-option-check" aria-hidden="true">
                  {option.code === lang ? "OK" : ""}
                </span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </nav>
  );
}
