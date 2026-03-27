import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { AuthError } from "@supabase/supabase-js";

import { AuthCard } from "./AuthCard";
import {
  checkEmailAvailability,
  googleAuthEnabled,
  supabase,
} from "../../lib/supabaseClient";
import { setRememberPreference } from "../../lib/authStorage";
import {
  createIdleValidation,
  type EmailValidationReason,
  type PasswordValidationReason,
  validateEmailFormat,
  validatePassword,
  validatePasswordConfirmation,
} from "../../lib/authValidation";

type AuthMode = "login" | "register" | "forgot" | "reset";

function EyeIcon({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      className="field-visibility-icon"
    >
      <path
        d="M2 12C4.25 7.75 7.86 5.5 12 5.5C16.14 5.5 19.75 7.75 22 12C19.75 16.25 16.14 18.5 12 18.5C7.86 18.5 4.25 16.25 2 12Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12" r="3.1" stroke="currentColor" strokeWidth="1.8" />
      {!open ? (
        <path
          d="M4 4L20 20"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
        />
      ) : null}
    </svg>
  );
}

function toMessage(t: (key: string) => string, error: unknown) {
  if (!error) return null;
  const raw =
    error instanceof AuthError
      ? error.message
      : error instanceof Error
        ? error.message
        : null;
  if (!raw) return t("auth.errors.generic");

  const normalized = raw.toLowerCase();
  if (normalized.includes("invalid login credentials")) {
    return t("auth.errors.invalidLogin");
  }
  if (normalized.includes("email not confirmed")) {
    return t("auth.errors.emailNotConfirmed");
  }
  if (
    normalized.includes("user already registered") ||
    normalized.includes("already been registered")
  ) {
    return t("auth.errors.emailTaken");
  }
  if (
    normalized.includes("over_email_send_rate_limit") ||
    normalized.includes("security purposes") ||
    normalized.includes("rate limit")
  ) {
    return t("auth.errors.rateLimited");
  }
  if (normalized.includes("password should be at least")) {
    return t("auth.errors.passwordLength");
  }
  if (normalized.includes("signup is disabled")) {
    return t("auth.errors.signUpDisabled");
  }
  if (normalized.includes("network request failed")) {
    return t("auth.errors.network");
  }
  return t("auth.errors.generic");
}

function useValidationMessage() {
  const { t } = useTranslation();

  return {
    email: (reason: EmailValidationReason | null) =>
      reason
        ? t(
            {
              empty: "auth.errors.emailRequired",
              invalid: "auth.errors.emailInvalid",
              checking: "auth.checkingEmail",
              taken: "auth.errors.emailTaken",
              network: "auth.errors.emailNetwork",
              available: "auth.emailAvailable",
            }[reason],
          )
        : null,
    password: (reason: PasswordValidationReason | null) =>
      reason
        ? t(
            {
              empty: "auth.errors.passwordRequired",
              length: "auth.errors.passwordLength",
              uppercase: "auth.errors.passwordUppercase",
              lowercase: "auth.errors.passwordLowercase",
              digit: "auth.errors.passwordDigit",
              common: "auth.errors.passwordCommon",
              email: "auth.errors.passwordEmail",
            }[reason],
          )
        : null,
    confirm: (reason: "empty" | "mismatch" | null) =>
      reason
        ? t(
            {
              empty: "auth.errors.confirmRequired",
              mismatch: "auth.errors.passwordMismatch",
            }[reason],
          )
        : null,
  };
}

