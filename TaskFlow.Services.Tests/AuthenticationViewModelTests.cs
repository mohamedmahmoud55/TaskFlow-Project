using System.ComponentModel.DataAnnotations;
using TaskFlow.Web.Models;

namespace TaskFlow.Services.Tests;

public sealed class AuthenticationViewModelTests
{
    [Fact]
    public void RegistrationRequiresMatchingPasswords()
    {
        var model = new RegisterViewModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Password = "Password1",
            ConfirmPassword = "Different1"
        };

        var validationResults = Validate(model);

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(RegisterViewModel.ConfirmPassword)));
    }

    [Fact]
    public void RegistrationRejectsInvalidEmail()
    {
        var model = new RegisterViewModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "not-an-email",
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var validationResults = Validate(model);

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(RegisterViewModel.Email)));
    }

    [Fact]
    public void LoginRequiresEmailAndPassword()
    {
        var validationResults = Validate(new LoginViewModel());

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(LoginViewModel.Email)));
        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(LoginViewModel.Password)));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
