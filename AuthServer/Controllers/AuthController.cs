using Microsoft.AspNetCore.Mvc;
using AuthServer.Models;
using Common.Database;  // 변경: Common의 AccountDatabase 사용

namespace AuthServer.Controllers;

/// <summary>
/// 인증 관련 API 컨트롤러
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AccountDatabase _accountDb = AccountDatabase.Instance;  // 변경: 싱글톤 직접 사용
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 로그인 엔드포인트
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        // 1. 입력 검증
        if (string.IsNullOrWhiteSpace(request.AccountId))
        {
            _logger.LogWarning("Login attempt with empty AccountId");
            return BadRequest(new LoginResponse 
            { 
                Success = false, 
                Message = "AccountId is required" 
            });
        }
        
        // 2. AccountId 형식 검증
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.AccountId, @"^[a-zA-Z0-9_]+$"))
        {
            _logger.LogWarning("Invalid AccountId format: {AccountId}", request.AccountId);
            return BadRequest(new LoginResponse 
            { 
                Success = false, 
                Message = "AccountId can only contain letters, numbers, and underscores" 
            });
        }
        
        _logger.LogInformation("Login attempt for account {AccountId}", request.AccountId);
        
        // 3. 로그인 처리 (Common AccountDatabase 사용)
        var result = await _accountDb.ProcessLoginAsync(request.AccountId);
        
        // 4. 응답 변환
        var response = new LoginResponse
        {
            Success = result.Success,
            Message = result.Success 
                ? (result.IsNewAccount ? "New account created successfully" : "Login successful")
                : result.ErrorMessage ?? "Login failed",
            PlayerId = result.PlayerId,
            Token = result.Token,
            IsNewAccount = result.IsNewAccount,
            LastLoginAt = result.LastLoginAt
        };
        
        if (result.Success)
        {
            _logger.LogInformation("Login successful - AccountId: {AccountId}, PlayerId: {PlayerId}", 
                request.AccountId, result.PlayerId);
            return Ok(response);
        }
        else
        {
            return StatusCode(500, response);
        }
    }
    
    /// <summary>
    /// 헬스 체크 엔드포인트
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}