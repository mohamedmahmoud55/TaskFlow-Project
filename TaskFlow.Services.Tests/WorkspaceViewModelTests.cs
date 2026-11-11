using System.ComponentModel.DataAnnotations;
using TaskFlow.Web.Models;

namespace TaskFlow.Services.Tests;

public sealed class WorkspaceViewModelTests
{
    [Fact]
    public void CreateWorkspaceRequiresName()
    {
        var validationResults = Validate(new CreateWorkspaceViewModel
        {
            Description = "Workspace description"
        });

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(CreateWorkspaceViewModel.Name)));
    }

    [Fact]
    public void CreateWorkspaceRejectsNameLongerThanConfiguredLimit()
    {
        var validationResults = Validate(new CreateWorkspaceViewModel
        {
            Name = new string('n', 151)
        });

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(CreateWorkspaceViewModel.Name)));
    }

    [Fact]
    public void CreateWorkspaceRejectsDescriptionLongerThanConfiguredLimit()
    {
        var validationResults = Validate(new CreateWorkspaceViewModel
        {
            Name = "Engineering",
            Description = new string('d', 1001)
        });

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(CreateWorkspaceViewModel.Description)));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
