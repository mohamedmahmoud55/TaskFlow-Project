using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;

namespace TaskFlow.Services.Tests;

public sealed class SqlServerIntegrationFixture
{
    public const string RequiredDatabaseName = "TaskFlowDB_Phase2_Test";
    private const string ConnectionEnvironmentVariable = "TASKFLOW_TEST_CONNECTION";

    private readonly string connectionString;

    public SqlServerIntegrationFixture()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionEnvironmentVariable} must be set for SQL Server integration tests.");
        }

        var builder = new SqlConnectionStringBuilder(configuredConnectionString);
        if (!string.Equals(builder.InitialCatalog, RequiredDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{ConnectionEnvironmentVariable} must target database '{RequiredDatabaseName}'.");
        }

        connectionString = builder.ConnectionString;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(connectionString);
    }

    public ApplicationDbContext CreateContext(SqlConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new ApplicationDbContext(options);
    }
}
