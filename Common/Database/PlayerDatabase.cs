using Microsoft.Data.Sqlite;

namespace Common.Database;

public class PlayerDatabase
{
    private static PlayerDatabase? _instance;
    public static PlayerDatabase Instance => _instance ??= new PlayerDatabase();

    private string _dbPath;  // readonly 제거
    private string _connectionString;  // 연결 문자열 별도 저장
    private bool _isMemoryDb = false;
    
    private PlayerDatabase()
    {
        // 테스트 환경 체크
        var testEnv = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");
        if (testEnv == "true")
        {
            _dbPath = "test_collection.db";
            _connectionString = $"Data Source={_dbPath}";
            Console.WriteLine("[DB] Using test database");
        }
        else
        {
            // 프로덕션 환경 - 프로젝트 루트 찾기
            _dbPath = GetProductionDbPath();
            _connectionString = $"Data Source={_dbPath}";
            Console.WriteLine($"[DB] Using production database: {_dbPath}");
        }
        
        InitializeDatabase();
    }
    
    private string GetProductionDbPath()
    {
        // 1. 환경변수로 지정된 경로 확인
        var envPath = Environment.GetEnvironmentVariable("ACTOR_DB_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            Console.WriteLine($"[DB] Using environment variable path: {envPath}");
            return envPath;
        }
        
        // 2. 프로젝트 루트 찾기 (ActorProject.sln 파일 기준)
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDir != null && !currentDir.GetFiles("ActorProject.sln").Any())
        {
            currentDir = currentDir.Parent;
        }
        
        if (currentDir != null)
        {
            var dbPath = Path.Combine(currentDir.FullName, "Database", "game.db");
            Console.WriteLine($"[DB] Found project root: {currentDir.FullName}");
            return dbPath;
        }
        
        // 3. 폴백: 절대 경로 사용
        Console.WriteLine("[DB] Using fallback absolute path");
        return "/Users/goormbee/study/ActorProject/Database/game.db";
    }

    private void InitializeDatabase()
    {
        Console.WriteLine("[DB] ===== Database Initialization =====");
        Console.WriteLine($"[DB] Target Path: {_dbPath}");
        
        // 디렉토리 생성
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Console.WriteLine($"[DB] Creating directory: {directory}");
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] Failed to create directory: {ex.Message}");
            }
        }
        
        // 기본 연결 시도
        try
        {
            Console.WriteLine("[DB] Attempting connection...");
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            CreateTable(conn);
            Console.WriteLine("[DB] ✅ Database connection successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] ❌ Connection failed: {ex.Message}");
            
            // 메모리 DB로 폴백
            Console.WriteLine("[DB] ⚠️  Falling back to in-memory database");
            _dbPath = ":memory:";
            _connectionString = "Data Source=:memory:";
            _isMemoryDb = true;
            
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                CreateTable(conn);
                Console.WriteLine("[DB] ✅ In-memory database initialized");
            }
            catch (Exception memEx)
            {
                Console.WriteLine($"[DB] ❌ CRITICAL: Even memory DB failed: {memEx.Message}");
                throw;
            }
        }
        
        Console.WriteLine("[DB] =====================================");
    }
    
    private void CreateTable(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
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
        Console.WriteLine("[DB] Table 'player_states' ready");
    }
    
    // 연결 헬퍼 메서드
    private SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public long GetOrCreatePlayerId(long playerId)
    {
        try
        {
            using var conn = GetConnection();
            conn.Open();

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
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] GetOrCreatePlayerId error: {ex.Message}");
            return playerId; // 에러가 나도 게임 진행
        }
    }

    public PlayerData? LoadPlayerData(long playerId)
    {
        try
        {
            using var conn = GetConnection();
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

                Console.WriteLine($"[DB] Loaded player data ID: {playerId}");
                return data;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] LoadPlayerData error: {ex.Message}");
        }
        
        return null;
    }

    public void SavePlayer(long playerId, float x, float y, int zone)
    {
        if (_isMemoryDb)
        {
            Console.WriteLine($"[DB] Memory mode - save skipped for Player_{playerId}");
            return;
        }
        
        try
        {
            using var conn = GetConnection();
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
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Save failed: {ex.Message}");
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
        try
        {
            using var conn = GetConnection();
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
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] UpdateLastLogin error: {ex.Message}");
        }
    }

    public string GetDbPath() => _dbPath;
    
    public bool IsMemoryMode() => _isMemoryDb;

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