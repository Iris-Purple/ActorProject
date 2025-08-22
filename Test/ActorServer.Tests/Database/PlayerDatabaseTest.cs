using ActorServer.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;
using Common.Database;

namespace ActorServer.Tests.Database;

[Collection("Database Collection")]
public class PlayerDatabaseTests
{
    private readonly DatabaseFixture _fixture;
    private static int _testCounter = 0;
    
    public PlayerDatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _testCounter++;
    }
    
    [Fact]
    public void Should_Use_Test_Database()
    {
        // 테스트 환경에서는 자동으로 test_collection.db 사용
        var db = PlayerDatabase.Instance;
        
        // Assert
        db.GetDbPath().Should().Be("test_collection.db");
        Console.WriteLine($"[TEST] Using database: {db.GetDbPath()}");
    }
    
    [Fact]
    public void Should_Auto_Generate_Player_Id()
    {
        // SimpleDatabase.Instance 직접 사용 (test_collection.db)
        var db = PlayerDatabase.Instance;
        var playerName = $"AutoPlayer{_testCounter}";
        
        // Act
        var playerId1 = db.GetOrCreatePlayerId(playerName);
        var playerId2 = db.GetOrCreatePlayerId($"AutoPlayer{_testCounter}_2");
        
        // Assert
        playerId1.Should().BeGreaterThan(0);
        playerId2.Should().BeGreaterThan(playerId1);
        Console.WriteLine($"Generated IDs: {playerId1}, {playerId2}");
    }
    
    [Fact]
    public void Should_Return_Same_Id_For_Existing_Player()
    {
        var db = PlayerDatabase.Instance;
        var playerName = $"ExistingPlayer{_testCounter}";
        
        // Act
        var firstId = db.GetOrCreatePlayerId(playerName);
        var secondId = db.GetOrCreatePlayerId(playerName);
        
        // Assert
        secondId.Should().Be(firstId, "Same player should get same ID");
    }
    
    [Fact]
    public void Should_Load_Complete_Player_Data()
    {
        var db = PlayerDatabase.Instance;
        var playerName = $"CompletePlayer{_testCounter}";
        
        // Act
        var playerId = db.GetOrCreatePlayerId(playerName);
        db.SavePlayer(playerId, playerName, 100f, 200f, "forest");
        var data = db.LoadPlayerData(playerId);
        
        // Assert
        data.Should().NotBeNull();
        data!.PlayerId.Should().Be(playerId);
        data.PlayerName.Should().Be(playerName);
        data.X.Should().Be(100f);
        data.Y.Should().Be(200f);
        data.ZoneId.Should().Be("forest");
    }
    
    [Fact]
    public void Should_Get_Player_Name_By_Id()
    {
        var db = PlayerDatabase.Instance;
        var playerName = $"NameLookup{_testCounter}";
        
        // Act
        var playerId = db.GetOrCreatePlayerId(playerName);
        var retrievedName = db.GetPlayerNameById(playerId);
        
        // Assert
        retrievedName.Should().Be(playerName);
    }
    
    [Fact]
    public void Should_Track_First_And_Last_Login()
    {
        var db = PlayerDatabase.Instance;
        var playerName = $"LoginTracker{_testCounter}";
        
        // Act
        var playerId = db.GetOrCreatePlayerId(playerName);
        var firstData = db.LoadPlayerData(playerId);
        
        Thread.Sleep(1000);

        // reconnect 
        db.GetOrCreatePlayerId(playerName);
        var secondData = db.LoadPlayerData(playerId);
        
        // Assert
        firstData.Should().NotBeNull();
        secondData.Should().NotBeNull();
        secondData!.FirstLogin.Should().Be(firstData!.FirstLogin);
        secondData.LastLogin.Should().NotBe(firstData.LastLogin);
    }
    
    [Fact]
    public void Database_Should_Create_Table_On_Initialize()
    {
        // test_collection.db 직접 확인
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
        var db = PlayerDatabase.Instance;
        var playerName = $"TestPlayer{_testCounter}";
        var playerId = db.GetOrCreatePlayerId(playerName);
        
        var x = 10.5f;
        var y = 20.3f;
        var zone = "forest";
        
        db.SavePlayer(playerId, playerName, x, y, zone);
        var loaded = db.LoadPlayer(playerId);
        
        loaded.Should().NotBeNull("Player state should be loaded");
        if (loaded is { } state)
        {
            state.x.Should().BeApproximately(x, 0.01f);
            state.y.Should().BeApproximately(y, 0.01f);
            state.zone.Should().Be(zone);
        }
    }
}