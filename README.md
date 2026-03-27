# Assistant

Монорепозиторий продукта `Assistant` с тремя клиентами:

- `web/` — веб-клиент на React + Vite
- `android-app/` — Android-клиент на Jetpack Compose
- `windows-app/` — десктоп-клиент на WinUI 3

Также в репозитории лежат:

- `supabase/` — миграции и Edge Functions
- `docs/requirements.md` — актуальные требования к продукту и разработке

## Что сейчас реализовано

- единый auth-flow для Web, Android и Windows
- вход по email/password
- регистрация с валидацией полей
- восстановление пароля
- `remember device` без хранения пароля
- переключение темы `System / Light / Dark`
- переключение языка `RU / EN`
- вход через Google
- базовый post-auth shell
- backend-логика для проверки занятости email через Supabase Edge Function

## Структура репозитория

```text
assistant/
├─ android-app/
├─ docs/
├─ supabase/
├─ web/
└─ windows-app/
```

## Технологии

### Web

- React 19
- Vite
- TypeScript
- React Router
- i18next
- `@supabase/supabase-js`

### Android

- Kotlin
- Jetpack Compose
- Material 3
- Supabase `auth-kt`
- Android Browser / Custom Tabs

### Windows

- C#
- WinUI 3
- Windows App SDK
- Supabase REST auth integration

### Backend

- Supabase Auth
- Supabase SQL migrations
- Supabase Edge Functions

## Быстрый старт

## 1. Прочитать требования

Перед любыми изменениями сначала открой:

- [docs/requirements.md](docs/requirements.md)

## 2. Web

```powershell
cd f:\pet-projects\assistant\web
pnpm install
pnpm run dev
```

Сборка:

```powershell
cd f:\pet-projects\assistant\web
pnpm run build
```

## 3. Android

Сборка debug:

```powershell
cd f:\pet-projects\assistant\android-app
.\gradlew.bat :app:assembleDebug
```

Установка на подключённое устройство:

```powershell
cd f:\pet-projects\assistant\android-app
.\gradlew.bat :app:installDebug
```

Подключение телефона по `adb` через Wi-Fi:

```powershell
# 1. Один раз с подключённым USB
& 'C:\Users\freed\AppData\Local\Android\Sdk\platform-tools\adb.exe' tcpip 5555

# 2. Узнать IP телефона в текущей Wi-Fi сети и подключиться
& 'C:\Users\freed\AppData\Local\Android\Sdk\platform-tools\adb.exe' connect <PHONE_IP>:5555

# 3. Проверить, что устройство видно по сети
& 'C:\Users\freed\AppData\Local\Android\Sdk\platform-tools\adb.exe' devices
```

Пример для текущего телефона:

```powershell
& 'C:\Users\freed\AppData\Local\Android\Sdk\platform-tools\adb.exe' connect 192.168.0.193:5555
```

Переустановка debug-сборки на телефон после подключения по Wi-Fi:

```powershell
cd f:\pet-projects\assistant\android-app
.\gradlew.bat :app:installDebug
```

Если нужно сначала снести установленную debug-версию:

```powershell
& 'C:\Users\freed\AppData\Local\Android\Sdk\platform-tools\adb.exe' uninstall com.assistant.app
cd f:\pet-projects\assistant\android-app
.\gradlew.bat :app:installDebug
```

Примечания:

- ПК может быть подключён к роутеру по кабелю, а телефон по Wi-Fi. Главное, чтобы они были в одной локальной сети.
- После смены Wi-Fi или перезагрузки роутера IP телефона может измениться, тогда нужно повторить `adb connect`.
- Если `adb` не добавлен в `PATH`, использовать полный путь к `adb.exe`, как в командах выше.

## 4. Windows

Сборка:

```powershell
cd f:\pet-projects\assistant
dotnet build windows-app/Assistant.WinUI/Assistant.WinUI/Assistant.WinUI.csproj
```

Запуск:

- через Visual Studio
- или запуском `Assistant.WinUI.exe` из `bin/x64/Debug/...`

## Конфигурация

### Android

Сейчас локальная конфигурация лежит в:

- [android-app/gradle.properties](android-app/gradle.properties)

Используются параметры:

- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `ENABLE_GOOGLE_AUTH`

### Web

Веб-клиент использует конфиг Supabase и флаги Google Auth из исходников/окружения проекта.

### Windows

WinUI-клиент читает конфиг из `AppConfig` и использует browser-based OAuth flow.

## Supabase

В репозитории лежат:

- миграции: [supabase/migrations](supabase/migrations)
- edge functions: [supabase/functions](supabase/functions)

Ключевая функция для auth:

- [supabase/functions/auth-email-availability/index.ts](supabase/functions/auth-email-availability/index.ts)

## Git

Репозиторий ведётся как один общий монорепо для всех платформ. В `.gitignore` уже исключены:

- `node_modules`, `dist`, `.vite`, build-артефакты web
- `build`, `.gradle`, `.kotlin`, `local.properties`, keystore-файлы Android
- `bin`, `obj`, `.vs`, MSIX/AppPackage артефакты WinUI

## Текущий фокус

Сейчас проект уже ушёл дальше UI-заглушек: основная работа сосредоточена вокруг единого auth-flow, консистентного UI между платформами и постепенного наполнения post-auth модулей.
