package com.assistant.app

import android.graphics.Bitmap
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.animation.togetherWith
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.wrapContentWidth
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import com.assistant.app.auth.AuthMode
import com.assistant.app.auth.AuthUiState
import com.assistant.app.auth.AuthViewModel
import com.assistant.app.auth.SupabaseProvider
import com.assistant.app.finance.FinanceAccount
import com.assistant.app.finance.FinanceCategory
import com.assistant.app.finance.FinanceOverview
import com.assistant.app.finance.FinanceRepository
import com.assistant.app.finance.FinanceTransactionsMonth
import com.assistant.app.settings.LinkedIdentity
import com.assistant.app.settings.SettingsRepository
import com.assistant.app.settings.SettingsSnapshot
import com.assistant.app.ui.theme.AssistantAndroidTheme
import com.assistant.app.ui.theme.BlobCool
import com.assistant.app.ui.theme.BlobWarm
import com.assistant.app.ui.theme.DarkInput
import com.assistant.app.ui.theme.LightInput
import java.text.NumberFormat
import java.io.ByteArrayOutputStream
import java.util.Currency
import java.util.Locale
import kotlin.math.roundToInt
import kotlinx.coroutines.launch
import kotlinx.coroutines.delay

class MainActivity : ComponentActivity() {
    private val authViewModel: AuthViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            var themeMode by rememberSaveable { mutableStateOf(ThemeMode.System) }
            var isRussian by rememberSaveable { mutableStateOf(true) }

            val isDark = when (themeMode) {
                ThemeMode.System -> isSystemInDarkTheme()
                ThemeMode.Light -> false
                ThemeMode.Dark -> true
            }

            AssistantAndroidTheme(darkTheme = isDark) {
                val viewModel = authViewModel
                val state = viewModel.uiState

                LaunchedEffect(Unit) {
                    viewModel.handleDeepLink(intent)
                    viewModel.clearEphemeralSessionIfNeeded()
                    viewModel.refreshUser()
                }

                LaunchedEffect(isRussian) {
                    viewModel.updateLanguage(isRussian)
                }

                AppScene(
                    isDark = isDark,
                    isRussian = isRussian,
                    themeMode = themeMode,
                    onThemeChange = { themeMode = it },
                    onLanguageChange = { isRussian = it },
                    state = state,
                    onEmailChange = viewModel::updateEmail,
                    onPasswordChange = viewModel::updatePassword,
                    onPasswordConfirmChange = viewModel::updatePasswordConfirm,
                    onModeChange = viewModel::setMode,
                    onSignIn = viewModel::signIn,
                    onSignUp = viewModel::signUp,
                    onResetPassword = viewModel::resetPassword,
                    onUpdatePassword = viewModel::updatePassword,
                    onGoogle = viewModel::signInWithGoogle,
                    onSignOut = viewModel::signOut,
                    onRememberDeviceChange = viewModel::updateRememberDevice,
                    isGoogleAuthEnabled = SupabaseProvider.isGoogleAuthEnabled,
                    authState = state
                )
            }
        }
    }

    override fun onNewIntent(intent: android.content.Intent?) {
        super.onNewIntent(intent)
        setIntent(intent)
        authViewModel.handleDeepLink(intent)
    }
}

private enum class ThemeMode {
    System, Light, Dark
}

private data class MetricItem(
    val label: String,
    val value: String,
    val detail: String
)

private data class ActivityItem(
    val title: String,
    val time: String,
    val detail: String
)

private data class FocusItem(
    val title: String,
    val detail: String
)

private enum class DashboardSection {
    Home,
    Finance,
    Health,
    Tasks,
    Settings
}

private enum class FinanceTab {
    Overview,
    Accounts,
    Transactions,
    Categories,
    Analytics
}

private enum class DashboardSubsection {
    Summary,
    Today,
    Insights,
    Overview,
    Accounts,
    Transactions,
    Categories,
    Analytics,
    Settings,
    Habits,
    Metrics,
    Records,
    Focus,
    Board,
    Archive,
    Profile,
    Preferences,
    Security
}

private data class DashboardNavItem(
    val id: DashboardSubsection,
    val label: String
)

private data class DashboardSectionText(
    val label: String,
    val title: String,
    val eyebrow: String,
    val badge: String,
    val note: String,
    val icon: String,
    val defaultSubsection: DashboardSubsection,
    val subsections: List<DashboardNavItem>
)

private data class FinanceOnboardingState(
    val currency: String? = null,
    val bank: String? = null,
    val primaryBalance: String = "",
    val cash: String = ""
)

private data class UiText(
    val themeSystem: String,
    val themeLight: String,
    val themeDark: String,
    val langSwitch: String,
    val pill: String,
    val title: String,
    val subtitle: String,
    val hint: String,
    val email: String,
    val emailPlaceholder: String,
    val password: String,
    val passwordPlaceholder: String,
    val passwordConfirm: String,
    val passwordConfirmPlaceholder: String,
    val remember: String,
    val forgot: String,
    val cta: String,
    val note: String,
    val loginTab: String,
    val registerTab: String,
    val google: String,
    val sendReset: String,
    val backToLogin: String,
    val updatePassword: String,
    val logout: String,
    val dashboardLabel: String,
    val dashboardTitle: String,
    val dashboardSubtitle: String,
    val liveBadge: String,
    val workspaceLabel: String,
    val workspaceTitle: String,
    val workspaceCopy: String,
    val userLabel: String,
    val userDetail: String,
    val activityLabel: String,
    val activityTitle: String,
    val activityHint: String,
    val focusLabel: String,
    val focusTitle: String,
    val insightLabel: String,
    val insightTitle: String,
    val insightLead: String,
    val insightBody: String,
    val stateLabel: String,
    val stateTitle: String
)

private val RuText = UiText(
    themeSystem = "Тема: Система",
    themeLight = "Тема: Светлая",
    themeDark = "Тема: Тёмная",
    langSwitch = "English",
    pill = "Вход в экосистему",
    title = "Добро пожаловать обратно.",
    subtitle = "Войдите, чтобы открыть ваши модули, задачи и приватные настройки.",
    hint = "Полная авторизация уже работает: Email/Password + восстановление пароля.",
    email = "EMAIL",
    emailPlaceholder = "you@example.com",
    password = "ПАРОЛЬ",
    passwordPlaceholder = "••••••••",
    passwordConfirm = "ПОВТОРИТЕ ПАРОЛЬ",
    passwordConfirmPlaceholder = "••••••••",
    remember = "Запомнить устройство",
    forgot = "Забыли пароль?",
    cta = "Войти",
    note = "Данные шифруются и хранятся безопасно.",
    loginTab = "Вход",
    registerTab = "Регистрация",
    google = "Продолжить с Google",
    sendReset = "Отправить ссылку",
    backToLogin = "Вернуться к входу",
    updatePassword = "Обновить пароль",
    logout = "Выйти",
    dashboardLabel = "Overview",
    dashboardTitle = "Первый единый dashboard после входа.",
    dashboardSubtitle = "Это первый единый dashboard после авторизации. Здесь держим фокус на статусе проекта, ближайших задачах и общем состоянии модулей.",
    liveBadge = "Live",
    workspaceLabel = "Workspace",
    workspaceTitle = "Assistant",
    workspaceCopy = "Общий центр управления после авторизации.",
    userLabel = "Пользователь",
    userDetail = "Сессия активна и защищена через Supabase Auth.",
    activityLabel = "Recent activity",
    activityTitle = "Что происходит сейчас",
    activityHint = "Последние 24 часа",
    focusLabel = "Today focus",
    focusTitle = "Приоритеты",
    insightLabel = "Insight",
    insightTitle = "Куда двигаться дальше",
    insightLead = "Сначала фиксируем shell, потом наполняем модули.",
    insightBody = "Сильный overview и единый язык важнее, чем три разрозненных экрана на старте.",
    stateLabel = "System state",
    stateTitle = "Текущее состояние"
)

private val EnText = UiText(
    themeSystem = "Theme: System",
    themeLight = "Theme: Light",
    themeDark = "Theme: Dark",
    langSwitch = "Русский",
    pill = "Access the ecosystem",
    title = "Welcome back.",
    subtitle = "Sign in to reach your modules, tasks, and private settings.",
    hint = "Email/password and recovery are already wired.",
    email = "EMAIL",
    emailPlaceholder = "you@example.com",
    password = "PASSWORD",
    passwordPlaceholder = "••••••••",
    passwordConfirm = "CONFIRM PASSWORD",
    passwordConfirmPlaceholder = "••••••••",
    remember = "Remember this device",
    forgot = "Forgot password?",
    cta = "Sign in",
    note = "We store your data securely.",
    loginTab = "Sign in",
    registerTab = "Register",
    google = "Continue with Google",
    sendReset = "Send reset link",
    backToLogin = "Back to sign in",
    updatePassword = "Update password",
    logout = "Sign out",
    dashboardLabel = "Overview",
    dashboardTitle = "First shared dashboard after sign-in.",
    dashboardSubtitle = "This is the first shared dashboard after sign-in. It keeps the project status, next tasks, and the overall module state in one place.",
    liveBadge = "Live",
    workspaceLabel = "Workspace",
    workspaceTitle = "Assistant",
    workspaceCopy = "Shared control center after sign-in.",
    userLabel = "User",
    userDetail = "Session is active and protected with Supabase Auth.",
    activityLabel = "Recent activity",
    activityTitle = "What is happening now",
    activityHint = "Last 24 hours",
    focusLabel = "Today focus",
    focusTitle = "Priorities",
    insightLabel = "Insight",
    insightTitle = "Where to go next",
    insightLead = "Lock the shell first, then fill the modules.",
    insightBody = "A strong overview and one visual language are more useful right now than three disconnected screens.",
    stateLabel = "System state",
    stateTitle = "Current state"
)

@Composable
private fun AppScene(
    isDark: Boolean,
    isRussian: Boolean,
    themeMode: ThemeMode,
    onThemeChange: (ThemeMode) -> Unit,
    onLanguageChange: (Boolean) -> Unit,
    state: AuthUiState,
    onEmailChange: (String) -> Unit,
    onPasswordChange: (String) -> Unit,
    onPasswordConfirmChange: (String) -> Unit,
    onModeChange: (AuthMode) -> Unit,
    onSignIn: () -> Unit,
    onSignUp: () -> Unit,
    onResetPassword: () -> Unit,
    onUpdatePassword: () -> Unit,
    onGoogle: () -> Unit,
    onSignOut: () -> Unit,
    onRememberDeviceChange: (Boolean) -> Unit,
    isGoogleAuthEnabled: Boolean,
    authState: AuthUiState
) {
    val t = if (isRussian) RuText else EnText
    val userEmail = state.userEmail
    val isSignedIn = !userEmail.isNullOrBlank()

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        DashboardBackdrop()

        if (isSignedIn) {
            DashboardScreen(
                isRussian = isRussian,
                themeMode = themeMode,
                onThemeChange = onThemeChange,
                onLanguageChange = onLanguageChange,
                onSignOut = onSignOut,
                t = t,
                userEmail = userEmail.orEmpty(),
                authState = authState
            )
        } else {
            AuthScreen(
                isDark = isDark,
                isRussian = isRussian,
                themeMode = themeMode,
                onThemeChange = onThemeChange,
                onLanguageChange = onLanguageChange,
                state = state,
                onEmailChange = onEmailChange,
                onPasswordChange = onPasswordChange,
                onPasswordConfirmChange = onPasswordConfirmChange,
                onModeChange = onModeChange,
                onSignIn = onSignIn,
                onSignUp = onSignUp,
                onResetPassword = onResetPassword,
                onUpdatePassword = onUpdatePassword,
                onGoogle = onGoogle,
                onRememberDeviceChange = onRememberDeviceChange,
                isGoogleAuthEnabled = isGoogleAuthEnabled,
                t = t
            )
        }
    }
}

@Composable
private fun DashboardBackdrop() {
    Canvas(modifier = Modifier.fillMaxSize()) {
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(BlobWarm.copy(alpha = 0.28f), Color.Transparent)
            ),
            radius = size.minDimension * 0.7f,
            center = Offset(0f, 0f)
        )
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(BlobCool.copy(alpha = 0.24f), Color.Transparent)
            ),
            radius = size.minDimension * 0.72f,
            center = Offset(size.width, size.height)
        )
    }
}

