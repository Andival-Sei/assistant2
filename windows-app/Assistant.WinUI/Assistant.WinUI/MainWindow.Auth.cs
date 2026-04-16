using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assistant.WinUI.Auth;
using Assistant.WinUI.Finance;
using Assistant.WinUI.Settings;
using Assistant.WinUI.Storage;
using AuthInputValidator = Assistant.WinUI.Application.Auth.AuthInputValidator;
using AppLocalizationService = Assistant.WinUI.Application.Abstractions.ILocalizationService;
using AppDashboardSection = Assistant.WinUI.Application.Shell.DashboardSection;
using AppShellNavItem = Assistant.WinUI.Application.Shell.ShellNavItem;
using AppShellSectionConfig = Assistant.WinUI.Application.Shell.ShellSectionConfig;
using AppShellViewModel = Assistant.WinUI.Application.Shell.ShellViewModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.System;
using WinRT.Interop;

namespace Assistant.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public async Task HandleProtocolActivationAsync(Uri uri)
        {
            var query = QueryHelpers.Parse(uri);
            if (query.TryGetValue("error", out var error))
            {
                SetStatus(error, isError: true);
                return;
            }

            if (query.TryGetValue("access_token", out var token))
            {
                _session = new AuthSession { AccessToken = token };
                PersistSession();
                SetStatus(_isRussian ? "Сессия обновлена." : "Session updated.", isError: false);
                _mode = AuthMode.Reset;
                ApplyMode();
                UpdateShellState();
                return;
            }

            if (query.TryGetValue("code", out var code))
            {
                var verifier = PkceStore.Load();
                if (string.IsNullOrWhiteSpace(verifier))
                {
                    SetStatus(_isRussian ? "Нет PKCE verifier." : "Missing PKCE verifier.", isError: true);
                    return;
                }

                try
                {
                    _session = await _authClient.ExchangeCodeForSessionAsync(code, verifier);
                    PersistSession();
                    PkceStore.Clear();

                    if (query.TryGetValue("type", out var type) &&
                        string.Equals(type, "recovery", StringComparison.OrdinalIgnoreCase))
                    {
                        _mode = AuthMode.Reset;
                        ApplyMode();
                        SetStatus(_isRussian ? "Ссылка подтверждена. Задайте новый пароль." : "Recovery link confirmed. Set a new password.", false);
                    }
                    else
                    {
                        SetStatus(_isRussian ? "Вход завершён." : "Sign-in complete.", false);
                    }

                    UpdateShellState();
                }
                catch (Exception ex)
                {
                    var localized = LocalizeAuthError(ex.Message, _isRussian ? "Не удалось завершить вход." : "Failed to finish sign-in.");
                    var details = string.IsNullOrWhiteSpace(ex.Message) ? localized : $"{localized} ({ex.Message})";
                    SetStatus(details, isError: true);
                }
            }
        }


        private void ForgotButton_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Forgot;
            ApplyMode();
        }

        private async void SettingsGeminiLinkButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://aistudio.google.com/app/apikey"));
        }

        private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
        {
            _mode = AuthMode.Login;
            ApplyMode();
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailInput.Text?.Trim() ?? string.Empty;
            var password = ReadPassword();
            var confirm = ReadConfirmPassword();

            try
            {
                SetBusy(true);

                switch (_mode)
                {
                    case AuthMode.Login:
                        _session = await _authClient.SignInWithPasswordAsync(email, password);
                        PersistSession();
                        SetStatus(_isRussian ? "Вы вошли." : "Signed in.", false);
                        break;
                    case AuthMode.Register:
                        var registerValidation = ValidateRegistration(email, password, confirm);
                        if (registerValidation != null)
                        {
                            SetStatus(registerValidation, true);
                            return;
                        }

                        _session = await _authClient.SignUpAsync(email, password, AppConfig.RedirectUri);
                        if (_session != null && !string.IsNullOrEmpty(_session.AccessToken))
                        {
                            PersistSession();
                            SetStatus(_isRussian ? "Аккаунт создан." : "Account created.", false);
                        }
                        else
                        {
                            SetStatus(_isRussian ? "Проверьте почту для подтверждения." : "Check your email to confirm.", false);
                        }
                        break;
                    case AuthMode.Forgot:
                        var forgotValidation = ValidateEmail(email);
                        if (forgotValidation != null)
                        {
                            SetStatus(forgotValidation, true);
                            return;
                        }

                        await _authClient.ResetPasswordAsync(email, AppConfig.RedirectUri);
                        SetStatus(_isRussian ? "Ссылка отправлена." : "Reset link sent.", false);
                        break;
                    case AuthMode.Reset:
                        if (_session == null || string.IsNullOrWhiteSpace(_session.AccessToken))
                        {
                            SetStatus(_isRussian ? "Нет активной сессии." : "No active session.", true);
                            return;
                        }

                        var resetValidation = ValidatePasswordForReset(email, password, confirm);
                        if (resetValidation != null)
                        {
                            SetStatus(resetValidation, true);
                            return;
                        }

                        await _authClient.UpdatePasswordAsync(_session.AccessToken, password);
                        SetStatus(_isRussian ? "Пароль обновлён." : "Password updated.", false);
                        _mode = AuthMode.Login;
                        ApplyMode();
                        break;
                }

                UpdateShellState();
            }
            catch (Exception ex)
            {
                SetStatus(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось выполнить действие." : "Failed to complete the action."), true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void GoogleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.GoogleAuthEnabled)
            {
                SetStatus(
                    _isRussian
                        ? "Google OAuth пока не настроен в проекте Supabase."
                        : "Google OAuth is not configured in this Supabase project.",
                    true);
                return;
            }

            try
            {
                SetBusy(true);
                var verifier = PkceUtil.CreateCodeVerifier();
                var challenge = PkceUtil.CreateCodeChallenge(verifier);
                PkceStore.Save(verifier);
                using var listener = new LoopbackOAuthListener(
                    AppConfig.GoogleLoopbackRedirectUri,
                    _isRussian,
                    "assistant://auth/return?source=google-auth");
                var url = _authClient.BuildGoogleAuthorizeUrl(
                    AppConfig.GoogleLoopbackRedirectUri,
                    challenge,
                    forceAccountSelection: true);
                await Launcher.LaunchUriAsync(new Uri(url));
                var callbackUri = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(2));
                BringWindowToFront();
                await HandleProtocolActivationAsync(callbackUri);
            }
            catch (Exception ex)
            {
                SetStatus(LocalizeAuthError(ex.Message, _isRussian ? "Ошибка Google OAuth." : "Google OAuth failed."), true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || string.IsNullOrWhiteSpace(_session.AccessToken))
            {
                return;
            }

            try
            {
                SetBusy(true);
                await _authClient.SignOutAsync(_session.AccessToken);
            }
            catch (Exception ex)
            {
                SetStatus(LocalizeAuthError(ex.Message, _isRussian ? "Ошибка выхода." : "Sign-out failed."), true);
            }
            finally
            {
                _sessionStore.Clear();
                _sessionStore.SetRememberDevice(RememberCheck.IsChecked == true);
                _session = null;
                _settingsSnapshot = null;
                _settingsBanner = null;
                SetBusy(false);
                SetStatus(_isRussian ? "Вы вышли." : "Signed out.", false);
                _mode = AuthMode.Login;
                ApplyMode();
                UpdateShellState();
            }
        }

        private bool HasSession() =>
            _session != null && !string.IsNullOrWhiteSpace(_session.AccessToken);

        private string ExtractUserId()
        {
            if (!string.IsNullOrWhiteSpace(_session?.UserId))
            {
                return _session.UserId!;
            }

            var token = _session?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2)
                {
                    return string.Empty;
                }

                var payload = parts[1]
                    .Replace('-', '+')
                    .Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);
                var sub = doc.RootElement.GetPropertyOrDefault("sub");
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    _session!.UserId = sub;
                    return sub;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private string ExtractEmail() =>
            !string.IsNullOrWhiteSpace(_session?.UserEmail)
                ? _session.UserEmail!
                : (string.IsNullOrWhiteSpace(EmailInput.Text) ? "session@assistant" : EmailInput.Text.Trim());

        private List<string> ExtractSessionProviders()
        {
            var token = _session?.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return new List<string>();
            }

            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2)
                {
                    return new List<string>();
                }

                var payload = parts[1]
                    .Replace('-', '+')
                    .Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);

                if (doc.RootElement.TryGetProperty("app_metadata", out var appMetadata))
                {
                    if (appMetadata.TryGetProperty("providers", out var providersElement) &&
                        providersElement.ValueKind == JsonValueKind.Array)
                    {
                        var providers = new List<string>();
                        foreach (var item in providersElement.EnumerateArray())
                        {
                            var provider = item.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(provider) &&
                                !providers.Contains(provider, StringComparer.OrdinalIgnoreCase))
                            {
                                providers.Add(provider);
                            }
                        }

                        if (providers.Count > 0)
                        {
                            return providers;
                        }
                    }

                    if (appMetadata.TryGetProperty("provider", out var providerElement))
                    {
                        var provider = providerElement.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(provider))
                        {
                            return new List<string> { provider };
                        }
                    }
                }
            }
            catch
            {
            }

            return new List<string>();
        }

        private string ExtractDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_settingsSnapshot?.DisplayName))
            {
                return _settingsSnapshot.DisplayName!;
            }

            var email = ExtractEmail();
            var local = email.Split('@', StringSplitOptions.RemoveEmptyEntries)[0];
            if (string.IsNullOrWhiteSpace(local))
            {
                return _isRussian ? "пользователь" : "user";
            }

            return char.ToUpper(local[0]) + local[1..];
        }

        private void SetStatus(string? message, bool isError)
        {
            var safeMessage = message ?? string.Empty;
            AuthStatusBar.Severity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            AuthStatusBar.Title = isError
                ? (_isRussian ? "Нужно внимание" : "Attention required")
                : (_isRussian ? "Готово" : "Done");
            AuthStatusBar.Message = safeMessage;
            _ = ShowAnimatedInfoBarAsync(
                AuthStatusBar,
                safeMessage,
                isError,
                true,
                false,
                isError
                    ? (_isRussian ? "Нужно внимание" : "Attention required")
                    : (_isRussian ? "Готово" : "Done"));
        }

        private async Task ShowAnimatedInfoBarAsync(
            InfoBar infoBar,
            string? message,
            bool isError,
            bool isAuthBar,
            bool persist,
            string title)
        {
            var animationCts = isAuthBar ? _authStatusAnimationCts : _settingsStatusAnimationCts;
            animationCts?.Cancel();
            animationCts?.Dispose();
            animationCts = new CancellationTokenSource();
            if (isAuthBar)
            {
                _authStatusAnimationCts = animationCts;
            }
            else
            {
                _settingsStatusAnimationCts = animationCts;
            }
            var token = animationCts.Token;

            if (string.IsNullOrWhiteSpace(message))
            {
                await HideAnimatedInfoBarAsync(infoBar, token);
                return;
            }

            EnsureAnimatedInfoBarSetup(infoBar);
            infoBar.Title = title;
            infoBar.Message = message;
            infoBar.Severity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            infoBar.Visibility = Visibility.Visible;
            infoBar.IsOpen = true;

            if (infoBar.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                infoBar.RenderTransform = transform;
            }

            var showStoryboard = new Storyboard();
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeIn, infoBar);
            Storyboard.SetTargetProperty(fadeIn, nameof(UIElement.Opacity));

            var slideIn = new DoubleAnimation
            {
                From = -10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(slideIn, transform);
            Storyboard.SetTargetProperty(slideIn, nameof(TranslateTransform.Y));

            showStoryboard.Children.Add(fadeIn);
            showStoryboard.Children.Add(slideIn);
            showStoryboard.Begin();

            try
            {
                if (persist)
                {
                    return;
                }

                await Task.Delay(isError ? TimeSpan.FromSeconds(4.8) : TimeSpan.FromSeconds(3.2), token);
                await HideAnimatedInfoBarAsync(infoBar, token);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static void EnsureAnimatedInfoBarSetup(InfoBar infoBar)
        {
            infoBar.Opacity = 0;
            if (infoBar.RenderTransform is not TranslateTransform)
            {
                infoBar.RenderTransform = new TranslateTransform();
            }
        }

        private static Task HideAnimatedInfoBarAsync(InfoBar infoBar, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            if (infoBar.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                infoBar.RenderTransform = transform;
            }

            var tcs = new TaskCompletionSource<object?>();
            var hideStoryboard = new Storyboard();

            var fadeOut = new DoubleAnimation
            {
                From = infoBar.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeOut, infoBar);
            Storyboard.SetTargetProperty(fadeOut, nameof(UIElement.Opacity));

            var slideOut = new DoubleAnimation
            {
                From = 0,
                To = -8,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(slideOut, transform);
            Storyboard.SetTargetProperty(slideOut, nameof(TranslateTransform.Y));

            hideStoryboard.Children.Add(fadeOut);
            hideStoryboard.Children.Add(slideOut);
            hideStoryboard.Completed += (_, _) =>
            {
                infoBar.IsOpen = false;
                infoBar.Visibility = Visibility.Collapsed;
                infoBar.Opacity = 1;
                transform.Y = 0;
                tcs.TrySetResult(null);
            };
            hideStoryboard.Begin();
            return tcs.Task;
        }

        private void SetBusy(bool busy)
        {
            SubmitButton.IsEnabled = !busy;
            GoogleButton.IsEnabled = !busy;
            SettingsLogoutButton.IsEnabled = !busy;
            SettingsDeleteButton.IsEnabled = !busy;
        }

        private string LocalizeAuthError(string? raw, string fallback)
        {
            var message = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return fallback;
            }

            var normalized = message.ToLowerInvariant();
            if (normalized.Contains("invalid login credentials"))
            {
                return _isRussian ? "Неверный email или пароль." : "Incorrect email or password.";
            }

            if (normalized.Contains("email not confirmed"))
            {
                return _isRussian ? "Подтвердите email и затем войдите." : "Confirm your email first, then sign in.";
            }

            if (normalized.Contains("user already registered") || normalized.Contains("already been registered"))
            {
                return _isRussian ? "Этот email уже занят." : "This email is already in use.";
            }

            if (normalized.Contains("over_email_send_rate_limit") || normalized.Contains("security purposes") || normalized.Contains("rate limit"))
            {
                return _isRussian ? "Слишком много попыток. Подождите немного и попробуйте снова." : "Too many attempts. Wait a bit and try again.";
            }

            if (normalized.Contains("signup is disabled"))
            {
                return _isRussian ? "Регистрация сейчас недоступна." : "Sign-up is currently unavailable.";
            }

            if (normalized.Contains("network request failed"))
            {
                return _isRussian ? "Проблема сети. Проверьте подключение и попробуйте снова." : "Network issue. Check your connection and try again.";
            }

            if (normalized.Contains("manual linking") && normalized.Contains("disabled"))
            {
                return _isRussian
                    ? "В текущей конфигурации Supabase ручная привязка или отвязка Google недоступна."
                    : "Manual Google linking or unlinking is unavailable in the current Supabase configuration.";
            }

            if (normalized.Contains("not added to the linked identities"))
            {
                return _isRussian
                    ? "Google OAuth завершился, но identity не привязалась. Скорее всего, в Supabase выключен Manual Linking."
                    : "Google OAuth completed, but the identity was not linked. Manual Linking is likely disabled in Supabase.";
            }

            if (normalized.Contains("different google account"))
            {
                return _isRussian
                    ? "Выбран другой Google-аккаунт. Для привязки нужен тот же email, что и у текущего профиля."
                    : "A different Google account was selected. Use the same email as the current profile.";
            }

            if (normalized.Contains("invalid jwt") || normalized.Contains("\"code\":401") || normalized.Contains("jwt"))
            {
                return _isRussian ? "Сессия устарела. Попробуйте открыть настройки ещё раз." : "Your session expired. Open Settings again.";
            }

            if (normalized.Contains("password should be at least"))
            {
                return _isRussian ? "Минимум 8 символов." : "Use at least 8 characters.";
            }

            return fallback;
        }

        private string LocalizeSettingsLoadError(string? raw)
        {
            var message = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return _isRussian ? "Не удалось загрузить настройки." : "Failed to load settings.";
            }

            var normalized = message.ToLowerInvariant();
            if (normalized.Contains("profiles"))
            {
                return _isRussian
                    ? "Не удалось загрузить профиль: имя или email."
                    : "Failed to load the profile: display name or email.";
            }

            if (normalized.Contains("user_settings"))
            {
                return _isRussian
                    ? "Не удалось загрузить настройки Gemini."
                    : "Failed to load Gemini settings.";
            }

            if (normalized.Contains("settings_get_linked_identities") || normalized.Contains("identity"))
            {
                return _isRussian
                    ? "Не удалось загрузить привязанные способы входа."
                    : "Failed to load linked sign-in methods.";
            }

            if (normalized.Contains("bad gateway") || normalized.Contains("gateway") || normalized.Contains("timeout") || normalized.Contains("timed out"))
            {
                return _isRussian
                    ? "Не удалось загрузить часть настроек из-за временной проблемы сети или Supabase."
                    : "Part of the settings could not be loaded because of a temporary network or Supabase issue.";
            }

            return _isRussian ? "Не удалось загрузить настройки." : "Failed to load settings.";
        }

        private void LoadSession()
        {
            RememberCheck.IsChecked = _sessionStore.LoadRememberDevice();
            if (RememberCheck.IsChecked != true)
            {
                _sessionStore.Clear();
                UpdateShellState();
                return;
            }

            var json = _sessionStore.Load();
            if (string.IsNullOrWhiteSpace(json))
            {
                UpdateShellState();
                return;
            }

            try
            {
                _session = JsonSerializer.Deserialize<AuthSession>(json);
                if (HasSession())
                {
                    SetStatus(_isRussian ? "Сессия восстановлена." : "Session restored.", false);
                }
            }
            catch
            {
                _session = null;
            }

            UpdateShellState();
        }

        private void PersistSession()
        {
            if (_session == null)
            {
                return;
            }

            var rememberDevice = RememberCheck.IsChecked == true;
            _sessionStore.Save(_session, rememberDevice);
        }

        private string? ValidateRegistration(string email, string password, string confirm)
        {
            return AuthInputValidator.ValidateRegistration(email, password, confirm, _isRussian);
        }

        private string? ValidatePasswordForReset(string email, string password, string confirm)
        {
            return AuthInputValidator.ValidatePasswordReset(email, password, confirm, _isRussian);
        }

        private string? ValidateEmail(string email)
        {
            return AuthInputValidator.ValidateEmail(email, _isRussian);
        }

        private string? ValidatePassword(string email, string password)
        {
            return AuthInputValidator.ValidatePassword(email, password, _isRussian);
        }

        private string? ValidatePasswordConfirmation(string password, string confirm)
        {
            return AuthInputValidator.ValidatePasswordConfirmation(password, confirm, _isRussian);
        }

        private string ReadPassword() => PasswordInput.Password ?? string.Empty;

        private string ReadConfirmPassword() => ConfirmPasswordInput.Password ?? string.Empty;

        private void AnimateAuthForm()
        {
            AuthFormPanel.Opacity = 0;
            AuthFormTranslate.Y = 12;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var slideIn = new DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(fadeIn, AuthFormPanel);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            Storyboard.SetTarget(slideIn, AuthFormTranslate);
            Storyboard.SetTargetProperty(slideIn, "Y");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(slideIn);
            storyboard.Begin();
        }
    }
}


