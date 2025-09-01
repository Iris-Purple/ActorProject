# 🧪 테스트 전략 및 가이드

## 📌 개요

이 문서는 프로젝트의 테스트 전략, 방법론, 그리고 실행 가이드를 설명합니다. Actor 기반 시스템의 특성을 고려한 테스트 접근법을 제시합니다.

## 🎯 테스트 철학

### 핵심 원칙
1. **격리성**: 각 테스트는 독립적으로 실행 가능
2. **재현성**: 동일한 조건에서 항상 같은 결과
3. **신속성**: 빠른 피드백 루프
4. **명확성**: 실패 시 원인을 즉시 파악 가능

## 📊 현재 테스트 현황

### 구현된 테스트
| 테스트 파일 | 테스트 수 | 주요 검증 내용 |
|------------|----------|---------------|
| **WorldActorTests.cs** | 10개 | PlayerActor 생성, 생명주기, 재접속 처리 |
| **ZoneActorTests.cs** | 9개 | Zone 변경, 플레이어 이동, 연결 해제 |
| **DatabaseTests.cs** | 1개 | Zone 변경 시 DB 저장 |
| **LoginPacketHandlerTests.cs** | 2개 | 토큰 검증, 로그인 처리 |
| **MovePacketHandlerTests.cs** | 3개 | 이동 명령 전달, 미인증 차단 |
| **LoginTests.cs** | 8개 | AuthServer API, 계정 생성 |

### 테스트 환경 구조
```
Test/
├── ActorServer.Tests/
│   ├── TestHelpers/
│   │   ├── AkkaTestKitBase.cs    # 테스트 베이스 클래스
│   │   └── MockActors.cs          # Mock Actor 구현
│   ├── WorldActorTests.cs        # WorldActor 테스트
│   ├── ZoneActorTests.cs         # ZoneActor 테스트
│   ├── DatabaseTests.cs          # DB 저장 테스트
│   ├── LoginPacketHandlerTests.cs # 로그인 핸들러 테스트
│   └── MovePacketHandlerTests.cs  # 이동 핸들러 테스트
│
└── AuthServer.Tests/
    ├── TestHelpers/
    │   └── TestCollection.cs     # 테스트 격리 설정
    └── LoginTests.cs             # 로그인 API 테스트
```

## 🛠️ 테스트 환경 설정

### 사용 중인 패키지
```xml
<!-- ActorServer.Tests.csproj -->
<PackageReference Include="Akka.TestKit.Xunit2" Version="1.5.46" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
<PackageReference Include="coverlet.collector" Version="6.0.0" />

<!-- AuthServer.Tests.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
```

## 🔬 구현된 단위 테스트

### 1. WorldActor 테스트

#### PlayerActor 생성 테스트
```csharp
[Fact]
public void WorldActor_Should_Create_PlayerActor_On_EnterWorld()
{
    using var scope = Test();
    
    // Arrange
    const long playerId = 7001;
    var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world");
    PlayerDatabase.Instance.GetOrCreatePlayerId(playerId);
    
    // Act
    worldActor.Tell(new EnterWorld(playerId));
    Thread.Sleep(1000);
    
    // Assert - DB에 저장되었는지로 간접 확인
    var savedData = PlayerDatabase.Instance.LoadPlayer(playerId);
    savedData.Should().NotBeNull();
    savedData!.Value.zone.Should().Be((int)ZoneId.Town);
    
    scope.LogSuccess($"Player {playerId} created and spawned in Town");
}
```

#### 재접속 처리 테스트
```csharp
[Fact]
public void WorldActor_Should_Handle_Reconnection()
{
    using var scope = Test();
    
    const long playerId = 7002;
    var worldActor = Sys.ActorOf(Props.Create<WorldActor>(), "world2");
    PlayerDatabase.Instance.GetOrCreatePlayerId(playerId);
    
    // 첫 접속
    worldActor.Tell(new EnterWorld(playerId));
    Thread.Sleep(500);
    
    // 재접속 (에러 없이 처리되어야 함)
    var exception = Record.Exception(() =>
    {
        worldActor.Tell(new EnterWorld(playerId));
        Thread.Sleep(500);
    });
    
    exception.Should().BeNull();
    scope.LogSuccess($"Player {playerId} reconnection handled without error");
}
```

### 2. ZoneActor 테스트

#### Zone 변경 성공 테스트
```csharp
[Fact]
public void ZoneActor_Should_Handle_Zone_Change_Successfully()
{
    using var scope = Test();
    
    // Arrange
    var playerProbe = CreateTestProbe("playerProbe");
    var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-actor");
    const long testPlayerId = 2001;
    
    // Act - Town으로 Zone 변경 요청
    var changeRequest = new ChangeZoneRequest(
        PlayerActor: playerProbe.Ref,
        PlayerId: testPlayerId,
        TargetZoneId: ZoneId.Town
    );
    
    zoneActor.Tell(changeRequest);
    
    // Assert
    var response = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(2));
    response.Should().NotBeNull();
    response.NewZoneId.Should().Be(ZoneId.Town);
    response.SpawnPosition.Should().Be(new Position(0, 0));
    
    scope.LogSuccess($"Zone change successful");
}
```

