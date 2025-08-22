namespace ActorServer.Messages;

// === 채팅 메시지 ===
public record ChatMessage(long PlayerId, string Message);
public record ChatBroadcast(long PlayerId, string Message, DateTime Timestamp);
