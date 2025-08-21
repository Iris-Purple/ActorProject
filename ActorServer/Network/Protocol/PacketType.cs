namespace ActorServer.Network.Protocol;

/// <summary>
/// 클라이언트-서버 통신 패킷 타입
/// 기존 명령어 기준으로 정의
/// </summary>
public enum PacketType
{
    // === Client -> Server (요청) ===
    Login = 100,        // /login <name>
    Move = 200,         // /move <x> <y>
    Say = 300,          // /say <message>
    Zone = 400,         // /zone <name>
    Status = 500,       // /status
    Help = 600,         // /help
    Quit = 700,         // /quit
    
    // === Server -> Client (응답/알림) ===
    LoginResponse = 101,
    MoveNotification = 201,     // 자신 또는 다른 플레이어 이동 알림
    ChatMessage = 301,           // 채팅 메시지 브로드캐스트
    ZoneChangeResponse = 401,    // Zone 변경 응답
    StatusInfo = 501,            // 상태 정보 응답
    HelpInfo = 601,              // 도움말 정보
    
    // === Server -> Client (시스템) ===
    SystemMessage = 900,         // 일반 시스템 메시지
    ErrorMessage = 999,          // 에러 메시지
    
    // === Server -> Client (Zone 관련 알림) ===
    PlayerJoined = 410,          // 다른 플레이어 Zone 진입
    PlayerLeft = 411,            // 다른 플레이어 Zone 퇴장
    ZoneInfo = 412,              // Zone 정보 (진입 시)
    CurrentPlayersInfo = 413,    // 현재 Zone의 플레이어 목록
}
