using Microsoft.Data.Sqlite;
using AuthServer.Models;
using Common.Database;

namespace AuthServer.Services;

public class AccountDatabase
{
    private readonly string _connectionString;
    private readonly ILogger<AccountDatabase>? _logger;

    public AccountDatabase() : this(null, null) { }
    public AccountDatabase(IConfiguration? configuration, ILogger<AccountDatabase>? logger)
    {
        _logger = logger;

        var testEnv = Environment.GetEnvironmentVariable("TEST_ENVIRONMENT");
        if (testEnv == "true")
        {
            _connectionString = "Data Source=test_collection.db";
            _logger?.LogInformation("Using test database: test_collection.db");
        }
        else
        {
            // 프로덕션: 공통 데이터베이스 경로 사용
            _connectionString = DatabaseConfig.ConnectionString;
            _logger?.LogInformation("Using database at: {Path}", DatabaseConfig.GetDatabasePath());
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
                    account_id TEXT PRIMARY KEY NOT NULL,
                    created_at TEXT NOT NULL,
                    last_login_at TEXT NOT NULL
                )";
            command.ExecuteNonQuery();

            _logger?.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    /// <summary>
    /// 로그인 처리 - 계정이 없으면 생성, 있으면 로그인
    /// </summary>
    public async Task<LoginResponse> ProcessLoginAsync(string accountId, string? clientIp)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // 트랜잭션 시작 - 동시성 문제 방지
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. 기존 계정 확인
            var existingAccount = await GetAccountAsync(connection, accountId);

            if (existingAccount != null)
            {
                // 2-1. 기존 계정 로그인 처리
                var lastLogin = existingAccount.LastLoginAt;
                await UpdateLoginInfoAsync(connection, accountId);

                transaction.Commit();

                _logger?.LogInformation("Account {AccountId} logged in successfully", accountId);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login successful",
                    IsNewAccount = false,
                    LastLoginAt = lastLogin,
                    Token = GenerateSimpleToken(accountId)
                };
            }
            else
            {
                // 2-2. 신규 계정 생성
                await CreateAccountAsync(connection, accountId);

                transaction.Commit();

                _logger?.LogInformation("New account {AccountId} created", accountId);

                return new LoginResponse
                {
                    Success = true,
                    Message = "New account created successfully",
                    IsNewAccount = true,
                    Token = GenerateSimpleToken(accountId)
                };
            }
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger?.LogError(ex, "Login process failed for {AccountId}", accountId);

            return new LoginResponse
            {
                Success = false,
                Message = $"Login failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 계정 조회
    /// </summary>
    private async Task<Account?> GetAccountAsync(SqliteConnection connection, string accountId)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT account_id, created_at, last_login_at
            FROM accounts
            WHERE account_id = @accountId";
        command.Parameters.AddWithValue("@accountId", accountId);
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Account
            {
                AccountId = reader.GetString(0),
                CreatedAt = DateTime.Parse(reader.GetString(1)),
                LastLoginAt = DateTime.Parse(reader.GetString(2)),
            };
        }

        return null;
    }

    /// <summary>
    /// 신규 계정 생성
    /// </summary>
    private async Task CreateAccountAsync(SqliteConnection connection, string accountId)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO accounts (account_id, created_at, last_login_at)
            VALUES (@accountId, @now, @now)";

        command.Parameters.AddWithValue("@accountId", accountId);
        command.Parameters.AddWithValue("@now", now);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 로그인 정보 업데이트
    /// </summary>
    private async Task UpdateLoginInfoAsync(SqliteConnection connection, string accountId)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE accounts 
            SET last_login_at = @now
            WHERE account_id = @accountId";

        command.Parameters.AddWithValue("@accountId", accountId);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));


        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 간단한 토큰 생성 (나중에 JWT로 교체 가능)
    /// </summary>
    private string GenerateSimpleToken(string accountId)
    {
        var tokenData = $"{accountId}:{DateTime.UtcNow.Ticks}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tokenData));
    }

    public string GetDbPath() => _connectionString.Replace("Data Source=", "");

}