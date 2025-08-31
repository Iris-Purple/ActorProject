using Xunit;

namespace AuthServer.Tests;

/// <summary>
/// AuthServer 테스트 Collection 정의
/// 테스트 간 격리를 보장하고 순차 실행을 위한 설정
/// </summary>
[CollectionDefinition("AuthServerTests")]
public class AuthServerTestCollection : ICollectionFixture<DatabaseFixture>
{
    // Collection marker class
}

/// <summary>
/// 데이터베이스 초기화 Fixture
/// 테스트 시작 전 DB 초기화 및 종료 후 정리
/// </summary>
public class DatabaseFixture : IDisposable
{
    public DatabaseFixture()
    {
        // 테스트 환경 설정
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
        
        // 테스트 DB 파일 초기화
        CleanupTestDatabase();
        
        Console.WriteLine("[DatabaseFixture] Test database initialized");
    }

    private void CleanupTestDatabase()
    {
        try
        {
            // 테스트 DB 파일 삭제 (있으면)
            var testDbPath = "test_collection.db";
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
                Console.WriteLine($"[DatabaseFixture] Deleted existing test DB: {testDbPath}");
            }

            // WAL 및 SHM 파일도 정리
            var walPath = testDbPath + "-wal";
            var shmPath = testDbPath + "-shm";
            
            if (File.Exists(walPath)) File.Delete(walPath);
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseFixture] Warning: Could not clean test DB: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // 테스트 완료 후 정리 (선택적)
        // CleanupTestDatabase(); // 디버깅을 위해 주석 처리
        Console.WriteLine("[DatabaseFixture] Test database fixture disposed");
    }
}