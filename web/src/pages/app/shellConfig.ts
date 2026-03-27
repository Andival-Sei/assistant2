import type {
  DashboardSection,
  DashboardSectionConfig,
  DashboardSubsection,
} from "../../types";

type Lang = "ru" | "en";

function createSectionConfig(lang: Lang): Record<DashboardSection, DashboardSectionConfig> {
  if (lang === "ru") {
    return {
      home: {
        id: "home",
        icon: "01",
        mobileIcon: "GL",
        label: "Главная",
        title: "Главная",
        eyebrow: "Command center",
        badge: "Активно",
        note: "Главный обзор проекта, быстрые действия и персональная сводка появятся здесь как основной operational surface.",
        defaultSubsection: "summary",
        subsections: [
          { id: "summary", label: "Сводка" },
          { id: "today", label: "Сегодня" },
          { id: "insights", label: "Инсайты" },
        ],
      },
      finance: {
        id: "finance",
        icon: "02",
        mobileIcon: "FN",
        label: "Финансы",
        title: "Финансы",
        eyebrow: "Money workspace",
        badge: "Live",
        note: "Центр финансового контроля: баланс, счета, транзакции и настройки в одной рабочей сцене.",
        defaultSubsection: "overview",
        subsections: [
          { id: "overview", label: "Обзор" },
          { id: "accounts", label: "Счета" },
          { id: "transactions", label: "Транзакции" },
          { id: "settings", label: "Настройки" },
        ],
      },
      health: {
        id: "health",
        icon: "03",
        mobileIcon: "HL",
        label: "Здоровье",
        title: "Здоровье",
        eyebrow: "Wellbeing",
        badge: "Soon",
        note: "Здесь будет ежедневный health surface: самочувствие, метрики и история состояния без перегруженного дашборд-мозаика.",
        defaultSubsection: "habits",
        subsections: [
          { id: "habits", label: "Привычки" },
          { id: "metrics", label: "Метрики" },
          { id: "records", label: "История" },
        ],
      },
      tasks: {
        id: "tasks",
        icon: "04",
        mobileIcon: "TK",
        label: "Задачи",
        title: "Задачи",
        eyebrow: "Execution",
        badge: "Soon",
        note: "Рабочие потоки, приоритеты и контроль исполнения будут собраны здесь в спокойной production-like оболочке.",
        defaultSubsection: "focus",
        subsections: [
          { id: "focus", label: "Фокус" },
          { id: "board", label: "Доска" },
          { id: "archive", label: "Архив" },
        ],
      },
      chat: {
        id: "chat",
        icon: "05",
        mobileIcon: "CH",
        label: "Чат",
        title: "Чат",
        eyebrow: "AI workspace",
        badge: "Beta",
        note: "Здесь останется отдельная AI-сцена для общения с ассистентом. Пока раздел отмечен как находящийся в разработке.",
        defaultSubsection: "chat",
        subsections: [
          { id: "chat", label: "Диалог" },
        ],
      },
      settings: {
        id: "settings",
        icon: "06",
        mobileIcon: "NS",
        label: "Настройки",
        title: "Настройки",
        eyebrow: "Control",
        badge: "Secure",
        note: "Параметры приложения, профиля и подключённых сервисов будут жить здесь с упором на безопасность и приватность.",
        defaultSubsection: "profile",
        subsections: [
          { id: "profile", label: "Профиль" },
          { id: "preferences", label: "Параметры" },
          { id: "security", label: "Безопасность" },
        ],
      },
    };
  }

  return {
    home: {
      id: "home",
      icon: "01",
      mobileIcon: "HM",
      label: "Home",
      title: "Home",
      eyebrow: "Command center",
      badge: "Active",
      note: "The main overview, quick actions, and personal summary will live here as the default operational surface.",
      defaultSubsection: "summary",
      subsections: [
        { id: "summary", label: "Summary" },
        { id: "today", label: "Today" },
        { id: "insights", label: "Insights" },
      ],
    },
    finance: {
      id: "finance",
      icon: "02",
      mobileIcon: "FN",
      label: "Finance",
      title: "Finance",
      eyebrow: "Money workspace",
      badge: "Live",
      note: "Your finance control center: balance, accounts, transactions, and settings in one stage.",
      defaultSubsection: "overview",
      subsections: [
        { id: "overview", label: "Overview" },
        { id: "accounts", label: "Accounts" },
        { id: "transactions", label: "Transactions" },
        { id: "settings", label: "Settings" },
      ],
    },
    health: {
      id: "health",
      icon: "03",
      mobileIcon: "HL",
      label: "Health",
      title: "Health",
      eyebrow: "Wellbeing",
      badge: "Soon",
      note: "This will become the daily health surface for habits, metrics, and recorded history without dashboard clutter.",
      defaultSubsection: "habits",
      subsections: [
        { id: "habits", label: "Habits" },
        { id: "metrics", label: "Metrics" },
        { id: "records", label: "History" },
      ],
    },
    tasks: {
      id: "tasks",
      icon: "04",
      mobileIcon: "TS",
      label: "Tasks",
      title: "Tasks",
      eyebrow: "Execution",
      badge: "Soon",
      note: "Workflows, priorities, and execution control will live here in a calmer production-style shell.",
      defaultSubsection: "focus",
      subsections: [
        { id: "focus", label: "Focus" },
        { id: "board", label: "Board" },
        { id: "archive", label: "Archive" },
      ],
    },
    chat: {
      id: "chat",
      icon: "05",
      mobileIcon: "CH",
      label: "Chat",
      title: "Chat",
      eyebrow: "AI workspace",
      badge: "Beta",
      note: "This dedicated AI scene will keep the current assistant-style shell. For now the section stays marked as in development.",
      defaultSubsection: "chat",
      subsections: [
        { id: "chat", label: "Dialog" },
      ],
    },
    settings: {
      id: "settings",
      icon: "06",
      mobileIcon: "ST",
      label: "Settings",
      title: "Settings",
      eyebrow: "Control",
      badge: "Secure",
      note: "App, profile, and connected-service settings will stay here with a strong emphasis on privacy and security.",
      defaultSubsection: "profile",
      subsections: [
        { id: "profile", label: "Profile" },
        { id: "preferences", label: "Preferences" },
        { id: "security", label: "Security" },
      ],
    },
  };
}

export function getDashboardShellConfig(lang: Lang) {
  return createSectionConfig(lang);
}

export function getDefaultSubsection(
  config: Record<DashboardSection, DashboardSectionConfig>,
  section: DashboardSection,
): DashboardSubsection {
  return config[section].defaultSubsection;
}
