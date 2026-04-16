using System.Collections.Generic;

namespace Assistant.WinUI.Application.Shell;

public static class ShellNavigationCatalog
{
    public static IReadOnlyDictionary<string, ShellSectionConfig> Create(bool isRussian)
    {
        if (isRussian)
        {
            return new Dictionary<string, ShellSectionConfig>
            {
                ["Home"] = new()
                {
                    Eyebrow = "Command center",
                    Badge = "Активно",
                    Note = "Главный обзор проекта, быстрые действия и персональная сводка станут базовой рабочей поверхностью.",
                    DefaultSubsection = "summary",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "summary", Label = "Сводка" },
                        new ShellNavItem { Key = "today", Label = "Сегодня" },
                        new ShellNavItem { Key = "insights", Label = "Инсайты" }
                    }
                },
                ["Finance"] = new()
                {
                    Eyebrow = "Money workspace",
                    Badge = "Live",
                    Note = "Финансовая сцена объединяет обзор, счета, транзакции, категории и аналитику в одном рабочем контексте.",
                    DefaultSubsection = "overview",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "overview", Label = "Обзор" },
                        new ShellNavItem { Key = "accounts", Label = "Счета" },
                        new ShellNavItem { Key = "transactions", Label = "Транзакции" },
                        new ShellNavItem { Key = "categories", Label = "Категории" },
                        new ShellNavItem { Key = "analytics", Label = "Аналитика" }
                    }
                },
                ["Health"] = new()
                {
                    Eyebrow = "Wellbeing",
                    Badge = "Soon",
                    Note = "Здесь появится спокойная health-сцена с привычками, метриками и историей состояния.",
                    DefaultSubsection = "habits",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "habits", Label = "Привычки" },
                        new ShellNavItem { Key = "metrics", Label = "Метрики" },
                        new ShellNavItem { Key = "records", Label = "История" }
                    }
                },
                ["Tasks"] = new()
                {
                    Eyebrow = "Execution",
                    Badge = "Soon",
                    Note = "Задачи, приоритеты и рабочие потоки будут собраны здесь без лишнего chrome.",
                    DefaultSubsection = "focus",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "focus", Label = "Фокус" },
                        new ShellNavItem { Key = "board", Label = "Доска" },
                        new ShellNavItem { Key = "archive", Label = "Архив" }
                    }
                },
                ["Chat"] = new()
                {
                    Eyebrow = "AI workspace",
                    Badge = "Beta",
                    Note = "Здесь сохранится отдельная AI-сцена для общения с ассистентом. Пока раздел находится в разработке.",
                    DefaultSubsection = "chat",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "chat", Label = "Диалог" }
                    }
                },
                ["Settings"] = new()
                {
                    Eyebrow = "Control",
                    Badge = "Secure",
                    Note = "Параметры приложения, профиля и подключённых сервисов будут жить здесь с упором на приватность.",
                    DefaultSubsection = "profile",
                    Subsections = new[]
                    {
                        new ShellNavItem { Key = "profile", Label = "Профиль" },
                        new ShellNavItem { Key = "preferences", Label = "Параметры" },
                        new ShellNavItem { Key = "security", Label = "Безопасность" }
                    }
                }
            };
        }

        return new Dictionary<string, ShellSectionConfig>
        {
            ["Home"] = new()
            {
                Eyebrow = "Command center",
                Badge = "Active",
                Note = "The main overview, quick actions, and personal summary will become the default working stage.",
                DefaultSubsection = "summary",
                Subsections = new[]
                {
                    new ShellNavItem { Key = "summary", Label = "Summary" },
                    new ShellNavItem { Key = "today", Label = "Today" },
                    new ShellNavItem { Key = "insights", Label = "Insights" }
                }
            },
            ["Finance"] = new()
            {
                Eyebrow = "Money workspace",
                Badge = "Live",
                Note = "The finance stage brings overview, accounts, transactions, categories, and analytics into one working context.",
                DefaultSubsection = "overview",
                Subsections = new[]
                {
                    new ShellNavItem { Key = "overview", Label = "Overview" },
                    new ShellNavItem { Key = "accounts", Label = "Accounts" },
                    new ShellNavItem { Key = "transactions", Label = "Transactions" },
                    new ShellNavItem { Key = "categories", Label = "Categories" },
                    new ShellNavItem { Key = "analytics", Label = "Analytics" }
                }
            },
            ["Health"] = new()
            {
                Eyebrow = "Wellbeing",
                Badge = "Soon",
                Note = "This will become the calmer health stage for habits, metrics, and recorded history.",
                DefaultSubsection = "habits",
                Subsections = new[]
                {
                    new ShellNavItem { Key = "habits", Label = "Habits" },
                    new ShellNavItem { Key = "metrics", Label = "Metrics" },
                    new ShellNavItem { Key = "records", Label = "History" }
                }
            },
            ["Tasks"] = new()
            {
                Eyebrow = "Execution",
                Badge = "Soon",
                Note = "Tasks, priorities, and workflows will live here without extra chrome.",
                DefaultSubsection = "focus",
                Subsections = new[]
                {
                    new ShellNavItem { Key = "focus", Label = "Focus" },
                    new ShellNavItem { Key = "board", Label = "Board" },
                    new ShellNavItem { Key = "archive", Label = "Archive" }
                }
            },
            ["Chat"] = new()
            {
                Eyebrow = "AI workspace",
                Badge = "Beta",
                Note = "This dedicated AI scene keeps the current assistant-style shell. For now the section stays in development.",
                DefaultSubsection = "chat",
                Subsections = new[]
                {
                    new ShellNavItem { Key = "chat", Label = "Dialog" }
                }
            },
            ["Settings"] = new()
            {
                Eyebrow = "Control",
                Badge = "Secure",
                Note = "App, profile, and connected-service settings will live here with stronger privacy cues.",
                DefaultSubsection = "profile",
                Subsections = new[]
                {
                    new ShellNavItem { Key = "profile", Label = "Profile" },
                    new ShellNavItem { Key = "preferences", Label = "Preferences" },
                    new ShellNavItem { Key = "security", Label = "Security" }
                }
            }
        };
    }
}
