using ActorServer.Database;
using Xunit;

namespace ActorServer.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public string TestDbPath { get; }
    public SimpleDatabase TestDatabase { get; }
    
    public DatabaseFixture()
    {
        TestDbPath = "test_collection.db";
        Console.WriteLine($"[FIXTURE] Creating test database: {TestDbPath}");
        TestDatabase = SimpleDatabase.CreateForTesting(TestDbPath);
    }
    
    public void Dispose()
    {
        Console.WriteLine($"[FIXTURE] Cleaning up database: {TestDbPath}");
        if (File.Exists(TestDbPath))
        {
            try
            {
                File.Delete(TestDbPath);
                Console.WriteLine($"[FIXTURE] Deleted test DB: {TestDbPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIXTURE] Failed to delete: {ex.Message}");
            }
        }
    }
}

[CollectionDefinition("Database Collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // 이 클래스는 비어있음 - xUnit이 사용
}