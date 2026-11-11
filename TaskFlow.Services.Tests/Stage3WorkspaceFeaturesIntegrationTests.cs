using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3WorkspaceFeaturesIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3WorkspaceFeaturesIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsMilestonesAndInvitationStates()
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
            foreach (var status in Enum.GetValues<MilestoneStatus>())
                context.ProjectMilestones.Add(new ProjectMilestone { ProjectId = graph.ProjectId, Title = $"Milestone {status}", Description = "Stage 3", DueDate = DateTime.UtcNow.AddDays(1), Status = status, CompletedAt = status == MilestoneStatus.Completed ? DateTime.UtcNow : null });
            foreach (var status in Enum.GetValues<InvitationStatus>())
                context.WorkspaceInvitations.Add(new WorkspaceInvitation { WorkspaceId = graph.WorkspaceId, InvitedEmail = $"{status}@example.test", InvitedRole = WorkspaceRole.Member, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddDays(1), Status = status, InvitedByUserId = graph.UserId });
            await context.SaveChangesAsync();
            Assert.Equal(4, await context.ProjectMilestones.CountAsync());
            Assert.Equal(4, await context.WorkspaceInvitations.CountAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask RejectsDuplicateInvitationToken()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var token = Guid.NewGuid().ToString("N");
            var milestoneId = Guid.NewGuid();
            var invitationId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new ProjectMilestone { Id = milestoneId, ProjectId = graph.ProjectId, Title = "Cascade milestone" },
                    new WorkspaceInvitation { Id = invitationId, WorkspaceId = graph.WorkspaceId, InvitedEmail = "one@example.test", TokenHash = token, ExpiresAt = DateTime.UtcNow.AddDays(1) });
                await context.SaveChangesAsync();
            }
            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.WorkspaceInvitations.Add(new WorkspaceInvitation { WorkspaceId = graph.WorkspaceId, InvitedEmail = "two@example.test", TokenHash = token, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage3TestData.AssertUniqueViolation(exception);

        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsSavedFiltersAndViewsWithNullableScopes()
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
                new SavedFilter { UserId = graph.UserId, Name = "User filter", FilterDefinitionJson = "{}", IsDefault = true },
                new SavedFilter { UserId = graph.UserId, WorkspaceId = graph.WorkspaceId, ProjectId = graph.ProjectId, Name = "Scoped filter", Description = "Description", FilterDefinitionJson = "{\"status\":1}", IsShared = true },
                new SavedView { UserId = graph.UserId, Name = "Board view", ViewType = SavedViewType.Board, ConfigurationJson = "{}", IsDefault = true },
                new SavedView { UserId = graph.UserId, WorkspaceId = graph.WorkspaceId, ProjectId = graph.ProjectId, Name = "Calendar view", ViewType = SavedViewType.Calendar, ConfigurationJson = "{\"group\":true}", IsShared = true });
            await context.SaveChangesAsync();
            Assert.Equal(2, await context.SavedFilters.CountAsync());
            Assert.Equal(2, await context.SavedViews.CountAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask SetsSavedFilterAndViewScopesNullWhenWorkspaceAndProjectAreDeleted()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var userId = await Stage3TestData.CreateUserAsync(fixture, connection, transaction);
            var (workspaceId, teamId, projectId) = await Stage3TestData.CreateProjectAsync(fixture, connection, transaction);
            var filterId = Guid.NewGuid();
            var viewId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new SavedFilter { Id = filterId, UserId = userId, WorkspaceId = workspaceId, ProjectId = projectId, Name = "SetNull filter", FilterDefinitionJson = "{}" },
                    new SavedView { Id = viewId, UserId = userId, WorkspaceId = workspaceId, ProjectId = projectId, Name = "SetNull view", ViewType = SavedViewType.List, ConfigurationJson = "{}" });
                await context.SaveChangesAsync();
            }
            await using (var deleteProjectContext = fixture.CreateContext(connection))
            {
                deleteProjectContext.Database.UseTransaction(transaction);
                var project = await deleteProjectContext.Projects.SingleAsync(p => p.Id == projectId);
                deleteProjectContext.Projects.Remove(project);
                await deleteProjectContext.SaveChangesAsync();
            }
            await using (var afterProjectDeleteContext = fixture.CreateContext(connection))
            {
                afterProjectDeleteContext.Database.UseTransaction(transaction);
                var filter = await afterProjectDeleteContext.SavedFilters.SingleAsync(f => f.Id == filterId);
                var view = await afterProjectDeleteContext.SavedViews.SingleAsync(v => v.Id == viewId);
                Assert.Null(filter.ProjectId);
                Assert.Null(view.ProjectId);
                Assert.Equal(workspaceId, filter.WorkspaceId);
                Assert.Equal(workspaceId, view.WorkspaceId);
            }
            await using (var deleteWorkspaceContext = fixture.CreateContext(connection))
            {
                deleteWorkspaceContext.Database.UseTransaction(transaction);
                var team = await deleteWorkspaceContext.Teams.SingleAsync(t => t.Id == teamId);
                deleteWorkspaceContext.Teams.Remove(team);
                await deleteWorkspaceContext.SaveChangesAsync();
                var workspace = await deleteWorkspaceContext.Workspaces.SingleAsync(w => w.Id == workspaceId);
                deleteWorkspaceContext.Workspaces.Remove(workspace);
                await deleteWorkspaceContext.SaveChangesAsync();
            }
            await using (var afterWorkspaceDeleteContext = fixture.CreateContext(connection))
            {
                afterWorkspaceDeleteContext.Database.UseTransaction(transaction);
                var filter = await afterWorkspaceDeleteContext.SavedFilters.SingleAsync(f => f.Id == filterId);
                var view = await afterWorkspaceDeleteContext.SavedViews.SingleAsync(v => v.Id == viewId);
                Assert.Null(filter.WorkspaceId);
                Assert.Null(view.WorkspaceId);
            }
            await using var deleteUserContext = fixture.CreateContext(connection);
            deleteUserContext.Database.UseTransaction(transaction);
            var user = await deleteUserContext.Users.SingleAsync(u => u.Id == userId);
            deleteUserContext.Users.Remove(user);
            await Assert.ThrowsAsync<DbUpdateException>(() => deleteUserContext.SaveChangesAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask CascadesMilestonesAndInvitationsAndSetsInviterNull()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var userId = await Stage3TestData.CreateUserAsync(fixture, connection, transaction);
            var (workspaceId, _, projectId) = await Stage3TestData.CreateProjectAsync(fixture, connection, transaction);
            var milestoneId = Guid.NewGuid();
            var invitationId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new ProjectMilestone { Id = milestoneId, ProjectId = projectId, Title = "Cascade milestone" },
                    new WorkspaceInvitation { Id = invitationId, WorkspaceId = workspaceId, InvitedEmail = "cascade@example.test", TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddDays(1), InvitedByUserId = userId });
                await context.SaveChangesAsync();
            }
            await using (var deleteUserContext = fixture.CreateContext(connection))
            {
                deleteUserContext.Database.UseTransaction(transaction);
                var user = await deleteUserContext.Users.SingleAsync(u => u.Id == userId);
                deleteUserContext.Users.Remove(user);
                await deleteUserContext.SaveChangesAsync();
            }
            await using (var afterUserDeleteContext = fixture.CreateContext(connection))
            {
                afterUserDeleteContext.Database.UseTransaction(transaction);
                var invitation = await afterUserDeleteContext.WorkspaceInvitations.SingleAsync(i => i.Id == invitationId);
                Assert.Null(invitation.InvitedByUserId);
            }
            await using (var deleteProjectContext = fixture.CreateContext(connection))
            {
                deleteProjectContext.Database.UseTransaction(transaction);
                var project = await deleteProjectContext.Projects.SingleAsync(p => p.Id == projectId);
                deleteProjectContext.Projects.Remove(project);
                await deleteProjectContext.SaveChangesAsync();
            }
            await using (var afterProjectDeleteContext = fixture.CreateContext(connection))
            {
                afterProjectDeleteContext.Database.UseTransaction(transaction);
                Assert.False(await afterProjectDeleteContext.ProjectMilestones.AnyAsync(m => m.Id == milestoneId));
            }
            await using (var deleteWorkspaceContext = fixture.CreateContext(connection))
            {
                deleteWorkspaceContext.Database.UseTransaction(transaction);
                var workspace = await deleteWorkspaceContext.Workspaces.SingleAsync(w => w.Id == workspaceId);
                deleteWorkspaceContext.Workspaces.Remove(workspace);
                await deleteWorkspaceContext.SaveChangesAsync();
            }
            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            Assert.False(await readContext.WorkspaceInvitations.AnyAsync(i => i.Id == invitationId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
