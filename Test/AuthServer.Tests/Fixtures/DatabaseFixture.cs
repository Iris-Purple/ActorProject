using Xunit;

namespace AuthServer.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public string TestDbPath { get; }
    
    public DatabaseFixture()
    {
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");

        TestDbPath = "test_collection.db";
        Console.WriteLine($"[FIXTURE] Initializing test database: {TestDbPath}");
        
        if (File.Exists(TestDbPath))
        {
            try
            {
                File.Delete(TestDbPath);
                Console.WriteLine($"[FIXTURE] Deleted existing test DB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FIXTURE] Could not delete existing DB: {ex.Message}");
            }
        }
    }
    
    public void Dispose()
    {
        Console.WriteLine($"[FIXTURE] Cleaning up database: {TestDbPath}");
        
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", null);
        
        if (File.Exists(TestDbPath))
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
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