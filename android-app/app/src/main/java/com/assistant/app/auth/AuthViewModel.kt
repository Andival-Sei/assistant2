package com.assistant.app.auth

import android.app.Application
import android.content.Intent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import io.github.jan.supabase.auth.auth
import io.github.jan.supabase.auth.handleDeeplinks
import io.github.jan.supabase.auth.providers.Google
import io.github.jan.supabase.auth.providers.builtin.Email
import io.github.jan.supabase.auth.status.SessionStatus
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch

class AuthViewModel(application: Application) : AndroidViewModel(application) {
    private val supabase = SupabaseProvider.client
    private var isRussian = true

    var uiState by mutableStateOf(AuthUiState())
        private set

    init {
        if (!SupabaseProvider.isConfigured) {
            uiState = uiState.copy(errorMessage = "Supabase не настроен.")
        }
        val rememberDevice = AuthPreferences.loadRememberDevice(application)
        uiState = uiState.copy(rememberDevice = rememberDevice)

        viewModelScope.launch {
            runCatching {
                supabase.auth.loadFromStorage(autoRefresh = true)
            }

            supabase.auth.sessionStatus.collectLatest { status ->
                when (status) {
                    is SessionStatus.Authenticated -> {
                        uiState = uiState.copy(
                            userEmail = status.session.user?.email,
                            isLoading = false,
                            errorMessage = null
                        )
                    }

                    is SessionStatus.NotAuthenticated -> {
                        uiState = uiState.copy(
                            userEmail = null,
                            isLoading = false
                        )
                    }

                    is SessionStatus.RefreshFailure -> {
                        uiState = uiState.copy(isLoading = false)
                    }

                    SessionStatus.Initializing -> {
                        uiState = uiState.copy(isLoading = true)
                    }
                }
            }
        }
    }

    fun setMode(mode: AuthMode) {
        uiState = uiState.copy(
            mode = mode,
            password = "",
            passwordConfirm = "",
            errorMessage = null,
            successMessage = null
        )
    }

    fun updateLanguage(value: Boolean) {
        isRussian = value
    }

    fun updateEmail(value: String) {
        uiState = uiState.copy(email = value, errorMessage = null)
    }

    fun updatePassword(value: String) {
        uiState = uiState.copy(password = value, errorMessage = null)
    }

    fun updatePasswordConfirm(value: String) {
        uiState = uiState.copy(passwordConfirm = value, errorMessage = null)
    }

    fun updateRememberDevice(value: Boolean) {
        AuthPreferences.saveRememberDevice(getApplication(), value)
        uiState = uiState.copy(rememberDevice = value, errorMessage = null)
    }

    fun refreshUser() {
        val user = supabase.auth.currentUserOrNull()
        uiState = uiState.copy(userEmail = user?.email, isLoading = false)
    }

    fun clearEphemeralSessionIfNeeded() {
        if (uiState.rememberDevice) return
        if (supabase.auth.currentUserOrNull() == null) return

        viewModelScope.launch {
            runCatching {
                supabase.auth.signOut()
            }
            uiState = uiState.copy(userEmail = null)
        }
    }

