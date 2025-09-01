# 🔧 문제 해결 가이드 (Troubleshooting)

## 📌 개요

이 문서는 프로젝트 개발 및 운영 중 발생할 수 있는 일반적인 문제들과 해결 방법을 정리했습니다. 문제 유형별로 분류하여 빠르게 해결책을 찾을 수 있도록 구성했습니다.

## 🚨 긴급 대응 체크리스트

서버 장애 시 다음 순서로 확인하세요:

1. **서비스 상태 확인**
   ```bash
   docker-compose ps                    # Docker 컨테이너 상태
   curl http://localhost:5006/api/auth/health  # AuthServer 헬스체크
   netstat -tulpn | grep 9999          # ActorServer 포트 확인
   ```

2. **로그 확인**
   ```bash
   docker-compose logs --tail=100      # 최근 로그 100줄
   docker-compose logs -f authserver   # AuthServer 실시간 로그
   docker-compose logs -f actorserver  # ActorServer 실시간 로그
   ```

3. **재시작**
   ```bash
   docker-compose restart              # 모든 서비스 재시작
   docker-compose restart authserver   # 특정 서비스만 재시작
   ```

## 🐳 Docker 관련 문제

### 문제 1: 컨테이너 시작 실패

**증상:**
```
ERROR: for authserver  Cannot start service authserver: 
driver failed programming external connectivity
```

**원인:**
- 포트가 이미 사용 중
- Docker 데몬 문제

**해결방법:**
```bash
# 1. 포트 사용 확인
sudo lsof -i :5006
sudo lsof -i :9999

# 2. 사용 중인 프로세스 종료
kill -9 [PID]

# 3. Docker 재시작
sudo systemctl restart docker  # Linux
# Windows: Docker Desktop 재시작

# 4. 포트 변경 (docker-compose.yml)
ports:
  - "5007:5006"  # 다른 포트로 변경
```

### 문제 2: 볼륨 권한 오류

**증상:**
```
SQLite Error: unable to open database file
Permission denied: /app/Database/game.db
```

**해결방법:**
```bash
# 1. 권한 확인
docker exec mmo-authserver ls -la /app/Database

# 2. 권한 수정
docker exec mmo-authserver chmod 755 /app/Database
docker exec mmo-authserver chmod 666 /app/Database/*.db

# 3. 볼륨 재생성
docker-compose down -v
docker-compose up -d
```

### 문제 3: 이미지 빌드 실패

**증상:**
```
ERROR: Service 'authserver' failed to build
The command '/bin/sh -c dotnet restore' returned a non-zero code: 1
```

**해결방법:**
```bash
# 1. 캐시 없이 재빌드
docker-compose build --no-cache

# 2. Docker 캐시 정리
docker system prune -a

# 3. .dockerignore 확인
# .dockerignore 파일에 필요한 파일이 제외되지 않았는지 확인
cat .dockerignore

# 4. 수동 빌드 테스트
docker build -f AuthServer/Dockerfile .
```

## 🌐 네트워크 관련 문제

### 문제 1: ActorServer 연결 실패

**증상:**
```
System.Net.Sockets.SocketException: Connection refused
```

**원인:**
- ActorServer가 실행되지 않음
- 방화벽 차단
- 잘못된 호스트/포트

**해결방법:**
```bash
# 1. 서비스 실행 확인
docker-compose ps
# actorserver 상태가 'Up'인지 확인

# 2. 포트 리스닝 확인
netstat -an | grep 9999

# 3. 방화벽 규칙 추가 (Windows)
netsh advfirewall firewall add rule name="ActorServer" dir=in action=allow protocol=TCP localport=9999

# 4. 연결 테스트
telnet localhost 9999
nc -zv localhost 9999
```

### 문제 2: AuthServer API 응답 없음

**증상:**
- HTTP 요청이 타임아웃
- Health check 실패

**해결방법:**
```bash
# 1. 컨테이너 내부에서 테스트
docker exec mmo-authserver curl http://localhost:5006/api/auth/health

# 2. 로그 확인
docker logs mmo-authserver | grep ERROR

# 3. 환경 변수 확인
docker exec mmo-authserver env | grep ASPNETCORE

# 4. 수동 실행 테스트
docker run -it --rm -p 5006:5006 mmo-authserver
```

