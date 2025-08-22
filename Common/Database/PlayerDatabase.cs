using Microsoft.Data.Sqlite;

namespace Common.Database;

public class PlayerDatabase
{
    // 환경에 따라 자동으로 DB 선택
    public static readonly PlayerDatabase Instance = new PlayerDatabase(GetDatabasePath());
    private readonly string _dbPath;
    // 환경 감지 - xUnit 실행 여부 확인
    private static string GetDatabasePath()
    {
        // 1. 환경 변수 확인
        var testEnv = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");
        if (testEnv == "true")
        {
            Console.WriteLine("[DB] TEST_ENVIRONMENT=true detected");
            return "test_collection.db";
        }
        
        // 2. xUnit 어셈블리 감지 (환경 변수 없어도 자동 감지)
        var isTest = AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.FullName != null && 
                     (a.FullName.Contains("xunit") || 
                      a.FullName.Contains("testhost") ||
                      a.FullName.Contains("ActorServer.Tests")));
        
        if (isTest)
        {
            Console.WriteLine("[DB] Test assembly detected");
            return "test_collection.db";
        }
        
        // 3. 프로덕션 환경
        Console.WriteLine("[DB] Production environment");
        return DatabaseConfig.ConnectionString;
    }
    
    private PlayerDatabase(string dbPath)
    {
        _dbPath = dbPath;
        Console.WriteLine($"[DB] Initializing database: {dbPath}");
        InitializeDatabase();
    }
    
    private void InitializeDatabase()
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            var cmd = conn.CreateCommand();
            
            // 테이블 생성
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS player_states (
                    player_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    player_name TEXT UNIQUE NOT NULL,
                    position_x REAL DEFAULT 0,
                    position_y REAL DEFAULT 0,
                    zone_id TEXT DEFAULT 'town',
                    first_login TEXT NOT NULL,
                    last_login TEXT NOT NULL,
                    last_saved TEXT
                )";
            cmd.ExecuteNonQuery();
            
            // 인덱스 생성
            cmd.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_player_name 
                ON player_states(player_name)";
            cmd.ExecuteNonQuery();
            
            Console.WriteLine($"[DB] Database initialized successfully: {_dbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] ERROR: Failed to initialize database: {ex.Message}");
            throw;
        }
    }
    
    // 플레이어 ID 가져오기 또는 생성
    public long GetOrCreatePlayerId(string playerName)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        
        // 기존 플레이어 확인
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT player_id FROM player_states WHERE player_name = @name";
        cmd.Parameters.AddWithValue("@name", playerName);
        
        var result = cmd.ExecuteScalar();
        if (result != null)
        {
            var playerId = Convert.ToInt64(result);
            UpdateLastLogin(playerId);
            Console.WriteLine($"[DB] Found existing player: {playerName} (ID: {playerId})");
            return playerId;
        }
        
        // 새 플레이어 생성
        cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO player_states 
            (player_name, position_x, position_y, zone_id, first_login, last_login, last_saved)
            VALUES (@name, 0, 0, 'town', @now, @now, @now)";
        
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        cmd.Parameters.AddWithValue("@name", playerName);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();
        
        // 생성된 ID 반환
        cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        var newId = Convert.ToInt64(cmd.ExecuteScalar());
        
        Console.WriteLine($"[DB] Created new player: {playerName} (ID: {newId})");
        return newId;
    }
    
    public string? GetPlayerNameById(long playerId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT player_name FROM player_states WHERE player_id = @id";
        cmd.Parameters.AddWithValue("@id", playerId);
        
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }
    
    public PlayerData? LoadPlayerData(long playerId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT player_name, position_x, position_y, zone_id, first_login, last_login
            FROM player_states 
            WHERE player_id = @id";
        cmd.Parameters.AddWithValue("@id", playerId);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var data = new PlayerData
            {
                PlayerId = playerId,
                PlayerName = reader.GetString(0),
                X = reader.GetFloat(1),
                Y = reader.GetFloat(2),
                ZoneId = reader.GetString(3),
                FirstLogin = reader.GetString(4),
                LastLogin = reader.GetString(5)
            };
            
            Console.WriteLine($"[DB] Loaded player data: {data.PlayerName} at ({data.X:F1},{data.Y:F1}) in {data.ZoneId}");
            return data;
        }
        
        Console.WriteLine($"[DB] No data found for Player ID: {playerId}");
        return null;
    }
    
    public void SavePlayer(long playerId, string name, float x, float y, string zone)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE player_states 
            SET position_x = @x, 
                position_y = @y, 
                zone_id = @zone,
                last_saved = @time
            WHERE player_id = @id";
        
        cmd.Parameters.AddWithValue("@id", playerId);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y);
        cmd.Parameters.AddWithValue("@zone", zone);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        
        var affected = cmd.ExecuteNonQuery();
        if (affected > 0)
        {
            Console.WriteLine($"[DB] Saved: {name} at ({x:F1},{y:F1}) in {zone}");
        }
    }
    
    public (float x, float y, string zone)? LoadPlayer(long playerId)
    {
        var data = LoadPlayerData(playerId);
        if (data != null)
        {
            return (data.X, data.Y, data.ZoneId);
        }
        return null;
    }
    
    private void UpdateLastLogin(long playerId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE player_states 
            SET last_login = @time 
            WHERE player_id = @id";
        
        cmd.Parameters.AddWithValue("@id", playerId);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }
    
    public string GetDbPath() => _dbPath;
}

public class PlayerData
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public string ZoneId { get; set; } = "town";
    public string FirstLogin { get; set; } = "";
    public string LastLogin { get; set; } = "";
}