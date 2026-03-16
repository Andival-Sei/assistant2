# Assistant (Web + WinUI + Android)

## Что сейчас есть
- Экран авторизации (UI‑заглушка без реальной авторизации).
- Переключатель темы: System / Light / Dark.
- Переключатель языка: RU / EN.
- Согласованный визуальный стиль между Web, WinUI и Android.

## Структура репозитория
- `web/` — Astro, лендинг + экран авторизации.
- `windows-app/` — WinUI 3 (пакетное приложение).
- `android-app/` — Android (Jetpack Compose).

## Web
- Стек: Astro.
- Роуты:
  - `/ru/`, `/en/` — лендинг.
  - `/ru/app/`, `/en/app/` — экран авторизации.
  - `/app` и `/` — редирект по языку.
- Главный layout auth‑экрана: `web/src/layouts/AppLayout.astro`.
- Локализация:
  - `web/src/pages/ru/app/index.astro`
  - `web/src/pages/en/app/index.astro`

Запуск:
```bash
cd f:\pet-projects\assistant\web
npm run dev
```

## WinUI
- Проект: `windows-app/Assistant.WinUI/Assistant.WinUI.slnx`.
- Основной экран: `windows-app/Assistant.WinUI/Assistant.WinUI/MainWindow.xaml`.
- Локализация/логика переключателей: `MainWindow.xaml.cs`.
- Тема/цвета: `App.xaml` (ThemeDictionaries).

Запуск:
- Открыть `Assistant.WinUI.slnx` в Visual Studio.
- Debug | x64 → ▶.

## Android
- Проект: `android-app/`.
- Основной экран: `android-app/app/src/main/java/com/assistant/app/MainActivity.kt`.
- Цвета: `android-app/app/src/main/java/com/assistant/app/ui/theme/Color.kt`.
- Тема: `android-app/app/src/main/java/com/assistant/app/ui/theme/Theme.kt`.

Сборка/установка (телефон подключен, USB‑debug включен):
```powershell
$env:JAVA_HOME = "C:\Users\freed\AppData\Local\Programs\Android Studio\jbr"
$env:Path = "$env:JAVA_HOME\bin;" + $env:Path
cd f:\pet-projects\assistant\android-app
.\gradlew.bat :app:installDebug
```

## Важные примечания
- Реальной авторизации пока нет — только UI.
- Android адаптивен: узкие экраны → одноколоночная версия.
- WinUI пока фиксирован по раскладке (без VisualState).

## Быстрые правки UI
- Web: `web/src/layouts/AppLayout.astro`
- WinUI: `windows-app/Assistant.WinUI/Assistant.WinUI/MainWindow.xaml`
- Android: `android-app/app/src/main/java/com/assistant/app/MainActivity.kt`
