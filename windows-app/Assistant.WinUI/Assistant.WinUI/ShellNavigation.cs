using System.Collections.Generic;

namespace Assistant.WinUI
{
    internal sealed class ShellNavItem
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
    }

    internal sealed class ShellSectionConfig
    {
        public required string Eyebrow { get; init; }
        public required string Badge { get; init; }
        public required string Note { get; init; }
        public required string DefaultSubsection { get; init; }
        public required IReadOnlyList<ShellNavItem> Subsections { get; init; }
    }

    internal static class ShellNavigationCatalog
    {
        public static IReadOnlyDictionary<string, ShellSectionConfig> Create(bool isRussian)
        {
            if (isRussian)
            {
                return new Dictionary<string, ShellSectionConfig>
                {
                    ["Home"] = new ShellSectionConfig
                    {
                        Eyebrow = "Command center",
                        Badge = "Активно",
                        Note = "Главный обзор проекта, быстрые действия и персональная сводка станут базовой рабочей поверхностью.",
                        DefaultSubsection = "summary",
                        Subsections = new[]
                        {
                            new ShellNavItem { Key = "summary", Label = "Сводка" },
                            new ShellNavItem { Key = "today", Label = "Сегодня" },
                            new ShellNavItem { Key = "insights", Label = "Инсайты" },
                        }
                    },
                    ["Finance"] = new ShellSectionConfig
                    {
                        Eyebrow = "Money workspace",
                        Badge = "Live",
                        Note = "Финансовая сцена объединяет баланс, счета, транзакции и настройки в одном рабочем контексте.",
                        DefaultSubsection = "overview",
                        Subsections = new[]
                        {
                            new ShellNavItem { Key = "overview", Label = "Обзор" },
                            new ShellNavItem { Key = "accounts", Label = "Счета" },
                            new ShellNavItem { Key = "transactions", Label = "Транзакции" },
                            new ShellNavItem { Key = "settings", Label = "Настройки" },
                        }
                    },
                    ["Health"] = new ShellSectionConfig
                    {
                        Eyebrow = "Wellbeing",
                        Badge = "Soon",
                        Note = "Здесь появится спокойная health-сцена с привычками, метриками и историей состояния.",
                        DefaultSubsection = "habits",
                        Subsections = new[]
                        {
                            new ShellNavItem { Key = "habits", Label = "Привычки" },
                            new ShellNavItem { Key = "metrics", Label = "Метрики" },
                            new ShellNavItem { Key = "records", Label = "История" },
                        }
                    },
                    ["Tasks"] = new ShellSectionConfig
                    {
                        Eyebrow = "Execution",
                        Badge = "Soon",
                        Note = "Задачи, приоритеты и рабочие потоки будут собраны здесь без лишнего chrome.",
                        DefaultSubsection = "focus",
                        Subsections = new[]
                        {
                            new ShellNavItem { Key = "focus", Label = "Фокус" },
                            new ShellNavItem { Key = "board", Label = "Доска" },
                            new ShellNavItem { Key = "archive", Label = "Архив" },
                        }
                    },
                    ["Settings"] = new ShellSectionConfig
                    {
                        Eyebrow = "Control",
                        Badge = "Secure",
                        Note = "Параметры приложения, профиля и подключённых сервисов будут жить здесь с упором на приватность.",
                        DefaultSubsection = "profile",
                        Subsections = new[]
                        {
                            new ShellNavItem { Key = "profile", Label = "Профиль" },
                            new ShellNavItem { Key = "preferences", Label = "Параметры" },
                            new ShellNavItem { Key = "security", Label = "Безопасность" },
                        }
                    }
                };
            }

            return new Dictionary<string, ShellSectionConfig>
            {
                ["Home"] = new ShellSectionConfig
                {
                    Eyebrow = "Command center",
                    Badge = "Active",
                    Note = "The main overview, quick actions, and personal summary will become the default working stage.",
                    DefaultSubsection = "summary",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "summary", Label = "Summary" },
                        new ShellNavItem { Key = "today", Label = "Today" },
                        new ShellNavItem { Key = "insights", Label = "Insights" },
                    }
                },
                ["Finance"] = new ShellSectionConfig
                {
                    Eyebrow = "Money workspace",
                    Badge = "Live",
                    Note = "The finance stage brings balance, accounts, transactions, and settings into one working context.",
                    DefaultSubsection = "overview",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "overview", Label = "Overview" },
                        new ShellNavItem { Key = "accounts", Label = "Accounts" },
                        new ShellNavItem { Key = "transactions", Label = "Transactions" },
                        new ShellNavItem { Key = "settings", Label = "Settings" },
                    }
                },
                ["Health"] = new ShellSectionConfig
                {
                    Eyebrow = "Wellbeing",
                    Badge = "Soon",
                    Note = "This will become the calmer health stage for habits, metrics, and recorded history.",
                    DefaultSubsection = "habits",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "habits", Label = "Habits" },
                        new ShellNavItem { Key = "metrics", Label = "Metrics" },
                        new ShellNavItem { Key = "records", Label = "History" },
                    }
                },
                ["Tasks"] = new ShellSectionConfig
                {
                    Eyebrow = "Execution",
                    Badge = "Soon",
                    Note = "Tasks, priorities, and workflows will live here without extra chrome.",
                    DefaultSubsection = "focus",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "focus", Label = "Focus" },
                        new ShellNavItem { Key = "board", Label = "Board" },
                        new ShellNavItem { Key = "archive", Label = "Archive" },
                    }
                },
                ["Settings"] = new ShellSectionConfig
                {
                    Eyebrow = "Control",
                    Badge = "Secure",
                    Note = "App, profile, and connected-service settings will live here with stronger privacy cues.",
                    DefaultSubsection = "profile",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "profile", Label = "Profile" },
                        new ShellNavItem { Key = "preferences", Label = "Preferences" },
                        new ShellNavItem { Key = "security", Label = "Security" },
                    }
                }
            };
        }
    }
}
