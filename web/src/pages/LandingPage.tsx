import { useMemo } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { TopNav } from "../components/top/TopNav";

type Tile = { label: string; icon: string; color: string };
type Feature = { title: string; text: string; icon: string; color: string };
type Plain = { title: string; text: string };
type Step = { step: string; title: string; text: string };
type Trust = { title: string; text: string; icon: string; color: string };

export function LandingPage() {
  const { t } = useTranslation();

  const tiles = useMemo(
    () => t("hero.tiles", { returnObjects: true }) as Tile[],
    [t],
  );
  const features = useMemo(
    () => t("features.items", { returnObjects: true }) as Feature[],
    [t],
  );
  const capabilities = useMemo(
    () => t("capabilities.items", { returnObjects: true }) as Plain[],
    [t],
  );
  const how = useMemo(
    () => t("how.items", { returnObjects: true }) as Step[],
    [t],
  );
  const trust = useMemo(
    () => t("trust.items", { returnObjects: true }) as Trust[],
    [t],
  );

  return (
    <div className="wrap">
      <TopNav variant="landing" />

      <section className="hero">
        <div>
          <div className="kicker">{t("hero.kicker")}</div>
          <h1>{t("hero.title")}</h1>
          <p>{t("hero.subtitle")}</p>
          <div className="hero-actions">
            <Link className="pill-btn" to="/auth/register">
              {t("hero.ctaPrimary")}
            </Link>
            <Link className="secondary-btn" to="/auth/login">
              {t("hero.ctaSecondary")}
            </Link>
          </div>

          <div className="section" style={{ marginTop: 40 }}>
            <div className="features-grid">
              {trust.map((item) => (
                <div key={item.icon} className="feature-card">
                  <div className="feature-head">
                    <div
                      className="feature-icon"
                      style={{ background: item.color }}
                    >
                      {item.icon}
                    </div>
                    <div style={{ fontWeight: 700 }}>{item.title}</div>
                  </div>
                  <p className="feature-text">{item.text}</p>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="grid-tiles" aria-label="Modules">
          {tiles.map((tile) => (
            <div key={tile.label} className="phone-tile">
              <div className="tile-top">
                <div className="tile-label">{tile.label}</div>
                <div className="tile-icon" style={{ background: tile.color }}>
                  {tile.icon}
                </div>
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className="section">
        <h2 className="section-title">{t("features.title")}</h2>
        <p className="section-subtitle">{t("features.subtitle")}</p>
        <div className="features-grid">
          {features.map((item) => (
            <div key={item.title} className="feature-card">
              <div className="feature-head">
                <div
                  className="feature-icon"
                  style={{ background: item.color }}
                >
                  {item.icon}
                </div>
                <div style={{ fontWeight: 700 }}>{item.title}</div>
              </div>
              <p className="feature-text">{item.text}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="section">
        <h2 className="section-title">{t("capabilities.title")}</h2>
        <p className="section-subtitle">{t("capabilities.subtitle")}</p>
        <div className="features-grid">
          {capabilities.map((item) => (
            <div key={item.title} className="feature-card">
              <div style={{ fontWeight: 700, marginBottom: 8 }}>
                {item.title}
              </div>
              <p className="feature-text">{item.text}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="section">
        <h2 className="section-title">{t("how.title")}</h2>
        <p className="section-subtitle">{t("how.subtitle")}</p>
        <div className="features-grid">
          {how.map((item) => (
            <div key={item.step} className="feature-card">
              <div
                style={{
                  display: "flex",
                  gap: 12,
                  alignItems: "baseline",
                  marginBottom: 8,
                }}
              >
                <div
                  style={{
                    color: "var(--accent)",
                    fontFamily: "'Space Mono', monospace",
                  }}
                >
                  {item.step}
                </div>
                <div style={{ fontWeight: 700 }}>{item.title}</div>
              </div>
              <p className="feature-text">{item.text}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="section">
        <div
          className="feature-card"
          style={{ background: "var(--panel-strong)" }}
        >
          <h2 className="section-title" style={{ margin: 0 }}>
            {t("cta.title")}
          </h2>
          <p className="section-subtitle" style={{ marginTop: 10 }}>
            {t("cta.subtitle")}
          </p>
          <div className="hero-actions">
            <Link className="pill-btn" to="/auth/register">
              {t("cta.primary")}
            </Link>
            <Link className="secondary-btn" to="/auth/login">
              {t("cta.secondary")}
            </Link>
          </div>
        </div>
      </section>

      <footer className="footer">
        <div>
          <div className="brand">{t("brand")}</div>
          <p style={{ marginTop: 10 }}>{t("footer.tagline")}</p>
          <p style={{ marginTop: 12 }}>{t("footer.note")}</p>
        </div>
        <div>
          <h3>{t("footer.linksTitle")}</h3>
          <Link to="/privacy">{t("footer.privacy")}</Link>
          <Link to="/terms">{t("footer.terms")}</Link>
        </div>
        <div>
          <h3>{t("footer.contactTitle")}</h3>
          <a href={`mailto:${t("footer.contact")}`}>{t("footer.contact")}</a>
        </div>
      </footer>
    </div>
  );
}
