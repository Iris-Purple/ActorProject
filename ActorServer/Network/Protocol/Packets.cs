using System.Text.Json.Serialization;

namespace ActorServer.Network.Protocol;

/// <summary>
/// 기본 패킷 구조
/// </summary>
public record Packet
{
    [JsonPropertyName("type")]
    public PacketType Type { get; init; }
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

// === Client -> Server 패킷 ===

/// <summary>
/// /login <name> 명령어에 대응
/// </summary>
public record LoginPacket : Packet
{
    [JsonPropertyName("playerId")]
    public long PlayerId { get; init; }
    
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;  // 추가: Token
    
    public LoginPacket() 
    { 
        Type = PacketType.Login; 
    }
}

/// <summary>
/// /move <x> <y> 명령어에 대응
/// </summary>
public record MovePacket : Packet
{
    [JsonPropertyName("x")]
    public float X { get; init; }
    
    [JsonPropertyName("y")]
    public float Y { get; init; }
    
    public MovePacket() 
    { 
        Type = PacketType.Move; 
    }
}

/// <summary>
/// /say <message> 명령어에 대응
/// </summary>
public record SayPacket : Packet
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    public SayPacket() 
    { 
        Type = PacketType.Say; 
    }
}

/// <summary>
/// /zone <name> 명령어에 대응
/// </summary>
public record ZonePacket : Packet
{
    [JsonPropertyName("zoneName")]
    public int ZoneId { get; init; }
    
    public ZonePacket() 
    { 
        Type = PacketType.Zone; 
    }
}

// === Server -> Client 패킷 ===

/// <summary>
/// 로그인 응답
/// </summary>
public record LoginResponsePacket : Packet
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    [JsonPropertyName("playerId")]
    public long PlayerId { get; init; }
    
    public LoginResponsePacket() 
    { 
        Type = PacketType.LoginResponse; 
    }
}

/// <summary>
/// 이동 알림 (자신 또는 다른 플레이어)
/// </summary>
public record MoveNotificationPacket : Packet
{
    [JsonPropertyName("playerName")]
    public long PlayerId { get; init; }
    
    [JsonPropertyName("x")]
    public float X { get; init; }
    
    [JsonPropertyName("y")]
    public float Y { get; init; }
    
    [JsonPropertyName("isSelf")]
    public bool IsSelf { get; init; }  // 자신의 이동인지 구분
    
    public MoveNotificationPacket() 
    { 
        Type = PacketType.MoveNotification; 
    }
}

/// <summary>
/// 채팅 메시지 (브로드캐스트)
/// </summary>
public record ChatMessagePacket : Packet
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; init; } = "";
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    [JsonPropertyName("isSelf")]
    public bool IsSelf { get; init; }  // 자신의 메시지인지 구분
    
    public ChatMessagePacket() 
    { 
        Type = PacketType.ChatMessage; 
    }
}

/// <summary>
/// Zone 변경 응답
/// </summary>
public record ZoneChangeResponsePacket : Packet
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("zoneName")]
    public string ZoneName { get; init; } = "";
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    public ZoneChangeResponsePacket() 
    { 
        Type = PacketType.ZoneChangeResponse; 
    }
}

/// <summary>
/// 상태 정보 응답
/// </summary>
public record StatusInfoPacket : Packet
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; init; } = "";
    
    [JsonPropertyName("currentZone")]
    public string CurrentZone { get; init; } = "";
    
    [JsonPropertyName("position")]
    public PositionInfo Position { get; init; } = new();
    
    public StatusInfoPacket() 
    { 
        Type = PacketType.StatusInfo; 
    }
    
    public record PositionInfo
    {
        [JsonPropertyName("x")]
        public float X { get; init; }
        
        [JsonPropertyName("y")]
        public float Y { get; init; }
    }
}

/// <summary>
/// 도움말 정보
/// </summary>
public record HelpInfoPacket : Packet
{
    [JsonPropertyName("commands")]
    public List<CommandInfo> Commands { get; init; } = new();
    
    public HelpInfoPacket() 
    { 
        Type = PacketType.HelpInfo; 
    }
    
    public record CommandInfo
    {
        [JsonPropertyName("command")]
        public string Command { get; init; } = "";
        
        [JsonPropertyName("description")]
        public string Description { get; init; } = "";
    }
}

/// <summary>
/// 시스템 메시지
/// </summary>
public record SystemMessagePacket : Packet
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    [JsonPropertyName("level")]
    public string Level { get; init; } = "info"; // info, warning, error
    
    public SystemMessagePacket() 
    { 
        Type = PacketType.SystemMessage; 
    }
}

/// <summary>
/// 에러 메시지
/// </summary>
public record ErrorMessagePacket : Packet
{
    [JsonPropertyName("error")]
    public string Error { get; init; } = "";
    
    [JsonPropertyName("details")]
    public string? Details { get; init; }
    
    public ErrorMessagePacket() 
    { 
        Type = PacketType.ErrorMessage; 
    }
}

/// <summary>
/// 다른 플레이어 Zone 진입 알림
/// </summary>
public record PlayerJoinedPacket : Packet
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; init; } = "";
    
    [JsonPropertyName("position")]
    public PositionInfo Position { get; init; } = new();
    
    public PlayerJoinedPacket() 
    { 
        Type = PacketType.PlayerJoined; 
    }
    
    public record PositionInfo
    {
        [JsonPropertyName("x")]
        public float X { get; init; }
        
        [JsonPropertyName("y")]
        public float Y { get; init; }
    }
}

/// <summary>
/// 다른 플레이어 Zone 퇴장 알림
/// </summary>
public record PlayerLeftPacket : Packet
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; init; } = "";
    
    public PlayerLeftPacket() 
    { 
        Type = PacketType.PlayerLeft; 
    }
}

/// <summary>
/// Zone 정보 (진입 시)
/// </summary>
public record ZoneInfoPacket : Packet
{
    [JsonPropertyName("zoneId")]
    public string ZoneId { get; init; } = "";
    
    [JsonPropertyName("zoneName")]
    public string ZoneName { get; init; } = "";
    
    [JsonPropertyName("zoneType")]
    public string ZoneType { get; init; } = "";
    
    [JsonPropertyName("spawnPoint")]
    public PositionInfo SpawnPoint { get; init; } = new();
    
    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; init; }
    
    public ZoneInfoPacket() 
    { 
        Type = PacketType.ZoneInfo; 
    }
    
    public record PositionInfo
    {
        [JsonPropertyName("x")]
        public float X { get; init; }
        
        [JsonPropertyName("y")]
        public float Y { get; init; }
    }
}

/// <summary>
/// 현재 Zone의 플레이어 목록
/// </summary>
public record CurrentPlayersInfoPacket : Packet
{
    [JsonPropertyName("players")]
    public List<PlayerInfo> Players { get; init; } = new();
    
    public CurrentPlayersInfoPacket() 
    { 
        Type = PacketType.CurrentPlayersInfo; 
    }
    
    public record PlayerInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";
        
        [JsonPropertyName("position")]
        public PositionInfo Position { get; init; } = new();
    }
    
    public record PositionInfo
    {
        [JsonPropertyName("x")]
        public float X { get; init; }
        
        [JsonPropertyName("y")]
        public float Y { get; init; }
    }
}