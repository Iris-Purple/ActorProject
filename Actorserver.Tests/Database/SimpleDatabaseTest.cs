using ActorServer.Database;
using ActorServer.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ActorServer.Tests.Database;

[Collection("Database Collection")]
public class SimpleDatabaseTests
{
    private readonly SimpleDatabase _db;
    private readonly string _dbPath;
    private static int _testCounter = 0;
    
    public SimpleDatabaseTests(DatabaseFixture fixture)
    {
        _dbPath = fixture.TestDbPath;
        _db = fixture.TestDatabase;
        _testCounter++;
    }
    
    [Fact]
    public void Database_Should_Create_Table_On_Initialize()
    {
        // Assert - 테이블이 생성되었는지 확인
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='player_states'";
        var tableName = cmd.ExecuteScalar() as string;
        
        tableName.Should().Be("player_states");
    }
    
    [Fact]
    public void Should_Save_And_Load_Player_State()
    {
        // Arrange - 고유한 ID 사용
        var playerId = 1000L + _testCounter;
        var playerName = $"TestPlayer{_testCounter}";
        var x = 10.5f;
        var y = 20.3f;
        var zone = "forest";
        
        _db.SavePlayer(playerId, playerName, x, y, zone);

        var loaded = _db.LoadPlayer(playerId);
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
        var playerId = 2000L + _testCounter;

        _db.SavePlayer(playerId, "Player1", 10f, 10f, "town");
        _db.SavePlayer(playerId, "Player1", 50f, 60f, "dungeon-1");
        
        var loaded = _db.LoadPlayer(playerId);
        
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
        var nonExistentId = 99999L;
        var loaded = _db.LoadPlayer(nonExistentId);
        loaded.Should().BeNull();
    }
    
    [Fact]
    public void Should_Handle_Multiple_Players()
    {
        var baseId = 3000L + (_testCounter * 10);
        
        _db.SavePlayer(baseId + 1, "Player1", 10f, 20f, "town");
        _db.SavePlayer(baseId + 2, "Player2", 30f, 40f, "forest");
        _db.SavePlayer(baseId + 3, "Player3", 50f, 60f, "dungeon-1");
        
        var player1 = _db.LoadPlayer(baseId + 1);
        var player2 = _db.LoadPlayer(baseId + 2);
        var player3 = _db.LoadPlayer(baseId + 3);
        
        player1.Should().NotBeNull();
        player2.Should().NotBeNull();
        player3.Should().NotBeNull();
    }
}