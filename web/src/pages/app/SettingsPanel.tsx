import { useEffect, useMemo, useState } from "react";
import type { User } from "@supabase/supabase-js";
import type { DashboardSubsection } from "../../types";
import { signOutEverywhere, supabase } from "../../lib/supabaseClient";

type Lang = "ru" | "en";

type SettingsPanelProps = {
  lang: Lang;
  subsection: DashboardSubsection;
  user: User | null;
  onSignedOut: () => void;
};

type ProfilesRow = {
  id: string;
  display_name: string | null;
  email: string | null;
};

type UserSettingsRow = {
  user_id: string;
  gemini_api_key: string | null;
};

type IdentityInfo = {
  identity_id?: string;
  provider?: string;
};

type SettingsCopy = {
  sections: Record<"profile" | "preferences" | "security", string>;
  profileTitle: string;
  profileNote: string;
  displayNameLabel: string;
  displayNamePlaceholder: string;
  displayNameSave: string;
  emailTitle: string;
  emailNote: string;
  emailLabel: string;
  emailPlaceholder: string;
  emailSave: string;
  preferencesTitle: string;
  preferencesNote: string;
  geminiTitle: string;
  geminiNote: string;
  geminiPlaceholder: string;
  geminiSave: string;
  geminiHint: string;
  securityTitle: string;
  securityNote: string;
  passwordTitle: string;
  passwordNote: string;
  passwordLabel: string;
  passwordConfirmLabel: string;
  passwordPlaceholder: string;
  passwordSave: string;
  googleTitle: string;
  googleLinked: string;
  googleUnlinked: string;
  googleLink: string;
  googleUnlink: string;
  googleBusy: string;
  logoutTitle: string;
  logoutNote: string;
  logoutAction: string;
  deleteTitle: string;
  deleteNote: string;
  deleteConfirmLabel: string;
  deleteConfirmPlaceholder: string;
  deleteOpen: string;
  deleteModalTitle: string;
  deleteModalBody: string;
  deleteCancel: string;
  deleteConfirmAction: string;
  deleteBusy: string;
  loading: string;
  saved: string;
  googleLinkedToast: string;
  errors: {
    emptyName: string;
    emptyEmail: string;
    passwordShort: string;
    passwordMismatch: string;
    deleteConfirm: string;
    generic: string;
    unlinkGuard: string;
  };
};

