package com.assistant.app.auth

enum class AuthMode {
    Login,
    Register,
    ForgotPassword,
    ResetPassword
}

data class AuthUiState(
    val mode: AuthMode = AuthMode.Login,
    val email: String = "",
    val password: String = "",
    val passwordConfirm: String = "",
    val rememberDevice: Boolean = false,
    val isLoading: Boolean = false,
    val errorMessage: String? = null,
    val successMessage: String? = null,
    val userEmail: String? = null
)
