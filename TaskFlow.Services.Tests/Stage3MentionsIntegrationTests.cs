using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3MentionsIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3MentionsIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsMentionsAndRejectsDuplicateCommentUserPair()
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
                context.TaskCommentMentions.Add(new TaskCommentMention { TaskCommentId = prerequisites.CommentId, MentionedUserId = prerequisites.SecondaryUserId });
                await context.SaveChangesAsync();
            }
            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.TaskCommentMentions.Add(new TaskCommentMention { TaskCommentId = prerequisites.CommentId, MentionedUserId = prerequisites.SecondaryUserId });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage3TestData.AssertUniqueViolation(exception);
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask CascadesMentionWhenCommentIsDeletedAndRestrictsMentionedUserDeletion()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var prerequisites = await Stage3TestData.CreateAsync(fixture, connection, transaction, graph);
            var mentionId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.TaskCommentMentions.Add(new TaskCommentMention { Id = mentionId, TaskCommentId = prerequisites.CommentId, MentionedUserId = prerequisites.SecondaryUserId });
                await context.SaveChangesAsync();
            }
            await using (var deleteUserContext = fixture.CreateContext(connection))
            {
                deleteUserContext.Database.UseTransaction(transaction);
                var user = await deleteUserContext.Users.SingleAsync(u => u.Id == prerequisites.SecondaryUserId);
                deleteUserContext.Users.Remove(user);
                await Assert.ThrowsAsync<DbUpdateException>(() => deleteUserContext.SaveChangesAsync());
            }
            await using (var deleteCommentContext = fixture.CreateContext(connection))
            {
                deleteCommentContext.Database.UseTransaction(transaction);
                var comment = await deleteCommentContext.TaskComments.SingleAsync(c => c.Id == prerequisites.CommentId);
                deleteCommentContext.TaskComments.Remove(comment);
                await deleteCommentContext.SaveChangesAsync();
            }
            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            Assert.False(await readContext.TaskCommentMentions.AnyAsync(m => m.Id == mentionId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
