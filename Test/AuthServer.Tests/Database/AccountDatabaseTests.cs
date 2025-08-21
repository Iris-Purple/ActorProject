using AuthServer.Services;
using AuthServer.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuthServer.Tests.Database;

[Collection("Database Collection")]
public class AccountDatabaseTests
{
    private readonly DatabaseFixture _fixture;
    private readonly AccountDatabase _db;

    public AccountDatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _db = new AccountDatabase();
    }

    [Fact]
    public void Should_Use_Test_Database()
    {
        // Assert - 둘 다 같은 테스트 DB를 사용해야 함
        _db.GetDbPath().Should().Be("test_collection.db");
        _fixture.TestDbPath.Should().Be("test_collection.db");
        Console.WriteLine($"[TEST] Using database: {_db.GetDbPath()}");
    }

    [Fact]
    public async Task Should_Create_New_Account()
    {
        // Arrange
        var accountId = $"test_account_{Guid.NewGuid().ToString().Substring(0, 8)}";

        // Act
        var result = await _db.ProcessLoginAsync(accountId);

        // Assert
        result.Success.Should().BeTrue();
        result.IsNewAccount.Should().BeTrue();
        result.Message.Should().Contain("created");
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Login_Existing_Account()
    {
        // Arrange
        var accountId = $"existing_{Guid.NewGuid().ToString().Substring(0, 8)}";

        var firstLogin = await _db.ProcessLoginAsync(accountId);
        firstLogin.IsNewAccount.Should().BeTrue();

        await Task.Delay(100);

        // Act
        var secondLogin = await _db.ProcessLoginAsync(accountId);

        // Assert
        secondLogin.Success.Should().BeTrue();
        secondLogin.IsNewAccount.Should().BeFalse();
        secondLogin.LastLoginAt.Should().NotBeNull();
        secondLogin.Message.Should().Contain("successful");
    }

    [Fact]
    public void Database_Should_Create_Tables_On_Initialize()
    {
        // test_collection.db 직접 확인
        using var conn = new SqliteConnection($"Data Source={_fixture.TestDbPath}");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='accounts'";
        var tableName = cmd.ExecuteScalar() as string;

        tableName.Should().Be("accounts");
    }

    [Fact]
    public async Task Should_Handle_Concurrent_Logins()
    {
        // Arrange
        var tasks = new List<Task<AuthServer.Models.LoginResponse>>();

        for (int i = 0; i < 10; i++)
        {
            var accountId = $"concurrent_{i}";
            tasks.Add(_db.ProcessLoginAsync(accountId));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Should_Generate_New_Token_On_Each_Login()
    {
        // Arrange
        var accountId = $"token_test_{Guid.NewGuid().ToString().Substring(0, 8)}";

        // Act - 첫 번째 로그인
        var firstLogin = await _db.ProcessLoginAsync(accountId);
        var firstToken = firstLogin.Token;

        await Task.Delay(100); // 시간 차이를 두기 위해

        // Act - 두 번째 로그인
        var secondLogin = await _db.ProcessLoginAsync(accountId);
        var secondToken = secondLogin.Token;

        // Assert - 매 로그인마다 새로운 토큰이 생성되어야 함
        firstToken.Should().NotBeNullOrEmpty();
        secondToken.Should().NotBeNullOrEmpty();
        firstToken.Should().NotBe(secondToken, "Each login should generate a new token");
    }

    [Fact]
    public async Task Should_Validate_Token_Correctly()
    {
        // Arrange
        var accountId = $"validate_test_{Guid.NewGuid().ToString().Substring(0, 8)}";

        // Act - 로그인하여 토큰 발급
        var loginResult = await _db.ProcessLoginAsync(accountId);
        var token = loginResult.Token;

        // Assert - 올바른 토큰은 유효해야 함
        var isValid = await _db.ValidateTokenAsync(accountId, token!);
        isValid.Should().BeTrue("Valid token should pass validation");

        // Assert - 잘못된 토큰은 유효하지 않아야 함
        var isInvalid = await _db.ValidateTokenAsync(accountId, "invalid_token");
        isInvalid.Should().BeFalse("Invalid token should fail validation");
    }

    [Fact]
    public async Task Should_Store_Token_In_Database()
    {
        // Arrange
        var accountId = $"store_test_{Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // Act - 로그인
        var loginResult = await _db.ProcessLoginAsync(accountId);
        
        // Assert - 데이터베이스에서 직접 확인
        using var conn = new SqliteConnection($"Data Source={_fixture.TestDbPath}");
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT token, token_expires_at 
            FROM accounts 
            WHERE account_id = @accountId";
        cmd.Parameters.AddWithValue("@accountId", accountId);
        
        using var reader = cmd.ExecuteReader();
        reader.Read().Should().BeTrue();
        
        var storedToken = reader.GetString(0);
        var expiresAt = reader.GetString(1);
        
        storedToken.Should().Be(loginResult.Token);
        expiresAt.Should().NotBeNullOrEmpty();
        
        // 만료 시간이 미래여야 함
        var expiryDate = DateTime.Parse(expiresAt);
        expiryDate.Should().BeAfter(DateTime.UtcNow);
    }
}