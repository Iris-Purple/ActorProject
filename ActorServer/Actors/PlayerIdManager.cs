using System.Collections.Concurrent;
using Akka.Actor;
using Common.Database;

namespace ActorServer.Actors;

public class PlayerIdManager
{
    public static PlayerIdManager Instance { get; } = new PlayerIdManager();
    
    // ⭐ 메모리 캐시만 유지 (파일 제거)
    private readonly ConcurrentDictionary<long, string> _idToName = new();
    private readonly ConcurrentDictionary<string, long> _nameToId = new();
    private readonly PlayerDatabase _db = PlayerDatabase.Instance;

    private PlayerIdManager()
    {
        Console.WriteLine("[PlayerIdManager] Initialized with database backend");
    }
    
    public long GetOrCreatePlayerId(string playerName)
    {
        var normalizedName = playerName.ToLower();
        
        // 1. 캐시 확인
        if (_nameToId.TryGetValue(normalizedName, out var cachedId))
        {
            Console.WriteLine($"[PlayerIdManager] Cache hit: {playerName} = {cachedId}");
            return cachedId;
        }
        
        // 2. DB에서 조회 또는 생성
        var playerId = _db.GetOrCreatePlayerId(playerName);
        
        // 3. 캐시 업데이트
        _idToName[playerId] = playerName;
        _nameToId[normalizedName] = playerId;
        
        Console.WriteLine($"[PlayerIdManager] ID resolved: {playerName} = {playerId}");
        return playerId;
    }
    
    public long? GetPlayerId(string playerName)
    {
        var normalizedName = playerName.ToLower();
        
        // 1. 캐시 확인
        if (_nameToId.TryGetValue(normalizedName, out var id))
        {
            return id;
        }
        
        // 2. DB 조회 시도
        var playerId = _db.GetOrCreatePlayerId(playerName);
        
        // 3. 캐시 업데이트
        _idToName[playerId] = playerName;
        _nameToId[normalizedName] = playerId;
        
        return playerId;
    }
    
    public string? GetPlayerName(long playerId)
    {
        // 1. 캐시 확인
        if (_idToName.TryGetValue(playerId, out var name))
        {
            return name;
        }
        
        // 2. DB 조회
        var playerName = _db.GetPlayerNameById(playerId);
        if (playerName != null)
        {
            // 3. 캐시 업데이트
            _idToName[playerId] = playerName;
            _nameToId[playerName.ToLower()] = playerId;
        }
        
        return playerName;
    }
    
    public string GetActorName(long playerId) => $"player-{playerId}";
    
    public bool ExistsId(long playerId) => _idToName.ContainsKey(playerId);
    
    public bool ExistsName(string playerName) => _nameToId.ContainsKey(playerName.ToLower());
    
    public void RemoveFromCache(long playerId)
    {
        if (_idToName.TryRemove(playerId, out var name))
        {
            _nameToId.TryRemove(name.ToLower(), out _);
            Console.WriteLine($"[PlayerIdManager] Removed from cache: {name} (ID: {playerId})");
        }
    }
}