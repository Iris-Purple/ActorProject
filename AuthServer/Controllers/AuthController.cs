using Microsoft.AspNetCore.Mvc;
using AuthServer.Models;
using AuthServer.Services;

namespace AuthServer.Controllers;

/// <summary>
/// 인증 관련 API 컨트롤러
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AccountDatabase _accountDb;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(AccountDatabase accountDb, ILogger<AuthController> logger)
    {
        _accountDb = accountDb;
        _logger = logger;
    }
    
    /// <summary>
    /// 로그인 엔드포인트
    /// </summary>
    /// <param name="request">로그인 요청 정보</param>
    /// <returns>로그인 결과</returns>
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
        
        // 2. AccountId 형식 검증 (영문, 숫자, 언더스코어만 허용)
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
        
        // 3. 로그인 처리 (변경: clientIp 파라미터 제거)
        var result = await _accountDb.ProcessLoginAsync(request.AccountId, null);
        
        // 4. 응답 반환
        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return StatusCode(500, result);
        }
    }
}