using Xunit;

namespace TaskFlow.Services.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
    public const string Name = "SQL Server integration tests";
}
