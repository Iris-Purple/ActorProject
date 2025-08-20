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
        var result = await _db.ProcessLoginAsync(accountId, null);
        
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
        
        var firstLogin = await _db.ProcessLoginAsync(accountId, null);
        firstLogin.IsNewAccount.Should().BeTrue();
        
        await Task.Delay(100);
        
        // Act
        var secondLogin = await _db.ProcessLoginAsync(accountId, null);
        
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
            tasks.Add(_db.ProcessLoginAsync(accountId, null));
        }
        
        // Act
        var results = await Task.WhenAll(tasks);
        
        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }
}