@Composable
private fun DashboardScreen(
    isRussian: Boolean,
    themeMode: ThemeMode,
    onThemeChange: (ThemeMode) -> Unit,
    onLanguageChange: (Boolean) -> Unit,
    onSignOut: () -> Unit,
    t: UiText,
    userEmail: String,
    authState: AuthUiState
) {
    var section by rememberSaveable { mutableStateOf(DashboardSection.Home) }
    var subsection by rememberSaveable { mutableStateOf(DashboardSubsection.Summary) }
    var financeTab by rememberSaveable { mutableStateOf(FinanceTab.Overview) }
    var financeOverview by remember { mutableStateOf<FinanceOverview?>(null) }
    var financeLoading by remember { mutableStateOf(false) }
    var financeError by remember { mutableStateOf<String?>(null) }
    var financeOnboardingStep by rememberSaveable { mutableStateOf(0) }
    var financeOnboarding by remember { mutableStateOf(FinanceOnboardingState()) }
    var settingsSnapshot by remember { mutableStateOf<SettingsSnapshot?>(null) }
    var settingsLoading by remember { mutableStateOf(false) }
    var settingsBanner by remember { mutableStateOf<Pair<Boolean, String>?>(null) }
    val sectionCopy = dashboardSectionCopy(isRussian)
    val activeSection = sectionCopy[section]!!
    val activeSubsection = activeSection.subsections.firstOrNull { it.id == subsection } ?: activeSection.subsections.first()
    val userName = rememberUserName(userEmail)
    val financeRepository = remember { FinanceRepository() }
    val settingsRepository = remember { SettingsRepository() }
    val coroutineScope = rememberCoroutineScope()
    val setSectionState: (DashboardSection) -> Unit = { next ->
        val nextSubsection = sectionCopy[next]!!.defaultSubsection
        section = next
        subsection = nextSubsection
        if (next == DashboardSection.Finance) {
            financeTab = when (nextSubsection) {
                DashboardSubsection.Accounts -> FinanceTab.Accounts
                DashboardSubsection.Transactions -> FinanceTab.Transactions
                DashboardSubsection.Categories -> FinanceTab.Categories
                DashboardSubsection.Analytics -> FinanceTab.Analytics
                else -> FinanceTab.Overview
            }
        }
    }
    val setFinanceTabState: (FinanceTab) -> Unit = { next ->
        financeTab = next
        subsection = when (next) {
            FinanceTab.Overview -> DashboardSubsection.Overview
            FinanceTab.Accounts -> DashboardSubsection.Accounts
            FinanceTab.Transactions -> DashboardSubsection.Transactions
            FinanceTab.Categories -> DashboardSubsection.Categories
            FinanceTab.Analytics -> DashboardSubsection.Analytics
        }
    }
    val completeFinanceOnboarding: (Boolean) -> Unit = { skip ->
        coroutineScope.launch {
            financeLoading = true
            financeError = null
            runCatching {
                financeRepository.completeOnboarding(
                    currency = if (skip) null else financeOnboarding.currency,
                    bank = if (skip) null else financeOnboarding.bank,
                    cashMinor = if (skip) null else parseAmountToMinor(financeOnboarding.cash),
                    primaryAccountBalanceMinor = if (skip) null else parseAmountToMinor(financeOnboarding.primaryBalance)
                )
            }.onSuccess {
                financeOverview = it
                financeOnboardingStep = 0
            }.onFailure { err ->
                financeError = err.message ?: if (isRussian) "Не удалось завершить онбординг." else "Failed to finish onboarding."
            }
            financeLoading = false
        }
    }

    LaunchedEffect(section, userEmail) {
        if (section != DashboardSection.Finance || userEmail.isBlank()) return@LaunchedEffect
        financeLoading = true
        financeError = null
        runCatching {
            financeRepository.getOverview()
        }.onSuccess {
            financeOverview = it
        }.onFailure { err ->
            financeError = err.message ?: if (isRussian) "Не удалось загрузить финансы." else "Failed to load finance."
        }
        financeLoading = false
    }

    LaunchedEffect(section, userEmail, authState.successMessage) {
        if (section != DashboardSection.Settings || userEmail.isBlank()) return@LaunchedEffect
        settingsLoading = true
        runCatching {
            settingsRepository.load()
        }.onSuccess {
            settingsSnapshot = it
        }.onFailure { err ->
            settingsBanner = false to (err.message ?: if (isRussian) {
                "Не удалось загрузить настройки."
            } else {
                "Failed to load settings."
            })
        }
        settingsLoading = false
    }

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxSize()
            .statusBarsPadding()
            .padding(horizontal = 18.dp, vertical = 20.dp)
    ) {
        val compact = maxWidth < 760.dp
        val scrollState = rememberScrollState()

        if (compact) {
            Box(
                modifier = Modifier.fillMaxSize()
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(scrollState)
                        .padding(bottom = 92.dp),
                    verticalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    DashboardTopBar(
                        isRussian = isRussian,
                        themeMode = themeMode,
                        onThemeChange = onThemeChange,
                        onLanguageChange = onLanguageChange
                    )
                    if (section != DashboardSection.Finance) {
                        SecondaryTabRow(
                            items = activeSection.subsections,
                            active = activeSubsection.id,
                            onSelect = { item ->
                                subsection = item
                            }
                        )
                    }
                    if (section == DashboardSection.Finance) {
                        FinanceSectionCard(
                            isRussian = isRussian,
                            compact = true,
                            financeRepository = financeRepository,
                            overview = financeOverview,
                            activeTab = financeTab,
                            loading = financeLoading,
                            error = financeError,
                            onboardingStep = financeOnboardingStep,
                            onboarding = financeOnboarding,
                            onTabChange = setFinanceTabState,
                            onOnboardingStepChange = { financeOnboardingStep = it },
                            onOnboardingChange = { financeOnboarding = it },
                            onOverviewChange = { financeOverview = it },
                            onCompleteOnboarding = completeFinanceOnboarding
                        )
                    } else if (section == DashboardSection.Settings) {
                        SettingsSectionCard(
                            isRussian = isRussian,
                            subsection = activeSubsection.id,
                            snapshot = settingsSnapshot,
                            loading = settingsLoading,
                            banner = settingsBanner,
                            onBannerChange = { settingsBanner = it },
                            onReload = {
                                coroutineScope.launch {
                                    settingsLoading = true
                                    runCatching { settingsRepository.load() }
                                        .onSuccess { settingsSnapshot = it }
                                        .onFailure { err ->
                                            settingsBanner = false to (err.message ?: if (isRussian) "Не удалось загрузить настройки." else "Failed to load settings.")
                                        }
                                    settingsLoading = false
                                }
                            },
                            onSaveDisplayName = { value ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.saveDisplayName(value)
                                        settingsRepository.load()
                                    }.onSuccess {
                                        settingsSnapshot = it
                                        settingsBanner = true to if (isRussian) "Имя сохранено." else "Name saved."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось сохранить имя." else "Failed to save name.")
                                    }
                                }
                            },
                            onSaveEmail = { value ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.changeEmail(value)
                                    }.onSuccess {
                                        settingsBanner = true to if (isRussian) "Запрос на смену почты отправлен." else "Email change request sent."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось сменить почту." else "Failed to change email.")
                                    }
                                }
                            },
                            onSaveGemini = { value ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.saveGeminiApiKey(value)
                                        settingsRepository.load()
                                    }.onSuccess {
                                        settingsSnapshot = it
                                        settingsBanner = true to if (isRussian) "Gemini API Key сохранён." else "Gemini API key saved."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось сохранить ключ." else "Failed to save the key.")
                                    }
                                }
                            },
                            onSavePassword = { value ->
                                coroutineScope.launch {
                                    runCatching { settingsRepository.changePassword(value) }
                                        .onSuccess {
                                            settingsBanner = true to if (isRussian) "Пароль обновлён." else "Password updated."
                                        }
                                        .onFailure { err ->
                                            settingsBanner = false to (err.message ?: if (isRussian) "Не удалось обновить пароль." else "Failed to update password.")
                                        }
                                }
                            },
                            onLinkGoogle = {
                                coroutineScope.launch {
                                    runCatching { settingsRepository.linkGoogle() }
                                        .onFailure { err ->
                                            settingsBanner = false to (err.message ?: if (isRussian) "Не удалось привязать Google." else "Failed to link Google.")
                                        }
                                }
                            },
                            onUnlinkGoogle = { identityId ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.unlinkIdentity(identityId)
                                        settingsRepository.load()
                                    }.onSuccess {
                                        settingsSnapshot = it
                                        settingsBanner = true to if (isRussian) "Google аккаунт отвязан." else "Google account unlinked."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось отвязать Google." else "Failed to unlink Google.")
                                    }
                                }
                            },
                            onDeleteAccount = {
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.deleteAccount()
                                    }.onSuccess {
                                        onSignOut()
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось удалить аккаунт." else "Failed to delete account.")
                                    }
                                }
                            },
                            onSignOut = onSignOut
                        )
                    } else {
                        DashboardPlaceholderCard(
                            section = section,
                            copy = activeSection,
                            subsectionLabel = activeSubsection.label,
                            userName = userName,
                            compact = true,
                            userEmail = userEmail,
                            onSignOut = onSignOut,
                            logoutLabel = t.logout
                        )
                    }
                }
                Box(
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .fillMaxWidth()
                        .height(132.dp)
                        .background(
                            Brush.verticalGradient(
                                colors = listOf(
                                    Color.Transparent,
                                    MaterialTheme.colorScheme.background.copy(alpha = 0.72f),
                                    MaterialTheme.colorScheme.background
                                )
                            )
                        )
                )
                DashboardBottomBar(
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .padding(bottom = 8.dp)
                        .navigationBarsPadding(),
                    current = section,
                    labels = sectionCopy,
                    onSelect = setSectionState
                )
            }
        } else {
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(scrollState),
                horizontalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                DashboardSidebarCard(
                    t = t,
                    userEmail = userEmail,
                    onSignOut = onSignOut,
                    current = section,
                    labels = sectionCopy,
                    onSelect = setSectionState
                )

                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    DashboardTopBar(
                        isRussian = isRussian,
                        themeMode = themeMode,
                        onThemeChange = onThemeChange,
                        onLanguageChange = onLanguageChange
                    )
                    if (section != DashboardSection.Finance) {
                        SecondaryTabRow(
                            items = activeSection.subsections,
                            active = activeSubsection.id,
                            onSelect = { item ->
                                subsection = item
                            }
                        )
                    }
                    if (section == DashboardSection.Finance) {
                        FinanceSectionCard(
                            isRussian = isRussian,
                            compact = false,
                            financeRepository = financeRepository,
                            overview = financeOverview,
                            activeTab = financeTab,
                            loading = financeLoading,
                            error = financeError,
                            onboardingStep = financeOnboardingStep,
                            onboarding = financeOnboarding,
                            onTabChange = setFinanceTabState,
                            onOnboardingStepChange = { financeOnboardingStep = it },
                            onOnboardingChange = { financeOnboarding = it },
                            onOverviewChange = { financeOverview = it },
                            onCompleteOnboarding = completeFinanceOnboarding
                        )
                    } else if (section == DashboardSection.Settings) {
                        SettingsSectionCard(
                            isRussian = isRussian,
                            subsection = activeSubsection.id,
                            snapshot = settingsSnapshot,
                            loading = settingsLoading,
                            banner = settingsBanner,
                            onBannerChange = { settingsBanner = it },
                            onReload = {
                                coroutineScope.launch {
                                    settingsLoading = true
                                    runCatching { settingsRepository.load() }
                                        .onSuccess { settingsSnapshot = it }
                                        .onFailure { err ->
                                            settingsBanner = false to (err.message ?: if (isRussian) "Не удалось загрузить настройки." else "Failed to load settings.")
                                        }
                                    settingsLoading = false
                                }
                            },
                            onSaveDisplayName = { value ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.saveDisplayName(value)
                                        settingsRepository.load()
                                    }.onSuccess {
                                        settingsSnapshot = it
                                        settingsBanner = true to if (isRussian) "Имя сохранено." else "Name saved."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось сохранить имя." else "Failed to save name.")
                                    }
                                }
                            },
                            onSaveEmail = { value ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.changeEmail(value)
                                    }.onSuccess {
                                        settingsBanner = true to if (isRussian) "Запрос на смену почты отправлен." else "Email change request sent."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось сменить почту." else "Failed to change email.")
                                    }
                                }
                            },
                            onSaveGemini = { value ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.saveGeminiApiKey(value)
                                        settingsRepository.load()
                                    }.onSuccess {
                                        settingsSnapshot = it
                                        settingsBanner = true to if (isRussian) "Gemini API Key сохранён." else "Gemini API key saved."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось сохранить ключ." else "Failed to save the key.")
                                    }
                                }
                            },
                            onSavePassword = { value ->
                                coroutineScope.launch {
                                    runCatching { settingsRepository.changePassword(value) }
                                        .onSuccess {
                                            settingsBanner = true to if (isRussian) "Пароль обновлён." else "Password updated."
                                        }
                                        .onFailure { err ->
                                            settingsBanner = false to (err.message ?: if (isRussian) "Не удалось обновить пароль." else "Failed to update password.")
                                        }
                                }
                            },
                            onLinkGoogle = {
                                coroutineScope.launch {
                                    runCatching { settingsRepository.linkGoogle() }
                                        .onFailure { err ->
                                            settingsBanner = false to (err.message ?: if (isRussian) "Не удалось привязать Google." else "Failed to link Google.")
                                        }
                                }
                            },
                            onUnlinkGoogle = { identityId ->
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.unlinkIdentity(identityId)
                                        settingsRepository.load()
                                    }.onSuccess {
                                        settingsSnapshot = it
                                        settingsBanner = true to if (isRussian) "Google аккаунт отвязан." else "Google account unlinked."
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось отвязать Google." else "Failed to unlink Google.")
                                    }
                                }
                            },
                            onDeleteAccount = {
                                coroutineScope.launch {
                                    runCatching {
                                        settingsRepository.deleteAccount()
                                    }.onSuccess {
                                        onSignOut()
                                    }.onFailure { err ->
                                        settingsBanner = false to (err.message ?: if (isRussian) "Не удалось удалить аккаунт." else "Failed to delete account.")
                                    }
                                }
                            },
                            onSignOut = onSignOut
                        )
                    } else {
                        DashboardPlaceholderCard(
                            section = section,
                            copy = activeSection,
                            subsectionLabel = activeSubsection.label,
                            userName = userName,
                            compact = false,
                            userEmail = userEmail,
                            onSignOut = onSignOut,
                            logoutLabel = t.logout
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun DashboardTopBar(
    isRussian: Boolean,
    themeMode: ThemeMode,
    onThemeChange: (ThemeMode) -> Unit,
    onLanguageChange: (Boolean) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = "ASSISTANT",
            fontSize = 12.sp,
            letterSpacing = 2.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onBackground
        )

        Row(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            CompactThemeSelector(
                themeMode = themeMode,
                onThemeChange = onThemeChange
            )
            LanguageMenu(
                isRussian = isRussian,
                onLanguageChange = onLanguageChange
            )
        }
    }
}

