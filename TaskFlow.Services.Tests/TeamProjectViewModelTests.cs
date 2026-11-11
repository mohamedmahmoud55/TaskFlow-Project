using System.ComponentModel.DataAnnotations;
using TaskFlow.Web.Models;

namespace TaskFlow.Services.Tests;

public sealed class TeamProjectViewModelTests
{
    [Fact]
    public void TeamNameIsRequired()
    {
        var results = Validate(new CreateTeamViewModel());

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateTeamViewModel.Name)));
    }

    [Fact]
    public void TeamNameMaximumLengthIsEnforced()
    {
        var results = Validate(new CreateTeamViewModel
        {
            Name = new string('t', 151)
        });

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateTeamViewModel.Name)));
    }

    [Fact]
    public void TeamDescriptionMaximumLengthIsEnforced()
    {
        var results = Validate(new CreateTeamViewModel
        {
            Name = "Platform",
            Description = new string('d', 1001)
        });

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateTeamViewModel.Description)));
    }

    [Fact]
    public void ProjectNameIsRequired()
    {
        var results = Validate(new CreateProjectViewModel());

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateProjectViewModel.Name)));
    }

    [Fact]
    public void ProjectNameMaximumLengthIsEnforced()
    {
        var results = Validate(new CreateProjectViewModel
        {
            Name = new string('p', 201)
        });

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateProjectViewModel.Name)));
    }

    [Fact]
    public void ProjectDescriptionMaximumLengthIsEnforced()
    {
        var results = Validate(new CreateProjectViewModel
        {
            Name = "Product Roadmap",
            Description = new string('d', 2001)
        });

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(CreateProjectViewModel.Description)));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
