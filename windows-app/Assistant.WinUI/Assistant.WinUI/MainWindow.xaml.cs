using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Assistant.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private enum ThemeMode
        {
            System,
            Light,
            Dark
        }

        private ThemeMode _themeMode = ThemeMode.System;
        private bool _isRussian = true;

        public MainWindow()
        {
            InitializeComponent();
            ApplyText();
            ApplyTheme();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _themeMode = _themeMode switch
            {
                ThemeMode.System => ThemeMode.Light,
                ThemeMode.Light => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            ApplyTheme();
        }

        private void LangButton_Click(object sender, RoutedEventArgs e)
        {
            _isRussian = !_isRussian;
            ApplyText();
        }

        private void ApplyTheme()
        {
            RootGrid.RequestedTheme = _themeMode switch
            {
                ThemeMode.Light => ElementTheme.Light,
                ThemeMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            ThemeButton.Content = _themeMode switch
            {
                ThemeMode.Light => _isRussian ? "Тема: Светлая" : "Theme: Light",
                ThemeMode.Dark => _isRussian ? "Тема: Тёмная" : "Theme: Dark",
                _ => _isRussian ? "Тема: Система" : "Theme: System"
            };
        }

        private void ApplyText()
        {
            if (_isRussian)
            {
                LogoText.Text = "ASSISTANT";
                LangButton.Content = "English";
                HeroPill.Text = "Вход в экосистему";
                HeroTitle.Text = "Добро пожаловать обратно.";
                HeroSubtitle.Text = "Войдите, чтобы открыть ваши модули, задачи и приватные настройки.";
                HeroHint.Text = "Пока без настоящей авторизации — это UI‑заглушка для макета.";

                FormTitle.Text = "Авторизация";
                EmailLabel.Text = "EMAIL";
                EmailInput.PlaceholderText = "you@example.com";
                PasswordLabel.Text = "ПАРОЛЬ";
                PasswordInput.PlaceholderText = "••••••••";
                RememberCheck.Content = "Запомнить устройство";
                ForgotButton.Content = "Забыли пароль?";
                SubmitButton.Content = "Войти";
                FormNote.Text = "Подключим реальную авторизацию после согласования бекенда.";
            }
            else
            {
                LogoText.Text = "ASSISTANT";
                LangButton.Content = "Русский";
                HeroPill.Text = "Access the ecosystem";
                HeroTitle.Text = "Welcome back.";
                HeroSubtitle.Text = "Sign in to reach your modules, tasks, and private settings.";
                HeroHint.Text = "UI placeholder only — real auth will come later.";

                FormTitle.Text = "Sign in";
                EmailLabel.Text = "EMAIL";
                EmailInput.PlaceholderText = "you@example.com";
                PasswordLabel.Text = "PASSWORD";
                PasswordInput.PlaceholderText = "••••••••";
                RememberCheck.Content = "Remember this device";
                ForgotButton.Content = "Forgot password?";
                SubmitButton.Content = "Sign in";
                FormNote.Text = "We will wire real auth after backend confirmation.";
            }

            ApplyTheme();
        }
    }
}
