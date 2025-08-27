using Microsoft.Data.Sqlite;

namespace Common.Database;

public class PlayerDatabase
{
    private static PlayerDatabase? _instance;
    public static PlayerDatabase Instance
    {
        get
        {
            // 테스트 환경에서는 DB 파일이 없으면 재생성
            if (_instance != null)
            {
                var testEnv = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");
                if (testEnv == "true" && !File.Exists(_instance._dbPath))
                {
                    Console.WriteLine("[DB] Test DB file deleted, recreating...");
                    _instance = null;
                }
            }

            return _instance ??= new PlayerDatabase(GetDatabasePath());
        }
    }

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
                    position_x REAL DEFAULT 0,
                    position_y REAL DEFAULT 0,
                    zone_id INTEGER DEFAULT 0,
                    first_login TEXT NOT NULL,
                    last_login TEXT NOT NULL,
                    last_saved TEXT
                )";
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
    public long GetOrCreatePlayerId(long playerId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        // 기존 플레이어 확인
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT player_id FROM player_states WHERE player_id = @id";
        cmd.Parameters.AddWithValue("@id", playerId);

        var result = cmd.ExecuteScalar();
        if (result != null)
        {
            UpdateLastLogin(playerId);
            Console.WriteLine($"[DB] Found existing player: ID:{playerId}");
            return playerId;
        }

        cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO player_states 
            (player_id, position_x, position_y, zone_id, first_login, last_login, last_saved)
            VALUES (@id, 0, 0, 0, @now, @now, @now)";

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        cmd.Parameters.AddWithValue("@id", playerId);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();

        Console.WriteLine($"[DB] Created new player state: ID:{playerId}");
        return playerId;
    }

    public PlayerData? LoadPlayerData(long playerId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT position_x, position_y, zone_id, first_login, last_login
            FROM player_states 
            WHERE player_id = @id";
        cmd.Parameters.AddWithValue("@id", playerId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var data = new PlayerData
            {
                PlayerId = playerId,
                X = reader.GetFloat(0),
                Y = reader.GetFloat(1),
                ZoneId = reader.GetInt32(2),
                FirstLogin = reader.GetString(3),
                LastLogin = reader.GetString(4)
            };

            Console.WriteLine($"[DB] Loaded player data ID: {playerId} at ({data.X:F1},{data.Y:F1}) in {data.ZoneId}");
            return data;
        }

        Console.WriteLine($"[DB] No data found for Player ID: {playerId}");
        return null;
    }

    public void SavePlayer(long playerId, float x, float y, int zone)
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
            Console.WriteLine($"[DB] Saved: Player_{playerId} at ({x:F1},{y:F1}) in {zone}");
        }
    }

    public (float x, float y, int zone)? LoadPlayer(long playerId)
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

    // 테스트용 메서드 추가
    public static void ResetInstance()
    {
        _instance = null;
        Console.WriteLine("[DB] Instance reset for testing");
    }
}

public class PlayerData
{
    public long PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int ZoneId { get; set; } = 0;
    public string FirstLogin { get; set; } = string.Empty;
    public string LastLogin { get; set; } = string.Empty;
}