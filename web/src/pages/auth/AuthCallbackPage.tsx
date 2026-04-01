import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AuthError } from "@supabase/supabase-js";

import { AuthCard } from "../../components/auth/AuthCard";
import { supabase } from "../../lib/supabaseClient";

function toMessage(t: (key: string) => string, error: unknown) {
  if (!error) return t("auth.errors.generic");

  const raw =
    error instanceof AuthError
      ? error.message
      : error instanceof Error
        ? error.message
        : null;

  if (!raw) return t("auth.errors.generic");

  const normalized = raw.toLowerCase();
  if (
    normalized.includes("pkce") ||
    normalized.includes("code verifier") ||
    normalized.includes("same browser")
  ) {
    return t("auth.errors.googleRetry");
  }
  if (normalized.includes("access_denied")) {
    return t("auth.errors.googleCancelled");
  }
  return t("auth.errors.generic");
}

export function AuthCallbackPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    const run = async () => {
      try {
        const params = new URLSearchParams(window.location.search);
        const intent = params.get("intent");
        const next = params.get("next") || "/app";
        const errorParam = params.get("error");
        const errorDesc = params.get("error_description");
        if (errorParam) {
          setError(errorDesc || t("auth.errors.generic"));
          return;
        }

        for (let attempt = 0; attempt < 20; attempt += 1) {
          const { data } = await supabase.auth.getSession();
          if (!mounted) return;
          if (data.session) {
            if (intent === "link") {
              window.localStorage.setItem("settings_google_linked", "1");
            }
            navigate(next, { replace: true });
            return;
          }
          await new Promise((resolve) => window.setTimeout(resolve, 250));
        }

        setError(t("auth.errors.googleRetry"));
      } catch (e: unknown) {
        if (!mounted) return;
        setError(toMessage(t, e));
      }
    };
    void run();
    return () => {
      mounted = false;
    };
  }, [navigate, t]);

  return (
    <AuthCard
      title={t("auth.callbackTitle")}
      subtitle={undefined}
    >
      {error ? (
        <div className="error">{error}</div>
      ) : (
        <div className="hint">{t("common.loading")}</div>
      )}
    </AuthCard>
  );
}
