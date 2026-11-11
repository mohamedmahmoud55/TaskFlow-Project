using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3NotificationsIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3NotificationsIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsNotificationsWithNullableFieldsAndAllTypes()
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
            foreach (var type in Enum.GetValues<NotificationType>())
            {
                context.Notifications.Add(new Notification
                {
                    UserId = graph.UserId,
                    Type = type,
                    Title = $"Stage 3 {type}",
                    Message = new string('m', 2000),
                    RelatedEntityType = "Task",
                    RelatedEntityId = graph.TaskId,
                    ActionUrl = "/tasks/" + graph.TaskId,
                    IsRead = type == NotificationType.Comment,
                    ReadAt = type == NotificationType.Comment ? DateTime.UtcNow : null,
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                });
            }
            await context.SaveChangesAsync();
            Assert.Equal(5, await context.Notifications.CountAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask RestrictsDeletingNotificationUser()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var prerequisites = await Stage3TestData.CreateAsync(fixture, connection, transaction, graph);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.Notifications.Add(new Notification { UserId = prerequisites.SecondaryUserId, Type = NotificationType.Assignment, Title = "Notice", Message = "Message" });
                await context.SaveChangesAsync();
            }
            await using var deleteContext = fixture.CreateContext(connection);
            deleteContext.Database.UseTransaction(transaction);
            var user = await deleteContext.Users.SingleAsync(u => u.Id == prerequisites.SecondaryUserId);
            deleteContext.Users.Remove(user);
            await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
