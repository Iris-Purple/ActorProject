using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using Xunit;
using AuthServer.Models;
using AuthServer.Tests.Fixtures;

namespace AuthServer.Tests.Integration;

[Collection("Database Collection")]
public class AuthControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DatabaseFixture _dbFixture;
    
    public AuthControllerIntegrationTests(
        WebApplicationFactory<Program> factory, 
        DatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
        
        // 테스트 환경 설정 (DatabaseFixture가 이미 설정했지만 확실히)
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 테스트용 서비스 설정 가능
            });
        });
        
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
    
    [Fact]
    public async Task Login_WithNewAccount_ShouldCreateAccount()
    {
        // Arrange - 새로운 계정 ID 생성
        var accountId = $"test_new_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var request = new LoginRequest { AccountId = accountId };
        
        // Act - 로그인 요청
        var response = await SendLoginRequest(request);
        
        // Assert - 검증
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
        
        loginResponse.Should().NotBeNull();
        loginResponse!.Success.Should().BeTrue();
        loginResponse.IsNewAccount.Should().BeTrue();
        loginResponse.Message.Should().Contain("created");
        loginResponse.Token.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task Login_WithExistingAccount_ShouldReturnSuccess()
    {
        // Arrange - 계정 먼저 생성
        var accountId = $"test_existing_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var request = new LoginRequest { AccountId = accountId };
        
        // 첫 번째 로그인 (계정 생성)
        var firstResponse = await SendLoginRequest(request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Act - 두 번째 로그인 (기존 계정)
        var secondResponse = await SendLoginRequest(request);
        
        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await secondResponse.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
        
        loginResponse.Should().NotBeNull();
        loginResponse!.Success.Should().BeTrue();
        loginResponse.IsNewAccount.Should().BeFalse();
        loginResponse.LastLoginAt.Should().NotBeNull();
        loginResponse.Token.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task Login_WithEmptyAccountId_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new LoginRequest { AccountId = "" };
        
        // Act
        var response = await SendLoginRequest(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
        
        loginResponse!.Success.Should().BeFalse();
        loginResponse.Message.Should().Contain("required");
    }
    
    [Theory]
    [InlineData("test@user")]      // @ 기호
    [InlineData("test user")]      // 공백
    [InlineData("test-user")]      // 하이픈
    [InlineData("test.user")]      // 점
    [InlineData("test#user")]      // # 기호
    public async Task Login_WithInvalidCharacters_ShouldReturnBadRequest(string invalidAccountId)
    {
        // Arrange
        var request = new LoginRequest { AccountId = invalidAccountId };
        
        // Act
        var response = await SendLoginRequest(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
        
        loginResponse!.Success.Should().BeFalse();
        loginResponse.Message.Should().Contain("letters, numbers, and underscores");
    }
    
    [Theory]
    [InlineData("testuser123")]     // 영문+숫자
    [InlineData("test_user")]       // 영문+언더스코어
    [InlineData("TEST_USER_123")]   // 대문자+언더스코어+숫자
    [InlineData("123456")]          // 숫자만
    [InlineData("abcdef")]          // 영문만
    public async Task Login_WithValidAccountId_ShouldSucceed(string validAccountId)
    {
        // Arrange
        var accountId = $"{validAccountId}_{Guid.NewGuid().ToString().Substring(0, 4)}";
        var request = new LoginRequest { AccountId = accountId };
        
        // Act
        var response = await SendLoginRequest(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
        
        loginResponse!.Success.Should().BeTrue();
    }
    
    [Fact]
    public async Task Login_MultipleRequests_ShouldHandleConcurrency()
    {
        // Arrange - 동시에 여러 요청 테스트
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 10; i++)
        {
            var request = new LoginRequest { AccountId = $"concurrent_test_{i}" };
            tasks.Add(SendLoginRequest(request));
        }
        
        // Act - 모든 요청 동시 실행
        var responses = await Task.WhenAll(tasks);
        
        // Assert - 모든 요청이 성공해야 함
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
    
    // 헬퍼 메서드
    private async Task<HttpResponseMessage> SendLoginRequest(LoginRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/api/auth/login", content);
    }
}