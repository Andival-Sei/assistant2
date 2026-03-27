import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { TopNav } from "../components/top/TopNav";

type Section = { title: string; text: string[] };

export function LegalPage({ type }: { type: "privacy" | "terms" }) {
  const { t } = useTranslation();
  const base = useMemo(
    () =>
      (t(`legal.${type}.sections`, { returnObjects: true }) as Section[]) ||
      [],
    [t, type],
  );

  return (
    <div className="wrap">
      <TopNav variant="auth" />
      <div className="auth-wrap">
        <div className="card" style={{ maxWidth: 820 }}>
          <h1 style={{ marginBottom: 8 }}>
            {t(`legal.${type}.title`)}
          </h1>
          <div className="hint" style={{ marginBottom: 20 }}>
            {t("legal.updated")}
          </div>
          {base.map((section) => (
            <div key={section.title} style={{ marginBottom: 20 }}>
              <h3 style={{ marginBottom: 8 }}>{section.title}</h3>
              {section.text.map((line) => (
                <p key={line} className="hint" style={{ marginTop: 6 }}>
                  {line}
                </p>
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
