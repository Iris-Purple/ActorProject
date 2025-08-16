using System.Collections.Concurrent;
using Akka.Actor;

namespace ActorServer.Actors;

public class PlayerIdManager
{
    public static PlayerIdManager Instance { get; } = new PlayerIdManager();
    private long _nextPlayerId = 1000;
    private readonly ConcurrentDictionary<long, string> _idToName = new();
    private readonly ConcurrentDictionary<string, long> _nameToId = new();

    private PlayerIdManager()
    {
        LoadLastId();
    }
    public long GetOrCreatePlayerId(string playerName)
    {
        if (_nameToId.TryGetValue(playerName.ToLower(), out var existingId))
            return existingId;

        var newId = Interlocked.Increment(ref _nextPlayerId);
        _idToName[newId] = playerName;
        _nameToId[playerName.ToLower()] = newId;

        SaveLastId();
        Console.WriteLine($"[PlyaerIdManager] New ID assigned: {playerName} = {newId}");
        return newId;
    }
    public long? GetPlayerId(string playerName)
    {
        return _nameToId.TryGetValue(playerName.ToLower(), out var id) ? id : null;
    }
    public string? GetPlayerName(long playerId)
    {
        return _idToName.TryGetValue(playerId, out var name) ? name : null;
    }
    public string GetActorName(long playerId) => $"player-{playerId}";
    public bool ExistsId(long playerId) => _idToName.ContainsKey(playerId);
    public bool ExistsName(string playerName) => _nameToId.ContainsKey(playerName);

    private void LoadLastId()
    {
        try
        {
            const string fileName = "last_player_id.txt";
            if (File.Exists(fileName))
            {
                _nextPlayerId = long.Parse(File.ReadAllText(fileName));
                Console.WriteLine($"[PlayerIdManager] Loaded last ID: {_nextPlayerId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerIdManager] Error loading last ID: {ex.Message}");
        }
    }
    private void SaveLastId()
    {
        try
        {
            File.WriteAllText("last_player_id.txt", _nextPlayerId.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerIdManager] Error saving last ID: {ex.Message}");
        }
    }
}