const COPY: Record<Lang, SettingsCopy> = {
  ru: {
    sections: {
      profile: "Профиль",
      preferences: "Параметры",
      security: "Безопасность",
    },
    profileTitle: "Профиль",
    profileNote: "Имя аккаунта и почта для входа управляются через Supabase Auth.",
    displayNameLabel: "Имя",
    displayNamePlaceholder: "Как к вам обращаться?",
    displayNameSave: "Сохранить имя",
    emailTitle: "Смена почты",
    emailNote: "После запроса Supabase может попросить подтвердить новую почту через письмо.",
    emailLabel: "Email",
    emailPlaceholder: "you@example.com",
    emailSave: "Сменить почту",
    preferencesTitle: "Интеграции",
    preferencesNote: "Подключаем то, что позже будет использоваться в сценариях ассистента.",
    geminiTitle: "Gemini API Key",
    geminiNote: "Ключ хранится в Supabase в вашей персональной записи настроек.",
    geminiPlaceholder: "AIza...",
    geminiSave: "Сохранить ключ",
    geminiHint:
      "Бесплатный ключ можно получить в Google AI Studio: https://aistudio.google.com/app/apikey",
    securityTitle: "Безопасность",
    securityNote: "Пароль, связанный Google и критические действия аккаунта.",
    passwordTitle: "Смена пароля",
    passwordNote: "Используйте минимум 8 символов. После обновления текущая сессия сохранится.",
    passwordLabel: "Новый пароль",
    passwordConfirmLabel: "Повторите пароль",
    passwordPlaceholder: "Минимум 8 символов",
    passwordSave: "Обновить пароль",
    googleTitle: "Google аккаунт",
    googleLinked: "Google уже привязан к вашему аккаунту.",
    googleUnlinked: "Привяжите Google, чтобы входить в один клик.",
    googleLink: "Подключить Google",
    googleUnlink: "Отключить Google",
    googleBusy: "Переходим в Google…",
    logoutTitle: "Выход из аккаунта",
    logoutNote: "Текущая сессия будет завершена на этом устройстве.",
    logoutAction: "Выйти из аккаунта",
    deleteTitle: "Удаление аккаунта",
    deleteNote: "Действие необратимо. Все пользовательские данные и сессии будут удалены.",
    deleteConfirmLabel: "Подтверждение",
    deleteConfirmPlaceholder: "Введите: удалить",
    deleteOpen: "Удалить аккаунт",
    deleteModalTitle: "Подтвердите удаление аккаунта",
    deleteModalBody:
      "Чтобы подтвердить удаление, введите слово «удалить». После этого аккаунт будет удалён без возможности восстановления.",
    deleteCancel: "Отмена",
    deleteConfirmAction: "Подтвердить удаление",
    deleteBusy: "Удаляем аккаунт…",
    loading: "Загружаем настройки…",
    saved: "Изменения сохранены.",
    googleLinkedToast: "Google аккаунт успешно привязан.",
    errors: {
      emptyName: "Имя не может быть пустым.",
      emptyEmail: "Введите корректный email.",
      passwordShort: "Пароль должен содержать минимум 8 символов.",
      passwordMismatch: "Пароли не совпадают.",
      deleteConfirm: "Введите слово «удалить» для подтверждения.",
      generic: "Не удалось выполнить действие.",
      unlinkGuard: "Нельзя отвязать единственный способ входа. Сначала задайте пароль или привяжите другую identity.",
    },
  },
  en: {
    sections: {
      profile: "Profile",
      preferences: "Preferences",
      security: "Security",
    },
    profileTitle: "Profile",
    profileNote: "Your name and sign-in email are managed through Supabase Auth.",
    displayNameLabel: "Name",
    displayNamePlaceholder: "How should we address you?",
    displayNameSave: "Save name",
    emailTitle: "Change email",
    emailNote: "Supabase may require email confirmation before the new address becomes active.",
    emailLabel: "Email",
    emailPlaceholder: "you@example.com",
    emailSave: "Change email",
    preferencesTitle: "Integrations",
    preferencesNote: "Connections that the assistant workflows will use later.",
    geminiTitle: "Gemini API Key",
    geminiNote: "The key is stored in Supabase in your personal settings row.",
    geminiPlaceholder: "AIza...",
    geminiSave: "Save key",
    geminiHint:
      "You can get a free key in Google AI Studio: https://aistudio.google.com/app/apikey",
    securityTitle: "Security",
    securityNote: "Password, linked Google account, and destructive account actions.",
    passwordTitle: "Change password",
    passwordNote: "Use at least 8 characters. Your current session stays active after update.",
    passwordLabel: "New password",
    passwordConfirmLabel: "Confirm password",
    passwordPlaceholder: "At least 8 characters",
    passwordSave: "Update password",
    googleTitle: "Google account",
    googleLinked: "Google is already linked to your account.",
    googleUnlinked: "Link Google for one-click sign-in.",
    googleLink: "Connect Google",
    googleUnlink: "Disconnect Google",
    googleBusy: "Opening Google…",
    logoutTitle: "Sign out",
    logoutNote: "The current session will be closed on this device.",
    logoutAction: "Sign out",
    deleteTitle: "Delete account",
    deleteNote: "This action is irreversible. All user data and sessions will be removed.",
    deleteConfirmLabel: "Confirmation",
    deleteConfirmPlaceholder: "Type: delete",
    deleteOpen: "Delete account",
    deleteModalTitle: "Confirm account deletion",
    deleteModalBody:
      "Type “delete” to confirm. The account will be removed permanently without recovery.",
    deleteCancel: "Cancel",
    deleteConfirmAction: "Confirm deletion",
    deleteBusy: "Deleting account…",
    loading: "Loading settings…",
    saved: "Changes saved.",
    googleLinkedToast: "Google account linked successfully.",
    errors: {
      emptyName: "Name cannot be empty.",
      emptyEmail: "Enter a valid email.",
      passwordShort: "Password must contain at least 8 characters.",
      passwordMismatch: "Passwords do not match.",
      deleteConfirm: "Type “delete” to confirm.",
      generic: "Failed to complete the action.",
      unlinkGuard: "You cannot unlink the only sign-in method. Set a password or link another identity first.",
    },
  },
};

function toMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

function getGoogleIdentity(identities: IdentityInfo[]) {
  return identities.find((identity) => identity.provider === "google");
}

