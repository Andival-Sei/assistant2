type StorageLike = Pick<Storage, "getItem" | "setItem" | "removeItem">;

export const AUTH_REMEMBER_KEY = "assistant-auth-remember";
const AUTH_STORAGE_KEY = "sb-oourhsgijmwujektcfih-auth-token";

function getBrowserStorage(): StorageLike | null {
  if (typeof window === "undefined") return null;
  return window.localStorage;
}

function getSessionStorage(): StorageLike | null {
  if (typeof window === "undefined") return null;
  return window.sessionStorage;
}

export function getRememberPreference() {
  return getBrowserStorage()?.getItem(AUTH_REMEMBER_KEY) === "true";
}

export function setRememberPreference(remember: boolean) {
  getBrowserStorage()?.setItem(AUTH_REMEMBER_KEY, remember ? "true" : "false");
}

function getTargetStorage() {
  return getRememberPreference() ? getBrowserStorage() : getSessionStorage();
}

export function clearStoredAuthSession() {
  getBrowserStorage()?.removeItem(AUTH_STORAGE_KEY);
  getSessionStorage()?.removeItem(AUTH_STORAGE_KEY);
}

export const authStorage = {
  getItem(key: string) {
    return (
      getBrowserStorage()?.getItem(key) ??
      getSessionStorage()?.getItem(key) ??
      null
    );
  },
  setItem(key: string, value: string) {
    clearStoredAuthSession();
    getTargetStorage()?.setItem(key, value);
  },
  removeItem(key: string) {
    getBrowserStorage()?.removeItem(key);
    getSessionStorage()?.removeItem(key);
  },
};
