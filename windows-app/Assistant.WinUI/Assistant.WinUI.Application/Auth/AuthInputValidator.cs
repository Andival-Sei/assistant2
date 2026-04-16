using System;
using System.Text.RegularExpressions;

namespace Assistant.WinUI.Application.Auth;

public static partial class AuthInputValidator
{
    public static string? ValidateRegistration(string email, string password, string confirmPassword, bool isRussian)
    {
        return ValidateEmail(email, isRussian)
            ?? ValidatePassword(email, password, isRussian)
            ?? ValidatePasswordConfirmation(password, confirmPassword, isRussian);
    }

    public static string? ValidatePasswordReset(string email, string password, string confirmPassword, bool isRussian)
    {
        return ValidateEmail(email, isRussian)
            ?? ValidatePassword(password, isRussian)
            ?? ValidatePasswordConfirmation(password, confirmPassword, isRussian);
    }

    public static string? ValidateEmail(string email, bool isRussian)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return isRussian ? "Введите email." : "Enter your email.";
        }

        if (!EmailRegex().IsMatch(email.Trim()))
        {
            return isRussian ? "Укажите корректный email." : "Provide a valid email.";
        }

        return null;
    }

    public static string? ValidatePassword(string email, string password, bool isRussian)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return isRussian ? "Введите пароль." : "Enter a password.";
        }

        if (password.Length < 8)
        {
            return isRussian ? "Пароль должен быть не короче 8 символов." : "Password must be at least 8 characters.";
        }

        if (!ContainsLatinLetter(password))
        {
            return isRussian ? "Добавьте латинские буквы в пароль." : "Add latin letters to the password.";
        }

        if (!ContainsDigit(password))
        {
            return isRussian ? "Добавьте хотя бы одну цифру в пароль." : "Add at least one digit to the password.";
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            password.Contains(email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return isRussian ? "Пароль не должен содержать email." : "Password must not contain the email.";
        }

        return null;
    }

    public static string? ValidatePassword(string password, bool isRussian)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return isRussian ? "Введите пароль." : "Enter a password.";
        }

        if (password.Length < 8)
        {
            return isRussian ? "Пароль должен быть не короче 8 символов." : "Password must be at least 8 characters.";
        }

        if (!ContainsLatinLetter(password))
        {
            return isRussian ? "Добавьте латинские буквы в пароль." : "Add latin letters to the password.";
        }

        if (!ContainsDigit(password))
        {
            return isRussian ? "Добавьте хотя бы одну цифру в пароль." : "Add at least one digit to the password.";
        }

        return null;
    }

    public static string? ValidatePasswordConfirmation(string password, string confirmPassword, bool isRussian)
    {
        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return isRussian ? "Пароли не совпадают." : "Passwords do not match.";
        }

        return null;
    }

    private static bool ContainsLatinLetter(string value)
    {
        foreach (var symbol in value)
        {
            if ((symbol is >= 'a' and <= 'z') || (symbol is >= 'A' and <= 'Z'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDigit(string value)
    {
        foreach (var symbol in value)
        {
            if (char.IsDigit(symbol))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
