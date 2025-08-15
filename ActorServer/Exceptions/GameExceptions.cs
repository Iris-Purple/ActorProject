namespace ActorServer.Exceptions;

/// <summary>
/// 게임 로직 예외 (Resume 대상)
/// 게임 규칙 위반, 잘못된 입력 등
/// </summary>
public class GameLogicException : Exception
{
    public GameLogicException(string message) : base(message) { }
    public GameLogicException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// 일시적 예외 (Restart 대상)
/// 네트워크 오류, 일시적 데이터 오류 등
/// </summary>
public class TemporaryGameException : Exception
{
    public TemporaryGameException(string message) : base(message) { }
    public TemporaryGameException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// 치명적 예외 (Stop 대상)
/// 복구 불가능한 오류, 심각한 데이터 손상 등
/// </summary>
public class CriticalGameException : Exception
{
    public CriticalGameException(string message) : base(message) { }
    public CriticalGameException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Zone 관련 예외
/// </summary>
public class ZoneException : Exception
{
    public string ZoneId { get; }
    public ZoneException(string zoneId, string message) : base(message)
    {
        ZoneId = zoneId;
    }
    public ZoneException(string zoneId, string message, Exception innerException)
        : base(message, innerException)
    {
        ZoneId = zoneId;
    }
}

/// <summary>
/// Zone 과부하 예외
/// </summary>
public class ZoneOverloadException : Exception
{
    public int PlayerCount { get; }
    public int MaxPlayers { get; }

    public ZoneOverloadException(int playerCount, int maxPlayers, string message)
        : base(message)
    {
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
    }
}

/// <summary>
/// 플레이어 데이터 예외
/// </summary>
public class PlayerDataException : Exception
{
    public string PlayerName { get; }
    public PlayerDataException(string playerName, string message) : base(message)
    {
        PlayerName = playerName;
    }
    public PlayerDataException(string playerName, string message, Exception innerException)
        : base(message, innerException)
    {
        PlayerName = playerName;
    }
}

/// <summary>
/// 네트워크 관련 예외
/// </summary>
public class NetworkGameException : Exception
{
    public string RemoteAddress { get; }
    public NetworkGameException(string remoteAddress, string message) : base(message)
    {
        RemoteAddress = remoteAddress;
    }
}
