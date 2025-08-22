namespace AuthServer.Models;

/// <summary>
/// 계정 정보 모델
/// </summary>
public class Account
{
    public string AccountId { get; set; } = "";  // 유니크한 계정 ID (로그인용)
    public long PlayerId { get; set; }
    public DateTime CreatedAt { get; set; }      // 계정 생성 시간
    public DateTime LastLoginAt { get; set; }    // 마지막 로그인 시간
    public string? Token { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}


/// <summary>
/// 로그인 요청 DTO
/// </summary>
public class LoginRequest
{
    public string AccountId { get; set; } = string.Empty;
}

/// <summary>
/// 로그인 응답 DTO
/// </summary>
public class LoginResponse
{
    public long PlayerId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }           // 나중에 토큰 인증용
    public bool IsNewAccount { get; set; }      // 신규 계정 여부
    public DateTime? LastLoginAt { get; set; }   // 이전 로그인 시간
}

// <summary>
/// Token 검증 요청 DTO - 추가
/// </summary>
public class ValidateTokenRequest
{
    public long PlayerId { get; set; }
    public string Token { get; set; } = "";
}

/// <summary>
/// Token 검증 응답 DTO - 추가
/// </summary>
public class ValidateTokenResponse
{
    public bool IsValid { get; set; }
    public string? AccountId { get; set; }
    public string? Message { get; set; }
}