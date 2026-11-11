using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3ActivityLogIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3ActivityLogIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsActivityLogWithNullableFieldsAndAllActions()
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
            foreach (var action in Enum.GetValues<ActivityActionType>())
            {
                context.ActivityLogs.Add(new ActivityLog
                {
                    WorkspaceId = graph.WorkspaceId,
                    UserId = graph.UserId,
                    EntityType = "Task",
                    EntityId = graph.TaskId,
                    ActionType = action,
                    Description = "Stage 3 activity",
                    OldValue = new string('o', 3000),
                    NewValue = new string('n', 3000),
                    IpAddress = "127.0.0.1",
                    UserAgent = new string('u', 1000)
                });
            }
            context.ActivityLogs.Add(new ActivityLog { EntityType = "Task", EntityId = graph.TaskId, ActionType = ActivityActionType.Created });
            await context.SaveChangesAsync();
            Assert.Equal(10, await context.ActivityLogs.CountAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask SetsActivityLogForeignKeysToNullWhenPrincipalsAreDeleted()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var userId = await Stage3TestData.CreateUserAsync(fixture, connection, transaction);
            var workspaceId = await Stage3TestData.CreateWorkspaceAsync(fixture, connection, transaction);
            var logId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.ActivityLogs.Add(new ActivityLog { Id = logId, WorkspaceId = workspaceId, UserId = userId, EntityType = "Task", EntityId = Guid.NewGuid(), ActionType = ActivityActionType.Updated });
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
                var activityLog = await afterUserDeleteContext.ActivityLogs.SingleAsync(a => a.Id == logId);
                Assert.Null(activityLog.UserId);
                Assert.Equal(workspaceId, activityLog.WorkspaceId);
            }
            await using (var deleteWorkspaceContext = fixture.CreateContext(connection))
            {
                deleteWorkspaceContext.Database.UseTransaction(transaction);
                var workspace = await deleteWorkspaceContext.Workspaces.SingleAsync(w => w.Id == workspaceId);
                deleteWorkspaceContext.Workspaces.Remove(workspace);
                await deleteWorkspaceContext.SaveChangesAsync();
            }
            await using (var afterWorkspaceDeleteContext = fixture.CreateContext(connection))
            {
                afterWorkspaceDeleteContext.Database.UseTransaction(transaction);
                var activityLog = await afterWorkspaceDeleteContext.ActivityLogs.SingleAsync(a => a.Id == logId);
                Assert.Null(activityLog.WorkspaceId);
                Assert.Null(activityLog.UserId);
            }
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
