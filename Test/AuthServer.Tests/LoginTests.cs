using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using AuthServer.Models;
using FluentAssertions;
using Common.Database;

namespace AuthServer.Tests;

/// <summary>
/// AuthServer Login 기능 테스트
/// </summary>
[Collection("AuthServerTests")]
public class LoginTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public LoginTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        
        // 테스트 환경 설정
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
        
        // 테스트용 HTTP 클라이언트 생성
        _client = _factory.CreateClient();
        
        _output.WriteLine("=== Login Tests Started ===");
    }

    /// <summary>
    /// 테스트 1: 신규 계정 로그인 - 계정 자동 생성
    /// </summary>
    [Fact]
    public async Task Login_Should_Create_New_Account_For_First_Login()
    {
        // Arrange - 준비
        var request = new LoginRequest
        {
            // 변경: GUID의 하이픈을 제거하여 AccountId 형식 규칙 준수
            AccountId = $"test_user_{Guid.NewGuid().ToString("N")}" // 하이픈 없는 GUID
        };
        
        _output.WriteLine($"Testing new account creation for: {request.AccountId}");

        // Act - 실행
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);
        
        // Assert - 검증
        response.Should().BeSuccessful(); // 200 OK
        
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Success.Should().BeTrue();
        loginResponse.IsNewAccount.Should().BeTrue(); // 신규 계정 확인
        loginResponse.PlayerId.Should().BeGreaterThan(0); // PlayerId 생성 확인
        loginResponse.Token.Should().NotBeNullOrEmpty(); // Token 생성 확인
        loginResponse.Message.Should().Contain("New account created");
        
        _output.WriteLine($"✅ New account created - PlayerId: {loginResponse.PlayerId}");
    }

    /// <summary>
    /// 테스트 2: 기존 계정 재로그인
    /// </summary>
    [Fact]
    public async Task Login_Should_Success_For_Existing_Account()
    {
        // Arrange - 준비
        // 변경: GUID의 하이픈을 제거
        var accountId = $"existing_user_{Guid.NewGuid().ToString("N")}";
        var request = new LoginRequest { AccountId = accountId };
        
        // 첫 번째 로그인 (계정 생성)
        var firstResponse = await _client.PostAsJsonAsync("/api/auth/login", request);
        var firstLogin = await firstResponse.Content.ReadFromJsonAsync<LoginResponse>();
        
        _output.WriteLine($"First login - PlayerId: {firstLogin!.PlayerId}");
        
        // 잠시 대기 (LastLoginAt 시간 차이를 위해)
        await Task.Delay(1000);

        // Act - 두 번째 로그인 (재로그인)
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/login", request);
        
        // Assert - 검증
        secondResponse.Should().BeSuccessful();
        
        var secondLogin = await secondResponse.Content.ReadFromJsonAsync<LoginResponse>();
        secondLogin.Should().NotBeNull();
        secondLogin!.Success.Should().BeTrue();
        secondLogin.IsNewAccount.Should().BeFalse(); // 기존 계정 확인
        secondLogin.PlayerId.Should().Be(firstLogin.PlayerId); // 같은 PlayerId
        secondLogin.Token.Should().NotBeNullOrEmpty(); // 새 토큰 발급
        secondLogin.Token.Should().NotBe(firstLogin.Token); // 다른 토큰
        secondLogin.LastLoginAt.Should().NotBeNull(); // 이전 로그인 시간 존재
        secondLogin.Message.Should().Contain("Login successful");
        
        _output.WriteLine($"✅ Re-login successful - Same PlayerId: {secondLogin.PlayerId}");
    }

    /// <summary>
    /// 테스트 3: 잘못된 AccountId 형식 처리
    /// </summary>
    [Fact]
    public async Task Login_Should_Reject_Invalid_AccountId_Format()
    {
        // Arrange - 준비
        var invalidRequests = new[]
        {
            new LoginRequest { AccountId = "user@123" },     // @ 특수문자
            new LoginRequest { AccountId = "user#test" },    // # 특수문자
            new LoginRequest { AccountId = "user space" },   // 공백
            new LoginRequest { AccountId = "user-name" },    // 하이픈
            new LoginRequest { AccountId = "한글계정" }       // 한글
        };

        foreach (var request in invalidRequests)
        {
            _output.WriteLine($"Testing invalid AccountId: {request.AccountId}");

            // Act - 실행
            var response = await _client.PostAsJsonAsync("/api/auth/login", request);
            
            // Assert - 검증
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
            
            var errorResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            errorResponse.Should().NotBeNull();
            errorResponse!.Success.Should().BeFalse();
            errorResponse.Message.Should().Contain("can only contain letters, numbers, and underscores");
            
            _output.WriteLine($"✅ Correctly rejected: {request.AccountId}");
        }
    }

    /// <summary>
    /// 테스트 5: 동시 다중 로그인 처리
    /// </summary>
    [Fact]
    public async Task Login_Should_Handle_Concurrent_Logins()
    {
        // Arrange - 준비
        var tasks = new List<Task<HttpResponseMessage>>();
        var accountCount = 10;
        
        _output.WriteLine($"Testing {accountCount} concurrent logins...");

        // Act - 동시에 10개 로그인 요청
        for (int i = 0; i < accountCount; i++)
        {
            var request = new LoginRequest 
            { 
                // 변경: GUID의 하이픈을 제거
                AccountId = $"concurrent_user_{i}_{Guid.NewGuid().ToString("N")}"
            };
            
            tasks.Add(_client.PostAsJsonAsync("/api/auth/login", request));
        }
        
        var responses = await Task.WhenAll(tasks);

        // Assert - 모든 요청이 성공해야 함
        foreach (var response in responses)
        {
            response.Should().BeSuccessful();
            
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            loginResponse.Should().NotBeNull();
            loginResponse!.Success.Should().BeTrue();
            loginResponse.PlayerId.Should().BeGreaterThan(0);
        }
        
        _output.WriteLine($"✅ All {accountCount} concurrent logins succeeded");
    }

    /// <summary>
    /// 테스트 6: PlayerId 자동 증가 확인
    /// </summary>
    [Fact]
    public async Task Login_Should_Generate_Sequential_PlayerIds()
    {
        // Arrange - 준비
        var playerIds = new List<long>();
        
        _output.WriteLine("Testing PlayerId auto-increment...");

        // Act - 3개 계정 순차 생성
        for (int i = 0; i < 3; i++)
        {
            var request = new LoginRequest 
            { 
                // 변경: GUID의 하이픈을 제거
                AccountId = $"sequential_user_{i}_{Guid.NewGuid().ToString("N")}"
            };
            
            var response = await _client.PostAsJsonAsync("/api/auth/login", request);
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            
            playerIds.Add(loginResponse!.PlayerId);
            _output.WriteLine($"Created PlayerId: {loginResponse.PlayerId}");
        }

        // Assert - PlayerId가 순차적으로 증가
        for (int i = 1; i < playerIds.Count; i++)
        {
            playerIds[i].Should().BeGreaterThan(playerIds[i - 1]);
        }
        
        // 모든 PlayerId가 1000 이상 (AUTOINCREMENT 시작값)
        playerIds.Should().OnlyContain(id => id >= 1000);
        
        _output.WriteLine($"✅ PlayerIds are sequential: {string.Join(", ", playerIds)}");
    }

    /// <summary>
    /// 테스트 7: 토큰 만료 시간 확인
    /// </summary>
    [Fact]
    public async Task Login_Should_Generate_Token_With_24Hour_Expiry()
    {
        // Arrange - 준비
        var request = new LoginRequest 
        { 
            // 변경: GUID의 하이픈을 제거
            AccountId = $"token_test_{Guid.NewGuid().ToString("N")}"
        };
        
        _output.WriteLine("Testing token generation and expiry...");

        // Act - 로그인
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

        // Assert - 토큰 확인
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
        
        // 토큰이 Base64 형식인지 확인
        Action decodeToken = () => Convert.FromBase64String(loginResponse.Token!);
        decodeToken.Should().NotThrow();
        
        // 토큰 내용 확인 (accountId:ticks:guid 형식)
        var decodedToken = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(loginResponse.Token!)
        );
        decodedToken.Should().Contain(request.AccountId);
        decodedToken.Should().Contain(":"); // 구분자 포함
        
        _output.WriteLine($"✅ Valid token generated: {loginResponse.Token!.Substring(0, 20)}...");
    }

    /// <summary>
    /// 테스트 8: Health Check 엔드포인트
    /// </summary>
    [Fact]
    public async Task Health_Endpoint_Should_Return_OK()
    {
        // Act - 실행
        var response = await _client.GetAsync("/api/auth/health");
        
        // Assert - 검증
        response.Should().BeSuccessful();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
        content.Should().Contain("timestamp");
        
        _output.WriteLine("✅ Health check endpoint working");
    }

    public void Dispose()
    {
        _client?.Dispose();
        _output.WriteLine("=== Login Tests Completed ===");
    }
}