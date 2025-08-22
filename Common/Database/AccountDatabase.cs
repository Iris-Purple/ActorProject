using Microsoft.Data.Sqlite;

namespace Common.Database;

/// <summary>
/// 계정 데이터베이스 - AuthServer와 ActorServer 공통 사용
/// </summary>
public class AccountDatabase
{
    private readonly string _connectionString;
    
    // 싱글톤 인스턴스
    private static AccountDatabase? _instance;
    public static AccountDatabase Instance => _instance ??= new AccountDatabase();
    
    private AccountDatabase()
    {
        // 테스트 환경 체크
        var testEnv = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");
        if (testEnv == "true")
        {
            _connectionString = "Data Source=test_collection.db";
            Console.WriteLine("[AccountDatabase] Using test database");
        }
        else
        {
            _connectionString = DatabaseConfig.ConnectionString;
            Console.WriteLine($"[AccountDatabase] Using database: {DatabaseConfig.GetDatabasePath()}");
        }
        
        InitializeDatabase();
    }
    
    /// <summary>
    /// 데이터베이스 초기화 - 테이블 생성
    /// </summary>
    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS accounts (
                    player_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    account_id TEXT UNIQUE NOT NULL,
                    created_at TEXT NOT NULL,
                    last_login_at TEXT NOT NULL,
                    token TEXT,
                    token_expires_at TEXT
                )";
            command.ExecuteNonQuery();

            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_account_id 
                ON accounts(account_id)";
            command.ExecuteNonQuery();
            
            // SQLite의 AUTOINCREMENT 시작값을 1000으로 설정
            command.CommandText = @"
                INSERT OR IGNORE INTO sqlite_sequence (name, seq) 
                VALUES ('accounts', 999)";
            command.ExecuteNonQuery();

            Console.WriteLine("[AccountDatabase] Database initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AccountDatabase] Failed to initialize: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 로그인 처리 - AuthServer에서 사용
    /// </summary>
    public async Task<LoginResult> ProcessLoginAsync(string accountId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var existingAccount = await GetAccountByAccountIdAsync(connection, accountId);
            var token = GenerateSecureToken(accountId);
            var tokenExpiresAt = DateTime.UtcNow.AddHours(24);
            
            if (existingAccount != null)
            {
                // 기존 계정 로그인
                await UpdateLoginInfoAsync(connection, accountId, token, tokenExpiresAt);
                transaction.Commit();
                
                return new LoginResult
                {
                    Success = true,
                    IsNewAccount = false,
                    PlayerId = existingAccount.PlayerId,
                    Token = token,
                    LastLoginAt = existingAccount.LastLoginAt
                };
            }
            else
            {
                // 신규 계정 생성
                var newPlayerId = await CreateAccountAsync(connection, accountId, token, tokenExpiresAt);
                transaction.Commit();
                
                return new LoginResult
                {
                    Success = true,
                    IsNewAccount = true,
                    PlayerId = newPlayerId,
                    Token = token
                };
            }
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"[AccountDatabase] Login failed for {accountId}: {ex.Message}");
            return new LoginResult { Success = false, ErrorMessage = ex.Message };
        }
    }
    
    /// <summary>
    /// Token 검증 - ActorServer에서 사용
    /// </summary>
    public async Task<bool> ValidateTokenAsync(long playerId, string token)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT token_expires_at 
                FROM accounts 
                WHERE player_id = @playerId AND token = @token";
            
            command.Parameters.AddWithValue("@playerId", playerId);
            command.Parameters.AddWithValue("@token", token);
            
            var result = await command.ExecuteScalarAsync();
            if (result == null) return false;
            
            var expiresAt = DateTime.Parse(result.ToString()!);
            return expiresAt > DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AccountDatabase] Token validation error: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// PlayerId로 계정 정보 조회 - ActorServer에서 사용
    /// </summary>
    public async Task<AccountInfo?> GetAccountByPlayerIdAsync(long playerId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT account_id, created_at, last_login_at
                FROM accounts 
                WHERE player_id = @playerId";
            
            command.Parameters.AddWithValue("@playerId", playerId);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AccountInfo
                {
                    PlayerId = playerId,
                    AccountId = reader.GetString(0),
                    CreatedAt = DateTime.Parse(reader.GetString(1)),
                    LastLoginAt = DateTime.Parse(reader.GetString(2))
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AccountDatabase] Error getting account: {ex.Message}");
            return null;
        }
    }
    
    // === Private Methods ===
    
    private async Task<AccountInfo?> GetAccountByAccountIdAsync(SqliteConnection connection, string accountId)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT player_id, created_at, last_login_at
            FROM accounts
            WHERE account_id = @accountId";
        command.Parameters.AddWithValue("@accountId", accountId);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AccountInfo
            {
                PlayerId = reader.GetInt64(0),
                AccountId = accountId,
                CreatedAt = DateTime.Parse(reader.GetString(1)),
                LastLoginAt = DateTime.Parse(reader.GetString(2))
            };
        }
        
        return null;
    }
    
    private async Task<long> CreateAccountAsync(SqliteConnection connection, string accountId, string token, DateTime tokenExpiresAt)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO accounts (account_id, created_at, last_login_at, token, token_expires_at)
            VALUES (@accountId, @now, @now, @token, @expiresAt)";
        
        command.Parameters.AddWithValue("@accountId", accountId);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@token", token);
        command.Parameters.AddWithValue("@expiresAt", tokenExpiresAt.ToString("yyyy-MM-dd HH:mm:ss"));
        
        await command.ExecuteNonQueryAsync();
        
        command = connection.CreateCommand();
        command.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
    
    private async Task UpdateLoginInfoAsync(SqliteConnection connection, string accountId, string token, DateTime tokenExpiresAt)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE accounts 
            SET last_login_at = @now,
                token = @token,
                token_expires_at = @expiresAt
            WHERE account_id = @accountId";
        
        command.Parameters.AddWithValue("@accountId", accountId);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@token", token);
        command.Parameters.AddWithValue("@expiresAt", tokenExpiresAt.ToString("yyyy-MM-dd HH:mm:ss"));
        
        await command.ExecuteNonQueryAsync();
    }
    
    private string GenerateSecureToken(string accountId)
    {
        var tokenData = $"{accountId}:{DateTime.UtcNow.Ticks}:{Guid.NewGuid()}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tokenData));
    }
    
    public string GetDbPath() => _connectionString.Replace("Data Source=", "");
}

// === DTOs ===

public class AccountInfo
{
    public long PlayerId { get; set; }
    public string AccountId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}

public class LoginResult
{
    public bool Success { get; set; }
    public bool IsNewAccount { get; set; }
    public long PlayerId { get; set; }
    public string? Token { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ErrorMessage { get; set; }
}