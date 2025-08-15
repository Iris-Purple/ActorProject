namespace ActorServer.Messages;


// === Supervision 테스트 ===
public record TestSupervision(string PlayerName, string TestType);
public record TestNullCommand();
public record SimulateCrash(string Reason);
public record SimulateOutOfMemory();
public record CrashAndRecover(string Reason);
