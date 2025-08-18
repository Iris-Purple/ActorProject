# 🎮 MMORPG Game Server with Akka.NET

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=.net)](https://dotnet.microsoft.com/)
[![Akka.NET](https://img.shields.io/badge/Akka.NET-1.5.46-blue)](https://getakka.net/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Actor 모델 기반의 확장 가능한 MMORPG 게임 서버 구현 프로젝트입니다. Akka.NET을 활용하여 고가용성과 장애 복구 능력을 갖춘 분산 시스템을 구축했습니다.

## 🌟 주요 특징

### Actor 기반 아키텍처
- **PlayerActor**: 각 플레이어를 독립적인 Actor로 관리
- **ZoneActor**: Zone별 플레이어 및 이벤트 처리
- **WorldActor**: 전체 게임 월드 조율 및 플레이어 라이프사이클 관리

### Supervision Strategy
- **자동 복구**: 에러 타입별 Resume/Restart/Stop 전략
- **장애 격리**: Actor 간 독립적인 에러 처리로 시스템 안정성 확보
- **계층적 감독**: WorldActor → ZoneManager → ZoneActor → PlayerActor

### 핵심 기능
- ✅ TCP 기반 실시간 통신
- ✅ Zone 기반 맵 시스템 (Town, Forest, Dungeon)
- ✅ Zone별 채팅 시스템
- ✅ 플레이어 이동 및 상태 관리
- ✅ 동시 다중 접속 처리

## 🛠️ 기술 스택

- **Language**: C# 12 / .NET 9.0
- **Actor Framework**: Akka.NET 1.5.46
- **Network**: TCP Socket (Akka.IO)
- **Test**: xUnit + Akka.TestKit + FluentAssertions
- **Client UI**: Spectre.Console

## 📁 프로젝트 구조
ActorProject/
├── ActorServer/              # 게임 서버
│   ├── Actors/              # Actor 구현체
│   │   ├── WorldActor.cs    # 월드 관리
│   │   ├── ZoneManager.cs   # Zone 관리
│   │   ├── ZoneActor.cs     # Zone별 로직
│   │   └── PlayerActor.cs   # 플레이어 로직
│   ├── Messages/            # Actor 메시지 정의
│   ├── Network/             # TCP 통신
│   └── Exceptions/          # 커스텀 예외
├── ActorClient/             # 터미널 클라이언트
└── ActorServer.Tests/       # 테스트 코드

## 🏗️ 아키텍처

```mermaid
graph TD
    Client[TCP Client] -->|Connect| TCP[TcpServerActor]
    TCP --> CC[ClientConnectionActor]
    CC --> WA[WorldActor]
    WA --> ZM[ZoneManager]
    ZM --> ZT[Zone: Town]
    ZM --> ZF[Zone: Forest]
    ZM --> ZD[Zone: Dungeon]
    WA --> PA1[PlayerActor 1]
    WA --> PA2[PlayerActor 2]
    WA --> PAN[PlayerActor N]
🚀 실행 방법
서버 실행
bashcd ActorServer
dotnet run
클라이언트 실행
bashcd ActorClient
dotnet run
테스트 실행
bashcd ActorServer.Tests
dotnet test
💻 클라이언트 명령어
명령어설명/login <name>서버 접속/move <x> <y>좌표 이동/say <message>채팅 메시지/zone <name>Zone 이동 (town/forest/dungeon-1)/quit접속 종료
🔄 Supervision Strategy
csharp// 에러 타입별 처리 전략
GameLogicException     → Resume   // 게임 로직 오류, 상태 유지
ArgumentNullException  → Resume   // 잘못된 입력, 무시하고 계속
TemporaryGameException → Restart  // 일시적 오류, 상태 초기화
CriticalGameException  → Stop     // 치명적 오류, Actor 종료
📊 주요 메시지 플로우
플레이어 로그인
Client → PlayerLoginRequest → WorldActor
WorldActor → Create PlayerActor
WorldActor → ChangeZoneRequest → ZoneManager
ZoneManager → AddPlayerToZone → ZoneActor
Zone 간 이동
Client → RequestZoneChange → WorldActor → ZoneManager
ZoneManager → RemovePlayerFromZone → Current Zone
ZoneManager → AddPlayerToZone → Target Zone
채팅 메시지
Client → ChatMessage → PlayerActor → ZoneActor
ZoneActor → Broadcast → All Players in Zone
🧪 테스트

Unit Tests: Position 데이터 검증
Actor Tests: PlayerActor 메시지 처리 검증
Integration Tests: 채팅 시스템 통합 테스트
Test Framework: xUnit + Akka.TestKit + FluentAssertions

📈 성능 특징

비동기 메시지 처리: Actor 모델의 논블로킹 처리
독립적 Actor 실행: 플레이어별 독립 처리로 확장성 확보
Zone별 부하 분산: Zone 단위로 플레이어 분산 관리
장애 격리: Actor 단위 에러 처리로 전체 시스템 영향 최소화

🔍 핵심 설계 원칙

Actor per Player: 각 플레이어를 독립적인 Actor로 관리
Hierarchical Supervision: 계층적 감독 구조로 안정성 확보
Message-Driven: 모든 통신은 비동기 메시지 기반
Fault Tolerance: Supervision Strategy를 통한 자동 복구
Location Transparency: Actor 위치 투명성으로 확장 가능

📝 개선 계획

 Akka.Cluster를 활용한 분산 서버 구성
 Persistence를 통한 플레이어 상태 저장
 WebSocket 지원 추가
 더 많은 Zone 타입 추가
 전투 시스템 구현