@Composable
private fun SecondaryTabRow(
    items: List<DashboardNavItem>,
    active: DashboardSubsection,
    onSelect: (DashboardSubsection) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        items.forEach { item ->
            val isActive = item.id == active
            Surface(
                shape = RoundedCornerShape(14.dp),
                color = if (isActive) {
                    MaterialTheme.colorScheme.secondary.copy(alpha = 0.14f)
                } else {
                    MaterialTheme.colorScheme.surface.copy(alpha = 0.72f)
                },
                border = BorderStroke(
                    1.dp,
                    if (isActive) MaterialTheme.colorScheme.secondary.copy(alpha = 0.18f) else MaterialTheme.colorScheme.outline
                ),
                modifier = Modifier.clickable { onSelect(item.id) }
            ) {
                Text(
                    text = item.label,
                    modifier = Modifier.padding(horizontal = 14.dp, vertical = 10.dp),
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = if (isActive) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun DashboardSidebarCard(
    t: UiText,
    userEmail: String,
    onSignOut: () -> Unit,
    current: DashboardSection,
    labels: Map<DashboardSection, DashboardSectionText>,
    onSelect: (DashboardSection) -> Unit
) {
    GlassSurface(modifier = Modifier.width(248.dp)) {
        Column(verticalArrangement = Arrangement.spacedBy(18.dp)) {
            Surface(
                modifier = Modifier.size(48.dp),
                shape = RoundedCornerShape(18.dp),
                color = MaterialTheme.colorScheme.secondary
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(
                        text = "A",
                        fontSize = 20.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.background
                    )
                }
            }

            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text(text = t.workspaceLabel.uppercase(), fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Text(text = t.workspaceTitle, fontSize = 28.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                Text(text = t.workspaceCopy, fontSize = 13.sp, lineHeight = 20.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }

            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                DashboardSection.entries.forEach { item ->
                    DashboardSectionNavButton(
                        section = item,
                        text = labels[item]!!.label,
                        active = current == item,
                        onClick = { onSelect(item) }
                    )
                }
            }

            Surface(
                shape = RoundedCornerShape(18.dp),
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.65f),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
            ) {
                Column(
                    modifier = Modifier.padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    Text(text = t.userLabel.uppercase(), fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(text = userEmail, fontSize = 14.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                    Text(text = t.userDetail, fontSize = 12.sp, lineHeight = 18.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
private fun FinanceSectionCard(
    isRussian: Boolean,
    compact: Boolean,
    financeRepository: FinanceRepository,
    overview: FinanceOverview?,
    activeTab: FinanceTab,
    loading: Boolean,
    error: String?,
    onboardingStep: Int,
    onboarding: FinanceOnboardingState,
    onTabChange: (FinanceTab) -> Unit,
    onOnboardingStepChange: (Int) -> Unit,
    onOnboardingChange: (FinanceOnboardingState) -> Unit,
    onOverviewChange: (FinanceOverview) -> Unit,
    onCompleteOnboarding: (Boolean) -> Unit
) {
    val tabLabels = if (isRussian) {
        mapOf(
            FinanceTab.Overview to "Обзор",
            FinanceTab.Accounts to "Счета",
            FinanceTab.Transactions to "Транзакции",
            FinanceTab.Categories to "Категории",
            FinanceTab.Analytics to "Аналитика"
        )
    } else {
        mapOf(
            FinanceTab.Overview to "Overview",
            FinanceTab.Accounts to "Accounts",
            FinanceTab.Transactions to "Transactions",
            FinanceTab.Categories to "Categories",
            FinanceTab.Analytics to "Analytics"
        )
    }
    val currencies = if (isRussian) {
        listOf("RUB" to "Рубли", "USD" to "Доллары", "EUR" to "Евро")
    } else {
        listOf("RUB" to "Rubles", "USD" to "US Dollars", "EUR" to "Euro")
    }
    val banks = if (isRussian) {
        listOf("Т-Банк", "Сбер", "Альфа", "ВТБ")
    } else {
        listOf("T-Bank", "Sber", "Alfa", "VTB")
    }
    val coroutineScope = rememberCoroutineScope()
    var transactionsMonth by remember { mutableStateOf<FinanceTransactionsMonth?>(null) }
    var transactionsLoading by remember { mutableStateOf(false) }
    var transactionsError by remember { mutableStateOf<String?>(null) }
    var selectedMonth by remember { mutableStateOf<String?>(null) }
    var monthMenuExpanded by remember { mutableStateOf(false) }
    var categories by remember { mutableStateOf<List<FinanceCategory>>(emptyList()) }
    var categoriesLoading by remember { mutableStateOf(false) }
    var categoriesError by remember { mutableStateOf<String?>(null) }
    var overviewCardOrder by remember { mutableStateOf<List<String>>(emptyList()) }
    var selectedOverviewCards by remember { mutableStateOf<List<String>>(emptyList()) }
    var overviewSettingsSaving by remember { mutableStateOf(false) }
    var overviewSettingsError by remember { mutableStateOf<String?>(null) }
    var overviewSettingsOpen by rememberSaveable { mutableStateOf(false) }
    var transactionFlowOpen by rememberSaveable { mutableStateOf(false) }
    var accountDialogOpen by rememberSaveable { mutableStateOf(false) }
    var editingAccountId by rememberSaveable { mutableStateOf<String?>(null) }
    var overviewRefreshToken by remember { mutableStateOf(0) }

    LaunchedEffect(overview?.overviewCards) {
        val configured = overview?.overviewCards.orEmpty()
        selectedOverviewCards = if (configured.isEmpty()) defaultOverviewCardIds() else configured
        overviewCardOrder = (if (configured.isEmpty()) defaultOverviewCardIds() else configured)
            .plus(defaultOverviewCardIds())
            .distinct()
    }

    LaunchedEffect(overview?.onboardingCompleted) {
        if (overview?.onboardingCompleted != true) return@LaunchedEffect
        transactionsLoading = true
        transactionsError = null
        runCatching { financeRepository.getTransactions() }
            .onSuccess {
                transactionsMonth = it
                selectedMonth = it.month
            }
            .onFailure { error ->
                transactionsError = error.message ?: if (isRussian) "Не удалось загрузить транзакции." else "Failed to load transactions."
            }
        transactionsLoading = false
    }

    LaunchedEffect(overview?.onboardingCompleted, activeTab) {
        if (overview?.onboardingCompleted != true || activeTab != FinanceTab.Categories || categories.isNotEmpty()) return@LaunchedEffect
        categoriesLoading = true
        categoriesError = null
        runCatching { financeRepository.getCategories() }
            .onSuccess { categories = it }
            .onFailure { error ->
                categoriesError = error.message ?: if (isRussian) "Не удалось загрузить категории." else "Failed to load categories."
            }
        categoriesLoading = false
    }

    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        if (overview?.onboardingCompleted == true) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End
            ) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Button(
                        onClick = {
                            if (categories.isEmpty() && !categoriesLoading) {
                                coroutineScope.launch {
                                    categoriesLoading = true
                                    categoriesError = null
                                    runCatching { financeRepository.getCategories() }
                                        .onSuccess { categories = it }
                                        .onFailure { loadError ->
                                            categoriesError = loadError.message ?: if (isRussian) "Не удалось загрузить категории." else "Failed to load categories."
                                        }
                                    categoriesLoading = false
                                }
                            }
                            transactionFlowOpen = true
                        },
                        shape = RoundedCornerShape(14.dp)
                    ) {
                        Text(if (isRussian) "Добавить транзакцию" else "Add transaction")
                    }
                    if (activeTab == FinanceTab.Accounts) {
                        Button(
                            onClick = {
                                editingAccountId = null
                                accountDialogOpen = true
                            },
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(if (isRussian) "Добавить счёт" else "Add account")
                        }
                    }
                    if (activeTab == FinanceTab.Overview) {
                        Button(
                            onClick = { overviewSettingsOpen = true },
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(if (isRussian) "Настроить обзор" else "Overview settings")
                        }
                    }
                }
            }
        }

        if (loading) {
            Text(
                text = if (isRussian) "Загружаем данные из Supabase…" else "Loading data from Supabase…",
                fontSize = 14.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            return
        }

        if (error != null) {
            Text(
                text = error,
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.error
            )
        }

            if (overview == null || !overview.onboardingCompleted) {
                val stepTitle = when (onboardingStep) {
                    0 -> if (isRussian) "Основная валюта" else "Base currency"
                    1 -> if (isRussian) "Основной банк" else "Primary bank"
                    else -> if (isRussian) "Наличные" else "Cash"
                }
                val stepBody = when (onboardingStep) {
                    0 -> if (isRussian) {
                        "Выберите валюту по умолчанию для всего финансового модуля."
                    } else {
                        "Pick the default currency for the finance module."
                    }
                    1 -> if (isRussian) {
                        "Добавьте основной карточный счёт и при желании задайте стартовый баланс."
                    } else {
                        "Add the main card account and optionally set its starting balance."
                    }
                    else -> if (isRussian) {
                        "Если хотите, сразу зафиксируйте сумму наличных."
                    } else {
                        "If needed, lock in the current cash amount."
                    }
                }

                Surface(
                    shape = RoundedCornerShape(22.dp),
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.74f),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                ) {
                    Column(
                        modifier = Modifier.padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(14.dp)
                    ) {
                        Text(
                            text = if (isRussian) "Онбординг" else "Onboarding",
                            fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.secondary
                        )
                        Text(
                            text = stepTitle,
                            fontSize = 24.sp,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = stepBody,
                            fontSize = 14.sp,
                            lineHeight = 22.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )

                        if (onboardingStep == 0) {
                            Row(
                                modifier = Modifier.horizontalScroll(rememberScrollState()),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                currencies.forEach { (code, label) ->
                                    DashboardNavChip(
                                        text = label,
                                        active = onboarding.currency == code,
                                        onClick = {
                                            onOnboardingChange(onboarding.copy(currency = code))
                                        }
                                    )
                                }
                            }
                        }

                        if (onboardingStep == 1) {
                            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                banks.forEach { bank ->
                                    DashboardNavChip(
                                        text = bank,
                                        active = onboarding.bank == bank,
                                        onClick = {
                                            onOnboardingChange(
                                                onboarding.copy(
                                                    bank = if (onboarding.bank == bank) null else bank
                                                )
                                            )
                                        }
                                    )
                                }
                                OutlinedTextField(
                                    value = onboarding.primaryBalance,
                                    onValueChange = {
                                        onOnboardingChange(onboarding.copy(primaryBalance = it))
                                    },
                                    placeholder = {
                                        Text(if (isRussian) "Стартовый баланс карты" else "Starting card balance")
                                    },
                                    singleLine = true,
                                    shape = RoundedCornerShape(10.dp)
                                )
                            }
                        }

                        if (onboardingStep == 2) {
                            OutlinedTextField(
                                value = onboarding.cash,
                                onValueChange = {
                                    onOnboardingChange(onboarding.copy(cash = it))
                                },
                                placeholder = {
                                    Text(if (isRussian) "Сумма наличных" else "Cash amount")
                                },
                                singleLine = true,
                                shape = RoundedCornerShape(10.dp)
                            )
                        }

                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(
                                onClick = {
                                    if (onboardingStep >= 2) onCompleteOnboarding(false)
                                    else onOnboardingStepChange(onboardingStep + 1)
                                },
                                enabled = onboardingStep != 0 || onboarding.currency != null,
                                shape = RoundedCornerShape(14.dp)
                            ) {
                                Text(if (onboardingStep >= 2) {
                                    if (isRussian) "Завершить" else "Finish"
                                } else {
                                    if (isRussian) "Далее" else "Continue"
                                })
                            }

                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                if (onboardingStep > 0) {
                                    Button(
                                        onClick = { onOnboardingStepChange(onboardingStep - 1) },
                                        shape = RoundedCornerShape(14.dp),
                                        colors = ButtonDefaults.buttonColors(
                                            containerColor = MaterialTheme.colorScheme.surface,
                                            contentColor = MaterialTheme.colorScheme.onSurface
                                        )
                                    ) {
                                        Text(if (isRussian) "Назад" else "Back")
                                    }
                                }

                                Button(
                                    onClick = {
                                        if (onboardingStep >= 2) onCompleteOnboarding(true)
                                        else onOnboardingStepChange(onboardingStep + 1)
                                    },
                                    shape = RoundedCornerShape(14.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.surface,
                                        contentColor = MaterialTheme.colorScheme.onSurface
                                    )
                                ) {
                                    Text(if (isRussian) "Пропустить" else "Skip")
                                }
                            }
                        }
                    }
                }
                return
            }

        Surface(
            shape = RoundedCornerShape(20.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.72f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Box(modifier = Modifier.padding(8.dp)) {
                FinanceTabStrip(
                    tabs = FinanceTab.entries.toList(),
                    labels = tabLabels,
                    activeTab = activeTab,
                    onTabChange = onTabChange
                )
            }
        }

        AnimatedContent(
            targetState = activeTab,
            transitionSpec = {
                fadeIn(animationSpec = tween(220, delayMillis = 40)) togetherWith
                    fadeOut(animationSpec = tween(140))
            },
            label = "finance-tab-content"
        ) { tab ->
            Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                when (tab) {
                        FinanceTab.Overview -> {
                            AnimatedContent(
                                targetState = overviewRefreshToken,
                                transitionSpec = {
                                    (fadeIn(animationSpec = tween(260, delayMillis = 40)) +
                                        slideInVertically(animationSpec = tween(260)) { it / 8 }) togetherWith
                                        (fadeOut(animationSpec = tween(180)) +
                                            slideOutVertically(animationSpec = tween(180)) { -it / 10 })
                                },
                                label = "finance-overview-refresh"
                            ) {
                                Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                                    FinanceSummaryCards(
                                        isRussian = isRussian,
                                        compact = compact,
                                        overview = overview
                                    )
                                    FinanceRecentTransactionsBlock(
                                        isRussian = isRussian,
                                        transactions = overview.recentTransactions
                                    )
                                }
                            }
                        }

                        FinanceTab.Accounts -> {
                            FinanceAccountsBlock(
                                isRussian = isRussian,
                                overview = overview,
                                onEdit = { account ->
                                    editingAccountId = account.id
                                    accountDialogOpen = true
                                }
                            )
                        }

                        FinanceTab.Transactions -> {
                            Surface(
                                shape = RoundedCornerShape(20.dp),
                                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.72f),
                                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(16.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                        Text(
                                            text = if (isRussian) "Месяц" else "Month",
                                            fontSize = 12.sp,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                        Text(
                                            text = selectedMonth?.let { formatMonthLabel(it, isRussian) }
                                                ?: if (isRussian) "Выберите месяц" else "Choose month",
                                            fontSize = 18.sp,
                                            fontWeight = FontWeight.SemiBold,
                                            color = MaterialTheme.colorScheme.onSurface
                                        )
                                    }
                                    Box {
                                        Button(
                                            onClick = { monthMenuExpanded = true },
                                            shape = RoundedCornerShape(14.dp),
                                            colors = ButtonDefaults.buttonColors(
                                                containerColor = MaterialTheme.colorScheme.surface,
                                                contentColor = MaterialTheme.colorScheme.onSurface
                                            )
                                        ) {
                                            Text(if (isRussian) "Выбрать" else "Select")
                                        }
                                        DropdownMenu(
                                            expanded = monthMenuExpanded,
                                            onDismissRequest = { monthMenuExpanded = false }
                                        ) {
                                            transactionsMonth?.availableMonths?.forEach { month ->
                                                DropdownMenuItem(
                                                    text = { Text(formatMonthLabel(month, isRussian)) },
                                                    onClick = {
                                                        monthMenuExpanded = false
                                                        selectedMonth = month
                                                        coroutineScope.launch {
                                                            transactionsLoading = true
                                                            transactionsError = null
                                                            runCatching { financeRepository.getTransactions(month) }
                                                                .onSuccess { transactionsMonth = it }
                                                                .onFailure { error ->
                                                                    transactionsError = error.message ?: if (isRussian) "Не удалось загрузить транзакции." else "Failed to load transactions."
                                                                }
                                                            transactionsLoading = false
                                                        }
                                                    }
                                                )
                                            }
                                        }
                                    }
                                }
                            }
                            FinanceTransactionsMonthBlock(
                                isRussian = isRussian,
                                loading = transactionsLoading,
                                error = transactionsError,
                                transactionsMonth = transactionsMonth
                            )
                        }

                        FinanceTab.Categories -> {
                            FinanceCategoriesBlock(
                                isRussian = isRussian,
                                loading = categoriesLoading,
                                error = categoriesError,
                                categories = categories
                            )
                        }

                        FinanceTab.Analytics -> {
                            FinanceAnalyticsBlock(isRussian = isRussian)
                        }
                }
            }
        }

        if (overviewSettingsOpen && overview?.onboardingCompleted == true) {
            FinanceOverviewSettingsDialog(
                isRussian = isRussian,
                orderedCards = overviewCardOrder,
                selectedCards = selectedOverviewCards,
                saving = overviewSettingsSaving,
                error = overviewSettingsError,
                onDismiss = { overviewSettingsOpen = false },
                onToggle = { cardId, enabled ->
                    selectedOverviewCards = toggleOverviewCard(selectedOverviewCards, cardId, enabled)
                },
                onMove = { cardId, targetIndex ->
                    overviewCardOrder = moveOverviewCardToIndex(overviewCardOrder, cardId, targetIndex)
                },
                onSave = {
                    overviewSettingsError = null
                    coroutineScope.launch {
                        overviewSettingsSaving = true
                        runCatching {
                            financeRepository.updateOverviewCards(
                                overviewCardOrder.filter { it in selectedOverviewCards }
                            )
                        }
                            .onSuccess { updated ->
                                onOverviewChange(updated)
                                selectedOverviewCards = updated.overviewCards
                                overviewCardOrder = updated.overviewCards.plus(defaultOverviewCardIds()).distinct()
                                overviewRefreshToken += 1
                                overviewSettingsOpen = false
                            }
                            .onFailure { saveError ->
                                overviewSettingsError = saveError.message ?: if (isRussian) {
                                    "Не удалось сохранить настройки обзора."
                                } else {
                                    "Failed to save overview settings."
                                }
                            }
                        overviewSettingsSaving = false
                    }
                }
            )
        }

        if (transactionFlowOpen && overview != null) {
            FinanceTransactionFlowDialog(
                isRussian = isRussian,
                overview = overview,
                categories = categories,
                categoriesLoading = categoriesLoading,
                financeRepository = financeRepository,
                selectedMonth = selectedMonth,
                onDismiss = { transactionFlowOpen = false },
                onSaved = { updatedOverview, updatedTransactions ->
                    onOverviewChange(updatedOverview)
                    if (updatedTransactions != null) {
                        transactionsMonth = updatedTransactions
                        selectedMonth = updatedTransactions.month
                    }
                    transactionFlowOpen = false
                }
            )
        }

        if (accountDialogOpen && overview != null) {
            FinanceAccountDialog(
                isRussian = isRussian,
                overview = overview,
                account = overview.accounts.firstOrNull { it.id == editingAccountId },
                onDismiss = { accountDialogOpen = false },
                onSaved = { updated ->
                    onOverviewChange(updated)
                    accountDialogOpen = false
                },
                financeRepository = financeRepository
            )
        }
    }
}

private data class FinanceCategoryNode(
    val category: FinanceCategory,
    val children: List<FinanceCategoryNode>
)

private data class FinanceDraftItemState(
    val id: String = java.util.UUID.randomUUID().toString(),
    val title: String = "",
    val amount: String = "",
    val categoryId: String? = null
)

private data class FinanceDraftState(
    val id: String = java.util.UUID.randomUUID().toString(),
    val sourceType: String = "manual",
    val documentKind: String = "manual",
    val direction: String = "expense",
    val title: String = "",
    val merchantName: String = "",
    val note: String = "",
    val accountId: String = "",
    val destinationAccountId: String = "",
    val currency: String = "RUB",
    val happenedAt: String = isoNowLocal(),
    val items: List<FinanceDraftItemState> = listOf(FinanceDraftItemState())
)

private data class FinanceOverviewCardVisual(
    val id: String,
    val title: String,
    val value: String,
    val detail: String
)

@Composable
private fun FinanceSummaryCards(
    isRussian: Boolean,
    compact: Boolean,
    overview: FinanceOverview
) {
    val currency = overview.defaultCurrency ?: "RUB"
    val monthLabel = currentMonthLabel(isRussian)
    val cards = remember(overview, isRussian) {
        overview.overviewCards.mapNotNull { cardId ->
            val metric = when (cardId) {
                "total_balance" -> kotlin.math.abs(overview.totalBalanceMinor)
                "card_balance" -> kotlin.math.abs(overview.cardBalanceMinor)
                "cash_balance" -> kotlin.math.abs(overview.cashBalanceMinor)
                "month_income" -> kotlin.math.abs(overview.monthIncomeMinor)
                "month_expense" -> kotlin.math.abs(overview.monthExpenseMinor)
                "month_result" -> kotlin.math.abs(overview.monthNetMinor)
                "recent_transactions" -> overview.recentTransactions.size.toLong()
                else -> 0L
            }
            if (metric == 0L) return@mapNotNull null

            when (cardId) {
                "total_balance" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "Общий баланс" else "Total balance",
                    value = formatMoney(overview.totalBalanceMinor, currency, isRussian),
                    detail = if (isRussian) "Все счета и наличные" else "All accounts and cash"
                )
                "card_balance" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "На картах" else "On cards",
                    value = formatMoney(overview.cardBalanceMinor, currency, isRussian),
                    detail = if (isRussian) "Карточные счета" else "Card accounts"
                )
                "cash_balance" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "Наличные" else "Cash",
                    value = formatMoney(overview.cashBalanceMinor, currency, isRussian),
                    detail = if (isRussian) "Физические деньги" else "Physical money"
                )
                "month_income" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "Доходы" else "Income",
                    value = formatMoney(overview.monthIncomeMinor, currency, isRussian),
                    detail = monthLabel
                )
                "month_expense" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "Расходы" else "Expenses",
                    value = formatMoney(-overview.monthExpenseMinor, currency, isRussian),
                    detail = monthLabel
                )
                "month_result" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "Результат месяца" else "Month result",
                    value = formatMoney(overview.monthNetMinor, currency, isRussian),
                    detail = monthLabel
                )
                "recent_transactions" -> FinanceOverviewCardVisual(
                    id = cardId,
                    title = if (isRussian) "Последние транзакции" else "Recent transactions",
                    value = overview.recentTransactions.size.toString(),
                    detail = if (isRussian) "Короткий список" else "Short list"
                )
                else -> null
            }
        }
    }

    BoxWithConstraints {
        val columns = when {
            maxWidth > 900.dp -> 3
            compact -> 1
            else -> 2
        }

        Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
            cards.filter { it.id != "recent_transactions" }.chunked(columns).forEach { row ->
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    row.forEachIndexed { index, card ->
                        FinanceOverviewMetricCard(
                            modifier = Modifier.weight(1f),
                            card = card,
                            emphasize = index == row.lastIndex && card.id == "total_balance" && !compact
                        )
                    }
                    repeat(columns - row.size) {
                        Spacer(modifier = Modifier.weight(1f))
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceOverviewMetricCard(
    modifier: Modifier = Modifier,
    card: FinanceOverviewCardVisual,
    emphasize: Boolean
) {
    val accent = financeOverviewCardAccent(card.id)
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(24.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.74f),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 164.dp)
                .padding(horizontal = 18.dp, vertical = 16.dp)
        ) {
            Box(
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .offset(x = 26.dp, y = (-26).dp)
                    .size(88.dp)
                    .background(accent.copy(alpha = 0.24f), CircleShape)
            )
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    text = card.title.uppercase(),
                    fontSize = 11.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    letterSpacing = 1.sp
                )
                Text(
                    text = card.value,
                    fontSize = if (emphasize) 42.sp else 34.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = card.detail,
                    fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun FinanceAccountsBlock(
    isRussian: Boolean,
    overview: FinanceOverview,
    onEdit: (FinanceAccount) -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(
            text = if (isRussian) "Счета" else "Accounts",
            fontSize = 18.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )
        if (overview.accounts.isEmpty()) {
            Text(
                text = if (isRussian) "Счета пока не добавлены." else "No accounts yet.",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        } else {
            overview.accounts.forEach { account ->
                Surface(
                    shape = RoundedCornerShape(20.dp),
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                ) {
                    Box {
                        Box(
                            modifier = Modifier
                                .align(Alignment.TopEnd)
                                .padding(top = 16.dp, end = 16.dp)
                                .size(72.dp)
                                .background(
                                    color = financeOverviewCardAccent(
                                        if (account.kind == "cash") "cash_balance" else "card_balance"
                                    ).copy(alpha = 0.18f),
                                    shape = CircleShape
                                )
                        )
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            verticalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                    Text(
                                        text = account.name,
                                        fontSize = 16.sp,
                                        fontWeight = FontWeight.SemiBold,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                    Text(
                                        text = account.bankName ?: if (account.kind == "cash") {
                                            if (isRussian) "Наличные" else "Cash"
                                        } else account.name,
                                        fontSize = 12.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                                Text(
                                    text = formatMoney(account.balanceMinor, account.currency, isRussian),
                                    fontSize = 15.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                            }
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(
                                    text = if (isRussian) "Операций: ${account.transactionCount}" else "Transactions: ${account.transactionCount}",
                                    fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                Button(
                                    onClick = { onEdit(account) },
                                    shape = RoundedCornerShape(12.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.surface,
                                        contentColor = MaterialTheme.colorScheme.onSurface
                                    ),
                                    contentPadding = ButtonDefaults.ContentPadding
                                ) {
                                    Text(if (isRussian) "Изменить" else "Edit")
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceRecentTransactionsBlock(
    isRussian: Boolean,
    transactions: List<com.assistant.app.finance.FinanceTransaction>
) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(
            text = if (isRussian) "Последние транзакции" else "Recent transactions",
            fontSize = 18.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )
        if (transactions.isEmpty()) {
            Text(
                text = if (isRussian) "Пока нет транзакций. Форма добавления будет следующим шагом." else "No transactions yet. Full entry comes next.",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        } else {
            transactions.forEach { transaction ->
                FinanceTransactionRow(isRussian = isRussian, transaction = transaction)
            }
        }
    }
}

@Composable
private fun FinanceAnimatedDialog(
    onDismiss: () -> Unit,
    dismissEnabled: Boolean = true,
    content: @Composable () -> Unit
) {
    var visible by remember { mutableStateOf(false) }
    val coroutineScope = rememberCoroutineScope()
    val alpha by androidx.compose.animation.core.animateFloatAsState(
        targetValue = if (visible) 1f else 0f,
        animationSpec = tween(180),
        label = "finance-dialog-alpha"
    )
    val scale by androidx.compose.animation.core.animateFloatAsState(
        targetValue = if (visible) 1f else 0.975f,
        animationSpec = tween(220),
        label = "finance-dialog-scale"
    )

    fun requestDismiss() {
        if (!dismissEnabled || !visible) return
        visible = false
        coroutineScope.launch {
            delay(180)
            onDismiss()
        }
    }

    LaunchedEffect(Unit) {
        visible = true
    }

    Dialog(onDismissRequest = { requestDismiss() }) {
        Box(
            modifier = Modifier.graphicsLayer {
                this.alpha = alpha
                scaleX = scale
                scaleY = scale
                translationY = (1f - alpha) * 24f
            }
        ) {
            content()
        }
    }
}

@Composable
private fun FinanceOverviewSettingsDialog(
    isRussian: Boolean,
    orderedCards: List<String>,
    selectedCards: List<String>,
    saving: Boolean,
    error: String?,
    onDismiss: () -> Unit,
    onToggle: (String, Boolean) -> Unit,
    onMove: (String, Int) -> Unit,
    onSave: () -> Unit
) {
    FinanceAnimatedDialog(onDismiss = onDismiss) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.96f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                FinanceOverviewSettingsBlock(
                    isRussian = isRussian,
                    orderedCards = orderedCards,
                    selectedCards = selectedCards,
                    saving = saving,
                    error = error,
                    onToggle = onToggle,
                    onMove = onMove
                )
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(
                            onClick = onDismiss,
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(if (isRussian) "Отмена" else "Cancel")
                        }
                        Button(
                            onClick = onSave,
                            enabled = !saving,
                            shape = RoundedCornerShape(14.dp)
                        ) {
                            Text(if (isRussian) "Сохранить" else "Save")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceSimpleDialog(
    title: String,
    body: String,
    onDismiss: () -> Unit,
    isRussian: Boolean
) {
    FinanceAnimatedDialog(onDismiss = onDismiss) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.96f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Text(
                    text = title,
                    fontSize = 20.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = body,
                    fontSize = 13.sp,
                    lineHeight = 20.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    Button(
                        onClick = onDismiss,
                        shape = RoundedCornerShape(14.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.surface,
                            contentColor = MaterialTheme.colorScheme.onSurface
                        )
                    ) {
                        Text(if (isRussian) "Закрыть" else "Close")
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceTransactionFlowDialog(
    isRussian: Boolean,
    overview: FinanceOverview,
    categories: List<FinanceCategory>,
    categoriesLoading: Boolean,
    financeRepository: FinanceRepository,
    selectedMonth: String?,
    onDismiss: () -> Unit,
    onSaved: (FinanceOverview, FinanceTransactionsMonth?) -> Unit
) {
    val coroutineScope = rememberCoroutineScope()
    val context = LocalContext.current
    val dialogScroll = rememberScrollState()
    var step by remember { mutableStateOf("chooser") }
    var drafts by remember(overview.accounts) {
        mutableStateOf(listOf(createManualFinanceDraft(overview)))
    }
    var selectedDraftId by remember { mutableStateOf(drafts.first().id) }
    var busy by remember { mutableStateOf<String?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var warnings by remember { mutableStateOf<List<String>>(emptyList()) }
    val selectedDraft = drafts.firstOrNull { it.id == selectedDraftId } ?: drafts.first()
    val categoryOptions = remember(categories, selectedDraft.direction, isRussian) {
        buildFinanceCategoryOptions(
            categories = categories,
            direction = if (selectedDraft.direction == "income") "income" else "expense"
        )
    }

    fun updateDraft(transform: (FinanceDraftState) -> FinanceDraftState) {
        drafts = drafts.map { draft ->
            if (draft.id == selectedDraftId) transform(draft) else draft
        }
    }

    suspend fun importDocument(fileName: String, mimeType: String, bytes: ByteArray, sourceType: String) {
        busy = "import"
        error = null
        runCatching {
            financeRepository.processReceiptImport(fileName, mimeType, bytes, sourceType)
        }.onSuccess { result ->
            warnings = result.warnings
            drafts = result.drafts.map { draft ->
                financeDraftFromImport(draft, overview)
            }.ifEmpty {
                listOf(createManualFinanceDraft(overview, sourceType = sourceType))
            }
            selectedDraftId = drafts.firstOrNull()?.id ?: selectedDraftId
            step = "editor"
        }.onFailure { throwable ->
            error = throwable.message ?: if (isRussian) "Не удалось обработать документ." else "Failed to process document."
        }
        busy = null
    }

    val photoLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.TakePicturePreview()
    ) { bitmap ->
        if (bitmap == null) return@rememberLauncherForActivityResult
        coroutineScope.launch {
            importDocument(
                fileName = "camera-${System.currentTimeMillis()}.jpg",
                mimeType = "image/jpeg",
                bytes = bitmapToJpeg(bitmap),
                sourceType = "photo"
            )
        }
    }

    val fileLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri == null) return@rememberLauncherForActivityResult
        coroutineScope.launch {
            runCatching { readBytesFromUri(context, uri) }
                .onSuccess { result ->
                    importDocument(
                        fileName = result.first,
                        mimeType = result.second,
                        bytes = result.third,
                        sourceType = "file"
                    )
                }
                .onFailure { throwable ->
                    error = throwable.message ?: if (isRussian) "Не удалось прочитать файл." else "Failed to read file."
                }
        }
    }

    FinanceAnimatedDialog(onDismiss = onDismiss, dismissEnabled = busy == null) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.97f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Column(
                modifier = Modifier
                    .widthIn(max = 720.dp)
                    .heightIn(max = 760.dp)
                    .fillMaxWidth()
                    .verticalScroll(dialogScroll)
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Text(
                    text = if (isRussian) "Добавить транзакцию" else "Add transaction",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                if (error != null) {
                    Text(error ?: "", fontSize = 12.sp, color = MaterialTheme.colorScheme.error)
                }
                if (warnings.isNotEmpty()) {
                    warnings.forEach { warning ->
                        Text(warning, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }

                if (step == "chooser") {
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        FinanceMethodButton(
                            eyebrow = if (isRussian) "РУЧНОЙ ВВОД" else "MANUAL",
                            title = if (isRussian) "Вручную" else "Manual",
                            body = if (isRussian) "Полностью заполнить транзакцию и позиции руками." else "Fill the transaction and items manually.",
                            onClick = {
                                drafts = listOf(createManualFinanceDraft(overview))
                                selectedDraftId = drafts.first().id
                                step = "editor"
                            }
                        )
                        FinanceMethodButton(
                            eyebrow = if (isRussian) "КАМЕРА" else "CAMERA",
                            title = if (isRussian) "Фото" else "Photo",
                            body = if (isRussian) "Сделать снимок чека и разобрать его через Gemini." else "Capture a receipt and parse it with Gemini.",
                            onClick = { photoLauncher.launch(null) },
                            emphasized = true
                        )
                        FinanceMethodButton(
                            eyebrow = if (isRussian) "ИМПОРТ" else "IMPORT",
                            title = if (isRussian) "Файл" else "File",
                            body = if (isRussian) "Открыть изображение, PDF или EML." else "Open an image, PDF, or EML.",
                            onClick = { fileLauncher.launch(arrayOf("image/*", "application/pdf", "message/rfc822")) }
                        )
                        if (busy == "import") {
                            Text(
                                text = if (isRussian) "Анализируем документ…" else "Analyzing document…",
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                } else {
                    if (drafts.size > 1) {
                        Row(
                            modifier = Modifier.horizontalScroll(rememberScrollState()),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            drafts.forEachIndexed { index, draft ->
                                DashboardNavChip(
                                    text = if (isRussian) "Черновик ${index + 1}" else "Draft ${index + 1}",
                                    active = draft.id == selectedDraftId,
                                    onClick = { selectedDraftId = draft.id }
                                )
                            }
                        }
                    }
                    if (categoriesLoading) {
                        Text(
                            text = if (isRussian) "Подгружаем категории…" else "Loading categories…",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    FinanceDraftEditor(
                        isRussian = isRussian,
                        overview = overview,
                        draft = selectedDraft,
                        categoryOptions = categoryOptions,
                        onDraftChange = { next -> updateDraft { next } }
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(
                            onClick = {
                                if (step == "chooser") onDismiss() else step = "chooser"
                            },
                            enabled = busy == null,
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(
                                if (step == "chooser") {
                                    if (isRussian) "Закрыть" else "Close"
                                } else {
                                    if (isRussian) "Назад" else "Back"
                                }
                            )
                        }
                        if (step == "editor") {
                            Button(
                                onClick = {
                                    coroutineScope.launch {
                                        busy = "save"
                                        error = null
                                        runCatching {
                                            val payloads = drafts.map { draft ->
                                                draft.toRepositoryPayload().toRepositoryRequest()
                                            }
                                            val updatedOverview = financeRepository.createTransactions(
                                                payloads
                                            )
                                            val updatedMonth = runCatching {
                                                financeRepository.getTransactions(selectedMonth)
                                            }.getOrNull()
                                            updatedOverview to updatedMonth
                                        }.onSuccess { (updatedOverview, updatedMonth) ->
                                            onSaved(updatedOverview, updatedMonth)
                                        }.onFailure { throwable ->
                                            error = throwable.message ?: if (isRussian) "Не удалось сохранить транзакцию." else "Failed to save transaction."
                                        }
                                        busy = null
                                    }
                                },
                                enabled = busy == null && selectedDraft.canSave(),
                                shape = RoundedCornerShape(14.dp)
                            ) {
                                Text(
                                    if (busy == "save") {
                                        if (isRussian) "Сохраняем…" else "Saving…"
                                    } else {
                                        if (drafts.size > 1) {
                                            if (isRussian) "Сохранить все черновики" else "Save all drafts"
                                        } else {
                                            if (isRussian) "Сохранить" else "Save"
                                        }
                                    }
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceOverviewSettingsBlock(
    isRussian: Boolean,
    orderedCards: List<String>,
    selectedCards: List<String>,
    saving: Boolean,
    error: String?,
    onToggle: (String, Boolean) -> Unit,
    onMove: (String, Int) -> Unit
) {
    val density = LocalDensity.current
    val rowHeightPx = with(density) { 78.dp.toPx() }
    val latestOrderedCards by rememberUpdatedState(orderedCards)
    var draggedCardId by remember { mutableStateOf<String?>(null) }
    var previewTargetIndex by remember { mutableStateOf<Int?>(null) }
    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
            Text(
                text = if (isRussian) "Настройки обзора" else "Overview settings",
                fontSize = 18.sp,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = if (isRussian) {
                    "Выбирай карточки для обзора и меняй их порядок. Нулевые карточки на экране обзора скрываются автоматически."
                } else {
                    "Choose overview cards and reorder them. Zero-value cards are hidden automatically on the overview screen."
                },
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            orderedCards.forEachIndexed { index, cardId ->
                var dragOffsetY by remember(cardId) { mutableStateOf(0f) }
                Surface(
                    modifier = Modifier.offset { IntOffset(0, dragOffsetY.roundToInt()) },
                    shape = RoundedCornerShape(16.dp),
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.58f),
                    border = BorderStroke(
                        if (previewTargetIndex == index && draggedCardId != cardId) 1.5.dp else 1.dp,
                        if (previewTargetIndex == index && draggedCardId != cardId) {
                            MaterialTheme.colorScheme.primary
                        } else {
                            MaterialTheme.colorScheme.outline
                        }
                    )
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(12.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Row(
                            modifier = Modifier.weight(1f),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            Checkbox(
                                checked = selectedCards.contains(cardId),
                                onCheckedChange = { checked -> onToggle(cardId, checked) }
                            )
                            Text(
                                text = financeOverviewCardLabel(cardId, isRussian),
                                fontSize = 14.sp,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Box(
                                modifier = Modifier
                                    .size(34.dp)
                                    .background(
                                        color = MaterialTheme.colorScheme.surface,
                                        shape = RoundedCornerShape(10.dp)
                                    )
                                    .pointerInput(cardId, saving) {
                                        detectDragGesturesAfterLongPress(
                                            onDragStart = {
                                                dragOffsetY = 0f
                                                draggedCardId = cardId
                                                previewTargetIndex = latestOrderedCards.indexOf(cardId).takeIf { it >= 0 }
                                            },
                                            onDragEnd = {
                                                val currentIndex = latestOrderedCards.indexOf(cardId)
                                                if (currentIndex != -1) {
                                                    val targetIndex = previewTargetIndex
                                                        ?: (currentIndex + (dragOffsetY / rowHeightPx).roundToInt())
                                                        .coerceIn(0, latestOrderedCards.lastIndex)
                                                    if (targetIndex != currentIndex) {
                                                        onMove(cardId, targetIndex)
                                                    }
                                                }
                                                dragOffsetY = 0f
                                                draggedCardId = null
                                                previewTargetIndex = null
                                            },
                                            onDragCancel = {
                                                dragOffsetY = 0f
                                                draggedCardId = null
                                                previewTargetIndex = null
                                            }
                                        ) { change, dragAmount ->
                                            change.consume()
                                            if (saving) return@detectDragGesturesAfterLongPress
                                            dragOffsetY += dragAmount.y
                                            val currentIndex = latestOrderedCards.indexOf(cardId)
                                            if (currentIndex != -1) {
                                                previewTargetIndex = (currentIndex + (dragOffsetY / rowHeightPx).roundToInt())
                                                    .coerceIn(0, latestOrderedCards.lastIndex)
                                            }
                                        }
                                    },
                                contentAlignment = Alignment.Center
                            ) {
                                Text(
                                    text = "⋮⋮",
                                    fontSize = 13.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                }
            }

            if (saving) {
                Text(
                    text = if (isRussian) "Сохраняем настройки…" else "Saving settings…",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            if (error != null) {
                Text(
                    text = error,
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.error
                )
            }
    }
}

@Composable
private fun FinanceAccountDialog(
    isRussian: Boolean,
    overview: FinanceOverview,
    account: FinanceAccount?,
    financeRepository: FinanceRepository,
    onDismiss: () -> Unit,
    onSaved: (FinanceOverview) -> Unit
) {
    val providers = remember(isRussian) { financeAccountProviders(isRussian) }
    val coroutineScope = rememberCoroutineScope()
    val initialProvider = account?.providerCode
        ?: providers.firstOrNull { it.first != "cash" }?.first
        ?: "tbank"
    var providerCode by remember(account?.id) { mutableStateOf(initialProvider) }
    var amountInput by remember(account?.id) {
        mutableStateOf(
            account?.let { accountValue ->
                val value = accountValue.balanceMinor / 100.0
                if (kotlin.math.abs(value % 1.0) < 0.000001) {
                    value.toLong().toString()
                } else {
                    value.toString().replace('.', ',')
                }
            } ?: ""
        )
    }
    var nameInput by remember(account?.id) {
        mutableStateOf(
            account?.takeIf { it.kind != "cash" }?.name
                ?.takeUnless { account.bankName != null && it == account.bankName }
                .orEmpty()
        )
    }
    var makePrimary by remember(account?.id) { mutableStateOf(account?.isPrimary == true) }
    var menuExpanded by remember { mutableStateOf(false) }
    var saving by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }

    val selectedProvider = providers.firstOrNull { it.first == providerCode } ?: providers.first()
    val isCash = providerCode == "cash"
    val balanceEditable = account?.balanceEditable ?: true
    val defaultCurrency = overview.defaultCurrency ?: account?.currency ?: "RUB"

    FinanceAnimatedDialog(
        onDismiss = onDismiss,
        dismissEnabled = !saving
    ) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.96f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Text(
                    text = if (account == null) {
                        if (isRussian) "Добавить счёт" else "Add account"
                    } else {
                        if (isRussian) "Изменить счёт" else "Edit account"
                    },
                    fontSize = 20.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = if (isRussian) {
                        "Выберите тип счёта и укажите текущий баланс. После появления первой транзакции баланс станет историей операций и редактирование суммы заблокируется."
                    } else {
                        "Choose the account type and set the current balance. Once the first transaction appears, the amount becomes transaction history and can no longer be edited directly."
                    },
                    fontSize = 13.sp,
                    lineHeight = 20.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        text = if (isRussian) "Счёт" else "Account type",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Box {
                        Button(
                            onClick = { menuExpanded = true },
                            enabled = !saving,
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(selectedProvider.second)
                                Text("▾", color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                        DropdownMenu(
                            expanded = menuExpanded,
                            onDismissRequest = { menuExpanded = false }
                        ) {
                            providers.forEach { (code, label) ->
                                DropdownMenuItem(
                                    text = { Text(label) },
                                    onClick = {
                                        providerCode = code
                                        if (code == "cash") {
                                            nameInput = ""
                                            makePrimary = false
                                        }
                                        menuExpanded = false
                                    }
                                )
                            }
                        }
                    }
                }

                if (!isCash) {
                    OutlinedTextField(
                        value = nameInput,
                        onValueChange = { nameInput = it },
                        label = { Text(if (isRussian) "Название" else "Name") },
                        placeholder = { Text(if (isRussian) "Например, Основная карта" else "For example, Main card") },
                        singleLine = true,
                        shape = RoundedCornerShape(14.dp),
                        enabled = !saving,
                        modifier = Modifier.fillMaxWidth()
                    )
                }

                OutlinedTextField(
                    value = amountInput,
                    onValueChange = { amountInput = it },
                    label = { Text(if (isRussian) "Текущий баланс" else "Current balance") },
                    placeholder = { Text(if (isRussian) "Например, 12500" else "For example, 12500") },
                    supportingText = {
                        Text(
                            if (!balanceEditable) {
                                if (isRussian) "Сумма заблокирована: у счёта уже есть транзакции." else "Amount is locked: this account already has transactions."
                            } else {
                                if (isRussian) "Вводи сумму в ${defaultCurrency}." else "Enter amount in ${defaultCurrency}."
                            }
                        )
                    },
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp),
                    enabled = !saving && balanceEditable,
                    modifier = Modifier.fillMaxWidth()
                )

                if (!isCash) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable(enabled = !saving) { makePrimary = !makePrimary },
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Checkbox(
                            checked = makePrimary,
                            onCheckedChange = { makePrimary = it },
                            enabled = !saving
                        )
                        Text(
                            text = if (isRussian) "Сделать основным счётом" else "Mark as primary account",
                            fontSize = 14.sp,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                }

                if (error != null) {
                    Text(
                        text = error ?: "",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.error
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(
                            onClick = onDismiss,
                            enabled = !saving,
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(if (isRussian) "Отмена" else "Cancel")
                        }
                        Button(
                            onClick = {
                                error = null
                                val amountMinor = if (balanceEditable) parseAmountToMinor(amountInput) else account?.balanceMinor
                                if (amountMinor == null) {
                                    error = if (isRussian) "Укажи корректную сумму." else "Enter a valid amount."
                                    return@Button
                                }
                                if (!isCash && nameInput.trim().isEmpty()) {
                                    error = if (isRussian) "Укажи название счёта." else "Enter the account name."
                                    return@Button
                                }
                                saving = true
                                val finalName = if (isCash) {
                                    if (isRussian) "Наличные" else "Cash"
                                } else {
                                    nameInput.trim()
                                }
                                val finalPrimary = if (isCash) false else makePrimary
                                val finalProviderCode = if (account?.kind == "cash") account.providerCode else providerCode
                                val finalAmount = if (balanceEditable) amountMinor else (account?.balanceMinor ?: amountMinor)
                                val accountId = account?.id
                                coroutineScope.launch {
                                    runCatching {
                                        financeRepository.upsertAccount(
                                            id = accountId,
                                            providerCode = finalProviderCode,
                                            balanceMinor = finalAmount,
                                            currency = defaultCurrency,
                                            name = finalName,
                                            makePrimary = finalPrimary
                                        )
                                    }.onSuccess(onSaved)
                                        .onFailure { saveError ->
                                            error = saveError.message ?: if (isRussian) {
                                                "Не удалось сохранить счёт."
                                            } else {
                                                "Failed to save account."
                                            }
                                        }
                                    saving = false
                                }
                            },
                            enabled = !saving,
                            shape = RoundedCornerShape(14.dp)
                        ) {
                            Text(
                                if (saving) {
                                    if (isRussian) "Сохраняем…" else "Saving…"
                                } else {
                                    if (account == null) {
                                        if (isRussian) "Создать" else "Create"
                                    } else {
                                        if (isRussian) "Сохранить" else "Save"
                                    }
                                }
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceMethodButton(
    eyebrow: String,
    title: String,
    body: String,
    onClick: () -> Unit,
    emphasized: Boolean = false
) {
    Surface(
        onClick = onClick,
        shape = RoundedCornerShape(22.dp),
        color = if (emphasized) MaterialTheme.colorScheme.primary.copy(alpha = 0.12f) else MaterialTheme.colorScheme.surface.copy(alpha = 0.72f),
        border = BorderStroke(1.dp, if (emphasized) MaterialTheme.colorScheme.primary.copy(alpha = 0.4f) else MaterialTheme.colorScheme.outline),
        tonalElevation = if (emphasized) 3.dp else 1.dp
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 18.dp, vertical = 16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = eyebrow,
                fontSize = 11.sp,
                fontWeight = FontWeight.Bold,
                color = if (emphasized) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(title, fontSize = 18.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
            Text(body, fontSize = 13.sp, lineHeight = 20.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun FinanceDraftEditor(
    isRussian: Boolean,
    overview: FinanceOverview,
    draft: FinanceDraftState,
    categoryOptions: List<Pair<String, String>>,
    onDraftChange: (FinanceDraftState) -> Unit
) {
    var accountMenuExpanded by remember { mutableStateOf(false) }
    var destinationMenuExpanded by remember { mutableStateOf(false) }
    val account = overview.accounts.firstOrNull { it.id == draft.accountId } ?: overview.accounts.first()

    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Surface(
            shape = RoundedCornerShape(20.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.62f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(14.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = if (isRussian) "Основное" else "Basics",
                    fontSize = 15.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                    listOf("expense", "income", "transfer").forEachIndexed { index, value ->
                        SegmentedButton(
                            selected = draft.direction == value,
                            onClick = {
                                onDraftChange(
                                    draft.copy(
                                        direction = value,
                                        items = if (value == "transfer") draft.items.take(1) else if (draft.items.isEmpty()) listOf(FinanceDraftItemState()) else draft.items
                                    )
                                )
                            },
                            shape = SegmentedButtonDefaults.itemShape(index = index, count = 3)
                        ) {
                            Text(
                                when (value) {
                                    "expense" -> if (isRussian) "Расход" else "Expense"
                                    "income" -> if (isRussian) "Доход" else "Income"
                                    else -> if (isRussian) "Перевод" else "Transfer"
                                }
                            )
                        }
                    }
                }

                Box {
                    Button(
                        onClick = { accountMenuExpanded = true },
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(14.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.surface,
                            contentColor = MaterialTheme.colorScheme.onSurface
                        )
                    ) {
                        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                            Text(account.name)
                            Text("▾")
                        }
                    }
                    DropdownMenu(expanded = accountMenuExpanded, onDismissRequest = { accountMenuExpanded = false }) {
                        overview.accounts.forEach { option ->
                            DropdownMenuItem(
                                text = { Text(option.name) },
                                onClick = {
                                    onDraftChange(draft.copy(accountId = option.id, currency = option.currency))
                                    accountMenuExpanded = false
                                }
                            )
                        }
                    }
                }

                if (draft.direction == "transfer") {
                    Box {
                        Button(
                            onClick = { destinationMenuExpanded = true },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                                Text(
                                    overview.accounts.firstOrNull { it.id == draft.destinationAccountId }?.name
                                        ?: if (isRussian) "Куда перевести" else "Destination account"
                                )
                                Text("▾")
                            }
                        }
                        DropdownMenu(expanded = destinationMenuExpanded, onDismissRequest = { destinationMenuExpanded = false }) {
                            overview.accounts.filter { it.id != draft.accountId }.forEach { option ->
                                DropdownMenuItem(
                                    text = { Text(option.name) },
                                    onClick = {
                                        onDraftChange(draft.copy(destinationAccountId = option.id))
                                        destinationMenuExpanded = false
                                    }
                                )
                            }
                        }
                    }
                }

                OutlinedTextField(
                    value = draft.happenedAt,
                    onValueChange = { onDraftChange(draft.copy(happenedAt = it)) },
                    label = { Text(if (isRussian) "Дата и время" else "Date and time") },
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier.fillMaxWidth()
                )

                OutlinedTextField(
                    value = draft.title,
                    onValueChange = { onDraftChange(draft.copy(title = it)) },
                    label = { Text(if (isRussian) "Описание" else "Description") },
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier.fillMaxWidth()
                )

                OutlinedTextField(
                    value = draft.merchantName,
                    onValueChange = { onDraftChange(draft.copy(merchantName = it)) },
                    label = { Text(if (isRussian) "Магазин / источник" else "Merchant / source") },
                    singleLine = true,
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier.fillMaxWidth()
                )
            }
        }

        Surface(
            shape = RoundedCornerShape(20.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.62f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(14.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = if (isRussian) "Позиции" else "Items",
                        fontSize = 15.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    if (draft.direction != "transfer") {
                        TextButton(onClick = { onDraftChange(draft.copy(items = draft.items + FinanceDraftItemState())) }) {
                            Text(if (isRussian) "Добавить" else "Add")
                        }
                    }
                }

                draft.items.forEachIndexed { index, item ->
                    FinanceDraftItemEditor(
                        isRussian = isRussian,
                        index = index,
                        item = item,
                        showCategory = draft.direction != "transfer",
                        categoryOptions = categoryOptions,
                        canRemove = draft.direction != "transfer" && draft.items.size > 1,
                        onChange = { next ->
                            onDraftChange(draft.copy(items = draft.items.map { current -> if (current.id == item.id) next else current }))
                        },
                        onRemove = {
                            onDraftChange(draft.copy(items = draft.items.filterNot { current -> current.id == item.id }))
                        }
                    )
                }
            }
        }
    }
}

@Composable
private fun FinanceDraftItemEditor(
    isRussian: Boolean,
    index: Int,
    item: FinanceDraftItemState,
    showCategory: Boolean,
    categoryOptions: List<Pair<String, String>>,
    canRemove: Boolean,
    onChange: (FinanceDraftItemState) -> Unit,
    onRemove: () -> Unit
) {
    var categoryMenuExpanded by remember { mutableStateOf(false) }
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.62f),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(
                    text = if (isRussian) "Позиция ${index + 1}" else "Item ${index + 1}",
                    fontSize = 14.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                if (canRemove) {
                    Text(
                        text = if (isRussian) "Удалить" else "Remove",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable(onClick = onRemove)
                    )
                }
            }

            OutlinedTextField(
                value = item.title,
                onValueChange = { onChange(item.copy(title = it)) },
                label = { Text(if (isRussian) "Название позиции" else "Item title") },
                singleLine = true,
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier.fillMaxWidth()
            )

            OutlinedTextField(
                value = item.amount,
                onValueChange = { onChange(item.copy(amount = it)) },
                label = { Text(if (isRussian) "Сумма" else "Amount") },
                singleLine = true,
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier.fillMaxWidth()
            )

            if (showCategory) {
                Box {
                    Button(
                        onClick = { categoryMenuExpanded = true },
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(14.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.surface,
                            contentColor = MaterialTheme.colorScheme.onSurface
                        )
                    ) {
                        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                            Text(categoryOptions.firstOrNull { it.first == item.categoryId }?.second ?: if (isRussian) "Выбрать категорию" else "Choose category")
                            Text("▾")
                        }
                    }
                    DropdownMenu(expanded = categoryMenuExpanded, onDismissRequest = { categoryMenuExpanded = false }) {
                        categoryOptions.forEach { (id, label) ->
                            DropdownMenuItem(
                                text = { Text(label, maxLines = 1, overflow = TextOverflow.Ellipsis) },
                                onClick = {
                                    onChange(item.copy(categoryId = id))
                                    categoryMenuExpanded = false
                                }
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceTransactionsMonthBlock(
    isRussian: Boolean,
    loading: Boolean,
    error: String?,
    transactionsMonth: FinanceTransactionsMonth?
) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(
            text = if (isRussian) "Транзакции" else "Transactions",
            fontSize = 18.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )
        when {
            loading -> Text(
                text = if (isRussian) "Загружаем транзакции…" else "Loading transactions…",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            error != null -> Text(
                text = error,
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.error
            )
            transactionsMonth?.transactions.isNullOrEmpty() -> Text(
                text = if (isRussian) "За этот месяц операций пока нет." else "No operations for this month yet.",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            else -> transactionsMonth!!.transactions.forEach { transaction ->
                FinanceTransactionRow(isRussian = isRussian, transaction = transaction)
            }
        }
    }
}

@Composable
private fun FinanceTransactionRow(
    isRussian: Boolean,
    transaction: com.assistant.app.finance.FinanceTransaction
) {
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.66f),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(
                    text = transaction.title,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = buildString {
                        append(transaction.accountName)
                        if (!transaction.destinationAccountName.isNullOrBlank()) {
                            append(" → ")
                            append(transaction.destinationAccountName)
                        }
                    },
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = transaction.happenedAt.take(16).replace('T', ' '),
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = buildString {
                        if (!transaction.categoryName.isNullOrBlank()) {
                            append(transaction.categoryName)
                        } else if (transaction.items.isNotEmpty()) {
                            append(
                                if (isRussian) {
                                    "Позиций: ${transaction.items.size}"
                                } else {
                                    "Items: ${transaction.items.size}"
                                }
                            )
                        }
                        if (isNotEmpty()) append(" · ")
                        append(
                            when (transaction.sourceType) {
                                "photo" -> if (isRussian) "Фото" else "Photo"
                                "file" -> if (isRussian) "Файл" else "File"
                                else -> if (isRussian) "Вручную" else "Manual"
                            }
                        )
                    },
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                if (transaction.items.size > 1) {
                    Text(
                        text = transaction.items.joinToString(
                            separator = " · ",
                            limit = 3,
                            truncated = if (isRussian) "ещё" else "more"
                        ) { item -> item.title },
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
            Text(
                text = formatMoney(
                    amountMinor = when (transaction.direction) {
                        "expense" -> -kotlin.math.abs(transaction.amountMinor)
                        "income" -> kotlin.math.abs(transaction.amountMinor)
                        else -> transaction.amountMinor
                    },
                    currencyCode = transaction.currency,
                    isRussian = isRussian
                ),
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurface
            )
        }
    }
}

@Composable
private fun FinanceCategoriesBlock(
    isRussian: Boolean,
    loading: Boolean,
    error: String?,
    categories: List<FinanceCategory>
) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(
            text = if (isRussian) "Категории" else "Categories",
            fontSize = 18.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface
        )
        when {
            loading -> Text(
                text = if (isRussian) "Загружаем дерево категорий…" else "Loading category tree…",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            error != null -> Text(
                text = error,
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.error
            )
            categories.isEmpty() -> Text(
                text = if (isRussian) "Категории ещё не заведены." else "No categories yet.",
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            else -> {
                val grouped = buildFinanceCategoryForest(categories)
                listOf("expense", "income").forEach { direction ->
                    val title = if (direction == "expense") {
                        if (isRussian) "Расходы" else "Expenses"
                    } else {
                        if (isRussian) "Доходы" else "Income"
                    }
                    Surface(
                        shape = RoundedCornerShape(20.dp),
                        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            verticalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            Text(
                                text = title,
                                fontSize = 16.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            grouped.getValue(direction).forEach { node ->
                                FinanceCategoryNodeView(node = node, depth = 0)
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FinanceCategoryNodeView(
    node: FinanceCategoryNode,
    depth: Int
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Surface(
            shape = RoundedCornerShape(16.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.54f),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(start = (12 + depth * 20).dp, top = 12.dp, end = 12.dp, bottom = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Box(
                    modifier = Modifier
                        .size(10.dp)
                        .background(MaterialTheme.colorScheme.secondary, CircleShape)
                )
                Text(
                    text = node.category.name,
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }
        }
        node.children.forEach { child ->
            FinanceCategoryNodeView(node = child, depth = depth + 1)
        }
    }
}

@Composable
private fun FinanceAnalyticsBlock(isRussian: Boolean) {
    Surface(
        shape = RoundedCornerShape(22.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.72f),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Column(
            modifier = Modifier.padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                text = if (isRussian) "Аналитика" else "Analytics",
                fontSize = 18.sp,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = if (isRussian) {
                    "Аналитика пока остаётся разделом в разработке. Контракт данных и навигация уже готовы."
                } else {
                    "Analytics intentionally remains in development for now. Navigation and data contract are ready."
                },
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

private fun buildFinanceCategoryForest(categories: List<FinanceCategory>): Map<String, List<FinanceCategoryNode>> {
    val byParent = categories.groupBy { it.parentId }

    fun build(parentId: String?): List<FinanceCategoryNode> =
        byParent[parentId]
            .orEmpty()
            .sortedBy { it.displayOrder }
            .map { FinanceCategoryNode(it, build(it.id)) }

    return mapOf(
        "expense" to build(null).filter { it.category.direction == "expense" },
        "income" to build(null).filter { it.category.direction == "income" }
    )
}

private data class FinanceCreatePayload(
    val accountId: String,
    val direction: String,
    val title: String?,
    val note: String?,
    val amountMinor: Long?,
    val currency: String?,
    val happenedAt: String?,
    val categoryId: String?,
    val items: List<com.assistant.app.finance.FinanceTransactionItemDraft>,
    val destinationAccountId: String?,
    val sourceType: String,
    val merchantName: String?
)

private fun FinanceCreatePayload.toRepositoryRequest(): com.assistant.app.finance.FinanceCreateTransactionRequest =
    com.assistant.app.finance.FinanceCreateTransactionRequest(
        accountId = accountId,
        direction = direction,
        title = title,
        note = note,
        amountMinor = amountMinor,
        currency = currency,
        happenedAt = happenedAt,
        categoryId = categoryId,
        items = items,
        destinationAccountId = destinationAccountId,
        sourceType = sourceType,
        merchantName = merchantName
    )

private fun createManualFinanceDraft(
    overview: FinanceOverview,
    sourceType: String = "manual"
): FinanceDraftState {
    val account = overview.accounts.firstOrNull { it.isPrimary } ?: overview.accounts.first()
    return FinanceDraftState(
        sourceType = sourceType,
        documentKind = if (sourceType == "manual") "manual" else "image",
        accountId = account.id,
        currency = account.currency
    )
}

private fun financeDraftFromImport(
    draft: com.assistant.app.finance.FinanceImportDraft,
    overview: FinanceOverview
): FinanceDraftState {
    val account = overview.accounts.firstOrNull { it.isPrimary } ?: overview.accounts.first()
    return FinanceDraftState(
        sourceType = draft.sourceType,
        documentKind = draft.documentKind,
        direction = draft.direction,
        title = draft.title,
        merchantName = draft.merchantName.orEmpty(),
        note = draft.note.orEmpty(),
        accountId = account.id,
        currency = draft.currency,
        happenedAt = draft.happenedAt ?: isoNowLocal(),
        items = draft.items.ifEmpty {
            listOf(com.assistant.app.finance.FinanceImportDraftItem(draft.title, draft.amountMinor, null, null, null, null))
        }.map { item ->
            FinanceDraftItemState(
                title = item.title,
                amount = minorToInput(item.amountMinor),
                categoryId = item.suggestedCategoryId
            )
        }
    )
}

private fun buildFinanceCategoryOptions(
    categories: List<FinanceCategory>,
    direction: String
): List<Pair<String, String>> {
    val byId = categories.associateBy { it.id }
    return categories
        .filter { it.direction == direction }
        .sortedWith(compareBy<FinanceCategory> { categoryDepth(it, byId) }.thenBy { it.displayOrder }.thenBy { it.name })
        .map { category -> category.id to buildCategoryPath(category, byId) }
}

private fun categoryDepth(category: FinanceCategory, byId: Map<String, FinanceCategory>): Int {
    var depth = 0
    var current = category.parentId?.let(byId::get)
    while (current != null) {
        depth += 1
        current = current.parentId?.let(byId::get)
    }
    return depth
}

private fun buildCategoryPath(category: FinanceCategory, byId: Map<String, FinanceCategory>): String {
    val path = mutableListOf(category.name)
    var current = category.parentId?.let(byId::get)
    while (current != null) {
        path += current.name
        current = current.parentId?.let(byId::get)
    }
    return path.asReversed().joinToString(" › ")
}

private fun FinanceDraftState.canSave(): Boolean {
    if (accountId.isBlank()) return false
    if (direction == "transfer") {
        return destinationAccountId.isNotBlank() && (parseAmountToMinor(items.firstOrNull()?.amount.orEmpty()) ?: 0) > 0
    }
    return items.isNotEmpty() && items.all { item ->
        (parseAmountToMinor(item.amount) ?: 0) > 0
    }
}

private fun FinanceDraftState.toRepositoryPayload(): FinanceCreatePayload {
    val normalizedItems = items.mapNotNull { item ->
        val amountMinor = parseAmountToMinor(item.amount) ?: return@mapNotNull null
        if (amountMinor <= 0) return@mapNotNull null
        com.assistant.app.finance.FinanceTransactionItemDraft(
            title = item.title.ifBlank { if (direction == "income") "Поступление" else "Позиция" },
            amountMinor = amountMinor,
            categoryId = item.categoryId
        )
    }
    val totalMinor = normalizedItems.sumOf { it.amountMinor }
    return FinanceCreatePayload(
        accountId = accountId,
        direction = direction,
        title = title.ifBlank { null },
        note = note.ifBlank { null },
        amountMinor = if (direction == "transfer") parseAmountToMinor(items.firstOrNull()?.amount.orEmpty()) else totalMinor,
        currency = currency,
        happenedAt = happenedAt,
        categoryId = if (normalizedItems.size == 1) normalizedItems.first().categoryId else null,
        items = if (direction == "transfer") emptyList() else normalizedItems,
        destinationAccountId = destinationAccountId.ifBlank { null },
        sourceType = sourceType,
        merchantName = merchantName.ifBlank { null }
    )
}

private fun isoNowLocal(): String = java.time.OffsetDateTime.now().withNano(0).toString()

private fun minorToInput(amountMinor: Long): String {
    val amount = amountMinor / 100.0
    return if (kotlin.math.abs(amount % 1.0) < 0.000001) {
        amount.toLong().toString()
    } else {
        amount.toString().replace('.', ',')
    }
}

private fun bitmapToJpeg(bitmap: Bitmap): ByteArray {
    val stream = ByteArrayOutputStream()
    bitmap.compress(Bitmap.CompressFormat.JPEG, 94, stream)
    return stream.toByteArray()
}

private suspend fun readBytesFromUri(
    context: android.content.Context,
    uri: android.net.Uri
): Triple<String, String, ByteArray> = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
    val resolver = context.contentResolver
    val fileName = resolver.query(uri, arrayOf(android.provider.OpenableColumns.DISPLAY_NAME), null, null, null)
        ?.use { cursor ->
            if (cursor.moveToFirst()) cursor.getString(0) else null
        }
        ?: "document-${System.currentTimeMillis()}"
    val mimeType = resolver.getType(uri) ?: when {
        fileName.endsWith(".pdf", true) -> "application/pdf"
        fileName.endsWith(".eml", true) -> "message/rfc822"
        else -> "image/jpeg"
    }
    val bytes = resolver.openInputStream(uri)?.use { it.readBytes() }
        ?: error("Unable to open selected file.")
    Triple(fileName, mimeType, bytes)
}

private fun defaultOverviewCardIds(): List<String> = listOf(
    "total_balance",
    "card_balance",
    "cash_balance",
    "month_income",
    "month_expense",
    "month_result",
    "recent_transactions"
)

private fun financeAccountProviders(isRussian: Boolean): List<Pair<String, String>> = if (isRussian) {
    listOf(
        "tbank" to "Т-Банк",
        "sber" to "Сбер",
        "alfabank" to "Альфа",
        "vtb" to "ВТБ",
        "yandex" to "Яндекс Банк",
        "cash" to "Наличные"
    )
} else {
    listOf(
        "tbank" to "T-Bank",
        "sber" to "Sber",
        "alfabank" to "Alfa",
        "vtb" to "VTB",
        "yandex" to "Yandex Bank",
        "cash" to "Cash"
    )
}

private fun toggleOverviewCard(cards: List<String>, cardId: String, enabled: Boolean): List<String> {
    val next = cards.toMutableList()
    if (enabled) {
        if (!next.contains(cardId)) next += cardId
    } else {
        next.removeAll { it == cardId }
    }
    return next
}

private fun moveOverviewCard(cards: List<String>, cardId: String, direction: Int): List<String> {
    val next = cards.toMutableList()
    val index = next.indexOf(cardId)
    if (index == -1) return next
    val target = (index + direction).coerceIn(0, next.lastIndex)
    if (index == target) return next
    val item = next.removeAt(index)
    next.add(target, item)
    return next
}

private fun moveOverviewCardToIndex(cards: List<String>, cardId: String, targetIndex: Int): List<String> {
    val next = cards.toMutableList()
    val currentIndex = next.indexOf(cardId)
    if (currentIndex == -1) return next
    val clampedTarget = targetIndex.coerceIn(0, next.lastIndex)
    if (currentIndex == clampedTarget) return next
    val item = next.removeAt(currentIndex)
    next.add(clampedTarget, item)
    return next
}

private fun financeOverviewCardLabel(cardId: String, isRussian: Boolean): String = when (cardId) {
    "total_balance" -> if (isRussian) "Общий баланс" else "Total balance"
    "card_balance" -> if (isRussian) "На картах" else "On cards"
    "cash_balance" -> if (isRussian) "Наличные" else "Cash"
    "month_income" -> if (isRussian) "Доходы за месяц" else "Month income"
    "month_expense" -> if (isRussian) "Расходы за месяц" else "Month expense"
    "month_result" -> if (isRussian) "Результат месяца" else "Month result"
    "recent_transactions" -> if (isRussian) "Краткий список транзакций" else "Short transaction list"
    else -> cardId
}

private fun financeOverviewCardAccent(cardId: String): Color = when (cardId) {
    "total_balance" -> Color(0xFF23E08A)
    "card_balance" -> Color(0xFF5C85FF)
    "cash_balance" -> Color(0xFFD69A3F)
    "month_income" -> Color(0xFF23E08A)
    "month_expense" -> Color(0xFFFF6F8E)
    "month_result" -> Color(0xFFFF6F8E)
    else -> Color.White
}

@Composable
private fun FinanceTabStrip(
    tabs: List<FinanceTab>,
    labels: Map<FinanceTab, String>,
    activeTab: FinanceTab,
    onTabChange: (FinanceTab) -> Unit
) {
    val scrollState = rememberScrollState()

    Row(
        modifier = Modifier.horizontalScroll(scrollState),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        tabs.forEach { tab ->
            Surface(
                modifier = Modifier.clickable { onTabChange(tab) },
                shape = RoundedCornerShape(16.dp),
                color = if (activeTab == tab) {
                    MaterialTheme.colorScheme.secondary.copy(alpha = 0.16f)
                } else {
                    MaterialTheme.colorScheme.surface.copy(alpha = 0.72f)
                },
                border = BorderStroke(
                    1.dp,
                    if (activeTab == tab) {
                        MaterialTheme.colorScheme.secondary.copy(alpha = 0.32f)
                    } else {
                        MaterialTheme.colorScheme.outline
                    }
                )
            ) {
                Text(
                    text = labels.getValue(tab),
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp),
                    fontSize = 14.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = if (activeTab == tab) {
                        MaterialTheme.colorScheme.onSurface
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    }
                )
            }
        }
    }
}

@Composable
private fun DashboardPlaceholderCard(
    section: DashboardSection,
    copy: DashboardSectionText,
    subsectionLabel: String,
    userName: String,
    compact: Boolean,
    userEmail: String,
    onSignOut: () -> Unit,
    logoutLabel: String
) {
    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        AnimatedContent(
            targetState = section,
            transitionSpec = {
                fadeIn(animationSpec = tween(280, delayMillis = 40)) togetherWith
                    fadeOut(animationSpec = tween(180))
            },
            label = "dashboard-section"
        ) { _ ->
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = if (compact) 300.dp else 420.dp),
                verticalArrangement = Arrangement.spacedBy(0.dp)
            ) {
                Text(
                    text = copy.eyebrow.uppercase(),
                    fontSize = 11.sp,
                    letterSpacing = 1.sp,
                    color = MaterialTheme.colorScheme.secondary
                )
                Spacer(modifier = Modifier.height(10.dp))
                Text(
                    text = "${copy.title}, $userName.",
                    fontSize = if (compact) 28.sp else 38.sp,
                    lineHeight = if (compact) 32.sp else 42.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(18.dp))
                Surface(
                    shape = RoundedCornerShape(999.dp),
                    color = MaterialTheme.colorScheme.secondary.copy(alpha = 0.14f)
                ) {
                    Text(
                        text = copy.badge,
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 7.dp),
                        fontSize = 11.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.secondary
                    )
                }
                Spacer(modifier = Modifier.height(16.dp))
                Text(
                    text = copy.note,
                    fontSize = 15.sp,
                    lineHeight = 24.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(modifier = Modifier.height(10.dp))
                Text(
                    text = subsectionLabel,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                if (section == DashboardSection.Settings) {
                    Spacer(modifier = Modifier.height(18.dp))
                    Button(
                        onClick = onSignOut,
                        shape = RoundedCornerShape(16.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = Color(0xFF10B981),
                            contentColor = Color(0xFF04120E)
                        ),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            text = logoutLabel,
                            color = Color(0xFF04120E),
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun SettingsSectionCard(
    isRussian: Boolean,
    subsection: DashboardSubsection,
    snapshot: SettingsSnapshot?,
    loading: Boolean,
    banner: Pair<Boolean, String>?,
    onBannerChange: (Pair<Boolean, String>?) -> Unit,
    onReload: () -> Unit,
    onSaveDisplayName: (String) -> Unit,
    onSaveEmail: (String) -> Unit,
    onSaveGemini: (String) -> Unit,
    onSavePassword: (String) -> Unit,
    onLinkGoogle: () -> Unit,
    onUnlinkGoogle: (String) -> Unit,
    onDeleteAccount: () -> Unit,
    onSignOut: () -> Unit
) {
    var displayName by remember(snapshot?.displayName) { mutableStateOf(snapshot?.displayName.orEmpty()) }
    var email by remember(snapshot?.email) { mutableStateOf(snapshot?.email.orEmpty()) }
    var geminiKey by remember(snapshot?.geminiApiKey) { mutableStateOf(snapshot?.geminiApiKey.orEmpty()) }
    var password by remember { mutableStateOf("") }
    var passwordConfirm by remember { mutableStateOf("") }
    var deleteConfirm by remember { mutableStateOf("") }
    var deleteDialogOpen by rememberSaveable { mutableStateOf(false) }

    val googleIdentity = snapshot?.identities?.firstOrNull { it.provider == "google" }
    val hasGoogleLinked = googleIdentity != null
    val needsDeleteWord = if (isRussian) "удалить" else "delete"

    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(18.dp)) {
            if (banner != null) {
                Surface(
                    shape = RoundedCornerShape(18.dp),
                    color = if (banner.first) {
                        Color(0xFF10B981).copy(alpha = 0.16f)
                    } else {
                        Color(0xFFFB7185).copy(alpha = 0.16f)
                    },
                    border = BorderStroke(
                        1.dp,
                        if (banner.first) Color(0xFF10B981).copy(alpha = 0.35f) else Color(0xFFFB7185).copy(alpha = 0.35f)
                    )
                ) {
                    Text(
                        text = banner.second,
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
                        color = MaterialTheme.colorScheme.onSurface,
                        fontSize = 13.sp,
                        lineHeight = 19.sp
                    )
                }
            }

            if (loading) {
                Text(
                    text = if (isRussian) "Загружаем настройки…" else "Loading settings…",
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            when (subsection) {
                DashboardSubsection.Profile -> {
                    Text(
                        text = if (isRussian) "Профиль" else "Profile",
                        fontSize = 24.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = if (isRussian) {
                            "Имя профиля и почта управляются через Supabase Auth и таблицу profiles."
                        } else {
                            "Your profile name and email are managed through Supabase Auth and the profiles table."
                        },
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        lineHeight = 21.sp
                    )

                    OutlinedTextField(
                        value = displayName,
                        onValueChange = {
                            displayName = it
                            onBannerChange(null)
                        },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(if (isRussian) "Имя" else "Name") },
                        placeholder = { Text(if (isRussian) "Как к вам обращаться?" else "How should we address you?") }
                    )
                    Button(
                        onClick = {
                            if (displayName.trim().isBlank()) {
                                onBannerChange(false to if (isRussian) "Имя не может быть пустым." else "Name cannot be empty.")
                            } else {
                                onSaveDisplayName(displayName.trim())
                            }
                        },
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Text(if (isRussian) "Сохранить имя" else "Save name")
                    }

                    OutlinedTextField(
                        value = email,
                        onValueChange = {
                            email = it
                            onBannerChange(null)
                        },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Email") },
                        placeholder = { Text("you@example.com") }
                    )
                    Button(
                        onClick = {
                            if (!email.contains("@") || !email.contains(".")) {
                                onBannerChange(false to if (isRussian) "Введите корректный email." else "Enter a valid email.")
                            } else {
                                onSaveEmail(email.trim())
                            }
                        },
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Text(if (isRussian) "Сменить почту" else "Change email")
                    }
                }

                DashboardSubsection.Preferences -> {
                    Text(
                        text = if (isRussian) "Параметры и интеграции" else "Preferences and integrations",
                        fontSize = 24.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = if (isRussian) {
                            "Сохраняем Gemini API Key в Supabase. Позже используем его в AI-сценариях."
                        } else {
                            "The Gemini API key is stored in Supabase and will be used later in AI workflows."
                        },
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        lineHeight = 21.sp
                    )

                    OutlinedTextField(
                        value = geminiKey,
                        onValueChange = {
                            geminiKey = it
                            onBannerChange(null)
                        },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Gemini API Key") },
                        placeholder = { Text("AIza...") },
                        visualTransformation = PasswordVisualTransformation()
                    )
                    Button(
                        onClick = { onSaveGemini(geminiKey.trim()) },
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Text(if (isRussian) "Сохранить ключ" else "Save key")
                    }

                    Text(
                        text = if (isRussian) {
                            "Бесплатный ключ можно получить в Google AI Studio: https://aistudio.google.com/app/apikey"
                        } else {
                            "You can get a free key in Google AI Studio: https://aistudio.google.com/app/apikey"
                        },
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        fontSize = 13.sp,
                        lineHeight = 20.sp
                    )
                }

                DashboardSubsection.Security -> {
                    Text(
                        text = if (isRussian) "Безопасность" else "Security",
                        fontSize = 24.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = if (isRussian) {
                            "Пароль, связанные identity и критические действия аккаунта."
                        } else {
                            "Password, linked identities, and destructive account actions."
                        },
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        lineHeight = 21.sp
                    )

                    OutlinedTextField(
                        value = password,
                        onValueChange = {
                            password = it
                            onBannerChange(null)
                        },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(if (isRussian) "Новый пароль" else "New password") },
                        placeholder = { Text(if (isRussian) "Минимум 8 символов" else "At least 8 characters") },
                        visualTransformation = PasswordVisualTransformation()
                    )
                    OutlinedTextField(
                        value = passwordConfirm,
                        onValueChange = {
                            passwordConfirm = it
                            onBannerChange(null)
                        },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(if (isRussian) "Повторите пароль" else "Confirm password") },
                        placeholder = { Text(if (isRussian) "Минимум 8 символов" else "At least 8 characters") },
                        visualTransformation = PasswordVisualTransformation()
                    )
                    Button(
                        onClick = {
                            when {
                                password.length < 8 -> onBannerChange(false to if (isRussian) "Пароль должен содержать минимум 8 символов." else "Password must contain at least 8 characters.")
                                password != passwordConfirm -> onBannerChange(false to if (isRussian) "Пароли не совпадают." else "Passwords do not match.")
                                else -> onSavePassword(password)
                            }
                        },
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Text(if (isRussian) "Обновить пароль" else "Update password")
                    }

                    Surface(
                        shape = RoundedCornerShape(20.dp),
                        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.55f),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(
                                modifier = Modifier.weight(1f),
                                verticalArrangement = Arrangement.spacedBy(6.dp)
                            ) {
                                Text(
                                    text = if (isRussian) "Google аккаунт" else "Google account",
                                    fontWeight = FontWeight.SemiBold,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                                Text(
                                    text = if (hasGoogleLinked) {
                                        if (isRussian) "Google уже привязан к аккаунту." else "Google is already linked to the account."
                                    } else {
                                        if (isRussian) "Привяжите Google для быстрого входа." else "Link Google for faster sign-in."
                                    },
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    fontSize = 13.sp,
                                    lineHeight = 19.sp
                                )
                            }
                            Spacer(modifier = Modifier.width(12.dp))
                            Button(
                                onClick = {
                                    if (hasGoogleLinked) {
                                        val linkedCount = snapshot?.identities?.size ?: 0
                                        if (linkedCount < 2) {
                                            onBannerChange(false to if (isRussian) {
                                                "Нельзя отвязать единственный способ входа. Сначала задайте пароль или добавьте другую identity."
                                            } else {
                                                "You cannot unlink the only sign-in method. Set a password or add another identity first."
                                            })
                                        } else {
                                            googleIdentity?.identityId?.let(onUnlinkGoogle)
                                        }
                                    } else {
                                        onLinkGoogle()
                                    }
                                },
                                shape = RoundedCornerShape(16.dp)
                            ) {
                                Text(
                                    if (hasGoogleLinked) {
                                        if (isRussian) "Отключить" else "Disconnect"
                                    } else {
                                        if (isRussian) "Подключить Google" else "Connect Google"
                                    }
                                )
                            }
                        }
                    }

                    Surface(
                        shape = RoundedCornerShape(20.dp),
                        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.55f),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(
                                modifier = Modifier.weight(1f),
                                verticalArrangement = Arrangement.spacedBy(6.dp)
                            ) {
                                Text(
                                    text = if (isRussian) "Выход из аккаунта" else "Sign out",
                                    fontWeight = FontWeight.SemiBold,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                                Text(
                                    text = if (isRussian) "Текущая сессия будет завершена на этом устройстве." else "The current session will be closed on this device.",
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    fontSize = 13.sp,
                                    lineHeight = 19.sp
                                )
                            }
                            Spacer(modifier = Modifier.width(12.dp))
                            Button(
                                onClick = onSignOut,
                                shape = RoundedCornerShape(16.dp)
                            ) {
                                Text(if (isRussian) "Выйти" else "Sign out")
                            }
                        }
                    }

                    Surface(
                        shape = RoundedCornerShape(20.dp),
                        color = Color(0xFFFB7185).copy(alpha = 0.12f),
                        border = BorderStroke(1.dp, Color(0xFFFB7185).copy(alpha = 0.35f))
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            Text(
                                text = if (isRussian) "Удаление аккаунта" else "Delete account",
                                fontWeight = FontWeight.SemiBold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = if (isRussian) {
                                    "Действие необратимо. Все пользовательские данные и сессии будут удалены."
                                } else {
                                    "This action is irreversible. All user data and sessions will be removed."
                                },
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                fontSize = 13.sp,
                                lineHeight = 19.sp
                            )
                            Button(
                                onClick = { deleteDialogOpen = true },
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = Color(0xFFFB7185).copy(alpha = 0.2f),
                                    contentColor = MaterialTheme.colorScheme.onSurface
                                ),
                                shape = RoundedCornerShape(16.dp)
                            ) {
                                Text(if (isRussian) "Удалить аккаунт" else "Delete account")
                            }
                        }
                    }
                }

                else -> {
                    Text(
                        text = if (isRussian) "Настройки" else "Settings",
                        fontSize = 24.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = if (isRussian) "Выберите подраздел настроек выше." else "Choose a settings subsection above.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            if (snapshot == null && !loading) {
                Button(
                    onClick = onReload,
                    shape = RoundedCornerShape(16.dp)
                ) {
                    Text(if (isRussian) "Повторить загрузку" else "Retry")
                }
            }
        }
    }

    if (deleteDialogOpen) {
        Dialog(onDismissRequest = { deleteDialogOpen = false }) {
            Surface(
                shape = RoundedCornerShape(24.dp),
                color = MaterialTheme.colorScheme.surface,
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
            ) {
                Column(
                    modifier = Modifier.padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    Text(
                        text = if (isRussian) "Подтвердите удаление аккаунта" else "Confirm account deletion",
                        fontSize = 22.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = if (isRussian) {
                            "Введите слово «удалить», чтобы безвозвратно удалить аккаунт."
                        } else {
                            "Type “delete” to permanently remove the account."
                        },
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        lineHeight = 20.sp
                    )
                    OutlinedTextField(
                        value = deleteConfirm,
                        onValueChange = { deleteConfirm = it },
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text(if (isRussian) "Подтверждение" else "Confirmation") },
                        placeholder = { Text(needsDeleteWord) }
                    )
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.End,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Button(
                            onClick = { deleteDialogOpen = false },
                            shape = RoundedCornerShape(16.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.surfaceVariant,
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(if (isRussian) "Отмена" else "Cancel")
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Button(
                            onClick = {
                                if (deleteConfirm.trim().lowercase() != needsDeleteWord) {
                                    onBannerChange(false to if (isRussian) {
                                        "Введите слово «удалить» для подтверждения."
                                    } else {
                                        "Type “delete” to confirm."
                                    })
                                } else {
                                    deleteDialogOpen = false
                                    onDeleteAccount()
                                }
                            },
                            shape = RoundedCornerShape(16.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = Color(0xFFFB7185).copy(alpha = 0.2f),
                                contentColor = MaterialTheme.colorScheme.onSurface
                            )
                        ) {
                            Text(if (isRussian) "Подтвердить" else "Confirm")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun MetricCard(item: MetricItem, modifier: Modifier = Modifier) {
    GlassSurface(modifier = modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(text = item.label, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(text = item.value, fontSize = 28.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
            Text(text = item.detail, fontSize = 12.sp, lineHeight = 18.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun DashboardActivityCard(t: UiText, activities: List<ActivityItem>) {
    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Top
            ) {
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(text = t.activityLabel.uppercase(), fontSize = 11.sp, color = MaterialTheme.colorScheme.secondary)
                    Text(text = t.activityTitle, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                }
                Text(text = t.activityHint, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }

            Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                activities.forEach { item ->
                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp), verticalAlignment = Alignment.Top) {
                        Box(
                            modifier = Modifier
                                .padding(top = 8.dp)
                                .size(12.dp)
                                .background(
                                    brush = Brush.linearGradient(
                                        colors = listOf(MaterialTheme.colorScheme.secondary, MaterialTheme.colorScheme.tertiary)
                                    ),
                                    shape = CircleShape
                                )
                        )
                        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(text = item.title, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                                Text(text = item.time, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Text(text = item.detail, fontSize = 13.sp, lineHeight = 20.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DashboardFocusCard(t: UiText, focusItems: List<FocusItem>) {
    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Text(text = t.focusLabel.uppercase(), fontSize = 11.sp, color = MaterialTheme.colorScheme.secondary)
            Text(text = t.focusTitle, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
            focusItems.forEach { item ->
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(text = item.title, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                    Text(text = item.detail, fontSize = 13.sp, lineHeight = 20.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
private fun DashboardInsightCard(t: UiText) {
    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(text = t.insightLabel.uppercase(), fontSize = 11.sp, color = MaterialTheme.colorScheme.secondary)
            Text(text = t.insightTitle, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
            Surface(
                shape = RoundedCornerShape(20.dp),
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.72f),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
            ) {
                Column(
                    modifier = Modifier.padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text(text = t.insightLead, fontSize = 15.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                    Text(text = t.insightBody, fontSize = 13.sp, lineHeight = 20.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
private fun DashboardStateCard(t: UiText) {
    val entries = listOf(
        "Web" to "dashboard shell ready",
        "Windows" to "overview in progress",
        "Android" to "overview in progress"
    )

    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Text(text = t.stateLabel.uppercase(), fontSize = 11.sp, color = MaterialTheme.colorScheme.secondary)
            Text(text = t.stateTitle, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
            entries.forEach { (label, value) ->
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text(text = label, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(text = value, fontSize = 13.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurface)
                }
            }
        }
    }
}

@Composable
private fun DashboardStatusBadge(label: String) {
    Surface(
        shape = RoundedCornerShape(999.dp),
        color = MaterialTheme.colorScheme.secondary.copy(alpha = 0.18f)
    ) {
        Text(
            text = label,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
            fontSize = 11.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.secondary
        )
    }
}

@Composable
private fun DashboardNavChip(text: String, active: Boolean, onClick: () -> Unit) {
    val background = if (active) MaterialTheme.colorScheme.surface.copy(alpha = 0.86f) else Color.Transparent
    val border = if (active) BorderStroke(1.dp, MaterialTheme.colorScheme.outline) else null

    Surface(
        modifier = Modifier.clickable(onClick = onClick),
        shape = RoundedCornerShape(16.dp),
        color = background,
        border = border
    ) {
        Text(
            text = text,
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold,
            color = if (active) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
private fun DashboardSectionNavButton(
    section: DashboardSection,
    text: String,
    active: Boolean,
    onClick: () -> Unit
) {
    val background = if (active) MaterialTheme.colorScheme.surface.copy(alpha = 0.86f) else Color.Transparent
    val border = if (active) BorderStroke(1.dp, MaterialTheme.colorScheme.outline) else null

    Surface(
        modifier = Modifier.clickable(onClick = onClick),
        shape = RoundedCornerShape(16.dp),
        color = background,
        border = border
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(
                shape = RoundedCornerShape(12.dp),
                color = if (active) {
                    MaterialTheme.colorScheme.secondary.copy(alpha = 0.16f)
                } else {
                    MaterialTheme.colorScheme.surface.copy(alpha = 0.5f)
                }
            ) {
                Box(
                    modifier = Modifier.size(32.dp),
                    contentAlignment = Alignment.Center
                ) {
                    DashboardSectionIcon(section = section, active = active)
                }
            }
            Text(
                text = text,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold,
                color = if (active) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun DashboardBottomBar(
    modifier: Modifier = Modifier,
    current: DashboardSection,
    labels: Map<DashboardSection, DashboardSectionText>,
    onSelect: (DashboardSection) -> Unit
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(999.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Row(
            modifier = Modifier
                .horizontalScroll(rememberScrollState())
                .padding(horizontal = 6.dp, vertical = 6.dp),
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            DashboardSection.entries.forEach { item ->
                val active = current == item
                Surface(
                    modifier = Modifier.widthIn(min = 70.dp),
                    shape = RoundedCornerShape(999.dp),
                    color = if (active) {
                        MaterialTheme.colorScheme.secondary.copy(alpha = 0.14f)
                    } else {
                        Color.Transparent
                    }
                ) {
                    Box(
                        modifier = Modifier
                            .clickable { onSelect(item) }
                            .padding(horizontal = 12.dp, vertical = 6.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Box(
                                modifier = Modifier.size(18.dp),
                                contentAlignment = Alignment.Center
                            ) {
                                DashboardSectionIcon(section = item, active = active)
                            }
                            Text(
                                text = labels[item]!!.label,
                                fontSize = 10.sp,
                                fontWeight = FontWeight.SemiBold,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                                color = if (active) {
                                    MaterialTheme.colorScheme.onSurface
                                } else {
                                    MaterialTheme.colorScheme.onSurfaceVariant
                                }
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DashboardSectionIcon(section: DashboardSection, active: Boolean) {
    val tint = if (active) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant
    Canvas(modifier = Modifier.size(18.dp)) {
        val strokeWidth = 1.8.dp.toPx()
        when (section) {
            DashboardSection.Home -> {
                drawLine(tint, Offset(size.width * 0.18f, size.height * 0.48f), Offset(size.width * 0.5f, size.height * 0.2f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.82f, size.height * 0.48f), Offset(size.width * 0.5f, size.height * 0.2f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.26f, size.height * 0.44f), Offset(size.width * 0.26f, size.height * 0.8f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.74f, size.height * 0.44f), Offset(size.width * 0.74f, size.height * 0.8f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.26f, size.height * 0.8f), Offset(size.width * 0.74f, size.height * 0.8f), strokeWidth, cap = StrokeCap.Round)
            }
            DashboardSection.Finance -> {
                drawRoundRect(
                    color = tint,
                    topLeft = Offset(size.width * 0.14f, size.height * 0.22f),
                    size = Size(size.width * 0.72f, size.height * 0.56f),
                    cornerRadius = CornerRadius(4.dp.toPx(), 4.dp.toPx()),
                    style = Stroke(width = strokeWidth)
                )
                drawLine(tint, Offset(size.width * 0.14f, size.height * 0.42f), Offset(size.width * 0.86f, size.height * 0.42f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.3f, size.height * 0.58f), Offset(size.width * 0.44f, size.height * 0.58f), strokeWidth, cap = StrokeCap.Round)
            }
            DashboardSection.Health -> {
                val path = Path().apply {
                    moveTo(size.width * 0.5f, size.height * 0.78f)
                    cubicTo(size.width * 0.14f, size.height * 0.5f, size.width * 0.12f, size.height * 0.22f, size.width * 0.34f, size.height * 0.2f)
                    cubicTo(size.width * 0.44f, size.height * 0.2f, size.width * 0.49f, size.height * 0.28f, size.width * 0.5f, size.height * 0.32f)
                    cubicTo(size.width * 0.51f, size.height * 0.28f, size.width * 0.56f, size.height * 0.2f, size.width * 0.66f, size.height * 0.2f)
                    cubicTo(size.width * 0.88f, size.height * 0.22f, size.width * 0.86f, size.height * 0.5f, size.width * 0.5f, size.height * 0.78f)
                }
                drawPath(path = path, color = tint, style = Stroke(width = strokeWidth))
            }
            DashboardSection.Tasks -> {
                repeat(3) { index ->
                    val y = size.height * (0.28f + index * 0.24f)
                    drawCircle(tint, radius = 1.3.dp.toPx(), center = Offset(size.width * 0.22f, y))
                    drawLine(tint, Offset(size.width * 0.34f, y), Offset(size.width * 0.82f, y), strokeWidth, cap = StrokeCap.Round)
                }
            }
            DashboardSection.Settings -> {
                drawCircle(tint, radius = size.minDimension * 0.17f, center = Offset(size.width / 2f, size.height / 2f), style = Stroke(width = strokeWidth))
                drawLine(tint, Offset(size.width * 0.5f, size.height * 0.08f), Offset(size.width * 0.5f, size.height * 0.24f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.5f, size.height * 0.76f), Offset(size.width * 0.5f, size.height * 0.92f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.08f, size.height * 0.5f), Offset(size.width * 0.24f, size.height * 0.5f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.76f, size.height * 0.5f), Offset(size.width * 0.92f, size.height * 0.5f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.24f, size.height * 0.24f), Offset(size.width * 0.34f, size.height * 0.34f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.66f, size.height * 0.66f), Offset(size.width * 0.76f, size.height * 0.76f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.24f, size.height * 0.76f), Offset(size.width * 0.34f, size.height * 0.66f), strokeWidth, cap = StrokeCap.Round)
                drawLine(tint, Offset(size.width * 0.66f, size.height * 0.34f), Offset(size.width * 0.76f, size.height * 0.24f), strokeWidth, cap = StrokeCap.Round)
            }
        }
    }
}

@Composable
private fun GlassSurface(
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit
) {
    Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.78f),
        shape = RoundedCornerShape(26.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            content = content
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AuthScreen(
    isDark: Boolean,
    isRussian: Boolean,
    themeMode: ThemeMode,
    onThemeChange: (ThemeMode) -> Unit,
    onLanguageChange: (Boolean) -> Unit,
    state: AuthUiState,
    onEmailChange: (String) -> Unit,
    onPasswordChange: (String) -> Unit,
    onPasswordConfirmChange: (String) -> Unit,
    onModeChange: (AuthMode) -> Unit,
    onSignIn: () -> Unit,
    onSignUp: () -> Unit,
    onResetPassword: () -> Unit,
    onUpdatePassword: () -> Unit,
    onGoogle: () -> Unit,
    onRememberDeviceChange: (Boolean) -> Unit,
    isGoogleAuthEnabled: Boolean,
    t: UiText
) {
    BoxWithConstraints(
        modifier = Modifier
            .fillMaxSize()
            .statusBarsPadding()
            .padding(24.dp)
    ) {
        val isCompact = maxWidth < 600.dp

        Column(modifier = Modifier.fillMaxSize()) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "ASSISTANT",
                    fontSize = 12.sp,
                    letterSpacing = 2.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onBackground
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    CompactThemeSelector(
                        themeMode = themeMode,
                        onThemeChange = onThemeChange
                    )
                    LanguageMenu(
                        isRussian = isRussian,
                        onLanguageChange = onLanguageChange
                    )
                }
            }

            Spacer(modifier = Modifier.height(32.dp))

            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                AuthCard(
                    t = t,
                    isDark = isDark,
                    state = state,
                    onEmailChange = onEmailChange,
                    onPasswordChange = onPasswordChange,
                    onPasswordConfirmChange = onPasswordConfirmChange,
                    onModeChange = onModeChange,
                    onSignIn = onSignIn,
                    onSignUp = onSignUp,
                    onResetPassword = onResetPassword,
                    onUpdatePassword = onUpdatePassword,
                    onGoogle = onGoogle,
                    onRememberDeviceChange = onRememberDeviceChange,
                    isGoogleAuthEnabled = isGoogleAuthEnabled,
                    modifier = if (isCompact) Modifier.fillMaxWidth() else Modifier.width(420.dp)
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AuthCard(
    t: UiText,
    isDark: Boolean,
    state: AuthUiState,
    onEmailChange: (String) -> Unit,
    onPasswordChange: (String) -> Unit,
    onPasswordConfirmChange: (String) -> Unit,
    onModeChange: (AuthMode) -> Unit,
    onSignIn: () -> Unit,
    onSignUp: () -> Unit,
    onResetPassword: () -> Unit,
    onUpdatePassword: () -> Unit,
    onGoogle: () -> Unit,
    onRememberDeviceChange: (Boolean) -> Unit,
    isGoogleAuthEnabled: Boolean,
    modifier: Modifier
) {
    var emailTouched by remember(state.mode) { mutableStateOf(false) }
    var passwordTouched by remember(state.mode) { mutableStateOf(false) }
    var confirmTouched by remember(state.mode) { mutableStateOf(false) }
    var emailHadFocus by remember(state.mode) { mutableStateOf(false) }
    var passwordHadFocus by remember(state.mode) { mutableStateOf(false) }
    var confirmHadFocus by remember(state.mode) { mutableStateOf(false) }
    var showPassword by remember(state.mode) { mutableStateOf(false) }
    var showConfirmPassword by remember(state.mode) { mutableStateOf(false) }
    val emailError = remember(state.email, state.mode, emailTouched) {
        if (!emailTouched || state.mode == AuthMode.ResetPassword) null else validateEmailError(state.email, t)
    }
    val passwordError = remember(state.email, state.password, state.mode, passwordTouched) {
        if (!passwordTouched || state.mode == AuthMode.ForgotPassword) null else validatePasswordError(state.email, state.password, t)
    }
    val confirmError = remember(state.password, state.passwordConfirm, state.mode, confirmTouched) {
        if (!confirmTouched || (state.mode != AuthMode.Register && state.mode != AuthMode.ResetPassword)) null
        else validatePasswordConfirmError(state.password, state.passwordConfirm, t)
    }

    Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(28.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Column(
            modifier = Modifier.padding(22.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(
                text = if (state.mode == AuthMode.Register) t.registerTab else t.loginTab,
                fontSize = 22.sp,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = if (state.mode == AuthMode.Register) {
                    if (t === RuText) "Один аккаунт для Web, Android и Windows." else "One account for Web, Android, and Windows."
                } else {
                    if (t === RuText) "Войдите, чтобы продолжить работу." else "Sign in to continue."
                },
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            if (isGoogleAuthEnabled && (state.mode == AuthMode.Login || state.mode == AuthMode.Register)) {
                Button(
                    onClick = onGoogle,
                    enabled = !state.isLoading,
                    shape = RoundedCornerShape(14.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = MaterialTheme.colorScheme.surfaceVariant,
                        contentColor = MaterialTheme.colorScheme.onSurface
                    ),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
                    contentPadding = ButtonDefaults.ContentPadding,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(text = t.google)
                }
                Text(
                    text = if (t === RuText) "или" else "or",
                    fontSize = 11.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.CenterHorizontally)
                )
            }

            AnimatedContent(
                targetState = state.mode,
                transitionSpec = {
                    fadeIn(animationSpec = tween(220)) togetherWith fadeOut(animationSpec = tween(160))
                },
                label = "auth_mode"
            ) { activeMode ->
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        AuthTab(text = t.loginTab, active = activeMode == AuthMode.Login, onClick = { onModeChange(AuthMode.Login) })
                        AuthTab(text = t.registerTab, active = activeMode == AuthMode.Register, onClick = { onModeChange(AuthMode.Register) })
                    }

                    if (activeMode != AuthMode.ResetPassword) {
                        Text(text = t.email, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        OutlinedTextField(
                            value = state.email,
                            onValueChange = onEmailChange,
                            placeholder = { Text(t.emailPlaceholder) },
                            singleLine = true,
                            shape = RoundedCornerShape(14.dp),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedContainerColor = if (isDark) DarkInput else LightInput,
                                unfocusedContainerColor = if (isDark) DarkInput else LightInput,
                                focusedBorderColor = MaterialTheme.colorScheme.outline,
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                                focusedTextColor = MaterialTheme.colorScheme.onSurface,
                                unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                                focusedPlaceholderColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                unfocusedPlaceholderColor = MaterialTheme.colorScheme.onSurfaceVariant
                            ),
                            modifier = Modifier
                                .fillMaxWidth()
                                .onFocusChanged {
                                    if (it.isFocused) {
                                        emailHadFocus = true
                                    } else if (emailHadFocus) {
                                        emailTouched = true
                                    }
                                }
                        )
                        if (emailError != null) {
                            Text(text = emailError, fontSize = 12.sp, color = MaterialTheme.colorScheme.error)
                        }
                    }

                    if (activeMode != AuthMode.ForgotPassword) {
                        Text(text = t.password, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        OutlinedTextField(
                            value = state.password,
                            onValueChange = onPasswordChange,
                            placeholder = { Text(t.passwordPlaceholder) },
                            singleLine = true,
                            visualTransformation = if (showPassword) VisualTransformation.None else PasswordVisualTransformation(),
                            trailingIcon = {
                                PasswordVisibilityButton(
                                    visible = showPassword,
                                    onClick = { showPassword = !showPassword },
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            },
                            shape = RoundedCornerShape(14.dp),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedContainerColor = if (isDark) DarkInput else LightInput,
                                unfocusedContainerColor = if (isDark) DarkInput else LightInput,
                                focusedBorderColor = MaterialTheme.colorScheme.outline,
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                                focusedTextColor = MaterialTheme.colorScheme.onSurface,
                                unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                                focusedPlaceholderColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                unfocusedPlaceholderColor = MaterialTheme.colorScheme.onSurfaceVariant
                            ),
                            modifier = Modifier
                                .fillMaxWidth()
                                .onFocusChanged {
                                    if (it.isFocused) {
                                        passwordHadFocus = true
                                    } else if (passwordHadFocus) {
                                        passwordTouched = true
                                    }
                                }
                        )
                        if (passwordError != null) {
                            Text(text = passwordError, fontSize = 12.sp, color = MaterialTheme.colorScheme.error)
                        }
                    }

                    if (activeMode == AuthMode.Register || activeMode == AuthMode.ResetPassword) {
                        Text(text = t.passwordConfirm, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        OutlinedTextField(
                            value = state.passwordConfirm,
                            onValueChange = onPasswordConfirmChange,
                            placeholder = { Text(t.passwordConfirmPlaceholder) },
                            singleLine = true,
                            visualTransformation = if (showConfirmPassword) VisualTransformation.None else PasswordVisualTransformation(),
                            trailingIcon = {
                                PasswordVisibilityButton(
                                    visible = showConfirmPassword,
                                    onClick = { showConfirmPassword = !showConfirmPassword },
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            },
                            shape = RoundedCornerShape(14.dp),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedContainerColor = if (isDark) DarkInput else LightInput,
                                unfocusedContainerColor = if (isDark) DarkInput else LightInput,
                                focusedBorderColor = MaterialTheme.colorScheme.outline,
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                                focusedTextColor = MaterialTheme.colorScheme.onSurface,
                                unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                                focusedPlaceholderColor = MaterialTheme.colorScheme.onSurfaceVariant,
                                unfocusedPlaceholderColor = MaterialTheme.colorScheme.onSurfaceVariant
                            ),
                            modifier = Modifier
                                .fillMaxWidth()
                                .onFocusChanged {
                                    if (it.isFocused) {
                                        confirmHadFocus = true
                                    } else if (confirmHadFocus) {
                                        confirmTouched = true
                                    }
                                }
                        )
                        if (confirmError != null) {
                            Text(text = confirmError, fontSize = 12.sp, color = MaterialTheme.colorScheme.error)
                        }
                    }

                    if (activeMode == AuthMode.Login) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Checkbox(
                                    checked = state.rememberDevice,
                                    onCheckedChange = onRememberDeviceChange
                                )
                                Text(text = t.remember, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Text(
                                text = t.forgot,
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.clickable { onModeChange(AuthMode.ForgotPassword) }
                            )
                        }
                    }

                    val buttonColors = if (isDark) {
                        ButtonDefaults.buttonColors(containerColor = Color(0xFF00DC82), contentColor = Color(0xFF04120E))
                    } else {
                        ButtonDefaults.buttonColors(containerColor = Color(0xFF10B981), contentColor = Color(0xFF04120E))
                    }

                    val primaryLabel = when (activeMode) {
                        AuthMode.Login -> t.cta
                        AuthMode.Register -> t.registerTab
                        AuthMode.ForgotPassword -> t.sendReset
                        AuthMode.ResetPassword -> t.updatePassword
                    }
                    val primaryAction = when (activeMode) {
                        AuthMode.Login -> onSignIn
                        AuthMode.Register -> onSignUp
                        AuthMode.ForgotPassword -> onResetPassword
                        AuthMode.ResetPassword -> onUpdatePassword
                    }

                    Button(
                        onClick = primaryAction,
                        enabled = !state.isLoading,
                        shape = RoundedCornerShape(14.dp),
                        colors = buttonColors,
                        contentPadding = ButtonDefaults.ContentPadding,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(text = primaryLabel)
                    }
                    if (activeMode == AuthMode.ForgotPassword || activeMode == AuthMode.ResetPassword) {
                        Text(
                            text = t.backToLogin,
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.clickable { onModeChange(AuthMode.Login) }
                        )
                    } else {
                        Text(
                            text = if (activeMode == AuthMode.Register) {
                                if (t === RuText) "Уже есть аккаунт? Вход" else "Already have an account? Sign in"
                            } else {
                                if (t === RuText) "Нет аккаунта? Регистрация" else "No account yet? Register"
                            },
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.clickable {
                                onModeChange(if (activeMode == AuthMode.Register) AuthMode.Login else AuthMode.Register)
                            }
                        )
                    }
                }
            }

            if (!state.errorMessage.isNullOrBlank()) {
                Surface(
                    color = MaterialTheme.colorScheme.error.copy(alpha = 0.08f),
                    shape = RoundedCornerShape(14.dp),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.error.copy(alpha = 0.35f))
                ) {
                    Text(
                        text = state.errorMessage ?: "",
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.error
                    )
                }
            }
            if (!state.successMessage.isNullOrBlank()) {
                Surface(
                    color = MaterialTheme.colorScheme.secondary.copy(alpha = 0.08f),
                    shape = RoundedCornerShape(14.dp),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.secondary.copy(alpha = 0.35f))
                ) {
                    Text(
                        text = state.successMessage ?: "",
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.secondary
                    )
                }
            }

            Text(text = t.note, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun PasswordVisibilityButton(
    visible: Boolean,
    onClick: () -> Unit,
    tint: Color
) {
    Box(
        modifier = Modifier
            .size(40.dp)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.size(18.dp)) {
            val strokeWidth = 1.8.dp.toPx()
            drawOval(
                color = tint,
                topLeft = Offset(1.5.dp.toPx(), 4.5.dp.toPx()),
                size = Size(width = size.width - 3.dp.toPx(), height = size.height - 9.dp.toPx()),
                style = Stroke(width = strokeWidth)
            )
            drawCircle(
                color = tint,
                radius = 2.7.dp.toPx(),
                center = center,
                style = Stroke(width = strokeWidth)
            )
            if (!visible) {
                drawLine(
                    color = tint,
                    start = Offset(3.dp.toPx(), 3.dp.toPx()),
                    end = Offset(size.width - 3.dp.toPx(), size.height - 3.dp.toPx()),
                    strokeWidth = strokeWidth,
                    cap = StrokeCap.Round
                )
            }
        }
    }
}

private fun validateEmailError(email: String, t: UiText): String? {
    val value = email.trim()
    if (value.isEmpty()) return if (t === RuText) "Введите email." else "Enter your email."
    val emailPattern = Regex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$")
    return if (emailPattern.matches(value)) null
    else if (t === RuText) "Введите корректный email." else "Enter a valid email."
}

private fun validatePasswordError(email: String, password: String, t: UiText): String? {
    if (password.isBlank()) return if (t === RuText) "Введите пароль." else "Enter your password."
    if (password.length < 8) return if (t === RuText) "Минимум 8 символов." else "Use at least 8 characters."
    if (!password.any { it.isUpperCase() }) return if (t === RuText) "Добавьте заглавную букву." else "Add an uppercase letter."
    if (!password.any { it.isLowerCase() }) return if (t === RuText) "Добавьте строчную букву." else "Add a lowercase letter."
    if (!password.any { it.isDigit() }) return if (t === RuText) "Добавьте цифру." else "Add a number."
    val emailHead = email.substringBefore("@", "").lowercase()
    if (emailHead.isNotBlank() && password.lowercase().contains(emailHead)) {
        return if (t === RuText) "Пароль не должен содержать имя из email." else "Password should not contain your email name."
    }
    return null
}

private fun validatePasswordConfirmError(password: String, confirmPassword: String, t: UiText): String? {
    if (confirmPassword.isBlank()) return if (t === RuText) "Повторите пароль." else "Confirm your password."
    return if (password == confirmPassword) null
    else if (t === RuText) "Пароли не совпадают." else "Passwords do not match."
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CompactThemeSelector(
    themeMode: ThemeMode,
    onThemeChange: (ThemeMode) -> Unit
) {
    val options = listOf(ThemeMode.System, ThemeMode.Light, ThemeMode.Dark)

    Surface(
        shape = RoundedCornerShape(22.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.92f),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.75f))
    ) {
        Row(
            modifier = Modifier.padding(4.dp),
            horizontalArrangement = Arrangement.spacedBy(4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            options.forEach { mode ->
                ThemeSelectorButton(
                    mode = mode,
                    active = themeMode == mode,
                    onClick = { onThemeChange(mode) }
                )
            }
        }
    }
}

@Composable
private fun ThemeSelectorButton(
    mode: ThemeMode,
    active: Boolean,
    onClick: () -> Unit
) {
    Box(
        modifier = Modifier
            .size(38.dp)
            .background(
                color = if (active) {
                    MaterialTheme.colorScheme.secondary.copy(alpha = 0.18f)
                } else {
                    Color.Transparent
                },
                shape = CircleShape
            )
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        ThemeModeGlyph(
            mode = mode,
            tint = if (active) {
                MaterialTheme.colorScheme.onSurface
            } else {
                MaterialTheme.colorScheme.onSurfaceVariant
            }
        )
    }
}

@Composable
private fun ThemeModeGlyph(
    mode: ThemeMode,
    tint: Color
) {
    Canvas(modifier = Modifier.size(18.dp)) {
        val strokeWidth = 1.7.dp.toPx()

        when (mode) {
            ThemeMode.System -> {
                drawRoundRect(
                    color = tint,
                    topLeft = Offset(2.dp.toPx(), 3.dp.toPx()),
                    size = Size(size.width - 4.dp.toPx(), size.height - 8.dp.toPx()),
                    cornerRadius = CornerRadius(3.dp.toPx(), 3.dp.toPx()),
                    style = Stroke(width = strokeWidth)
                )
                drawLine(
                    color = tint,
                    start = Offset(6.dp.toPx(), size.height - 3.dp.toPx()),
                    end = Offset(size.width - 6.dp.toPx(), size.height - 3.dp.toPx()),
                    strokeWidth = strokeWidth,
                    cap = StrokeCap.Round
                )
            }

            ThemeMode.Light -> {
                drawCircle(
                    color = tint,
                    radius = 3.6.dp.toPx(),
                    center = center,
                    style = Stroke(width = strokeWidth)
                )

                val ray = 3.1.dp.toPx()
                val rayInset = 1.8.dp.toPx()
                val centerX = center.x
                val centerY = center.y
                val radius = 6.8.dp.toPx()
                val rayStarts = listOf(
                    Offset(centerX, centerY - radius),
                    Offset(centerX, centerY + radius),
                    Offset(centerX - radius, centerY),
                    Offset(centerX + radius, centerY),
                    Offset(centerX - 4.8.dp.toPx(), centerY - 4.8.dp.toPx()),
                    Offset(centerX + 4.8.dp.toPx(), centerY - 4.8.dp.toPx()),
                    Offset(centerX - 4.8.dp.toPx(), centerY + 4.8.dp.toPx()),
                    Offset(centerX + 4.8.dp.toPx(), centerY + 4.8.dp.toPx())
                )
                val rayEnds = listOf(
                    Offset(centerX, centerY - radius - ray),
                    Offset(centerX, centerY + radius + ray),
                    Offset(centerX - radius - ray, centerY),
                    Offset(centerX + radius + ray, centerY),
                    Offset(centerX - 4.8.dp.toPx() - rayInset, centerY - 4.8.dp.toPx() - rayInset),
                    Offset(centerX + 4.8.dp.toPx() + rayInset, centerY - 4.8.dp.toPx() - rayInset),
                    Offset(centerX - 4.8.dp.toPx() - rayInset, centerY + 4.8.dp.toPx() + rayInset),
                    Offset(centerX + 4.8.dp.toPx() + rayInset, centerY + 4.8.dp.toPx() + rayInset)
                )
                rayStarts.zip(rayEnds).forEach { (start, end) ->
                    drawLine(
                        color = tint,
                        start = start,
                        end = end,
                        strokeWidth = strokeWidth,
                        cap = StrokeCap.Round
                    )
                }
            }

            ThemeMode.Dark -> {
                drawArc(
                    color = tint,
                    startAngle = 48f,
                    sweepAngle = 272f,
                    useCenter = false,
                    topLeft = Offset(4.dp.toPx(), 2.5.dp.toPx()),
                    size = Size(10.dp.toPx(), 13.dp.toPx()),
                    style = Stroke(width = strokeWidth, cap = StrokeCap.Round)
                )
            }
        }
    }
}

@Composable
private fun LanguageMenu(
    isRussian: Boolean,
    onLanguageChange: (Boolean) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val currentCode = if (isRussian) "RU" else "EN"
    val options = listOf(
        true to (if (isRussian) "Русский" else "Russian"),
        false to "English"
    )

    Box {
        Button(
            onClick = { expanded = true },
            shape = RoundedCornerShape(16.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = MaterialTheme.colorScheme.surface,
                contentColor = MaterialTheme.colorScheme.onSurface
            ),
            contentPadding = ButtonDefaults.ContentPadding,
            modifier = Modifier.height(32.dp)
        ) {
            Text(
                text = currentCode,
                fontSize = 12.sp,
                fontWeight = FontWeight.SemiBold
            )
        }

        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            containerColor = MaterialTheme.colorScheme.surface
        ) {
            options.forEach { (value, label) ->
                DropdownMenuItem(
                    text = {
                        Text(
                            text = if (value == isRussian) "$label  ✓" else label,
                            fontSize = 13.sp
                        )
                    },
                    onClick = {
                        expanded = false
                        onLanguageChange(value)
                    }
                )
            }
        }
    }
}

@Composable
private fun AuthTab(text: String, active: Boolean, onClick: () -> Unit) {
    val colors = if (active) {
        ButtonDefaults.buttonColors(
            containerColor = MaterialTheme.colorScheme.primary,
            contentColor = Color.White
        )
    } else {
        ButtonDefaults.buttonColors(
            containerColor = MaterialTheme.colorScheme.surface,
            contentColor = MaterialTheme.colorScheme.onSurface
        )
    }

    Button(
        onClick = onClick,
        shape = RoundedCornerShape(14.dp),
        colors = colors,
        contentPadding = ButtonDefaults.ContentPadding,
        modifier = Modifier.height(32.dp)
    ) {
        Text(text = text, fontSize = 12.sp)
    }
}

private fun formatMoney(amountMinor: Long, currencyCode: String, isRussian: Boolean): String {
    val locale = if (isRussian) Locale.forLanguageTag("ru-RU") else Locale.US
    val format = NumberFormat.getCurrencyInstance(locale)
    runCatching {
        format.currency = Currency.getInstance(currencyCode)
    }
    val amount = amountMinor / 100.0
    if (kotlin.math.abs(amount % 1.0) < 0.000001) {
        format.minimumFractionDigits = 0
        format.maximumFractionDigits = 0
    } else {
        format.minimumFractionDigits = 2
        format.maximumFractionDigits = 2
    }
    return format.format(amount)
}

private fun parseAmountToMinor(raw: String): Long? {
    val normalized = raw.trim()
        .replace(" ", "")
        .replace(',', '.')
    if (normalized.isBlank()) return null
    val amount = normalized.toBigDecimalOrNull() ?: return null
    return amount.multiply(java.math.BigDecimal(100))
        .setScale(0, java.math.RoundingMode.HALF_UP)
        .longValueExact()
}

private fun formatMonthLabel(month: String, isRussian: Boolean): String {
    val locale = if (isRussian) Locale.forLanguageTag("ru-RU") else Locale.US
    return runCatching {
        val parts = month.split("-")
        val year = parts[0].toInt()
        val monthValue = parts[1].toInt()
        java.time.YearMonth.of(year, monthValue)
            .atDay(1)
            .month
            .getDisplayName(java.time.format.TextStyle.FULL_STANDALONE, locale)
            .replaceFirstChar { if (it.isLowerCase()) it.titlecase(locale) else it.toString() } + " " + year
    }.getOrElse { month }
}

private fun currentMonthLabel(isRussian: Boolean): String {
    val now = java.time.LocalDate.now()
    return formatMonthLabel("${now.year}-${now.monthValue.toString().padStart(2, '0')}", isRussian)
}

private fun dashboardMetrics(isRussian: Boolean): List<MetricItem> = listOf(
    MetricItem(
        label = if (isRussian) "Контекст" else "Context",
        value = if (isRussian) "12 блоков" else "12 blocks",
        detail = if (isRussian) "+3 за сегодня" else "+3 today"
    ),
    MetricItem(
        label = if (isRussian) "Активные задачи" else "Active tasks",
        value = "7",
        detail = if (isRussian) "2 требуют решения" else "2 need decisions"
    ),
    MetricItem(
        label = if (isRussian) "Последний sync" else "Last sync",
        value = if (isRussian) "4 мин" else "4 min",
        detail = "web / windows / android"
    ),
    MetricItem(
        label = if (isRussian) "Фокус недели" else "Week focus",
        value = "Dashboard",
        detail = if (isRussian) "единый shell и модули" else "shared shell and modules"
    )
)

private fun dashboardSectionCopy(isRussian: Boolean): Map<DashboardSection, DashboardSectionText> =
    if (isRussian) {
        mapOf(
            DashboardSection.Home to DashboardSectionText(
                label = "Главная",
                title = "Главная",
                eyebrow = "Command center",
                badge = "В разработке",
                note = "Раздел в разработке. Здесь появится главный обзор проекта, быстрые действия и персональная сводка.",
                icon = "GL",
                defaultSubsection = DashboardSubsection.Summary,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Summary, "Сводка"),
                    DashboardNavItem(DashboardSubsection.Today, "Сегодня"),
                    DashboardNavItem(DashboardSubsection.Insights, "Инсайты")
                )
            ),
            DashboardSection.Finance to DashboardSectionText(
                label = "Финансы",
                title = "Финансы",
                eyebrow = "Money workspace",
                badge = "Live",
                note = "Раздел в разработке. Здесь будут бюджеты, кошельки, транзакции и финансовая аналитика.",
                icon = "FN",
                defaultSubsection = DashboardSubsection.Overview,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Overview, "Обзор"),
                    DashboardNavItem(DashboardSubsection.Accounts, "Счета"),
                    DashboardNavItem(DashboardSubsection.Transactions, "Транзакции"),
                    DashboardNavItem(DashboardSubsection.Categories, "Категории"),
                    DashboardNavItem(DashboardSubsection.Analytics, "Аналитика")
                )
            ),
            DashboardSection.Health to DashboardSectionText(
                label = "Здоровье",
                title = "Здоровье",
                eyebrow = "Wellbeing",
                badge = "В разработке",
                note = "Раздел в разработке. Здесь появятся трекинг самочувствия, метрики и история состояния.",
                icon = "ZD",
                defaultSubsection = DashboardSubsection.Habits,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Habits, "Привычки"),
                    DashboardNavItem(DashboardSubsection.Metrics, "Метрики"),
                    DashboardNavItem(DashboardSubsection.Records, "История")
                )
            ),
            DashboardSection.Tasks to DashboardSectionText(
                label = "Задачи",
                title = "Задачи",
                eyebrow = "Execution",
                badge = "В разработке",
                note = "Раздел в разработке. Здесь будут списки задач, статусы, приоритеты и рабочие потоки.",
                icon = "TK",
                defaultSubsection = DashboardSubsection.Focus,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Focus, "Фокус"),
                    DashboardNavItem(DashboardSubsection.Board, "Доска"),
                    DashboardNavItem(DashboardSubsection.Archive, "Архив")
                )
            ),
            DashboardSection.Settings to DashboardSectionText(
                label = "Настройки",
                title = "Настройки",
                eyebrow = "Control",
                badge = "Secure",
                note = "Раздел в разработке. Здесь будут параметры приложения, профиля и подключённых сервисов.",
                icon = "NS",
                defaultSubsection = DashboardSubsection.Profile,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Profile, "Профиль"),
                    DashboardNavItem(DashboardSubsection.Preferences, "Параметры"),
                    DashboardNavItem(DashboardSubsection.Security, "Безопасность")
                )
            )
        )
    } else {
        mapOf(
            DashboardSection.Home to DashboardSectionText(
                label = "Home",
                title = "Home",
                eyebrow = "Command center",
                badge = "In development",
                note = "This section is in development. It will contain the main project overview, quick actions, and personal summary.",
                icon = "HM",
                defaultSubsection = DashboardSubsection.Summary,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Summary, "Summary"),
                    DashboardNavItem(DashboardSubsection.Today, "Today"),
                    DashboardNavItem(DashboardSubsection.Insights, "Insights")
                )
            ),
            DashboardSection.Finance to DashboardSectionText(
                label = "Finance",
                title = "Finance",
                eyebrow = "Money workspace",
                badge = "Live",
                note = "This section is in development. It will contain budgets, wallets, transactions, and financial analytics.",
                icon = "FN",
                defaultSubsection = DashboardSubsection.Overview,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Overview, "Overview"),
                    DashboardNavItem(DashboardSubsection.Accounts, "Accounts"),
                    DashboardNavItem(DashboardSubsection.Transactions, "Transactions"),
                    DashboardNavItem(DashboardSubsection.Categories, "Categories"),
                    DashboardNavItem(DashboardSubsection.Analytics, "Analytics")
                )
            ),
            DashboardSection.Health to DashboardSectionText(
                label = "Health",
                title = "Health",
                eyebrow = "Wellbeing",
                badge = "In development",
                note = "This section is in development. It will contain wellbeing tracking, metrics, and health history.",
                icon = "HL",
                defaultSubsection = DashboardSubsection.Habits,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Habits, "Habits"),
                    DashboardNavItem(DashboardSubsection.Metrics, "Metrics"),
                    DashboardNavItem(DashboardSubsection.Records, "History")
                )
            ),
            DashboardSection.Tasks to DashboardSectionText(
                label = "Tasks",
                title = "Tasks",
                eyebrow = "Execution",
                badge = "In development",
                note = "This section is in development. It will contain task lists, statuses, priorities, and work flows.",
                icon = "TS",
                defaultSubsection = DashboardSubsection.Focus,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Focus, "Focus"),
                    DashboardNavItem(DashboardSubsection.Board, "Board"),
                    DashboardNavItem(DashboardSubsection.Archive, "Archive")
                )
            ),
            DashboardSection.Settings to DashboardSectionText(
                label = "Settings",
                title = "Settings",
                eyebrow = "Control",
                badge = "Secure",
                note = "This section is in development. It will contain app, profile, and connected service settings.",
                icon = "ST",
                defaultSubsection = DashboardSubsection.Profile,
                subsections = listOf(
                    DashboardNavItem(DashboardSubsection.Profile, "Profile"),
                    DashboardNavItem(DashboardSubsection.Preferences, "Preferences"),
                    DashboardNavItem(DashboardSubsection.Security, "Security")
                )
            )
        )
    }

private fun dashboardActivities(isRussian: Boolean): List<ActivityItem> = listOf(
    ActivityItem(
        title = if (isRussian) "Auth стабилизирован" else "Auth stabilized",
        time = if (isRussian) "Сегодня, 09:40" else "Today, 09:40",
        detail = if (isRussian) "Email/password flow уже работает на web, android и windows." else "Email/password flow is already working on web, android, and windows."
    ),
    ActivityItem(
        title = if (isRussian) "Supabase проект подключён" else "Supabase project connected",
        time = if (isRussian) "Сегодня, 10:15" else "Today, 10:15",
        detail = if (isRussian) "Assistant2 используется как единый backend для локального проекта." else "Assistant2 is used as the shared backend for the local project."
    ),
    ActivityItem(
        title = if (isRussian) "Следующий этап" else "Next stage",
        time = if (isRussian) "Сейчас" else "Now",
        detail = if (isRussian) "Собираем overview-dashboard по мотивам reference/assistant." else "Building the overview dashboard based on reference/assistant."
    )
)

private fun dashboardFocus(isRussian: Boolean): List<FocusItem> = listOf(
    FocusItem(
        title = "Overview",
        detail = if (isRussian) "Главный экран после входа с краткой картиной по состоянию проекта." else "Post-auth home with a compact picture of the project state."
    ),
    FocusItem(
        title = "Modules",
        detail = if (isRussian) "Finance, health и tasks появятся как отдельные секции второго этапа." else "Finance, health, and tasks will become dedicated sections in the next phase."
    ),
    FocusItem(
        title = if (isRussian) "Синхронность" else "Consistency",
        detail = if (isRussian) "Одинаковый визуальный язык между браузером, WinUI и Android." else "One visual language across browser, WinUI, and Android."
    )
)

private fun rememberUserName(email: String): String {
    val head = email.substringBefore("@", email)
    return head.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
}
