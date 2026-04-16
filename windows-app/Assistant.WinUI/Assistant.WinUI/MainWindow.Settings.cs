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
        private void RenderSettingsContent()
        {
            var profileVisible = _section == DashboardSection.Settings &&
                string.Equals(_activeSubsection, "profile", StringComparison.OrdinalIgnoreCase);
            var preferencesVisible = _section == DashboardSection.Settings &&
                string.Equals(_activeSubsection, "preferences", StringComparison.OrdinalIgnoreCase);
            var securityVisible = _section == DashboardSection.Settings &&
                string.Equals(_activeSubsection, "security", StringComparison.OrdinalIgnoreCase);

            SettingsProfileCard.Visibility = profileVisible ? Visibility.Visible : Visibility.Collapsed;
            SettingsPreferencesCard.Visibility = preferencesVisible ? Visibility.Visible : Visibility.Collapsed;
            SettingsSecurityCard.Visibility = securityVisible ? Visibility.Visible : Visibility.Collapsed;

            SettingsStatusBar.Severity = _settingsBannerIsError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            SettingsStatusBar.Title = _settingsBannerIsError
                ? (_isRussian ? "Проблема с настройками" : "Settings issue")
                : (_isRussian ? "Настройки обновлены" : "Settings updated");
            SettingsStatusBar.Message = _settingsBanner ?? string.Empty;
            SettingsLoadingText.Visibility = _settingsLoading ? Visibility.Visible : Visibility.Collapsed;

            var hasGoogle = _settingsSnapshot?.Identities.Any(identity =>
                string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase)) == true;
            SettingsGoogleBody.Text = hasGoogle
                ? (_isRussian ? "Google уже привязан к вашему аккаунту." : "Google is already linked to your account.")
                : (_isRussian ? "Подключите Google к текущему аккаунту." : "Connect Google to the current account.");

            var googleBusy = _settingsBusyAction is "google_link" or "google_unlink";
            SettingsGoogleButton.Content = googleBusy
                ? (_isRussian ? "Переходим..." : "Opening...")
                : (hasGoogle
                    ? (_isRussian ? "Отключить Google" : "Disconnect Google")
                    : (_isRussian ? "Подключить Google" : "Connect Google"));

            ApplySettingsBusyState();
        }

        private void ApplySettingsBusyState()
        {
            var busy = _settingsLoading || !string.IsNullOrWhiteSpace(_settingsBusyAction);
            var hasSavedGeminiKey = _settingsSnapshot?.HasGeminiApiKey == true;
            SettingsDisplayNameInput.IsEnabled = !busy;
            SettingsEmailInput.IsEnabled = !busy;
            SettingsGeminiInput.IsEnabled = !busy;
            SettingsAiEnhancementsToggle.IsEnabled = !busy && hasSavedGeminiKey;
            SettingsPasswordInput.IsEnabled = !busy;
            SettingsPasswordConfirmInput.IsEnabled = !busy;
            SettingsDisplayNameButton.IsEnabled = !busy;
            SettingsEmailButton.IsEnabled = !busy;
            SettingsGeminiButton.IsEnabled = !busy;
            SettingsGeminiClearButton.IsEnabled = !busy && hasSavedGeminiKey;
            SettingsGeminiLinkButton.IsEnabled = !busy;
            SettingsPasswordButton.IsEnabled = !busy;
            SettingsGoogleButton.IsEnabled = !busy;
            SettingsLogoutButton.IsEnabled = !busy;
            SettingsFinanceResetButton.IsEnabled = !busy;
            SettingsDeleteButton.IsEnabled = !busy;
        }

        private void ApplySettingsGeminiPresentation()
        {
            var hasSavedGeminiKey = _settingsSnapshot?.HasGeminiApiKey == true;
            SettingsGeminiInput.PlaceholderText = _isRussian
                ? "Вставьте Gemini API key"
                : "Paste your Gemini API key";

            SettingsGeminiStateText.Text = hasSavedGeminiKey
                ? (_isRussian
                    ? "Сохранённый ключ загружен. Можно заменить его и снова нажать «Сохранить»."
                    : "The saved key is loaded. Replace it and click Save again if needed.")
                : (_isRussian
                    ? "Ключ ещё не сохранён."
                    : "No Gemini key has been saved yet.");

            SettingsGeminiClearButton.Visibility = hasSavedGeminiKey ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetSettingsBanner(string? message, bool isError, bool persist = false)
        {
            _settingsBanner = message;
            _settingsBannerIsError = isError;
            RenderSettingsContent();
            _ = ShowAnimatedInfoBarAsync(
                SettingsStatusBar,
                message,
                isError,
                false,
                persist,
                _isRussian
                    ? (_settingsBannerIsError ? "Проблема с настройками" : "Настройки обновлены")
                    : (_settingsBannerIsError ? "Settings issue" : "Settings updated"));
        }

        private async Task LoadSettingsAsync(bool clearBanner = true)
        {
            if (_settingsClient == null || !HasSession() || _session == null || _section != DashboardSection.Settings)
            {
                return;
            }

            var userId = ExtractUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                SetSettingsBanner(
                    _isRussian ? "Не удалось определить пользователя для настроек." : "Failed to resolve the current user for settings.",
                    true);
                return;
            }

            try
            {
                _settingsLoading = true;
                if (clearBanner)
                {
                    SetSettingsBanner(null, false);
                }
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();

                var snapshot = await _settingsClient.GetSnapshotAsync(
                    accessToken,
                    userId,
                    ExtractEmail());

                var cachedDisplayName = _displayNameStore.Load(userId);
                if (string.IsNullOrWhiteSpace(snapshot.DisplayName) && !string.IsNullOrWhiteSpace(_settingsSnapshot?.DisplayName))
                {
                    snapshot.DisplayName = _settingsSnapshot.DisplayName;
                }
                if (string.IsNullOrWhiteSpace(snapshot.DisplayName) && !string.IsNullOrWhiteSpace(cachedDisplayName))
                {
                    snapshot.DisplayName = cachedDisplayName;
                }

                var cachedGeminiSettings = _geminiSettingsStore.Load(userId);
                if (!snapshot.HasGeminiApiKey && cachedGeminiSettings is { } cached && !string.IsNullOrWhiteSpace(cached.GeminiApiKey))
                {
                    snapshot.GeminiApiKey = cached.GeminiApiKey;
                    snapshot.AiEnhancementsEnabled = cached.AiEnhancementsEnabled;
                }

                snapshot.Identities = BuildLinkedIdentityFallbacksFromSession();
                try
                {
                    snapshot.Identities = await LoadLinkedIdentitiesAsync();
                }
                catch
                {
                    // Keep the fallback provider list from the current session so the UI remains stable
                    // even when the identities RPC is temporarily unavailable.
                }

                _settingsSnapshot = snapshot;
                if (!string.IsNullOrWhiteSpace(_settingsSnapshot.DisplayName))
                {
                    _displayNameStore.Save(userId, _settingsSnapshot.DisplayName);
                }
                if (_settingsSnapshot.HasGeminiApiKey)
                {
                    _geminiSettingsStore.Save(_settingsSnapshot.UserId, _settingsSnapshot.GeminiApiKey, _settingsSnapshot.AiEnhancementsEnabled);
                }
                SettingsDisplayNameInput.Text = snapshot.DisplayName;
                SettingsEmailInput.Text = snapshot.Email;
                SettingsGeminiInput.Password = snapshot.GeminiApiKey;
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = snapshot.HasGeminiApiKey && snapshot.AiEnhancementsEnabled;
                _suppressSettingsAiToggleSave = false;
                SettingsPasswordInput.Password = string.Empty;
                SettingsPasswordConfirmInput.Password = string.Empty;
                ApplySettingsGeminiPresentation();
            }
            catch (Exception ex)
            {
                _settingsSnapshot = CreateFallbackSettingsSnapshot(userId);
                var cachedDisplayName = _displayNameStore.Load(userId);
                if (string.IsNullOrWhiteSpace(_settingsSnapshot.DisplayName) && !string.IsNullOrWhiteSpace(cachedDisplayName))
                {
                    _settingsSnapshot.DisplayName = cachedDisplayName;
                }
                var cachedGeminiSettings = _geminiSettingsStore.Load(userId);
                if (cachedGeminiSettings is { } cached && !string.IsNullOrWhiteSpace(cached.GeminiApiKey))
                {
                    _settingsSnapshot.GeminiApiKey = cached.GeminiApiKey;
                    _settingsSnapshot.AiEnhancementsEnabled = cached.AiEnhancementsEnabled;
                }
                SettingsDisplayNameInput.Text = _settingsSnapshot.DisplayName;
                SettingsEmailInput.Text = _settingsSnapshot.Email;
                SettingsGeminiInput.Password = _settingsSnapshot.GeminiApiKey;
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = _settingsSnapshot.HasGeminiApiKey && _settingsSnapshot.AiEnhancementsEnabled;
                _suppressSettingsAiToggleSave = false;
                SettingsPasswordInput.Password = string.Empty;
                SettingsPasswordConfirmInput.Password = string.Empty;
                if (_settingsSnapshot.Identities.Count == 0)
                {
                    _settingsSnapshot.Identities = BuildLinkedIdentityFallbacksFromSession();
                }
                if (clearBanner)
                {
                    var hasVisibleData =
                        !string.IsNullOrWhiteSpace(_settingsSnapshot.Email) ||
                        !string.IsNullOrWhiteSpace(_settingsSnapshot.DisplayName) ||
                        _settingsSnapshot.HasGeminiApiKey ||
                        _settingsSnapshot.Identities.Count > 0;

                    if (hasVisibleData)
                    {
                        SetSettingsBanner(null, false);
                    }
                    else
                    {
                        SetSettingsBanner(LocalizeSettingsLoadError(ex.Message), true);
                    }
                }
                ApplySettingsGeminiPresentation();
            }
            finally
            {
                _settingsLoading = false;
                RenderSettingsContent();
            }
        }

        private SettingsSnapshot CreateFallbackSettingsSnapshot(string userId)
        {
            return new SettingsSnapshot
            {
                UserId = userId,
                Email = ExtractEmail(),
                DisplayName = string.Empty,
                GeminiApiKey = _settingsSnapshot?.GeminiApiKey ?? string.Empty,
                AiEnhancementsEnabled = (_settingsSnapshot?.HasGeminiApiKey ?? false) &&
                                        (_settingsSnapshot?.AiEnhancementsEnabled ?? false),
                Identities = _settingsSnapshot?.Identities ?? new List<LinkedIdentity>()
            };
        }

        private async Task SaveDisplayNameAsync()
        {
            if (_settingsClient == null || _session == null || _settingsSnapshot == null)
            {
                return;
            }

            var nextName = SettingsDisplayNameInput.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextName))
            {
                SetSettingsBanner(_isRussian ? "Имя не может быть пустым." : "Name cannot be empty.", true);
                return;
            }

            try
            {
                _settingsBusyAction = "display_name";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _settingsClient.SaveDisplayNameAsync(accessToken, _settingsSnapshot.UserId, ExtractEmail(), nextName);
                _settingsSnapshot.DisplayName = nextName;
                _displayNameStore.Save(_settingsSnapshot.UserId, nextName);
                SetSettingsBanner(_isRussian ? "Имя сохранено." : "Name saved.", false);
                DashboardWelcomeTitle.Text = _isRussian
                    ? $"{SectionLabel(_section)}, {ExtractDisplayName()}."
                    : $"{SectionLabel(_section)}, {ExtractDisplayName()}.";
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось сохранить имя." : "Failed to save name."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task SaveEmailAsync()
        {
            if (_session == null)
            {
                return;
            }

            var nextEmail = SettingsEmailInput.Text?.Trim() ?? string.Empty;
            var validation = ValidateEmail(nextEmail);
            if (validation != null)
            {
                SetSettingsBanner(validation, true);
                return;
            }

            try
            {
                _settingsBusyAction = "email";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _authClient.UpdateEmailAsync(accessToken, nextEmail);
                if (_settingsSnapshot != null)
                {
                    _settingsSnapshot.Email = nextEmail;
                }
                SetSettingsBanner(
                    _isRussian
                        ? "Запрос на смену почты отправлен. Подтвердите новую почту, если Supabase запросит подтверждение."
                        : "Email change requested. Confirm the new email if Supabase asks for verification.",
                    false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось изменить почту." : "Failed to change email."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task SaveGeminiApiKeyAsync()
        {
            if (_settingsClient == null || _session == null || _settingsSnapshot == null)
            {
                return;
            }

            try
            {
                _settingsBusyAction = "gemini";
                RenderSettingsContent();
                await PersistGeminiSettingsAsync(
                    preserveExistingKeyWhenInputBlank: true,
                    successMessage: _isRussian ? "Настройки Gemini сохранены." : "Gemini settings saved.");
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось сохранить настройки Gemini." : "Failed to save Gemini settings."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task PersistGeminiSettingsAsync(bool preserveExistingKeyWhenInputBlank, string successMessage)
        {
            if (_settingsClient == null || _settingsSnapshot == null)
            {
                return;
            }

            var accessToken = await GetFreshAccessTokenAsync();
            var enteredGeminiApiKey = SettingsGeminiInput.Password?.Trim() ?? string.Empty;
            var geminiApiKey = !string.IsNullOrWhiteSpace(enteredGeminiApiKey)
                ? enteredGeminiApiKey
                : preserveExistingKeyWhenInputBlank
                    ? _settingsSnapshot.GeminiApiKey
                    : string.Empty;

            var aiEnhancementsEnabled = !string.IsNullOrWhiteSpace(geminiApiKey) && SettingsAiEnhancementsToggle.IsOn;
            await _settingsClient.SaveGeminiSettingsAsync(
                accessToken,
                _settingsSnapshot.UserId,
                geminiApiKey,
                aiEnhancementsEnabled);

            _settingsSnapshot.GeminiApiKey = geminiApiKey;
            _settingsSnapshot.AiEnhancementsEnabled = aiEnhancementsEnabled;
            _geminiSettingsStore.Save(_settingsSnapshot.UserId, geminiApiKey, aiEnhancementsEnabled);
            SettingsGeminiInput.Password = geminiApiKey;
            _suppressSettingsAiToggleSave = true;
            SettingsAiEnhancementsToggle.IsOn = _settingsSnapshot.HasGeminiApiKey && _settingsSnapshot.AiEnhancementsEnabled;
            _suppressSettingsAiToggleSave = false;
            ApplySettingsGeminiPresentation();
            SetSettingsBanner(successMessage, false);
        }

        private void SettingsGeminiInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ApplySettingsGeminiPresentation();
            ApplySettingsBusyState();
        }

        private async void SettingsAiEnhancementsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingsAiToggleSave || _settingsLoading || _settingsBusyAction != null || _settingsSnapshot?.HasGeminiApiKey != true)
            {
                return;
            }

            try
            {
                _settingsBusyAction = "gemini_toggle";
                RenderSettingsContent();
                await PersistGeminiSettingsAsync(
                    preserveExistingKeyWhenInputBlank: true,
                    successMessage: SettingsAiEnhancementsToggle.IsOn
                        ? (_isRussian ? "AI-улучшения включены." : "AI enhancements enabled.")
                        : (_isRussian ? "AI-улучшения выключены." : "AI enhancements disabled."));
            }
            catch (Exception ex)
            {
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = _settingsSnapshot.AiEnhancementsEnabled;
                _suppressSettingsAiToggleSave = false;
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось обновить AI-улучшения." : "Failed to update AI enhancements."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async void SettingsGeminiClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsSnapshot?.HasGeminiApiKey != true || _settingsClient == null || _session == null)
            {
                return;
            }

            try
            {
                _settingsBusyAction = "gemini_clear";
                _suppressSettingsAiToggleSave = true;
                SettingsAiEnhancementsToggle.IsOn = false;
                _suppressSettingsAiToggleSave = false;
                RenderSettingsContent();
                SettingsGeminiInput.Password = string.Empty;
                await PersistGeminiSettingsAsync(
                    preserveExistingKeyWhenInputBlank: false,
                    successMessage: _isRussian ? "Ключ Gemini удалён." : "Gemini key deleted.");
                _geminiSettingsStore.Clear(_settingsSnapshot.UserId);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось удалить ключ Gemini." : "Failed to delete Gemini key."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task SavePasswordAsync()
        {
            if (_session == null)
            {
                return;
            }

            var nextPassword = SettingsPasswordInput.Password ?? string.Empty;
            var confirmPassword = SettingsPasswordConfirmInput.Password ?? string.Empty;
            var validation = ValidatePasswordForReset(ExtractEmail(), nextPassword, confirmPassword);
            if (validation != null)
            {
                SetSettingsBanner(validation, true);
                return;
            }

            try
            {
                _settingsBusyAction = "password";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _authClient.UpdatePasswordAsync(accessToken, nextPassword);
                SettingsPasswordInput.Password = string.Empty;
                SettingsPasswordConfirmInput.Password = string.Empty;
                SetSettingsBanner(_isRussian ? "Пароль обновлён." : "Password updated.", false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось обновить пароль." : "Failed to update password."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task ToggleGoogleIdentityAsync()
        {
            if (_session == null || _settingsClient == null)
            {
                return;
            }

            var googleIdentity = _settingsSnapshot?.Identities.FirstOrDefault(identity =>
                string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase));
            if (googleIdentity == null)
            {
                try
                {
                    var previousSession = CloneSession(_session);
                    var previousUserId = ExtractUserId();
                    _settingsBusyAction = "google_link";
                    RenderSettingsContent();
                    var verifier = PkceUtil.CreateCodeVerifier();
                    var challenge = PkceUtil.CreateCodeChallenge(verifier);
                    PkceStore.Save(verifier);
                    using var listener = new LoopbackOAuthListener(
                        AppConfig.GoogleLoopbackRedirectUri,
                        _isRussian,
                        "assistant://auth/return?source=google-link");
                    var url = _authClient.BuildGoogleAuthorizeUrl(
                        AppConfig.GoogleLoopbackRedirectUri,
                        challenge,
                        forceAccountSelection: true);
                    await Launcher.LaunchUriAsync(new Uri(url));
                    var callbackUri = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(2));
                    BringWindowToFront();
                    var query = QueryHelpers.Parse(callbackUri);
                    if (query.TryGetValue("error", out var error))
                    {
                        if (query.TryGetValue("error_description", out var description) && !string.IsNullOrWhiteSpace(description))
                        {
                            throw new InvalidOperationException(description);
                        }

                        throw new InvalidOperationException(error);
                    }

                    await CompleteGoogleLinkSignInAsync(callbackUri);
                    var currentUserId = ExtractUserId();
                    if (!string.Equals(previousUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
                    {
                        _session = previousSession;
                        PersistSession();
                        await LoadSettingsAsync(clearBanner: false);
                        throw new InvalidOperationException(
                            _isRussian
                                ? "Вы вошли через другой Google-аккаунт. Для привязки используйте Google с тем же email, что и у текущего профиля."
                                : "You signed in with a different Google account. Use the Google account with the same email as the current profile.");
                    }

                    var identities = await WaitForLinkedIdentitiesAsync();
                    _settingsSnapshot ??= CreateFallbackSettingsSnapshot(currentUserId);
                    _settingsSnapshot.Identities = identities;
                    await LoadSettingsAsync(clearBanner: false);
                    RenderSettingsContent();

                    var linkedGoogleIdentity = identities.FirstOrDefault(identity =>
                        string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase));
                    if (linkedGoogleIdentity == null)
                    {
                        var providers = identities
                            .Select(identity => identity.Provider)
                            .Where(provider => !string.IsNullOrWhiteSpace(provider))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var providersText = providers.Count == 0
                            ? (_isRussian ? "identity не найдены" : "no identities found")
                            : string.Join(", ", providers);
                        throw new InvalidOperationException(
                            _isRussian
                                ? $"Google вход завершился, но identity не появилась в профиле. Текущие identity: {providersText}."
                                : $"Google sign-in completed, but the identity did not appear in the profile. Current identities: {providersText}.");
                    }

                    SetSettingsBanner(_isRussian ? "Google аккаунт подключён." : "Google account linked.", false);
                }
                catch (Exception ex)
                {
                    SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось привязать Google." : "Failed to link Google."), true);
                }
                finally
                {
                    _settingsBusyAction = null;
                    RenderSettingsContent();
                }

                return;
            }

            if ((_settingsSnapshot?.Identities.Count ?? 0) < 2)
            {
                SetSettingsBanner(
                    _isRussian
                        ? "Нельзя отвязать единственный способ входа. Сначала задайте пароль или привяжите другую identity."
                        : "You cannot unlink the only sign-in method. Set a password or link another identity first.",
                    true);
                return;
            }

            try
            {
                _settingsBusyAction = "google_unlink";
                RenderSettingsContent();
                var accessToken = await GetFreshAccessTokenAsync();
                await _settingsClient.UnlinkGoogleIdentityAsync(accessToken);
                if (_settingsSnapshot != null)
                {
                    _settingsSnapshot.Identities = _settingsSnapshot.Identities
                        .Where(identity => !string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                RenderSettingsContent();
                await LoadSettingsAsync(clearBanner: false);
                SetSettingsBanner(_isRussian ? "Google аккаунт отключён." : "Google account disconnected.", false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось отвязать Google." : "Failed to unlink Google."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private static AuthSession CloneSession(AuthSession session) => new()
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            TokenType = session.TokenType,
            ExpiresIn = session.ExpiresIn,
            ExpiresAt = session.ExpiresAt,
            UserId = session.UserId,
            UserEmail = session.UserEmail
        };

        private async Task<List<LinkedIdentity>> WaitForLinkedIdentitiesAsync()
        {
            if (_settingsClient == null || _session == null)
            {
                return new List<LinkedIdentity>();
            }

            List<LinkedIdentity> latest = new();
            for (var attempt = 0; attempt < 8; attempt++)
            {
                latest = await LoadLinkedIdentitiesAsync();
                if (latest.Any(identity => string.Equals(identity.Provider, "google", StringComparison.OrdinalIgnoreCase)))
                {
                    return latest;
                }

                await Task.Delay(350);
            }

            return latest;
        }

        private async Task CompleteGoogleLinkSignInAsync(Uri callbackUri)
        {
            var query = QueryHelpers.Parse(callbackUri);
            if (query.TryGetValue("error", out var error))
            {
                if (query.TryGetValue("error_description", out var description) && !string.IsNullOrWhiteSpace(description))
                {
                    throw new InvalidOperationException(description);
                }

                throw new InvalidOperationException(error);
            }

            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException(_isRussian ? "Код авторизации Google не получен." : "Google authorization code is missing.");
            }

            var verifier = PkceStore.Load();
            if (string.IsNullOrWhiteSpace(verifier))
            {
                throw new InvalidOperationException(_isRussian ? "Нет PKCE verifier." : "Missing PKCE verifier.");
            }

            _session = await _authClient.ExchangeCodeForSessionAsync(code, verifier);
            PersistSession();
            PkceStore.Clear();
        }

        private async Task<List<LinkedIdentity>> LoadLinkedIdentitiesAsync()
        {
            if (_settingsClient == null)
            {
                return new List<LinkedIdentity>();
            }

            Exception? lastError = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var accessToken = await GetFreshAccessTokenAsync();
                    return await _settingsClient.GetLinkedIdentitiesAsync(accessToken);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 4)
                    {
                        await Task.Delay(250);
                    }
                }
            }

            throw lastError ?? new InvalidOperationException(_isRussian ? "Не удалось загрузить связанные identity." : "Failed to load linked identities.");
        }

        private List<LinkedIdentity> BuildLinkedIdentityFallbacksFromSession()
        {
            var providers = ExtractSessionProviders();
            return providers
                .Select(provider => new LinkedIdentity
                {
                    Provider = provider
                })
                .ToList();
        }

        private void BringWindowToFront()
        {
            try
            {
                Activate();
                var hWnd = WindowNative.GetWindowHandle(this);
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                if (AppWindow?.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
                {
                    ShowWindow(hWnd, SwRestore);
                }
                else
                {
                    ShowWindow(hWnd, SwShow);
                }
                SetForegroundWindow(hWnd);
            }
            catch
            {
                // ignore focus errors
            }
        }

        private async Task ConfirmDeleteAccountAsync()
        {
            if (_settingsClient == null || _session == null)
            {
                return;
            }

            var input = new TextBox
            {
                PlaceholderText = _isRussian ? "Введите: удалить" : "Type: delete"
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Подтвердите удаление аккаунта" : "Confirm account deletion",
                PrimaryButtonText = _isRussian ? "Удалить аккаунт" : "Delete account",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Close,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = _isRussian
                                ? "Чтобы подтвердить удаление, введите слово «удалить». После этого аккаунт будет удалён без возможности восстановления."
                                : "Type “delete” to confirm. The account will be removed permanently without recovery.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        input
                    }
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var expected = _isRussian ? "удалить" : "delete";
            if (!string.Equals(input.Text?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                SetSettingsBanner(
                    _isRussian ? "Введите слово «удалить» для подтверждения." : "Type “delete” to confirm.",
                    true);
                return;
            }

            try
            {
                _settingsBusyAction = "delete";
                RenderSettingsContent();
                await _settingsClient.DeleteAccountAsync(_session.AccessToken);
                _sessionStore.Clear();
                _sessionStore.SetRememberDevice(RememberCheck.IsChecked == true);
                _session = null;
                _settingsSnapshot = null;
                _settingsBanner = null;
                _mode = AuthMode.Login;
                ApplyMode();
                UpdateShellState();
                SetStatus(_isRussian ? "Аккаунт удалён." : "Account deleted.", false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(LocalizeAuthError(ex.Message, _isRussian ? "Не удалось удалить аккаунт." : "Failed to delete account."), true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private async Task ConfirmFinanceResetAsync()
        {
            if (_financeClient == null || _session == null)
            {
                return;
            }

            var input = new TextBox
            {
                PlaceholderText = _isRussian ? "Введите: сбросить" : "Type: reset"
            };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = _isRussian ? "Подтвердите сброс финансов" : "Confirm finance reset",
                PrimaryButtonText = _isRussian ? "Сбросить финансы" : "Reset finance",
                CloseButtonText = _isRussian ? "Отмена" : "Cancel",
                DefaultButton = ContentDialogButton.Close,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = _isRussian
                                ? "Чтобы подтвердить сброс, введите слово «сбросить». Все счета, транзакции, категории и настройки обзора будут удалены. После этого финансы начнутся с чистого листа."
                                : "Type “reset” to confirm. All accounts, transactions, categories, and overview settings will be deleted. Finance will start again from a clean state.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        input
                    }
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var expected = _isRussian ? "сбросить" : "reset";
            if (!string.Equals(input.Text?.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                SetSettingsBanner(
                    _isRussian ? "Введите слово «сбросить» для подтверждения." : "Type “reset” to confirm.",
                    true);
                return;
            }

            try
            {
                _settingsBusyAction = "finance_reset";
                RenderSettingsContent();
                await _financeClient.ResetAllAsync(_session.AccessToken);
                ResetFinanceState();
                SetSettingsBanner(
                    _isRussian ? "Финансы сброшены. Раздел можно заполнить заново." : "Finance reset complete. You can set up the workspace again.",
                    false);
            }
            catch (Exception ex)
            {
                SetSettingsBanner(
                    LocalizeAuthError(ex.Message, _isRussian ? "Не удалось сбросить финансы." : "Failed to reset finance."),
                    true);
            }
            finally
            {
                _settingsBusyAction = null;
                RenderSettingsContent();
            }
        }

        private void ResetFinanceState()
        {
            _financeOnboardingStep = 0;
            _financeOverview = null;
            _financeTransactionsMonth = null;
            _financeCategories.Clear();
            _financeSelectedTransactionsMonth = null;
            _financeTransactionsMonthLoading = false;
            _financeMonthCache.Clear();
            _financeAnalytics = null;
            _financeAnalyticsFromMonth = null;
            _financeAnalyticsToMonth = null;
            _financeAnalyticsPreset = "current";
            _financeAnalyticsLoading = false;
            FinanceErrorCard.Visibility = Visibility.Collapsed;
            FinanceLoadingCard.Visibility = Visibility.Collapsed;
            FinanceCurrencyCombo.SelectedIndex = -1;
            FinanceBankCombo.SelectedIndex = -1;
            FinanceOnboardingCardTypeCombo.SelectedIndex = -1;
            FinanceOnboardingLastFourInput.Text = string.Empty;
            FinancePrimaryBalanceInput.Text = string.Empty;
            FinanceCashInput.Text = string.Empty;

            if (_section == DashboardSection.Finance)
            {
                _financeTab = FinanceTab.Overview;
                _activeSubsection = "overview";
                ApplySecondaryTabs();
                ApplyFinanceTabButtons();
                RenderFinanceContent();
            }
        }
}
}