## 💾 데이터베이스 관련 문제

### 문제 1: Database is locked

**증상:**
```
Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: 'database is locked'
```

**원인:**
- 동시 쓰기 작업
- 트랜잭션 미완료
- 테스트 중 DB 파일 잠금

**해결방법:**
```csharp
// 1. Connection String에 타임아웃 추가
"Data Source=game.db;Cache=Shared;Timeout=30"

// 2. 트랜잭션 제대로 종료
using var transaction = connection.BeginTransaction();
try
{
    // 작업
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}

// 3. 테스트에서는 격리된 DB 사용
Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "true");
```

### 문제 2: 데이터 마이그레이션 실패

**증상:**
- 테이블이 없다는 오류
- 컬럼이 없다는 오류

**해결방법:**
```bash
# 1. DB 백업
cp Database/game.db Database/game.db.backup

# 2. DB 재생성
rm Database/game.db
# 서버 재시작하면 자동 생성

# 3. SQLite 직접 확인
sqlite3 Database/game.db
.tables
.schema player_states
```

## 🎮 Actor 시스템 관련 문제

### 문제 1: Actor 메시지 DeadLetter

**증상:**
```
[WARNING] Message [EnterWorld] from Actor[/user/tcp-server] to Actor[/user/world] was not delivered. [1] dead letters encountered.
```

**원인:**
- Actor가 아직 생성되지 않음
- Actor가 종료됨
- 잘못된 Actor 경로

**해결방법:**
```csharp
// 1. Actor 생성 확인
var worldActor = Context.ActorSelection("/user/world");
worldActor.ResolveOne(TimeSpan.FromSeconds(3)).Wait();

// 2. Actor 경로 확인
Console.WriteLine($"Actor path: {Self.Path}");

// 3. Supervision 전략 확인
protected override SupervisorStrategy SupervisorStrategy()
{
    return new OneForOneStrategy(
        maxNrOfRetries: 10,
        withinTimeRange: TimeSpan.FromMinutes(1),
        localOnlyDecider: ex => ex switch
        {
            GameLogicException => Directive.Resume,
            _ => Directive.Restart
        });
}
```

### 문제 2: Actor 메시지 타임아웃

**증상:**
- ExpectMsg 테스트 실패
- Ask 패턴 타임아웃

**해결방법:**
```csharp
// 1. 타임아웃 시간 늘리기
var result = await actor.Ask<Response>(message, TimeSpan.FromSeconds(5));

// 2. 테스트에서 충분한 대기
var msg = ExpectMsg<ZoneChanged>(TimeSpan.FromSeconds(3));

// 3. Fire-and-forget 사용 (응답 불필요시)
actor.Tell(message);  // Ask 대신 Tell 사용
```

## 🧪 테스트 관련 문제

### 문제 1: 테스트 간 간섭

**증상:**
- 단독 실행은 성공, 전체 실행은 실패
- Random 테스트 실패

**해결방법:**
```csharp
// 1. TestCollection으로 순차 실행
[Collection("ActorTests")]
public class MyTest { }

// 2. 각 테스트마다 고유 이름 사용
var actorName = $"test-actor-{Guid.NewGuid()}";

// 3. 테스트 후 정리
public void Dispose()
{
    Sys.Terminate().Wait();
}
```

### 문제 2: CI/CD 파이프라인 테스트 실패

**증상:**
- 로컬은 성공, GitHub Actions는 실패

**해결방법:**
```yaml
# 1. 환경 변수 설정
env:
  TEST_ENVIRONMENT: true
  DOTNET_NOLOGO: true

# 2. 타임아웃 늘리기
- name: Run Tests
  timeout-minutes: 10
  run: dotnet test

# 3. 상세 로그 출력
run: dotnet test --logger "console;verbosity=detailed"
```

## 🔌 클라이언트 연결 문제

### 문제 1: 패킷 파싱 오류

**증상:**
```
Invalid packet format
Failed to deserialize JSON
```

