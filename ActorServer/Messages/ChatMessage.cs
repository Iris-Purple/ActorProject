namespace ActorServer.Messages;

// === 채팅 메시지 ===
public record ChatMessage(string PlayerName, string Message);
public record ChatBroadcast(string PlayerName, string Message, DateTime Timestamp);