#### 플레이어 연결 해제 테스트
```csharp
[Fact]
public void ZoneActor_Should_Remove_Player_On_Disconnection()
{
    using var scope = Test();
    
    var playerProbe = CreateTestProbe("playerProbe");
    var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-disconnect");
    const long testPlayerId = 2004;
    
    // 플레이어를 Town에 추가
    zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Town));
    var response = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
    response.NewZoneId.Should().Be(ZoneId.Town);
    
    // 플레이어 연결 종료
    zoneActor.Tell(new PlayerDisconnected(testPlayerId));
    Thread.Sleep(500);
    
    // 같은 플레이어로 다시 Zone 진입 시도 (제거되었으면 가능해야 함)
    zoneActor.Tell(new ChangeZoneRequest(playerProbe.Ref, testPlayerId, ZoneId.Town));
    var reconnectResponse = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(1));
    
    reconnectResponse.Should().NotBeNull();
    scope.LogSuccess("Player successfully removed and can re-enter zone");
}
```

### 3. 패킷 핸들러 테스트

#### LoginPacketHandler 테스트
```csharp
[Fact]
public async Task LoginHandler_Should_Success_With_Valid_Token()
{
    using var scope = Test();
    
    // Arrange
    var handler = new LoginPacketHandler();
    var clientProbe = CreateTestProbe("clientProbe");
    var worldProbe = CreateTestProbe("worldProbe");
    
    // world Actor 등록
    Sys.ActorOf(Props.Create(() => new ForwardActor(worldProbe)), "world");
    
    var context = new TestClientContext(clientProbe.Ref);
    
    // AccountDatabase에 테스트 계정 생성
    var accountDb = AccountDatabase.Instance;
    var loginResult = await accountDb.ProcessLoginAsync("test_player_1");
    
    var loginPacket = new LoginPacket
    {
        PlayerId = loginResult.PlayerId,
        Token = loginResult.Token!
    };
    
    // Act
    var worldSelection = Sys.ActorSelection("/user/world");
    await handler.HandlePacket(loginPacket, context, worldSelection);
    
    // Assert
    var enterWorldMsg = worldProbe.ExpectMsg<EnterWorld>(TimeSpan.FromSeconds(1));
    enterWorldMsg.PlayerId.Should().Be(loginResult.PlayerId);
    
    context.PlayerId.Should().Be(loginResult.PlayerId);
    scope.LogSuccess($"Login successful for PlayerId: {loginResult.PlayerId}");
}
```

## 🔗 통합 테스트

### AuthServer API 테스트 (LoginTests.cs)

```csharp
[Collection("AuthServerTests")]
public class LoginTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Login_Should_Create_New_Account_For_First_Login()
    {
        // Arrange
        var request = new LoginRequest
        {
            AccountId = $"test_user_{Guid.NewGuid():N}"
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);
        
        // Assert
        response.Should().BeSuccessful();
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse!.Success.Should().BeTrue();
        loginResponse.IsNewAccount.Should().BeTrue();
        loginResponse.PlayerId.Should().BeGreaterThan(0);
        loginResponse.Token.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task Login_Should_Handle_Concurrent_Logins()
    {
        // 10개 동시 로그인 테스트
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 10; i++)
        {
            var request = new LoginRequest 
            { 
                AccountId = $"concurrent_user_{i}_{Guid.NewGuid():N}"
            };
            tasks.Add(_client.PostAsJsonAsync("/api/auth/login", request));
        }
        
        var responses = await Task.WhenAll(tasks);
        
        foreach (var response in responses)
        {
            response.Should().BeSuccessful();
        }
    }
}
```

### 데이터베이스 통합 테스트
```csharp
[Fact]
public void ZoneActor_Should_Save_Player_Position_To_DB_On_Zone_Change()
{
    using var scope = Test();
    
    var playerProbe = CreateTestProbe("playerProbe");
    var zoneActor = Sys.ActorOf(Props.Create<ZoneActor>(), "test-zone-db");
    const long testPlayerId = 3001;
    var db = PlayerDatabase.Instance;
    
    db.GetOrCreatePlayerId(testPlayerId);
    
    // Town으로 이동
    var changeRequest = new ChangeZoneRequest(
        PlayerActor: playerProbe.Ref,
        PlayerId: testPlayerId,
        TargetZoneId: ZoneId.Town
    );
    
    zoneActor.Tell(changeRequest);
    var response = playerProbe.ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(2));
    
    // DB 확인
    Thread.Sleep(500);
    var savedData = db.LoadPlayer(testPlayerId);
    
    savedData.Should().NotBeNull();
    savedData!.Value.zone.Should().Be((int)ZoneId.Town);
    
    scope.LogSuccess($"DB save verified");
}
```

