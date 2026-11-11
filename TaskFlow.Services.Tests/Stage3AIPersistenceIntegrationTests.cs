using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage3AIPersistenceIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage3AIPersistenceIntegrationTests(SqlServerIntegrationFixture fixture) => this.fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsRecommendationsAtScoreBoundariesAndAllOptionalStates()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var prerequisites = await Stage3TestData.CreateAsync(fixture, connection, transaction, graph);
            await using var context = fixture.CreateContext(connection);
            context.Database.UseTransaction(transaction);
            context.TaskAIRecommendations.AddRange(
                new TaskAIRecommendation { TaskId = graph.TaskId, RequestedByUserId = graph.UserId, SuggestedPriority = TaskPriority.Low, PriorityScore = 0, Explanation = "Low", ModelProvider = "provider" },
                new TaskAIRecommendation { TaskId = graph.TaskId, RequestedByUserId = graph.UserId, SuggestedPriority = TaskPriority.High, PriorityScore = 100, Explanation = "High", ModelProvider = "provider", ModelName = "model", IsApproved = true, ApprovedByUserId = prerequisites.SecondaryUserId, ApprovedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            Assert.Equal(2, await context.TaskAIRecommendations.CountAsync());
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [Trait("Category", "Integration")]
    public async AsyncTask RejectsAnalysisItemPriorityScoresOutsideRange(int score)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var sessionId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AIAnalysisSessions.Add(new AIAnalysisSession { Id = sessionId, WorkspaceId = graph.WorkspaceId, RequestedByUserId = graph.UserId, ModelProvider = "provider", StartedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }
            await using var itemContext = fixture.CreateContext(connection);
            itemContext.Database.UseTransaction(transaction);
            itemContext.AIAnalysisItems.Add(new AIAnalysisItem { AIAnalysisSessionId = sessionId, TaskId = graph.TaskId, SuggestedPriority = TaskPriority.Medium, PriorityScore = score, Explanation = "Invalid" });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => itemContext.SaveChangesAsync());
            Stage3TestData.AssertCheckConstraintViolation(exception);
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [Trait("Category", "Integration")]
    public async AsyncTask RejectsRecommendationPriorityScoresOutsideRange(int score)
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
            context.TaskAIRecommendations.Add(new TaskAIRecommendation { TaskId = graph.TaskId, RequestedByUserId = graph.UserId, SuggestedPriority = TaskPriority.Medium, PriorityScore = score, Explanation = "Invalid", ModelProvider = "provider" });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Stage3TestData.AssertCheckConstraintViolation(exception);
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsAnalysisSessionAndItemsAndRejectsDuplicateSessionTaskPair()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var sessionId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AIAnalysisSessions.Add(new AIAnalysisSession { Id = sessionId, WorkspaceId = graph.WorkspaceId, RequestedByUserId = graph.UserId, Status = AIAnalysisStatus.Completed, ModelProvider = "provider", ResultSummary = "result", StartedAt = DateTime.UtcNow.AddMinutes(-1), CompletedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
                context.AIAnalysisItems.Add(new AIAnalysisItem { AIAnalysisSessionId = sessionId, TaskId = graph.TaskId, SuggestedPriority = TaskPriority.Medium, PriorityScore = 100, Explanation = "item", RiskLevel = "Low" });
                await context.SaveChangesAsync();
            }
            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.AIAnalysisItems.Add(new AIAnalysisItem { AIAnalysisSessionId = sessionId, TaskId = graph.TaskId, SuggestedPriority = TaskPriority.Medium, PriorityScore = 0, Explanation = "duplicate" });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage3TestData.AssertUniqueViolation(exception);
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask CascadesAnalysisItemsWhenSessionIsDeletedAndRestrictsTaskDeletion()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            var sessionId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new AIAnalysisSession { Id = sessionId, WorkspaceId = graph.WorkspaceId, RequestedByUserId = graph.UserId, ModelProvider = "provider", StartedAt = DateTime.UtcNow },
                    new AIAnalysisItem { Id = itemId, AIAnalysisSessionId = sessionId, TaskId = graph.TaskId, SuggestedPriority = TaskPriority.Medium, PriorityScore = 50, Explanation = "item" });
                await context.SaveChangesAsync();
            }
            await using (var deleteTaskContext = fixture.CreateContext(connection))
            {
                deleteTaskContext.Database.UseTransaction(transaction);
                var task = await deleteTaskContext.Tasks.SingleAsync(t => t.Id == graph.TaskId);
                deleteTaskContext.Tasks.Remove(task);
                await Assert.ThrowsAsync<DbUpdateException>(() => deleteTaskContext.SaveChangesAsync());
            }
            await using (var deleteSessionContext = fixture.CreateContext(connection))
            {
                deleteSessionContext.Database.UseTransaction(transaction);
                var session = await deleteSessionContext.AIAnalysisSessions.SingleAsync(s => s.Id == sessionId);
                deleteSessionContext.AIAnalysisSessions.Remove(session);
                await deleteSessionContext.SaveChangesAsync();
            }
            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            Assert.False(await readContext.AIAnalysisItems.AnyAsync(i => i.Id == itemId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask EnforcesRecommendationUserRelationshipsAndTaskCascade()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;
        try
        {
            var taskGraph = await Stage3TestData.CreateTaskAsync(fixture, connection, transaction);
            var approverId = await Stage3TestData.CreateUserAsync(fixture, connection, transaction);
            var requesterId = await Stage3TestData.CreateUserAsync(fixture, connection, transaction);
            var recommendationId = Guid.NewGuid();
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.TaskAIRecommendations.Add(new TaskAIRecommendation { Id = recommendationId, TaskId = taskGraph.TaskId, RequestedByUserId = requesterId, ApprovedByUserId = approverId, IsApproved = true, ApprovedAt = DateTime.UtcNow, SuggestedPriority = TaskPriority.High, PriorityScore = 90, Explanation = "Recommendation", ModelProvider = "provider" });
                await context.SaveChangesAsync();
            }
            await using (var deleteApproverContext = fixture.CreateContext(connection))
            {
                deleteApproverContext.Database.UseTransaction(transaction);
                var approver = await deleteApproverContext.Users.SingleAsync(u => u.Id == approverId);
                deleteApproverContext.Users.Remove(approver);
                await deleteApproverContext.SaveChangesAsync();
            }
            await using (var afterApproverDeleteContext = fixture.CreateContext(connection))
            {
                afterApproverDeleteContext.Database.UseTransaction(transaction);
                var recommendation = await afterApproverDeleteContext.TaskAIRecommendations.SingleAsync(r => r.Id == recommendationId);
                Assert.Null(recommendation.ApprovedByUserId);
            }
            await using (var deleteRequesterContext = fixture.CreateContext(connection))
            {
                deleteRequesterContext.Database.UseTransaction(transaction);
                var requester = await deleteRequesterContext.Users.SingleAsync(u => u.Id == requesterId);
                deleteRequesterContext.Users.Remove(requester);
                await Assert.ThrowsAsync<DbUpdateException>(() => deleteRequesterContext.SaveChangesAsync());
            }
            await using (var deleteTaskContext = fixture.CreateContext(connection))
            {
                deleteTaskContext.Database.UseTransaction(transaction);
                var task = await deleteTaskContext.Tasks.SingleAsync(t => t.Id == taskGraph.TaskId);
                deleteTaskContext.Tasks.Remove(task);
                await deleteTaskContext.SaveChangesAsync();
            }
            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            Assert.False(await readContext.TaskAIRecommendations.AnyAsync(r => r.Id == recommendationId));
        }
        catch (Exception exception) { originalException = exception; throw; }
        finally { await Stage3TestData.CleanupAsync(transaction, originalException); }
    }
}
