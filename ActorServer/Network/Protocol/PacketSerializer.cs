using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Akka.IO;

namespace ActorServer.Network.Protocol;

/// <summary>
/// 패킷 직렬화/역직렬화 헬퍼
/// </summary>
public static class PacketSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
    
    /// <summary>
    /// 패킷을 JSON 문자열로 직렬화
    /// </summary>
    public static string Serialize<T>(T packet) where T : Packet
    {
        return JsonSerializer.Serialize(packet, _options);
    }
    
    /// <summary>
    /// 패킷을 ByteString으로 직렬화 (TCP 전송용)
    /// </summary>
    public static ByteString SerializeToBytes<T>(T packet) where T : Packet
    {
        var json = Serialize(packet);
        // 변경: 구분자로 \n 추가 (기존 방식과 호환)
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        return ByteString.FromBytes(bytes);
    }
    
    /// <summary>
    /// JSON 문자열을 패킷으로 역직렬화
    /// </summary>
    public static Packet? Deserialize(string json)
    {
        try
        {
            // 먼저 기본 패킷으로 역직렬화하여 타입 확인
            var basePacket = JsonSerializer.Deserialize<Packet>(json, _options);
            if (basePacket == null) return null;
            
            // 타입에 따라 구체적인 패킷으로 역직렬화
            return basePacket.Type switch
            {
                PacketType.Login => JsonSerializer.Deserialize<LoginPacket>(json, _options),
                PacketType.Move => JsonSerializer.Deserialize<MovePacket>(json, _options),
                PacketType.Say => JsonSerializer.Deserialize<SayPacket>(json, _options),
                PacketType.Zone => JsonSerializer.Deserialize<ZonePacket>(json, _options),
                PacketType.Status => JsonSerializer.Deserialize<StatusPacket>(json, _options),
                PacketType.Help => JsonSerializer.Deserialize<HelpPacket>(json, _options),
                PacketType.Quit => JsonSerializer.Deserialize<QuitPacket>(json, _options),
                _ => basePacket
            };
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[PacketSerializer] Failed to deserialize: {ex.Message}");
            return null;
        }
    }
}