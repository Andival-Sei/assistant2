import i18n from "i18next";
import { initReactI18next } from "react-i18next";

import { en } from "./resources/en";
import { ru } from "./resources/ru";

const STORAGE_KEY = "assistant-lang";

function normalizeLanguage(lang: string | null | undefined): "ru" | "en" {
  const value = (lang ?? "").toLowerCase();
  return value.startsWith("ru") ? "ru" : "en";
}

const initialLanguage = normalizeLanguage(
  localStorage.getItem(STORAGE_KEY) || navigator.language,
);

void i18n.use(initReactI18next).init({
  resources: { ru, en },
  lng: initialLanguage,
  fallbackLng: "en",
  interpolation: { escapeValue: false },
});

i18n.on("languageChanged", (lng) => {
  localStorage.setItem(STORAGE_KEY, normalizeLanguage(lng));
  document.documentElement.lang = normalizeLanguage(lng);
});

document.documentElement.lang = initialLanguage;

export { i18n };
