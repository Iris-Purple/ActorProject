using Microsoft.Data.Sqlite;

namespace ActorServer.Database;

public class SimpleDatabase
{
    private static SimpleDatabase? _instance;
    private static readonly object _lock = new object(); // ⭐ 스레드 안전성
    
    public static SimpleDatabase Instance 
    {
        get
        {
            lock (_lock) // ⭐ 동기화
            {
                if (_instance == null)
                {
                    Console.WriteLine("[DB] Creating new SimpleDatabase instance");
                    _instance = new SimpleDatabase();
                }
                return _instance;
            }
        }
    }
    
    private readonly string _dbPath;
    
    private SimpleDatabase() : this("game.db") { }
    
    internal SimpleDatabase(string dbPath)
    {
        _dbPath = dbPath;
        InitializeDatabase();
    }
    
    // ⭐ 테스트용 초기화 메서드 (인스턴스 교체)
    public static void InitializeForTesting(string testDbPath)
    {
        lock (_lock)
        {
            // 기존 인스턴스 제거
            _instance = null;
            
            // 테스트용 DB 파일 삭제
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
            }
            
            // 새 인스턴스 생성
            Console.WriteLine($"[DB] Initializing test database: {testDbPath}");
            _instance = new SimpleDatabase(testDbPath);
        }
    }
    
    // ⭐ 테스트 후 정리
    public static void ResetInstance()
    {
        lock (_lock)
        {
            Console.WriteLine("[DB] Resetting SimpleDatabase instance");
            _instance = null;
        }
    }
    
    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS player_states (
                player_id INTEGER PRIMARY KEY,
                player_name TEXT,
                position_x REAL,
                position_y REAL,
                zone_id TEXT,
                last_saved TEXT
            )";
        cmd.ExecuteNonQuery();
        Console.WriteLine($"[DB] Database initialized: {_dbPath}");
    }
    
    // ⭐ 저장 (단순하게)
    public void SavePlayer(long playerId, string name, float x, float y, string zone)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO player_states 
            VALUES (@id, @name, @x, @y, @zone, @time)";
        
        cmd.Parameters.AddWithValue("@id", playerId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y);
        cmd.Parameters.AddWithValue("@zone", zone);
        cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString());
        
        cmd.ExecuteNonQuery();
        Console.WriteLine($"[DB] Saved: {name} at ({x:F1},{y:F1}) in {zone}");
    }
    
    // ⭐ 로드 (단순하게)
    public (float x, float y, string zone)? LoadPlayer(long playerId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT position_x, position_y, zone_id FROM player_states WHERE player_id = @id";
        cmd.Parameters.AddWithValue("@id", playerId);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var x = reader.GetFloat(0);
            var y = reader.GetFloat(1);
            var zone = reader.GetString(2);
            Console.WriteLine($"[DB] Loaded: Player {playerId} at ({x:F1},{y:F1}) in {zone}");
            return (x, y, zone);
        }
        
        Console.WriteLine($"[DB] No data found for Player {playerId}");
        return null;
    }
    
    // ⭐ 디버깅용 메서드
    public string GetDbPath() => _dbPath;
}