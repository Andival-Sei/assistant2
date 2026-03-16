package com.assistant.app.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val DarkColorScheme = darkColorScheme(
    primary = DarkInk,
    onPrimary = DarkBg,
    background = DarkBg,
    onBackground = DarkInk,
    surface = DarkPanel,
    onSurface = DarkInk,
    surfaceVariant = DarkPanel,
    onSurfaceVariant = DarkMuted,
    outline = DarkStroke,
    secondary = DarkAccent,
    tertiary = DarkAccent2
)

private val LightColorScheme = lightColorScheme(
    primary = LightInk,
    onPrimary = LightPanel,
    background = LightBg,
    onBackground = LightInk,
    surface = LightPanel,
    onSurface = LightInk,
    surfaceVariant = LightPanel,
    onSurfaceVariant = LightMuted,
    outline = LightStroke,
    secondary = LightAccent,
    tertiary = LightAccent2
)

@Composable
fun AssistantAndroidTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit
) {
    val colorScheme = if (darkTheme) DarkColorScheme else LightColorScheme

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}
