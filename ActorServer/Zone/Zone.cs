using Akka.Actor;
using ActorServer.Messages;

namespace ActorServer.Zone;


/// <summary>
/// Zone 로직만 담당하는 일반 클래스 (Actor 아님)
/// </summary>
public class ZoneInfo
{
    private readonly ZoneData _info;
    private readonly Dictionary<long, PlayerInfo> _players = new();
    private readonly object _lockObject = new();

    public ZoneId ZoneId => _info.ZoneId;

    public int PlayerCount => _players.Count;
    public string Name => _info.Name;
    public int MaxPlayers => _info.MaxPlayers;
    public Position GetSpawnPoint() => _info.SpawnPoint;


    public bool IsFull => _info.MaxPlayers > 0 && PlayerCount >= _info.MaxPlayers;
    public ZoneData Data => _info;

    public ZoneInfo(ZoneData info)
    {
        _info = info;
    }

    // 플레이어 추가 (반환값으로 성공 여부 전달)
    public (bool success, string message) TryAddPlayer(long playerId)
    {
        lock (_lockObject)
        {
            if (_players.ContainsKey(playerId))
                return (false, "Player already in zone");

            if (IsFull)
                return (false, "Zone is full");

            _players[playerId] = new PlayerInfo(playerId, _info.SpawnPoint);
            return (true, "Player added");
        }
    }

    // 플레이어 제거
    public bool RemovePlayer(long playerId)
    {
        lock (_lockObject)
        {
            return _players.Remove(playerId);
        }
    }

    // Zone 내 모든 플레이어 가져오기
    public List<PlayerInfo> GetAllPlayers()
    {
        lock (_lockObject)
        {
            return _players.Values.ToList();
        }
    }

    // 추가: 특정 플레이어 정보 가져오기
    public PlayerInfo? GetPlayer(long playerId)
    {
        lock (_lockObject)
        {
            return _players.TryGetValue(playerId, out var info) ? info : null;
        }
    }

    // 추가: 플레이어 위치 업데이트
    public bool UpdatePlayerPosition(long playerId, Position newPosition)
    {
        lock (_lockObject)
        {
            if (_players.TryGetValue(playerId, out var info))
            {
                _players[playerId] = info with { Position = newPosition };
                return true;
            }
            return false;
        }
    }

    // 추가: 플레이어 존재 여부 확인
    public bool HasPlayer(long playerId)
    {
        lock (_lockObject)
        {
            return _players.ContainsKey(playerId);
        }
    }
}