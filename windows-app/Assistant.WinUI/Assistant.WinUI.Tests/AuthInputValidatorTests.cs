using Assistant.WinUI.Application.Auth;

namespace Assistant.WinUI.Tests;

[TestClass]
public sealed class AuthInputValidatorTests
{
    [TestMethod]
    public void ValidateRegistration_ReturnsError_WhenEmailIsInvalid()
    {
        var error = AuthInputValidator.ValidateRegistration("bad", "Valid123", "Valid123", isRussian: true);

        Assert.AreEqual("Укажите корректный email.", error);
    }

    [TestMethod]
    public void ValidateRegistration_ReturnsNull_ForValidPayload()
    {
        var error = AuthInputValidator.ValidateRegistration("user@example.com", "Valid123", "Valid123", isRussian: false);

        Assert.IsNull(error);
    }

    [TestMethod]
    public void ValidatePasswordReset_ReturnsMismatchError()
    {
        var error = AuthInputValidator.ValidatePasswordReset("user@example.com", "Valid123", "Valid124", isRussian: false);

        Assert.AreEqual("Passwords do not match.", error);
    }
}
