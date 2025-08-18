using ActorServer.Database;
using ActorServer.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ActorServer.Tests.Database;

// ⭐ Collection 사용 - 모든 테스트가 같은 DB 공유
[Collection("Database Collection")]
public class SimpleDatabaseTests
{
    private readonly DatabaseFixture _fixture;
    private static int _testCounter = 0; // ⭐ 테스트별 고유 ID 생성용
    
    public SimpleDatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _testCounter++;
    }
    
    [Fact]
    public void Database_Should_Create_Table_On_Initialize()
    {
        // Assert - 테이블이 생성되었는지 확인
        using var conn = new SqliteConnection($"Data Source={_fixture.TestDbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='player_states'";
        var tableName = cmd.ExecuteScalar() as string;
        
        tableName.Should().Be("player_states");
    }
    
    [Fact]
    public void Should_Save_And_Load_Player_State()
    {
        // Arrange - ⭐ 고유한 ID 사용
        var db = SimpleDatabase.Instance;
        var playerId = 1000L + _testCounter;
        var playerName = $"TestPlayer{_testCounter}";
        var x = 10.5f;
        var y = 20.3f;
        var zone = "forest";
        
        // Act - 저장
        db.SavePlayer(playerId, playerName, x, y, zone);
        
        // Act - 로드
        var loaded = db.LoadPlayer(playerId);
        
        // Assert
        loaded.Should().NotBeNull("Player state should be loaded");
        if (loaded is { } state)
        {
            state.x.Should().BeApproximately(x, 0.01f);
            state.y.Should().BeApproximately(y, 0.01f);
            state.zone.Should().Be(zone);
        }
    }
    
    [Fact]
    public void Should_Update_Existing_Player()
    {
        // Arrange - ⭐ 고유한 ID 사용
        var db = SimpleDatabase.Instance;
        var playerId = 2000L + _testCounter;
        
        // Act - 첫 번째 저장
        db.SavePlayer(playerId, "Player1", 10f, 10f, "town");
        
        // Act - 업데이트
        db.SavePlayer(playerId, "Player1", 50f, 60f, "dungeon-1");
        
        // Act - 로드
        var loaded = db.LoadPlayer(playerId);
        
        // Assert
        loaded.Should().NotBeNull();
        if (loaded is { } state)
        {
            state.x.Should().Be(50f);
            state.y.Should().Be(60f);
            state.zone.Should().Be("dungeon-1");
        }
    }
    
    [Fact]
    public void Should_Return_Null_For_NonExistent_Player()
    {
        // Arrange
        var db = SimpleDatabase.Instance;
        var nonExistentId = 99999L; // ⭐ 충분히 큰 ID
        
        // Act
        var loaded = db.LoadPlayer(nonExistentId);
        
        // Assert
        loaded.Should().BeNull();
    }
    
    [Fact] 
    public void Should_Handle_Multiple_Players()
    {
        // Arrange - ⭐ 고유한 ID 범위 사용
        var db = SimpleDatabase.Instance;
        var baseId = 3000L + (_testCounter * 10);
        
        // Act - 여러 플레이어 저장
        db.SavePlayer(baseId + 1, "Player1", 10f, 20f, "town");
        db.SavePlayer(baseId + 2, "Player2", 30f, 40f, "forest");
        db.SavePlayer(baseId + 3, "Player3", 50f, 60f, "dungeon-1");
        
        // Act - 각각 로드
        var player1 = db.LoadPlayer(baseId + 1);
        var player2 = db.LoadPlayer(baseId + 2);
        var player3 = db.LoadPlayer(baseId + 3);
        
        // Assert
        player1.Should().NotBeNull();
        player2.Should().NotBeNull();
        player3.Should().NotBeNull();
    }
    
    // ⭐ IDisposable 제거 - Fixture가 처리
}