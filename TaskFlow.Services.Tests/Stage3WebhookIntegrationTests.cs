using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3WebhookIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3WebhookIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsWebhookSubscriptionsAndDeliveriesWithProtectedSecret()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var subscriptionId = Guid.NewGuid();
            var protectedSecret = new string('p', 1000);
            await using var context = fixture.CreateContext(connection);
            context.Database.UseTransaction(transaction);
            context.AddRange(
                new WebhookSubscription { Id = subscriptionId, WorkspaceId = graph.WorkspaceId, CreatedByUserId = graph.UserId, EndpointUrl = "https://example.test/hooks", ProtectedSecret = protectedSecret, EventTypesJson = "[\"task.updated\"]", Status = WebhookSubscriptionStatus.Active },
                new WebhookDelivery { WebhookSubscriptionId = subscriptionId, EventType = "task.updated", ResponseStatusCode = 200, ResponseBody = new string('r', 5000), Succeeded = true, CompletedAt = DateTime.UtcNow },
                new WebhookDelivery { WebhookSubscriptionId = subscriptionId, EventType = "task.failed", ErrorMessage = "failure", Succeeded = false });
            await context.SaveChangesAsync();
            var subscription = await context.WebhookSubscriptions.SingleAsync(s => s.Id == subscriptionId);
            Assert.Equal(protectedSecret, subscription.ProtectedSecret);
            Assert.Equal(2, await context.WebhookDeliveries.CountAsync(d => d.WebhookSubscriptionId == subscriptionId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask RestrictsCreatorDeletionAndCascadesDeliveriesAndSubscriptionOnWorkspaceDeletion()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var subscriptionId = Guid.NewGuid();
            var deliveryId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new WebhookSubscription { Id = subscriptionId, WorkspaceId = graph.WorkspaceId, CreatedByUserId = graph.UserId, EndpointUrl = "https://example.test/hooks", ProtectedSecret = "protected", EventTypesJson = "[]" },
                    new WebhookDelivery { Id = deliveryId, WebhookSubscriptionId = subscriptionId, EventType = "task.created", Succeeded = true });
                await context.SaveChangesAsync();
            }
            await using (var deleteUserContext = fixture.CreateContext(connection))
            {
                deleteUserContext.Database.UseTransaction(transaction);
                var user = await deleteUserContext.Users.SingleAsync(u => u.Id == graph.UserId);
                deleteUserContext.Users.Remove(user);
                await Assert.ThrowsAsync<DbUpdateException>(() => deleteUserContext.SaveChangesAsync());
            }
            await using (var deleteWorkspaceContext = fixture.CreateContext(connection))
            {
                deleteWorkspaceContext.Database.UseTransaction(transaction);
                var workspace = await deleteWorkspaceContext.Workspaces.SingleAsync(w => w.Id == graph.WorkspaceId);
                workspace.Members.Clear();
                deleteWorkspaceContext.Workspaces.Remove(workspace);
                await deleteWorkspaceContext.SaveChangesAsync();
            }
            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            Assert.False(await readContext.WebhookSubscriptions.AnyAsync(s => s.Id == subscriptionId));
            Assert.False(await readContext.WebhookDeliveries.AnyAsync(d => d.Id == deliveryId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
