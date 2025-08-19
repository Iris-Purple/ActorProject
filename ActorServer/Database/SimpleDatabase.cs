using Microsoft.Data.Sqlite;

namespace ActorServer.Database;

public class SimpleDatabase
{
    public static readonly SimpleDatabase Instance = new SimpleDatabase();
    private readonly string _dbPath;
    // ⭐ 프로덕션용 기본 생성자
    private SimpleDatabase() : this("game.db") { }
    // ⭐ 테스트용 내부 생성자
    internal SimpleDatabase(string dbPath)
    {
        _dbPath = dbPath;
        InitializeDatabase();
    }
    
    // ⭐ 테스트용 별도 인스턴스 생성 메서드
    public static SimpleDatabase CreateForTesting(string testDbPath)
    {
        // 기존 테스트 DB 파일 삭제
        if (File.Exists(testDbPath))
        {
            File.Delete(testDbPath);
            Console.WriteLine($"[DB] Deleted existing test DB: {testDbPath}");
        }
        
        Console.WriteLine($"[DB] Creating test database: {testDbPath}");
        return new SimpleDatabase(testDbPath);
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
    
    public string GetDbPath() => _dbPath;
}