## 🎭 테스트 헬퍼 클래스

### AkkaTestKitBase.cs
```csharp
public abstract class AkkaTestKitBase : TestKit
{
    protected AkkaTestKitBase(ITestOutputHelper output)
        : base(@"
            akka {
                loglevel = WARNING
                stdout-loglevel = WARNING
                log-dead-letters = off
            }
        ", output: output)
    {
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
    }

    // 테스트 자동 로깅
    protected TestScope Test(int milliseconds = 100, [CallerMemberName] string testName = "")
        => new TestScope(Output, testName, milliseconds);

    // 긴 테스트용
    protected TestScope SlowTest([CallerMemberName] string testName = "")
        => new TestScope(Output, testName, 2000);
}
```

### MockActors.cs
```csharp
public class MockClientActor : ReceiveActor
{
    private readonly IActorRef _probe;

    public MockClientActor(IActorRef probe)
    {
        _probe = probe;
        ReceiveAny(msg => _probe.Forward(msg));
    }
}

public class MockZoneActor : ReceiveActor
{
    private readonly IActorRef _probe;

    public MockZoneActor(IActorRef probe)
    {
        _probe = probe;
        ReceiveAny(msg => _probe.Forward(msg));
    }
}
```

## 🏃 테스트 실행 가이드

### 모든 테스트 실행
```bash
# 솔루션 루트에서
dotnet test

# 상세 출력
dotnet test --logger "console;verbosity=normal"
```

### 특정 테스트 실행
```bash
# ActorServer 테스트만
dotnet test Test/ActorServer.Tests

# AuthServer 테스트만
dotnet test Test/AuthServer.Tests

# 특정 테스트 클래스
dotnet test --filter "FullyQualifiedName~WorldActorTests"

# 특정 테스트 메서드
dotnet test --filter "WorldActor_Should_Create_PlayerActor"
```

### CI/CD 파이프라인 (GitHub Actions)
```yaml
# .github/workflows/ci.yml
- name: Run AuthServer Tests
  run: |
    echo "Running AuthServer Tests..."
    dotnet test Test/AuthServer.Tests --no-build --configuration Release --logger "console;verbosity=normal"

- name: Run ActorServer Tests  
  run: |
    echo "Running ActorServer Tests..."
    dotnet test Test/ActorServer.Tests --no-build --configuration Release --logger "console;verbosity=normal"
```

## 🐛 디버깅 팁

### Visual Studio
1. 테스트 탐색기 열기 (`Ctrl+E, T`)
2. 테스트 우클릭 → "디버그"
3. 중단점 설정하여 디버깅

### VS Code
```json
// .vscode/launch.json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Debug ActorServer Tests",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/Test/ActorServer.Tests/bin/Debug/net9.0/ActorServer.Tests.dll",
            "args": [],
            "cwd": "${workspaceFolder}/Test/ActorServer.Tests"
        }
    ]
}
```

### 테스트 출력 로깅
```csharp
// TestScope 사용 (AkkaTestKitBase)
using var scope = Test();
scope.Log("테스트 시작");
scope.LogSuccess("성공!");
scope.LogError("에러 발생");
scope.LogWarning("경고");

// ITestOutputHelper 직접 사용
_output.WriteLine($"디버그 정보: {someValue}");
```

## 🚨 일반적인 문제 해결

### 1. "Database is locked" 오류
```csharp
// 해결: TestCollection으로 순차 실행
[Collection("ActorTests")]
public class MyDatabaseTest { }
```

### 2. ExpectMsg Timeout
```csharp
// 충분한 대기 시간 설정
var msg = ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(2));

// 또는 SlowTest 사용
using var scope = SlowTest();
```

### 3. 테스트 환경 DB 분리
```csharp
// AkkaTestKitBase 생성자에서 자동 설정
Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
// → test_collection.db 사용
```

## ✅ 테스트 체크리스트

### 새 기능 추가 시
- [ ] 단위 테스트 작성
- [ ] 엣지 케이스 테스트
- [ ] 에러 처리 테스트
- [ ] 기존 테스트 영향 확인

### PR 제출 전
- [ ] 모든 테스트 통과 (`dotnet test`)
- [ ] CI 파이프라인 통과
- [ ] 테스트 실행 시간 확인
- [ ] 불필요한 로그 제거

## 📚 참고 자료

- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)
- [Akka.NET TestKit](https://getakka.net/articles/actors/testing-actor-systems.html)
- [FluentAssertions Guide](https://fluentassertions.com/introduction)
