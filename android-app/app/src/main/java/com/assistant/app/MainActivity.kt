package com.assistant.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.wrapContentWidth
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.assistant.app.ui.theme.AssistantAndroidTheme
import com.assistant.app.ui.theme.BlobCool
import com.assistant.app.ui.theme.BlobWarm
import com.assistant.app.ui.theme.DarkInput
import com.assistant.app.ui.theme.LightInput

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            var themeMode by rememberSaveable { mutableStateOf(ThemeMode.System) }
            var isRussian by rememberSaveable { mutableStateOf(true) }

            val isDark = when (themeMode) {
                ThemeMode.System -> androidx.compose.foundation.isSystemInDarkTheme()
                ThemeMode.Light -> false
                ThemeMode.Dark -> true
            }

            AssistantAndroidTheme(darkTheme = isDark) {
                AuthScreen(
                    isDark = isDark,
                    isRussian = isRussian,
                    themeMode = themeMode,
                    onToggleTheme = { themeMode = themeMode.next() },
                    onToggleLanguage = { isRussian = !isRussian }
                )
            }
        }
    }
}

private enum class ThemeMode { System, Light, Dark;
    fun next(): ThemeMode = when (this) {
        System -> Light
        Light -> Dark
        Dark -> System
    }
}

private data class UiText(
    val themeSystem: String,
    val themeLight: String,
    val themeDark: String,
    val langSwitch: String,
    val pill: String,
    val title: String,
    val subtitle: String,
    val hint: String,
    val formTitle: String,
    val email: String,
    val emailPlaceholder: String,
    val password: String,
    val passwordPlaceholder: String,
    val remember: String,
    val forgot: String,
    val cta: String,
    val note: String
)

private val RuText = UiText(
    themeSystem = "Тема: Система",
    themeLight = "Тема: Светлая",
    themeDark = "Тема: Тёмная",
    langSwitch = "English",
    pill = "Вход в экосистему",
    title = "Добро пожаловать обратно.",
    subtitle = "Войдите, чтобы открыть ваши модули, задачи и приватные настройки.",
    hint = "Пока без настоящей авторизации — это UI‑заглушка для макета.",
    formTitle = "Авторизация",
    email = "EMAIL",
    emailPlaceholder = "you@example.com",
    password = "ПАРОЛЬ",
    passwordPlaceholder = "••••••••",
    remember = "Запомнить устройство",
    forgot = "Забыли пароль?",
    cta = "Войти",
    note = "Подключим реальную авторизацию после согласования бекенда."
)

private val EnText = UiText(
    themeSystem = "Theme: System",
    themeLight = "Theme: Light",
    themeDark = "Theme: Dark",
    langSwitch = "Русский",
    pill = "Access the ecosystem",
    title = "Welcome back.",
    subtitle = "Sign in to reach your modules, tasks, and private settings.",
    hint = "UI placeholder only — real auth will come later.",
    formTitle = "Sign in",
    email = "EMAIL",
    emailPlaceholder = "you@example.com",
    password = "PASSWORD",
    passwordPlaceholder = "••••••••",
    remember = "Remember this device",
    forgot = "Forgot password?",
    cta = "Sign in",
    note = "We will wire real auth after backend confirmation."
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AuthScreen(
    isDark: Boolean,
    isRussian: Boolean,
    themeMode: ThemeMode,
    onToggleTheme: () -> Unit,
    onToggleLanguage: () -> Unit
) {
    val t = if (isRussian) RuText else EnText

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        Canvas(modifier = Modifier.fillMaxSize()) {
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(BlobWarm.copy(alpha = 0.3f), Color.Transparent)
                ),
                radius = size.minDimension * 0.65f,
                center = androidx.compose.ui.geometry.Offset(0f, 0f)
            )
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(BlobCool.copy(alpha = 0.3f), Color.Transparent)
                ),
                radius = size.minDimension * 0.65f,
                center = androidx.compose.ui.geometry.Offset(size.width, size.height)
            )
        }

        BoxWithConstraints(
            modifier = Modifier
                .fillMaxSize()
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
                        PillButton(
                            text = when (themeMode) {
                                ThemeMode.System -> t.themeSystem
                                ThemeMode.Light -> t.themeLight
                                ThemeMode.Dark -> t.themeDark
                            },
                            onClick = onToggleTheme
                        )
                        PillButton(
                            text = t.langSwitch,
                            onClick = onToggleLanguage
                        )
                    }
                }

                Spacer(modifier = Modifier.height(32.dp))

                if (isCompact) {
                    Column(verticalArrangement = Arrangement.spacedBy(18.dp)) {
                        HeroBlock(t, isCompact)
                        AuthCard(t, isDark, modifier = Modifier.fillMaxWidth())
                    }
                } else {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Column(
                            modifier = Modifier.weight(1.2f),
                            verticalArrangement = Arrangement.spacedBy(14.dp)
                        ) {
                            HeroBlock(t, isCompact)
                        }

                        Spacer(modifier = Modifier.width(24.dp))

                        AuthCard(t, isDark, modifier = Modifier.width(320.dp))
                    }
                }
            }
        }
    }
}