export function SettingsPanel({
  lang,
  subsection,
  user,
  onSignedOut,
}: SettingsPanelProps) {
  const copy = COPY[lang];
  const [loading, setLoading] = useState(true);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [geminiKey, setGeminiKey] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState("");
  const [identities, setIdentities] = useState<IdentityInfo[]>([]);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);

  const hasGoogleLinked = useMemo(
    () => Boolean(getGoogleIdentity(identities)),
    [identities],
  );

  useEffect(() => {
    if (!user) {
      setLoading(false);
      return;
    }

    let active = true;
    const load = async () => {
      setLoading(true);
      setBanner(null);
      try {
        const [profileResult, settingsResult, identitiesResult] = await Promise.all([
          supabase
            .from("profiles")
            .select("id, display_name, email")
            .eq("id", user.id)
            .maybeSingle<ProfilesRow>(),
          supabase
            .from("user_settings")
            .select("user_id, gemini_api_key")
            .eq("user_id", user.id)
            .maybeSingle<UserSettingsRow>(),
          supabase.auth.getUserIdentities(),
        ]);

        if (!active) return;

        if (profileResult.error) {
          throw profileResult.error;
        }

        if (settingsResult.error && settingsResult.error.code !== "PGRST116") {
          throw settingsResult.error;
        }

        if (identitiesResult.error) {
          throw identitiesResult.error;
        }

        setDisplayName(profileResult.data?.display_name ?? "");
        setEmail(user.email ?? profileResult.data?.email ?? "");
        setGeminiKey(settingsResult.data?.gemini_api_key ?? "");
        setIdentities((identitiesResult.data?.identities as IdentityInfo[] | undefined) ?? []);

        if (window.localStorage.getItem("settings_google_linked") === "1") {
          setBanner({ kind: "success", text: copy.googleLinkedToast });
          window.localStorage.removeItem("settings_google_linked");
        }
      } catch (error) {
        if (!active) return;
        setBanner({
          kind: "error",
          text: toMessage(error, copy.errors.generic),
        });
      } finally {
        if (active) setLoading(false);
      }
    };

    void load();
    return () => {
      active = false;
    };
  }, [copy.errors.generic, copy.googleLinkedToast, user]);

  const setBusy = (value: string | null) => setBusyAction(value);

  const saveDisplayName = async () => {
    if (!user) return;
    const nextName = displayName.trim();
    if (!nextName) {
      setBanner({ kind: "error", text: copy.errors.emptyName });
      return;
    }

    try {
      setBusy("display_name");
      const { error } = await supabase.from("profiles").upsert(
        {
          id: user.id,
          email: user.email ?? email.trim(),
          display_name: nextName,
        },
        { onConflict: "id" },
      );

      if (error) throw error;
      setBanner({ kind: "success", text: copy.saved });
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
    } finally {
      setBusy(null);
    }
  };

  const saveEmail = async () => {
    const nextEmail = email.trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(nextEmail)) {
      setBanner({ kind: "error", text: copy.errors.emptyEmail });
      return;
    }

    try {
      setBusy("email");
      const { error } = await supabase.auth.updateUser({ email: nextEmail });
      if (error) throw error;
      setBanner({ kind: "success", text: copy.saved });
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
    } finally {
      setBusy(null);
    }
  };

  const saveGeminiKey = async () => {
    if (!user) return;

    try {
      setBusy("gemini");
      const { error } = await supabase.from("user_settings").upsert(
        {
          user_id: user.id,
          gemini_api_key: geminiKey.trim(),
        },
        { onConflict: "user_id" },
      );
      if (error) throw error;
      setBanner({ kind: "success", text: copy.saved });
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
    } finally {
      setBusy(null);
    }
  };

  const savePassword = async () => {
    if (newPassword.length < 8) {
      setBanner({ kind: "error", text: copy.errors.passwordShort });
      return;
    }
    if (newPassword !== confirmPassword) {
      setBanner({ kind: "error", text: copy.errors.passwordMismatch });
      return;
    }

    try {
      setBusy("password");
      const { error } = await supabase.auth.updateUser({ password: newPassword });
      if (error) throw error;
      setNewPassword("");
      setConfirmPassword("");
      setBanner({ kind: "success", text: copy.saved });
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
    } finally {
      setBusy(null);
    }
  };

  const linkGoogle = async () => {
    try {
      setBusy("google_link");
      const redirectTo = `${window.location.origin}/auth/callback?intent=link&next=${encodeURIComponent("/app")}`;
      const { data, error } = await supabase.auth.linkIdentity({
        provider: "google",
        options: {
          redirectTo,
          queryParams: { prompt: "select_account" },
          skipBrowserRedirect: true,
        },
      });

      if (error) throw error;
      if (!data?.url) throw new Error(copy.errors.generic);
      window.location.assign(data.url);
    } catch (error) {
      setBusy(null);
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
    }
  };

  const unlinkGoogle = async () => {
    if (identities.length < 2) {
      setBanner({ kind: "error", text: copy.errors.unlinkGuard });
      return;
    }

    const googleIdentity = getGoogleIdentity(identities);
    if (!googleIdentity) {
      setBanner({ kind: "success", text: copy.saved });
      return;
    }

    try {
      setBusy("google_unlink");
      const { error } = await supabase.auth.unlinkIdentity(googleIdentity as never);
      if (error) throw error;
      setIdentities((current) =>
        current.filter((identity) => identity.identity_id !== googleIdentity.identity_id),
      );
      setBanner({ kind: "success", text: copy.saved });
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
    } finally {
      setBusy(null);
    }
  };

  const signOut = async () => {
    try {
      setBusy("logout");
      await signOutEverywhere();
      onSignedOut();
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
      setBusy(null);
    }
  };

  const deleteAccount = async () => {
    const expected = lang === "ru" ? "удалить" : "delete";
    if (deleteConfirm.trim().toLowerCase() !== expected) {
      setBanner({ kind: "error", text: copy.errors.deleteConfirm });
      return;
    }

    try {
      setBusy("delete");
      const { error } = await supabase.functions.invoke("delete-account");
      if (error) throw error;
      await signOutEverywhere();
      onSignedOut();
    } catch (error) {
      setBanner({ kind: "error", text: toMessage(error, copy.errors.generic) });
      setBusy(null);
    }
  };

  return (
    <>
      <section className="settings-panel">
        {banner ? (
          <div className={banner.kind === "error" ? "error" : "success"}>{banner.text}</div>
        ) : null}

        {loading ? <div className="hint subtle">{copy.loading}</div> : null}

        {!loading && subsection === "profile" ? (
          <div className="settings-grid">
            <SettingsCard title={copy.profileTitle} subtitle={copy.profileNote}>
              <label>{copy.displayNameLabel}</label>
              <div className="settings-inline-form">
                <input
                  type="text"
                  value={displayName}
                  placeholder={copy.displayNamePlaceholder}
                  onChange={(event) => setDisplayName(event.target.value)}
                  disabled={busyAction !== null}
                />
                <button
                  type="button"
                  className="secondary-btn settings-action-btn"
                  onClick={() => void saveDisplayName()}
                  disabled={busyAction !== null}
                >
                  {busyAction === "display_name" ? "…" : copy.displayNameSave}
                </button>
              </div>
            </SettingsCard>

            <SettingsCard title={copy.emailTitle} subtitle={copy.emailNote}>
              <label>{copy.emailLabel}</label>
              <div className="settings-inline-form">
                <input
                  type="email"
                  value={email}
                  placeholder={copy.emailPlaceholder}
                  onChange={(event) => setEmail(event.target.value)}
                  disabled={busyAction !== null}
                />
                <button
                  type="button"
                  className="secondary-btn settings-action-btn"
                  onClick={() => void saveEmail()}
                  disabled={busyAction !== null}
                >
                  {busyAction === "email" ? "…" : copy.emailSave}
                </button>
              </div>
            </SettingsCard>
          </div>
        ) : null}

        {!loading && subsection === "preferences" ? (
          <div className="settings-grid">
            <SettingsCard title={copy.preferencesTitle} subtitle={copy.preferencesNote}>
              <div className="settings-stack">
                <div>
                  <strong>{copy.geminiTitle}</strong>
                  <p className="settings-card-text">{copy.geminiNote}</p>
                </div>

                <div className="settings-inline-form settings-inline-form--wide">
                  <input
                    type="password"
                    value={geminiKey}
                    placeholder={copy.geminiPlaceholder}
                    onChange={(event) => setGeminiKey(event.target.value)}
                    disabled={busyAction !== null}
                    autoComplete="off"
                  />
                  <button
                    type="button"
                    className="secondary-btn settings-action-btn"
                    onClick={() => void saveGeminiKey()}
                    disabled={busyAction !== null}
                  >
                    {busyAction === "gemini" ? "…" : copy.geminiSave}
                  </button>
                </div>

                <p className="hint subtle settings-hint">
                  <a href="https://aistudio.google.com/app/apikey" target="_blank" rel="noreferrer">
                    {copy.geminiHint}
                  </a>
                </p>
              </div>
            </SettingsCard>
          </div>
        ) : null}

        {!loading && subsection === "security" ? (
          <div className="settings-grid">
            <SettingsCard title={copy.securityTitle} subtitle={copy.securityNote}>
              <div className="settings-stack">
                <div className="settings-block">
                  <strong>{copy.passwordTitle}</strong>
                  <p className="settings-card-text">{copy.passwordNote}</p>
                  <div className="settings-form-grid">
                    <div>
                      <label>{copy.passwordLabel}</label>
                      <input
                        type="password"
                        value={newPassword}
                        placeholder={copy.passwordPlaceholder}
                        onChange={(event) => setNewPassword(event.target.value)}
                        disabled={busyAction !== null}
                      />
                    </div>
                    <div>
                      <label>{copy.passwordConfirmLabel}</label>
                      <input
                        type="password"
                        value={confirmPassword}
                        placeholder={copy.passwordPlaceholder}
                        onChange={(event) => setConfirmPassword(event.target.value)}
                        disabled={busyAction !== null}
                      />
                    </div>
                  </div>
                  <button
                    type="button"
                    className="secondary-btn settings-action-btn settings-action-btn--align-start"
                    onClick={() => void savePassword()}
                    disabled={busyAction !== null}
                  >
                    {busyAction === "password" ? "…" : copy.passwordSave}
                  </button>
                </div>

                <div className="settings-block settings-row-card">
                  <div>
                    <strong>{copy.googleTitle}</strong>
                    <p className="settings-card-text">
                      {hasGoogleLinked ? copy.googleLinked : copy.googleUnlinked}
                    </p>
                  </div>
                  <button
                    type="button"
                    className="secondary-btn settings-action-btn"
                    onClick={() => void (hasGoogleLinked ? unlinkGoogle() : linkGoogle())}
                    disabled={busyAction !== null}
                  >
                    {busyAction === "google_link" ? copy.googleBusy : null}
                    {busyAction !== "google_link"
                      ? hasGoogleLinked
                        ? copy.googleUnlink
                        : copy.googleLink
                      : null}
                  </button>
                </div>

                <div className="settings-block settings-row-card">
                  <div>
                    <strong>{copy.logoutTitle}</strong>
                    <p className="settings-card-text">{copy.logoutNote}</p>
                  </div>
                  <button
                    type="button"
                    className="secondary-btn settings-action-btn"
                    onClick={() => void signOut()}
                    disabled={busyAction !== null}
                  >
                    {busyAction === "logout" ? "…" : copy.logoutAction}
                  </button>
                </div>

                <div className="settings-block settings-block-danger">
                  <div>
                    <strong>{copy.deleteTitle}</strong>
                    <p className="settings-card-text">{copy.deleteNote}</p>
                  </div>
                  <button
                    type="button"
                    className="dashboard-logout settings-delete-btn"
                    onClick={() => setDeleteModalOpen(true)}
                    disabled={busyAction !== null}
                  >
                    {copy.deleteOpen}
                  </button>
                </div>
              </div>
            </SettingsCard>
          </div>
        ) : null}
      </section>

      {deleteModalOpen ? (
        <div className="settings-modal-backdrop" role="presentation">
          <div
            className="settings-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="settings-delete-title"
          >
            <div className="settings-modal-copy">
              <strong id="settings-delete-title">{copy.deleteModalTitle}</strong>
              <p>{copy.deleteModalBody}</p>
            </div>

            <div className="settings-form-grid">
              <div>
                <label>{copy.deleteConfirmLabel}</label>
                <input
                  type="text"
                  value={deleteConfirm}
                  placeholder={copy.deleteConfirmPlaceholder}
                  onChange={(event) => setDeleteConfirm(event.target.value)}
                  disabled={busyAction !== null}
                />
              </div>
            </div>

            <div className="settings-modal-actions">
              <button
                type="button"
                className="secondary-btn settings-action-btn"
                onClick={() => setDeleteModalOpen(false)}
                disabled={busyAction !== null}
              >
                {copy.deleteCancel}
              </button>
              <button
                type="button"
                className="dashboard-logout settings-delete-btn"
                onClick={() => void deleteAccount()}
                disabled={busyAction !== null}
              >
                {busyAction === "delete" ? copy.deleteBusy : copy.deleteConfirmAction}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}

function SettingsCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: React.ReactNode;
}) {
  return (
    <article className="settings-card">
      <div className="settings-card-head">
        <h3>{title}</h3>
        <p>{subtitle}</p>
      </div>
      {children}
    </article>
  );
}
