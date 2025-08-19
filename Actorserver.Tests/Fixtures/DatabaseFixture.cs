using ActorServer.Database;
using Xunit;

namespace ActorServer.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public string TestDbPath { get; }
    
    public DatabaseFixture()
    {
        // 테스트 환경 변수 설정 (선택적 - 자동 감지도 작동)
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");

        TestDbPath = "test_collection.db";
        Console.WriteLine($"[FIXTURE] Initializing test database: {TestDbPath}");
        // 기존 파일 있으면 삭제
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
        
        // 첫 접근 시 초기화됨
        var db = SimpleDatabase.Instance;
        Console.WriteLine($"[FIXTURE] Database initialized: {db.GetDbPath()}");
    }
    
    public void Dispose()
    {
        Console.WriteLine($"[FIXTURE] Cleaning up database: {TestDbPath}");
        
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", null);
        
        if (File.Exists(TestDbPath))
        {
            try
            {
                // SQLite 연결이 남아있을 수 있으므로 GC 강제 실행
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