**원인:**
- 패킷 끝에 개행문자(\n) 누락
- JSON 형식 오류

**해결방법:**
```csharp
// 1. 패킷 전송 시 개행문자 추가
var json = JsonSerializer.Serialize(packet);
var bytes = Encoding.UTF8.GetBytes(json + "\n");  // \n 필수!

// 2. 버퍼 처리 확인
private StringBuilder _buffer = new();

private void ProcessData(string data)
{
    _buffer.Append(data);
    var lines = _buffer.ToString().Split('\n');
    
    for (int i = 0; i < lines.Length - 1; i++)
    {
        if (!string.IsNullOrWhiteSpace(lines[i]))
            ProcessPacket(lines[i]);
    }
    
    _buffer.Clear();
    _buffer.Append(lines[^1]);  // 마지막 불완전 패킷 보존
}
```

### 문제 2: 토큰 인증 실패

**증상:**
```
Invalid or expired token
```

**해결방법:**
```bash
# 1. 토큰 만료 시간 확인 (24시간)
# AuthServer 로그 확인
docker logs mmo-authserver | grep token

# 2. 시간 동기화 확인
date  # 서버 시간 확인

# 3. 재로그인으로 새 토큰 발급
curl -X POST http://localhost:5006/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"accountId":"test_user"}'
```

## 🎯 성능 문제

### 문제 1: 메모리 누수

**증상:**
- 메모리 사용량 지속 증가
- OutOfMemoryException

**해결방법:**
```csharp
// 1. Actor 정리
protected override void PostStop()
{
    // 리소스 해제
    _connections?.Clear();
    base.PostStop();
}

// 2. 이벤트 핸들러 해제
public void Dispose()
{
    SomeEvent -= EventHandler;
}

// 3. 메모리 프로파일링
dotnet-counters monitor -p [PID]
```

### 문제 2: 높은 CPU 사용률

**증상:**
- CPU 100% 사용
- 응답 지연

**해결방법:**
```csharp
// 1. 불필요한 폴링 제거
// Bad
while (true)
{
    CheckSomething();
}

// Good
Context.System.Scheduler.ScheduleTellRepeatedly(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(1),
    Self,
    new CheckMessage(),
    Self
);

// 2. 배치 처리
var batch = new List<Message>();
// 100개 또는 100ms마다 처리
```

## 📝 로그 분석 팁

### 유용한 로그 명령어

```bash
# 에러만 필터링
docker-compose logs | grep -E "ERROR|EXCEPTION"

# 특정 PlayerId 추적
docker-compose logs | grep "Player 1001"

# 시간대별 로그
docker-compose logs -t | grep "2024-01-20"

# 실시간 에러 모니터링
docker-compose logs -f | grep --line-buffered ERROR

# 로그 파일로 저장
docker-compose logs > logs_$(date +%Y%m%d).txt
```

### 디버그 로그 추가

```csharp
// Actor 생명주기 로깅
protected override void PreStart()
{
    Console.WriteLine($"[{Self.Path}] Starting");
    base.PreStart();
}

protected override void PostStop()
{
    Console.WriteLine($"[{Self.Path}] Stopped");
    base.PostStop();
}

// 메시지 로깅
Receive<SomeMessage>(msg => 
{
    Console.WriteLine($"[{Self.Path}] Received: {msg}");
    // 처리
});
```

## 🆘 추가 지원

### 도움을 받을 수 있는 곳

1. **프로젝트 GitHub Issues**
   - https://github.com/Iris-Purple/ActorProject/issues

2. **Akka.NET 커뮤니티**
   - Gitter: https://gitter.im/akkadotnet/akka.net
   - Discord: Akka.NET 서버

3. **Stack Overflow**
   - 태그: `akka.net`, `docker`, `dotnet`

### 디버깅 체크리스트

문제 해결이 안 될 때:
- [ ] 에러 메시지 전체를 정확히 기록
- [ ] 재현 가능한 최소 코드 작성
- [ ] 환경 정보 수집 (OS, .NET 버전, Docker 버전)
- [ ] 로그 파일 수집
- [ ] 네트워크 상태 확인
- [ ] 리소스 사용량 확인
