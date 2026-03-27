package com.assistant.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.wrapContentWidth
import androidx.compose.foundation.horizontalScroll
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
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
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
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.assistant.app.auth.AuthMode
import com.assistant.app.auth.AuthUiState
import com.assistant.app.auth.AuthViewModel
import com.assistant.app.auth.SupabaseProvider
import com.assistant.app.finance.FinanceOverview
import com.assistant.app.finance.FinanceRepository
import com.assistant.app.ui.theme.AssistantAndroidTheme
import com.assistant.app.ui.theme.BlobCool
import com.assistant.app.ui.theme.BlobWarm
import com.assistant.app.ui.theme.DarkInput
import com.assistant.app.ui.theme.LightInput
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale
import kotlinx.coroutines.launch

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
                    isGoogleAuthEnabled = SupabaseProvider.isGoogleAuthEnabled
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
    Settings
}

private enum class DashboardSubsection {
    Summary,
    Today,
    Insights,
    Overview,
    Accounts,
    Transactions,
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
    isGoogleAuthEnabled: Boolean
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
                userEmail = userEmail.orEmpty()
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
    userEmail: String
) {
    var section by rememberSaveable { mutableStateOf(DashboardSection.Home) }
    var subsection by rememberSaveable { mutableStateOf(DashboardSubsection.Summary) }
    var financeTab by rememberSaveable { mutableStateOf(FinanceTab.Overview) }
    var financeOverview by remember { mutableStateOf<FinanceOverview?>(null) }
    var financeLoading by remember { mutableStateOf(false) }
    var financeError by remember { mutableStateOf<String?>(null) }
    var financeOnboardingStep by rememberSaveable { mutableStateOf(0) }
    var financeOnboarding by remember { mutableStateOf(FinanceOnboardingState()) }
    val sectionCopy = dashboardSectionCopy(isRussian)
    val activeSection = sectionCopy[section]!!
    val activeSubsection = activeSection.subsections.firstOrNull { it.id == subsection } ?: activeSection.subsections.first()
    val userName = rememberUserName(userEmail)
    val financeRepository = remember { FinanceRepository() }
    val coroutineScope = rememberCoroutineScope()
    val setSectionState: (DashboardSection) -> Unit = { next ->
        val nextSubsection = sectionCopy[next]!!.defaultSubsection
        section = next
        subsection = nextSubsection
        if (next == DashboardSection.Finance) {
            financeTab = when (nextSubsection) {
                DashboardSubsection.Accounts -> FinanceTab.Accounts
                DashboardSubsection.Transactions -> FinanceTab.Transactions
                DashboardSubsection.Settings -> FinanceTab.Settings
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
            FinanceTab.Settings -> DashboardSubsection.Settings
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
                    SecondaryTabRow(
                        items = activeSection.subsections,
                        active = activeSubsection.id,
                        onSelect = { item ->
                            subsection = item
                            if (section == DashboardSection.Finance) {
                                setFinanceTabState(
                                    when (item) {
                                        DashboardSubsection.Accounts -> FinanceTab.Accounts
                                        DashboardSubsection.Transactions -> FinanceTab.Transactions
                                        DashboardSubsection.Settings -> FinanceTab.Settings
                                        else -> FinanceTab.Overview
                                    }
                                )
                            }
                        }
                    )
                    if (section == DashboardSection.Finance) {
                        FinanceSectionCard(
                            isRussian = isRussian,
                            compact = true,
                            overview = financeOverview,
                            activeTab = financeTab,
                            loading = financeLoading,
                            error = financeError,
                            onboardingStep = financeOnboardingStep,
                            onboarding = financeOnboarding,
                            onTabChange = setFinanceTabState,
                            onOnboardingStepChange = { financeOnboardingStep = it },
                            onOnboardingChange = { financeOnboarding = it },
                            onCompleteOnboarding = completeFinanceOnboarding
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
                    SecondaryTabRow(
                        items = activeSection.subsections,
                        active = activeSubsection.id,
                        onSelect = { item ->
                            subsection = item
                            if (section == DashboardSection.Finance) {
                                setFinanceTabState(
                                    when (item) {
                                        DashboardSubsection.Accounts -> FinanceTab.Accounts
                                        DashboardSubsection.Transactions -> FinanceTab.Transactions
                                        DashboardSubsection.Settings -> FinanceTab.Settings
                                        else -> FinanceTab.Overview
                                    }
                                )
                            }
                        }
                    )
                    if (section == DashboardSection.Finance) {
                        FinanceSectionCard(
                            isRussian = isRussian,
                            compact = false,
                            overview = financeOverview,
                            activeTab = financeTab,
                            loading = financeLoading,
                            error = financeError,
                            onboardingStep = financeOnboardingStep,
                            onboarding = financeOnboarding,
                            onTabChange = setFinanceTabState,
                            onOnboardingStepChange = { financeOnboardingStep = it },
                            onOnboardingChange = { financeOnboarding = it },
                            onCompleteOnboarding = completeFinanceOnboarding
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
    overview: FinanceOverview?,
    activeTab: FinanceTab,
    loading: Boolean,
    error: String?,
    onboardingStep: Int,
    onboarding: FinanceOnboardingState,
    onTabChange: (FinanceTab) -> Unit,
    onOnboardingStepChange: (Int) -> Unit,
    onOnboardingChange: (FinanceOnboardingState) -> Unit,
    onCompleteOnboarding: (Boolean) -> Unit
) {
    val financeTitle = if (isRussian) "Финансы" else "Finance"
    val financeSubtitle = if (isRussian) {
        "Управляйте балансом, счетами и будущими транзакциями в одном пространстве."
    } else {
        "Manage balance, accounts, and future transactions in one workspace."
    }
    val tabLabels = if (isRussian) {
        mapOf(
            FinanceTab.Overview to "Главная",
            FinanceTab.Accounts to "Счета",
            FinanceTab.Transactions to "Транзакции",
            FinanceTab.Settings to "Настройки"
        )
    } else {
        mapOf(
            FinanceTab.Overview to "Overview",
            FinanceTab.Accounts to "Accounts",
            FinanceTab.Transactions to "Transactions",
            FinanceTab.Settings to "Settings"
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

    GlassSurface(modifier = Modifier.fillMaxWidth()) {
        Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
            Text(
                text = financeTitle.uppercase(),
                fontSize = 11.sp,
                color = MaterialTheme.colorScheme.secondary
            )
            Text(
                text = financeTitle,
                fontSize = if (compact) 30.sp else 38.sp,
                fontWeight = FontWeight.ExtraBold,
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = financeSubtitle,
                fontSize = 14.sp,
                lineHeight = 22.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            if (loading) {
                Text(
                    text = if (isRussian) "Загружаем данные из Supabase…" else "Loading data from Supabase…",
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                return@Column
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
                return@Column
            }

            FinanceTabStrip(
                tabs = FinanceTab.entries.toList(),
                labels = tabLabels,
                activeTab = activeTab,
                onTabChange = onTabChange
            )

            if (activeTab == FinanceTab.Overview) {
                Surface(
                    shape = RoundedCornerShape(22.dp),
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.74f),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                ) {
                    Column(
                        modifier = Modifier.padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Text(
                            text = if (isRussian) "Текущий баланс" else "Current balance",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = formatMoney(overview.totalBalanceMinor, overview.defaultCurrency ?: "RUB", isRussian),
                            fontSize = if (compact) 32.sp else 42.sp,
                            fontWeight = FontWeight.ExtraBold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = if (isRussian) {
                                "Баланс складывается из всех карточных счетов и наличных."
                            } else {
                                "Balance is built from card accounts and cash."
                            },
                            fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

            if (activeTab == FinanceTab.Overview || activeTab == FinanceTab.Accounts) {
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
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(16.dp),
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
                                        text = when {
                                            account.kind == "cash" && isRussian -> "Наличные"
                                            account.kind == "cash" -> "Cash"
                                            account.isPrimary && isRussian -> "Основной счёт"
                                            account.isPrimary -> "Primary account"
                                            else -> account.bankName ?: account.name
                                        },
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
                        }
                    }
                }
            }

            if (activeTab == FinanceTab.Overview || activeTab == FinanceTab.Transactions) {
                Text(
                    text = if (isRussian) "Последние транзакции" else "Recent transactions",
                    fontSize = 18.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                if (overview.recentTransactions.isEmpty()) {
                    Text(
                        text = if (isRussian) {
                            "Транзакций пока нет. Следующим этапом подключим ввод операций."
                        } else {
                            "No transactions yet. Data entry comes next."
                        },
                        fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                } else {
                    overview.recentTransactions.forEach { transaction ->
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
                                        text = transaction.happenedAt,
                                        fontSize = 12.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                                Text(
                                    text = formatMoney(transaction.amountMinor, transaction.currency, isRussian),
                                    fontSize = 14.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                            }
                        }
                    }
                }
            }

            if (activeTab == FinanceTab.Settings) {
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
                            text = if (isRussian) "Настройки раздела" else "Module settings",
                            fontSize = 18.sp,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = if (isRussian) {
                                "Валюта по умолчанию: ${overview.defaultCurrency ?: "RUB"}"
                            } else {
                                "Default currency: ${overview.defaultCurrency ?: "RUB"}"
                            },
                            fontSize = 13.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }
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
    return format.format(amountMinor / 100.0)
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
                    DashboardNavItem(DashboardSubsection.Settings, "Настройки")
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
                    DashboardNavItem(DashboardSubsection.Settings, "Settings")
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
