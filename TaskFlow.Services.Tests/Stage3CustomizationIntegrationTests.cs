using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3CustomizationIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3CustomizationIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsAutomationRulesAndCustomFieldDefinitions()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using var context = fixture.CreateContext(connection);
            context.Database.UseTransaction(transaction);
            context.AddRange(
                new AutomationRule { WorkspaceId = graph.WorkspaceId, CreatedByUserId = graph.UserId, Name = "Stage 3 rule", TriggerType = "TaskUpdated", ConditionsJson = "{}", ActionsJson = "{}", ExecutionCount = 2, LastExecutedAt = DateTime.UtcNow },
                new CustomFieldDefinition { WorkspaceId = graph.WorkspaceId, ProjectId = null, Name = "Workspace text", FieldType = CustomFieldType.Text },
                new CustomFieldDefinition { WorkspaceId = graph.WorkspaceId, ProjectId = graph.ProjectId, Name = "Project select", FieldType = CustomFieldType.Select, OptionsJson = "[\"A\",\"B\"]" });
            await context.SaveChangesAsync();
            Assert.Equal(1, await context.AutomationRules.CountAsync());
            Assert.Equal(2, await context.CustomFieldDefinitions.CountAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsAllCustomFieldValueTypesAndRejectsDuplicateDefinitionTaskPair()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var definitionId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.Add(new CustomFieldDefinition { Id = definitionId, WorkspaceId = graph.WorkspaceId, ProjectId = graph.ProjectId, Name = "Values", FieldType = CustomFieldType.Text });
                await context.SaveChangesAsync();
                context.Add(new CustomFieldValue { CustomFieldDefinitionId = definitionId, TaskId = graph.TaskId, TextValue = "text", NumberValue = 12.3456m, DateValue = DateTime.UtcNow.Date, BooleanValue = true, SelectedValue = "selected" });
                await context.SaveChangesAsync();
            }
            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.CustomFieldValues.Add(new CustomFieldValue { CustomFieldDefinitionId = definitionId, TaskId = graph.TaskId, TextValue = "duplicate" });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage3TestData.AssertUniqueViolation(exception);
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask CascadesCustomFieldValuesWhenDefinitionOrTaskIsDeleted()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var taskGraph = await Stage3TestData.CreateTaskAsync(fixture, connection, transaction);
            var definitionId = Guid.NewGuid();
            var valueId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new CustomFieldDefinition { Id = definitionId, WorkspaceId = taskGraph.WorkspaceId, ProjectId = taskGraph.ProjectId, Name = "Cascade definition", FieldType = CustomFieldType.Text },
                    new CustomFieldValue { Id = valueId, CustomFieldDefinitionId = definitionId, TaskId = taskGraph.TaskId, TextValue = "value" });
                await context.SaveChangesAsync();
            }
            await using (var deleteDefinitionContext = fixture.CreateContext(connection))
            {
                deleteDefinitionContext.Database.UseTransaction(transaction);
                var definition = await deleteDefinitionContext.CustomFieldDefinitions.SingleAsync(d => d.Id == definitionId);
                deleteDefinitionContext.CustomFieldDefinitions.Remove(definition);
                await deleteDefinitionContext.SaveChangesAsync();
            }
            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            Assert.False(await readContext.CustomFieldValues.AnyAsync(v => v.Id == valueId));

            var secondDefinitionId = Guid.NewGuid();
            var secondValueId = Guid.NewGuid();
            await using (var secondContext = fixture.CreateContext(connection))
            {
                secondContext.Database.UseTransaction(transaction);
                secondContext.AddRange(
                    new CustomFieldDefinition { Id = secondDefinitionId, WorkspaceId = taskGraph.WorkspaceId, ProjectId = taskGraph.ProjectId, Name = "Task cascade definition", FieldType = CustomFieldType.Text },
                    new CustomFieldValue { Id = secondValueId, CustomFieldDefinitionId = secondDefinitionId, TaskId = taskGraph.TaskId, TextValue = "task cascade" });
                await secondContext.SaveChangesAsync();
            }
            await using (var deleteTaskContext = fixture.CreateContext(connection))
            {
                deleteTaskContext.Database.UseTransaction(transaction);
                var task = await deleteTaskContext.Tasks.SingleAsync(t => t.Id == taskGraph.TaskId);
                deleteTaskContext.Tasks.Remove(task);
                await deleteTaskContext.SaveChangesAsync();
            }
            await using var afterTaskDeleteContext = fixture.CreateContext(connection);
            afterTaskDeleteContext.Database.UseTransaction(transaction);
            Assert.False(await afterTaskDeleteContext.CustomFieldValues.AnyAsync(v => v.Id == secondValueId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask RestrictsDeletingCustomFieldDefinitionWorkspaceAndProject()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var (workspaceId, teamId, projectId) = await Stage3TestData.CreateProjectAsync(fixture, connection, transaction);
            var definitionId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.CustomFieldDefinitions.Add(new CustomFieldDefinition { Id = definitionId, WorkspaceId = workspaceId, ProjectId = projectId, Name = "Restrict definition", FieldType = CustomFieldType.Text });
                await context.SaveChangesAsync();
            }
            await using (var deleteProjectContext = fixture.CreateContext(connection))
            {
                deleteProjectContext.Database.UseTransaction(transaction);
                var project = await deleteProjectContext.Projects.SingleAsync(p => p.Id == projectId);
                deleteProjectContext.Projects.Remove(project);
                await Assert.ThrowsAsync<DbUpdateException>(() => deleteProjectContext.SaveChangesAsync());
            }
            await using (var deleteWorkspaceContext = fixture.CreateContext(connection))
            {
                deleteWorkspaceContext.Database.UseTransaction(transaction);
                var team = await deleteWorkspaceContext.Teams.SingleAsync(t => t.Id == teamId);
                deleteWorkspaceContext.Teams.Remove(team);
                await deleteWorkspaceContext.SaveChangesAsync();
                var workspace = await deleteWorkspaceContext.Workspaces.SingleAsync(w => w.Id == workspaceId);
                deleteWorkspaceContext.Workspaces.Remove(workspace);
                await Assert.ThrowsAsync<DbUpdateException>(() => deleteWorkspaceContext.SaveChangesAsync());
            }
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