@Composable
private fun HeroBlock(t: UiText, isCompact: Boolean) {
    Surface(
        color = MaterialTheme.colorScheme.secondary.copy(alpha = 0.18f),
        shape = RoundedCornerShape(999.dp)
    ) {
        Text(
            text = t.pill,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
            fontSize = 12.sp,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.secondary
        )
    }
    Text(
        text = t.title,
        fontSize = if (isCompact) 26.sp else 30.sp,
        lineHeight = if (isCompact) 32.sp else 36.sp,
        fontFamily = FontFamily.Serif,
        fontWeight = FontWeight.SemiBold,
        color = MaterialTheme.colorScheme.onBackground,
        textAlign = TextAlign.Start
    )
    Text(
        text = t.subtitle,
        fontSize = 14.sp,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )
    Text(
        text = t.hint,
        fontSize = 12.sp,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AuthCard(t: UiText, isDark: Boolean, modifier: Modifier) {
    Surface(
        modifier = modifier,
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(18.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(text = t.formTitle, fontSize = 16.sp, fontWeight = FontWeight.SemiBold)

            Text(text = t.email, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            OutlinedTextField(
                value = "",
                onValueChange = {},
                placeholder = { Text(t.emailPlaceholder) },
                singleLine = true,
                shape = RoundedCornerShape(10.dp),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedContainerColor = if (isDark) DarkInput else LightInput,
                    unfocusedContainerColor = if (isDark) DarkInput else LightInput,
                    focusedBorderColor = MaterialTheme.colorScheme.outline,
                    unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                    focusedTextColor = MaterialTheme.colorScheme.onSurface,
                    unfocusedTextColor = MaterialTheme.colorScheme.onSurface
                )
            )

            Text(text = t.password, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            OutlinedTextField(
                value = "",
                onValueChange = {},
                placeholder = { Text(t.passwordPlaceholder) },
                singleLine = true,
                shape = RoundedCornerShape(10.dp),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedContainerColor = if (isDark) DarkInput else LightInput,
                    unfocusedContainerColor = if (isDark) DarkInput else LightInput,
                    focusedBorderColor = MaterialTheme.colorScheme.outline,
                    unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                    focusedTextColor = MaterialTheme.colorScheme.onSurface,
                    unfocusedTextColor = MaterialTheme.colorScheme.onSurface
                )
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = false, onCheckedChange = {})
                    Text(text = t.remember, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Text(text = t.forgot, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }

            val buttonColors = if (isDark) {
                ButtonDefaults.buttonColors(
                    containerColor = Color(0xFFF3F5F7),
                    contentColor = Color(0xFF0B0F13)
                )
            } else {
                ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = Color.White
                )
            }

            Button(
                onClick = {},
                shape = RoundedCornerShape(12.dp),
                colors = buttonColors,
                contentPadding = ButtonDefaults.ContentPadding,
                modifier = Modifier.wrapContentWidth()
            ) {
                Text(text = t.cta)
            }

            Text(text = t.note, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun PillButton(text: String, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        shape = RoundedCornerShape(16.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = MaterialTheme.colorScheme.surface,
            contentColor = MaterialTheme.colorScheme.onSurface
        ),
        contentPadding = ButtonDefaults.ContentPadding,
        modifier = Modifier.height(32.dp)
    ) {
        Text(text = text, fontSize = 12.sp)
    }
}
