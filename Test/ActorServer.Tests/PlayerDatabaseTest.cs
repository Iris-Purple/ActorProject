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
    private static long _testPlayerId = 2000;  // 변경: 테스트용 PlayerId 시작값
    
    public PlayerDatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _testPlayerId++;  // 변경: 각 테스트마다 고유한 ID 사용
    }
    
    [Fact]
    public void Should_Use_Test_Database()
    {
        var db = PlayerDatabase.Instance;
        
        // Assert
        db.GetDbPath().Should().Be("test_collection.db");
        Console.WriteLine($"[TEST] Using database: {db.GetDbPath()}");
    }
    
    [Fact]
    public void Should_Create_New_Player_State()
    {
        // 변경: PlayerId를 직접 전달
        var db = PlayerDatabase.Instance;
        var playerId = _testPlayerId;
        
        // Act
        var resultId = db.GetOrCreatePlayerId(playerId);
        
        // Assert
        resultId.Should().Be(playerId);
        
        // DB에서 확인
        var data = db.LoadPlayerData(playerId);
        data.Should().NotBeNull();
        data!.PlayerId.Should().Be(playerId);
        data.ZoneId.Should().Be("town");  // 기본값
        data.X.Should().Be(0);  // 기본값
        data.Y.Should().Be(0);  // 기본값
    }
    
    [Fact]
    public void Should_Return_Same_Id_For_Existing_Player()
    {
        var db = PlayerDatabase.Instance;
        var playerId = _testPlayerId;
        
        // Act
        var firstCall = db.GetOrCreatePlayerId(playerId);
        var secondCall = db.GetOrCreatePlayerId(playerId);
        
        // Assert
        secondCall.Should().Be(firstCall);
        secondCall.Should().Be(playerId);
    }
    
    [Fact]
    public void Should_Load_Complete_Player_Data()
    {
        var db = PlayerDatabase.Instance;
        var playerId = _testPlayerId;

        // Act
        db.GetOrCreatePlayerId(playerId);
        db.SavePlayer(playerId, 100f, 200f, "forest");
        var data = db.LoadPlayerData(playerId);
        
        // Assert
        data.Should().NotBeNull();
        data!.PlayerId.Should().Be(playerId);
        data.X.Should().Be(100f);
        data.Y.Should().Be(200f);
        data.ZoneId.Should().Be("forest");
    }
    
    [Fact]
    public void Should_Track_First_And_Last_Login()
    {
        var db = PlayerDatabase.Instance;
        var playerId = _testPlayerId;
        
        // Act
        db.GetOrCreatePlayerId(playerId);
        var firstData = db.LoadPlayerData(playerId);
        
        Thread.Sleep(1000);
        
        // 재접속 시뮬레이션
        db.GetOrCreatePlayerId(playerId);  // 변경: 동일한 ID로 다시 호출
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
        var playerId = _testPlayerId;
        
        // 변경: PlayerId 먼저 생성
        db.GetOrCreatePlayerId(playerId);
        
        var x = 10.5f;
        var y = 20.3f;
        var zone = "forest";
        
        db.SavePlayer(playerId, x, y, zone);
        var loaded = db.LoadPlayer(playerId);
        
        loaded.Should().NotBeNull("Player state should be loaded");
        if (loaded is { } state)
        {
            state.x.Should().BeApproximately(x, 0.01f);
            state.y.Should().BeApproximately(y, 0.01f);
            state.zone.Should().Be(zone);
        }
    }
    
    [Fact]
    public void Should_Handle_Multiple_Players()
    {
        // 추가: 여러 플레이어 동시 처리 테스트
        var db = PlayerDatabase.Instance;
        var playerIds = new List<long>();
        
        // 5명의 플레이어 생성
        for (int i = 0; i < 5; i++)
        {
            var playerId = _testPlayerId++;
            playerIds.Add(playerId);
            db.GetOrCreatePlayerId(playerId);
        }
        
        // 각 플레이어 데이터 저장
        for (int i = 0; i < playerIds.Count; i++)
        {
            db.SavePlayer(playerIds[i], i * 10f, i * 20f, $"zone-{i}");
        }
        
        // 검증
        for (int i = 0; i < playerIds.Count; i++)
        {
            var data = db.LoadPlayerData(playerIds[i]);
            data.Should().NotBeNull();
            data!.X.Should().Be(i * 10f);
            data!.Y.Should().Be(i * 20f);
            data!.ZoneId.Should().Be($"zone-{i}");
        }
    }
    
    [Fact]
    public void Should_Update_Last_Login_On_Reconnect()
    {
        // 추가: 재접속 시 last_login 업데이트 확인
        var db = PlayerDatabase.Instance;
        var playerId = _testPlayerId;
        
        // 첫 접속
        db.GetOrCreatePlayerId(playerId);
        var firstLogin = db.LoadPlayerData(playerId)!.LastLogin;
        
        // 1초 대기
        Thread.Sleep(1000);
        
        // 재접속
        db.GetOrCreatePlayerId(playerId);
        var secondLogin = db.LoadPlayerData(playerId)!.LastLogin;
        
        // last_login이 업데이트되었는지 확인
        DateTime.Parse(secondLogin).Should().BeAfter(DateTime.Parse(firstLogin));
    }
    
    [Fact]
    public void Should_Preserve_Position_On_Reconnect()
    {
        // 추가: 재접속 시 위치 정보 유지 확인
        var db = PlayerDatabase.Instance;
        var playerId = _testPlayerId;
        
        // 플레이어 생성 및 위치 저장
        db.GetOrCreatePlayerId(playerId);
        db.SavePlayer(playerId, 150f, 250f, "dungeon-1");
        
        // 재접속 시뮬레이션
        db.GetOrCreatePlayerId(playerId);
        
        // 위치 정보가 유지되는지 확인
        var data = db.LoadPlayerData(playerId);
        data!.X.Should().Be(150f);
        data!.Y.Should().Be(250f);
        data!.ZoneId.Should().Be("dungeon-1");
    }
}