export function AuthScreen({ initialMode }: { initialMode: AuthMode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const messages = useValidationMessage();

  const [mode, setMode] = useState<AuthMode>(initialMode);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [rememberDevice, setRememberDevice] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [emailValidation, setEmailValidation] = useState(() =>
    createIdleValidation<EmailValidationReason>(),
  );
  const [passwordValidation, setPasswordValidation] = useState(() =>
    createIdleValidation<PasswordValidationReason>(),
  );
  const [confirmValidation, setConfirmValidation] = useState(() => ({
    touched: false,
    valid: true,
    reason: null as "empty" | "mismatch" | null,
  }));

  const resetTransientState = (nextMode: AuthMode) => {
    setMode(nextMode);
    setError(null);
    setSuccess(null);
    setShowPassword(false);
    setShowConfirmPassword(false);
    setPassword("");
    setConfirmPassword("");
    setEmailValidation(createIdleValidation<EmailValidationReason>());
    setPasswordValidation(createIdleValidation<PasswordValidationReason>());
    setConfirmValidation({
      touched: false,
      valid: true,
      reason: null,
    });
  };

  useEffect(() => {
    resetTransientState(initialMode);
  }, [initialMode]);

  useEffect(() => {
    setPasswordValidation((current) =>
      current.touched ? validatePassword(password, email) : current,
    );
  }, [email, password]);

  useEffect(() => {
    setConfirmValidation((current) =>
      current.touched
        ? validatePasswordConfirmation(password, confirmPassword)
        : current,
    );
  }, [confirmPassword, password]);

  const canSubmit = useMemo(() => {
    if (pending) return false;
    if (mode === "login") {
      return email.trim().length > 3 && password.length >= 8;
    }
    if (mode === "register") {
      return (
        emailValidation.valid &&
        passwordValidation.valid &&
        confirmValidation.valid &&
        !!email &&
        !!password &&
        !!confirmPassword
      );
    }
    if (mode === "forgot") {
      return validateEmailFormat(email).valid;
    }
    return passwordValidation.valid && confirmValidation.valid && !!password;
  }, [
    confirmPassword,
    confirmValidation.valid,
    email,
    emailValidation.valid,
    mode,
    password,
    passwordValidation.valid,
    pending,
  ]);

  const applyRemember = () => {
    setRememberPreference(rememberDevice);
  };

  const runEmailCheck = async () => {
    const next = validateEmailFormat(email);
    setEmailValidation(next);
    if (!next.valid) return false;
    if (mode !== "register") return true;

    setEmailValidation({ touched: true, valid: false, reason: "checking" });
    try {
      const result = await checkEmailAvailability(email.trim());
      if (!result?.valid) {
        setEmailValidation({ touched: true, valid: false, reason: "invalid" });
        return false;
      }
      if (!result.available) {
        setEmailValidation({ touched: true, valid: false, reason: "taken" });
        return false;
      }
      setEmailValidation({ touched: true, valid: true, reason: "available" });
      return true;
    } catch {
      setEmailValidation({ touched: true, valid: false, reason: "network" });
      return false;
    }
  };

  const onGoogle = async () => {
    applyRemember();
    setPending(true);
    setError(null);
    setSuccess(null);
    try {
      const redirectTo = new URL(
        "/auth/callback",
        window.location.origin,
      ).toString();
      const { error: oauthError } = await supabase.auth.signInWithOAuth({
        provider: "google",
        options: { redirectTo },
      });
      if (oauthError) throw oauthError;
      setSuccess(t("auth.googlePending"));
    } catch (e) {
      setError(toMessage(t, e));
      setPending(false);
    }
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (mode === "register") {
      const emailOk = await runEmailCheck();
      const passwordOk = validatePassword(password, email);
      const confirmOk = validatePasswordConfirmation(password, confirmPassword);
      setPasswordValidation(passwordOk);
      setConfirmValidation(confirmOk);
      if (!emailOk || !passwordOk.valid || !confirmOk.valid) return;
    }

    if (mode === "forgot") {
      const emailOk = validateEmailFormat(email);
      setEmailValidation(emailOk);
      if (!emailOk.valid) return;
    }

    if (mode === "reset") {
      const passwordOk = validatePassword(password, email);
      const confirmOk = validatePasswordConfirmation(password, confirmPassword);
      setPasswordValidation(passwordOk);
      setConfirmValidation(confirmOk);
      if (!passwordOk.valid || !confirmOk.valid) return;
    }

    applyRemember();
    setPending(true);
    try {
      if (mode === "login") {
        const { error: signInError } = await supabase.auth.signInWithPassword({
          email: email.trim(),
          password,
        });
        if (signInError) throw signInError;
        navigate("/app", { replace: true });
        return;
      }

      if (mode === "register") {
        const emailRedirectTo = new URL(
          "/auth/callback",
          window.location.origin,
        ).toString();
        const { data, error: signUpError } = await supabase.auth.signUp({
          email: email.trim(),
          password,
          options: { emailRedirectTo },
        });
        if (signUpError) throw signUpError;
        if (data.session) {
          navigate("/app", { replace: true });
          return;
        }
        setSuccess(t("auth.success.verifyEmail"));
        return;
      }

      if (mode === "forgot") {
        const redirectTo = new URL("/auth/reset", window.location.origin).toString();
        const { error: resetError } = await supabase.auth.resetPasswordForEmail(
          email.trim(),
          { redirectTo },
        );
        if (resetError) throw resetError;
        setSuccess(t("auth.success.resetEmail"));
        return;
      }

      const { error: updateError } = await supabase.auth.updateUser({
        password,
      });
      if (updateError) throw updateError;
      setSuccess(t("auth.success.passwordUpdated"));
      setTimeout(() => navigate("/auth/login", { replace: true }), 600);
    } catch (e2) {
      setError(toMessage(t, e2));
    } finally {
      setPending(false);
    }
  };

  const title =
    mode === "register"
      ? t("auth.titleRegister")
      : mode === "forgot"
        ? t("auth.forgotTitle")
        : mode === "reset"
          ? t("auth.resetTitle")
          : t("auth.titleLogin");

  const subtitle =
    mode === "register"
      ? t("auth.subtitleRegister")
      : mode === "forgot"
        ? t("auth.forgotSubtitle")
        : mode === "reset"
          ? t("auth.resetSubtitle")
          : t("auth.subtitleLogin");

  return (
    <AuthCard
      title={title}
      subtitle={subtitle}
    >
      <div key={mode} className="auth-mode-stage">
        <div className="auth-mode-tabs" role="tablist" aria-label="Auth mode">
          <button
            className={`auth-mode-tab ${mode === "login" ? "active" : ""}`}
            type="button"
            onClick={() => resetTransientState("login")}
          >
            {t("auth.ctaLogin")}
          </button>
          <button
            className={`auth-mode-tab ${mode === "register" ? "active" : ""}`}
            type="button"
            onClick={() => resetTransientState("register")}
          >
            {t("auth.ctaRegister")}
          </button>
        </div>

        {mode !== "reset" && googleAuthEnabled ? (
          <>
            <button
              className="google-btn auth-google-btn"
              type="button"
              onClick={onGoogle}
              disabled={pending}
            >
              {t("auth.google")}
            </button>
            <div className="divider">{t("auth.or")}</div>
          </>
        ) : (
          <div className="hint subtle" style={{ marginTop: 10 }}>
            {t("auth.googleUnavailable")}
          </div>
        )}

        {error ? <div className="error">{error}</div> : null}
        {success ? <div className="success">{success}</div> : null}

        <form className="form auth-form" onSubmit={onSubmit}>
          {mode !== "reset" ? (
            <div className="field-shell">
              <label htmlFor="email">{t("auth.email")}</label>
              <div className="field-input-wrap">
                <span className="field-leading-icon" aria-hidden="true">
                  @
                </span>
                <input
                  id="email"
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={(ev) => setEmail(ev.target.value)}
                  onBlur={() => {
                    if (mode === "register") {
                      void runEmailCheck();
                      return;
                    }
                    setEmailValidation(validateEmailFormat(email));
                  }}
                  placeholder="you@example.com"
                  disabled={pending}
                />
              </div>
              <div
                className={`field-note ${
                  emailValidation.touched && !emailValidation.valid
                    ? "error"
                    : emailValidation.reason === "available"
                      ? "success"
                      : ""
                }`}
              >
                {emailValidation.touched
                  ? messages.email(emailValidation.reason)
                  : "\u00A0"}
              </div>
            </div>
          ) : null}

          {mode !== "forgot" ? (
            <div className="field-shell">
              <label htmlFor="password">{t("auth.password")}</label>
              <div className="field-input-wrap">
                <span className="field-leading-icon" aria-hidden="true">
                  •
                </span>
                <input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  autoComplete={mode === "login" ? "current-password" : "new-password"}
                  value={password}
                  onChange={(ev) => setPassword(ev.target.value)}
                  onBlur={() => setPasswordValidation(validatePassword(password, email))}
                  placeholder="••••••••"
                  disabled={pending}
                />
                <button
                  className="field-visibility-btn"
                  type="button"
                  onClick={() => setShowPassword((value) => !value)}
                  aria-label={showPassword ? t("auth.hidePassword") : t("auth.showPassword")}
                >
                  <EyeIcon open={showPassword} />
                </button>
              </div>
              <div
                className={`field-note ${
                  passwordValidation.touched && !passwordValidation.valid
                    ? "error"
                    : ""
                }`}
              >
                {passwordValidation.touched
                  ? messages.password(passwordValidation.reason)
                  : "\u00A0"}
              </div>
            </div>
          ) : null}

          {mode === "register" || mode === "reset" ? (
            <div className="field-shell">
              <label htmlFor="confirmPassword">{t("auth.confirmPassword")}</label>
              <div className="field-input-wrap">
                <span className="field-leading-icon" aria-hidden="true">
                  •
                </span>
                <input
                  id="confirmPassword"
                  type={showConfirmPassword ? "text" : "password"}
                  autoComplete="new-password"
                  value={confirmPassword}
                  onChange={(ev) => setConfirmPassword(ev.target.value)}
                  onBlur={() =>
                    setConfirmValidation(
                      validatePasswordConfirmation(password, confirmPassword),
                    )
                  }
                  placeholder="••••••••"
                  disabled={pending}
                />
                <button
                  className="field-visibility-btn"
                  type="button"
                  onClick={() => setShowConfirmPassword((value) => !value)}
                  aria-label={
                    showConfirmPassword ? t("auth.hidePassword") : t("auth.showPassword")
                  }
                >
                  <EyeIcon open={showConfirmPassword} />
                </button>
              </div>
              <div
                className={`field-note ${
                  confirmValidation.touched && !confirmValidation.valid
                    ? "error"
                    : ""
                }`}
              >
                {confirmValidation.touched
                  ? messages.confirm(confirmValidation.reason)
                  : "\u00A0"}
              </div>
            </div>
          ) : null}

          {mode === "login" ? (
            <div className="row auth-row">
              <label className="remember-toggle">
                <input
                  type="checkbox"
                  checked={rememberDevice}
                  onChange={(ev) => setRememberDevice(ev.target.checked)}
                />
                <span>{t("auth.remember")}</span>
              </label>
              <button
                className="ghost-link"
                type="button"
                onClick={() => resetTransientState("forgot")}
              >
                {t("auth.forgot")}
              </button>
            </div>
          ) : null}

          {mode === "login" ? (
            <div className="hint subtle auth-remember-hint">
              {t("auth.rememberHint")}
            </div>
          ) : null}

          <button className="cta auth-submit" type="submit" disabled={!canSubmit}>
            {mode === "login"
              ? t("auth.ctaLogin")
              : mode === "register"
                ? t("auth.ctaRegister")
                : mode === "forgot"
                  ? t("auth.ctaSendReset")
                  : t("auth.ctaUpdatePassword")}
          </button>
        </form>

        <div className="hint auth-switch-link">
          {mode === "register" ? (
            <button className="ghost-link" type="button" onClick={() => resetTransientState("login")}>
              {t("auth.linkToLogin")}
            </button>
          ) : mode === "forgot" || mode === "reset" ? (
            <button className="ghost-link" type="button" onClick={() => resetTransientState("login")}>
              {t("auth.linkToLogin")}
            </button>
          ) : (
            <button className="ghost-link" type="button" onClick={() => resetTransientState("register")}>
              {t("auth.linkToRegister")}
            </button>
          )}
        </div>
      </div>
    </AuthCard>
  );
}
