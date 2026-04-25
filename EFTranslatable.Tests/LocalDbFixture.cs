using System;
using Microsoft.Data.SqlClient;

namespace EFTranslatable.Tests;

/// <summary>
/// Probes for SQL Server LocalDB availability once per test session and exposes a connection string
/// to a per-fixture-scoped database. Tests that need SQL Server should take this fixture and
/// Skip.If(!fixture.IsAvailable, "...") at the top.
/// </summary>
public sealed class LocalDbFixture : IDisposable
{
    private const string MasterConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

    public string DatabaseName { get; }
    public string ConnectionString { get; }
    public bool IsAvailable { get; }
    public string? UnavailableReason { get; }

    public LocalDbFixture()
    {
        DatabaseName = "EFTranslatable_Tests_" + Guid.NewGuid().ToString("N");
        ConnectionString =
            $@"Server=(localdb)\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

        try
        {
            using var connection = new SqlConnection(MasterConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{DatabaseName}];";
            cmd.ExecuteNonQuery();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = "SQL Server LocalDB is not available: " + ex.Message;
        }
    }

    public void Dispose()
    {
        if (!IsAvailable) return;

        try
        {
            using var connection = new SqlConnection(MasterConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // best effort cleanup
        }
    }
}
