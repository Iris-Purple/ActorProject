using System;
using System.IO;
using ActorServer.Database;
using Xunit;

namespace ActorServer.Tests.Fixtures;

// ⭐ 테스트 컬렉션 전체에서 공유되는 Fixture
public class DatabaseFixture : IDisposable
{
    public string TestDbPath { get; }
    
    public DatabaseFixture()
    {
        TestDbPath = "test_collection.db";
        
        // ⭐ 테스트 시작 시 한 번만 초기화
        Console.WriteLine($"[FIXTURE] Initializing database: {TestDbPath}");
        
        // 기존 파일 있으면 삭제
        if (File.Exists(TestDbPath))
        {
            File.Delete(TestDbPath);
        }
        
        SimpleDatabase.InitializeForTesting(TestDbPath);
    }
    
    public void Dispose()
    {
        // ⭐ 모든 테스트 끝나고 한 번만 정리
        Console.WriteLine($"[FIXTURE] Cleaning up database: {TestDbPath}");
        
        SimpleDatabase.ResetInstance();
        
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

// ⭐ Collection Definition
[CollectionDefinition("Database Collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // 이 클래스는 비어있음 - xUnit이 사용
}