    fun handleDeepLink(intent: Intent?) {
        if (intent == null) return
        val isRecoveryFlow = intent.dataString?.contains("type=recovery") == true
        viewModelScope.launch {
            runCatching {
                supabase.handleDeeplinks(intent)
            }.onSuccess {
                uiState = uiState.copy(
                    mode = if (isRecoveryFlow) AuthMode.ResetPassword else uiState.mode,
                    successMessage = if (isRecoveryFlow) {
                        if (isRussian) "Ссылка подтверждена. Задайте новый пароль." else "Recovery link confirmed. Set a new password."
                    } else {
                        if (isRussian) "Вход завершён. Можно продолжить." else "Sign-in complete. You can continue."
                    },
                    errorMessage = null,
                    isLoading = false,
                    password = "",
                    passwordConfirm = ""
                )
            }.onFailure { err ->
                uiState = uiState.copy(
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Не удалось обработать ссылку.",
                        fallbackEn = "Failed to process the link."
                    )
                )
            }
        }
    }

    fun signIn() {
        if (uiState.email.isBlank() || uiState.password.isBlank()) {
            uiState = uiState.copy(errorMessage = "Введите email и пароль.")
            return
        }
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, errorMessage = null, successMessage = null)
            runCatching {
                supabase.auth.signInWith(Email) {
                    email = uiState.email.trim()
                    password = uiState.password
                }
            }.onSuccess {
                refreshUser()
                uiState = uiState.copy(
                    isLoading = false,
                    successMessage = "Вы вошли в аккаунт."
                )
            }.onFailure { err ->
                uiState = uiState.copy(
                    isLoading = false,
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Ошибка входа.",
                        fallbackEn = "Sign-in failed."
                    )
                )
            }
        }
    }

    fun signUp() {
        if (uiState.email.isBlank() || uiState.password.isBlank()) {
            uiState = uiState.copy(errorMessage = "Введите email и пароль.")
            return
        }
        if (uiState.password != uiState.passwordConfirm) {
            uiState = uiState.copy(errorMessage = "Пароли не совпадают.")
            return
        }
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, errorMessage = null, successMessage = null)
            runCatching {
                supabase.auth.signUpWith(Email, redirectUrl = SupabaseProvider.redirectUrl) {
                    email = uiState.email.trim()
                    password = uiState.password
                }
            }.onSuccess {
                refreshUser()
                uiState = uiState.copy(
                    isLoading = false,
                    successMessage = "Проверьте почту для подтверждения."
                )
            }.onFailure { err ->
                uiState = uiState.copy(
                    isLoading = false,
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Ошибка регистрации.",
                        fallbackEn = "Sign-up failed."
                    )
                )
            }
        }
    }

    fun resetPassword() {
        if (uiState.email.isBlank()) {
            uiState = uiState.copy(errorMessage = "Введите email.")
            return
        }
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, errorMessage = null, successMessage = null)
            runCatching {
                supabase.auth.resetPasswordForEmail(
                    email = uiState.email.trim(),
                    redirectUrl = SupabaseProvider.redirectUrl
                )
            }.onSuccess {
                uiState = uiState.copy(
                    isLoading = false,
                    successMessage = "Ссылка отправлена. Проверьте почту."
                )
            }.onFailure { err ->
                uiState = uiState.copy(
                    isLoading = false,
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Не удалось отправить письмо.",
                        fallbackEn = "Failed to send the email."
                    )
                )
            }
        }
    }

    fun updatePassword() {
        if (uiState.password.isBlank() || uiState.passwordConfirm.isBlank()) {
            uiState = uiState.copy(errorMessage = "Введите новый пароль.")
            return
        }
        if (uiState.password != uiState.passwordConfirm) {
            uiState = uiState.copy(errorMessage = "Пароли не совпадают.")
            return
        }
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, errorMessage = null, successMessage = null)
            runCatching {
                supabase.auth.updateUser {
                    password = uiState.password
                }
            }.onSuccess {
                uiState = uiState.copy(
                    isLoading = false,
                    successMessage = "Пароль обновлён."
                )
            }.onFailure { err ->
                uiState = uiState.copy(
                    isLoading = false,
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Не удалось обновить пароль.",
                        fallbackEn = "Failed to update password."
                    )
                )
            }
        }
    }

    fun signInWithGoogle() {
        if (!SupabaseProvider.isGoogleAuthEnabled) {
            uiState = uiState.copy(
                errorMessage = "Google OAuth пока не настроен в проекте Supabase."
            )
            return
        }
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, errorMessage = null, successMessage = null)
            runCatching {
                supabase.auth.signInWith(Google, redirectUrl = SupabaseProvider.redirectUrl) {
                    queryParams["prompt"] = "select_account"
                }
            }.onSuccess {
                uiState = uiState.copy(isLoading = false)
            }.onFailure { err ->
                uiState = uiState.copy(
                    isLoading = false,
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Ошибка Google OAuth.",
                        fallbackEn = "Google OAuth failed."
                    )
                )
            }
        }
    }

    fun signOut() {
        viewModelScope.launch {
            uiState = uiState.copy(isLoading = true, errorMessage = null, successMessage = null)
            runCatching {
                supabase.auth.signOut()
            }.onSuccess {
                uiState = uiState.copy(
                    isLoading = false,
                    userEmail = null,
                    successMessage = "Вы вышли из аккаунта."
                )
            }.onFailure { err ->
                uiState = uiState.copy(
                    isLoading = false,
                    errorMessage = localizeAuthError(
                        raw = err.message,
                        isRussian = isRussian,
                        fallbackRu = "Ошибка выхода.",
                        fallbackEn = "Sign-out failed."
                    )
                )
            }
        }
    }
}

private fun localizeAuthError(
    raw: String?,
    isRussian: Boolean,
    fallbackRu: String,
    fallbackEn: String
): String {
    val message = raw?.trim().orEmpty()
    if (message.isBlank()) return if (isRussian) fallbackRu else fallbackEn

    val normalized = message.lowercase()
    return when {
        "invalid login credentials" in normalized ->
            if (isRussian) "Неверный email или пароль." else "Incorrect email or password."
        "email not confirmed" in normalized ->
            if (isRussian) "Подтвердите email и затем войдите." else "Confirm your email first, then sign in."
        "user already registered" in normalized || "already been registered" in normalized ->
            if (isRussian) "Этот email уже занят." else "This email is already in use."
        "over_email_send_rate_limit" in normalized || "security purposes" in normalized || "rate limit" in normalized ->
            if (isRussian) "Слишком много попыток. Подождите немного и попробуйте снова." else "Too many attempts. Wait a bit and try again."
        "signup is disabled" in normalized ->
            if (isRussian) "Регистрация сейчас недоступна." else "Sign-up is currently unavailable."
        "network request failed" in normalized ->
            if (isRussian) "Проблема сети. Проверьте подключение и попробуйте снова." else "Network issue. Check your connection and try again."
        "password should be at least" in normalized ->
            if (isRussian) "Минимум 8 символов." else "Use at least 8 characters."
        else -> if (isRussian) fallbackRu else fallbackEn
    }
}
