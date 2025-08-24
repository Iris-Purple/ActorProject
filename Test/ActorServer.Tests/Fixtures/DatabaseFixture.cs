using Common.Database;
using Xunit;

namespace ActorServer.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public string TestDbPath { get; }
    private static readonly object _lock = new object();

    public DatabaseFixture()
    {
        lock (_lock)
        {
            Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");

            // 고정된 테스트 DB 경로 사용
            TestDbPath = "test_collection.db";
            Environment.SetEnvironmentVariable("TEST_DB_PATH", TestDbPath);

            Console.WriteLine($"[FIXTURE] Initializing test database: {TestDbPath}");

            // 기존 DB 파일들 정리
            CleanupDatabaseFiles();

            // PlayerDatabase 싱글톤 리셋하여 새 DB 생성
            PlayerDatabase.ResetInstance();

            // DB 초기화 확인
            var db = PlayerDatabase.Instance;
            Console.WriteLine($"[FIXTURE] Database initialized at: {db.GetDbPath()}");
        }
    }

    private void CleanupDatabaseFiles()
    {
        var files = new[] { TestDbPath, $"{TestDbPath}-shm", $"{TestDbPath}-wal" };
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                try
                {
                    // 파일 속성 변경 (읽기 전용 해제)
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    Console.WriteLine($"[FIXTURE] Deleted: {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FIXTURE] Could not delete {file}: {ex.Message}");
                }
            }
        }
    }
    public void Dispose()
    {
        lock (_lock)
        {
            Console.WriteLine($"[FIXTURE] Cleaning up database: {TestDbPath}");

            // 정리
            CleanupDatabaseFiles();

            Environment.SetEnvironmentVariable("TEST_DB_PATH", null);
            Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", null);

            PlayerDatabase.ResetInstance();
        }
    }
}

[CollectionDefinition("Database Collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // 이 클래스는 비어있음 - xUnit이 사용
}