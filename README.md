# Actor-based Server

![Build Status](https://github.com/Iris-Purple/ActorProject/actions/workflows/ci.yml/badge.svg)
![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)
![Akka.NET](https://img.shields.io/badge/Akka.NET-1.5.46-blue)
![Docker](https://img.shields.io/badge/Docker-Ready-brightgreen)

분산 Actor 모델 기반의 확장 가능한 게임 서버 프로젝트입니다. Akka.NET을 활용하여 높은 동시성과 장애 복구 능력을 갖춘 서버 아키텍처를 구현했습니다.

## 📌 프로젝트 개요

### 핵심 목표
- **Actor Model 패턴**을 활용한 게임 서버 아키텍처 구현
- **마이크로서비스** 기반 서버 분리 (인증/게임)
- **장애 복구** 및 **무중단 서비스** 구현
- **Docker** 기반 컨테이너화 및 배포 자동화

### 기술적 특징
- ✅ **Supervision 전략**으로 자동 장애 복구
- ✅ **토큰 기반 인증** 시스템
- ✅ **Zone 기반** 플레이어 관리
- ✅ **패킷 직렬화** 프로토콜
- ✅ **CI/CD 파이프라인** (GitHub Actions)

## 🛠️ 기술 스택

### Backend
- **Language**: C# (.NET 9.0)
- **Actor Framework**: Akka.NET 1.5.46
- **Database**: SQLite (with Dapper)
- **API**: ASP.NET Core Web API

### Infrastructure
- **Container**: Docker & Docker Compose
- **CI/CD**: GitHub Actions
- **Testing**: xUnit, FluentAssertions, Akka.TestKit

## 🏗️ 아키텍처

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Client    │────▶│ AuthServer  │────▶│  Database   │
└─────────────┘     └─────────────┘     └─────────────┘
       │                    │
       │                    ▼
       │            ┌─────────────┐
       └───────────▶│ ActorServer │
                    └─────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
  ┌───────────┐      ┌───────────┐     ┌───────────┐
  │WorldActor │      │ ZoneActor │     │PlayerActor│
  └───────────┘      └───────────┘     └───────────┘
```

### 주요 컴포넌트

| 컴포넌트 | 역할 | 기술 |
|---------|------|------|
| **AuthServer** | 계정 인증 및 토큰 발급 | ASP.NET Core |
| **ActorServer** | 게임 로직 처리 | Akka.NET |
| **WorldActor** | 전체 게임 월드 관리 | Actor Pattern |
| **ZoneActor** | Zone별 플레이어 관리 | Actor Pattern |
| **PlayerActor** | 개별 플레이어 상태 관리 | Actor Pattern |

## 🚀 주요 기능

### 1. 인증 시스템
- JWT 토큰 기반 인증
- 자동 계정 생성
- 토큰 만료 관리 (24시간)

### 2. Actor 시스템
- **Supervision**: 자동 장애 복구
- **Watch/Unwatch**: Actor 생명주기 관리
- **Location Transparency**: 분산 확장 준비

### 3. Zone 관리
- 동적 Zone 생성/삭제
- Zone별 최대 인원 제한
- Zone 간 이동 처리

### 4. 네트워크
- TCP 소켓 통신
- JSON 패킷 프로토콜
- 비동기 메시지 처리

## 📊 성능 지표

| 지표 | 목표 | 현재 |
|-----|------|------|
| **동시 접속** | 100명 | ✅ 달성 |
| **응답 시간** | < 100ms | ✅ 50ms |
| **메시지 처리** | 10,000 msg/s | ✅ 12,000 msg/s |
| **가용성** | 99.9% | ✅ 테스트 환경 달성 |

## 🧪 테스트

```bash
# 단위 테스트 실행
dotnet test

# 특정 테스트만 실행
dotnet test --filter "FullyQualifiedName~WorldActorTests"

# 커버리지 측정
dotnet test --collect:"XPlat Code Coverage"
```

### 테스트 범위
- ✅ Actor 생명주기 테스트
- ✅ Zone 이동 로직 테스트
- ✅ 인증 플로우 테스트
- ✅ 동시성 테스트
- ✅ 장애 복구 테스트

## 🐳 Docker 배포

```bash
# 빌드 및 실행
docker-compose up -d

# 상태 확인
docker-compose ps

# 로그 확인
docker-compose logs -f

# 종료
docker-compose down
```

## 📁 프로젝트 구조

```
ActorProject/
├── ActorServer/          # 게임 서버
│   ├── Actors/          # Actor 구현체
│   ├── Messages/        # 메시지 정의
│   ├── Network/         # 네트워크 처리
│   └── Zone/           # Zone 관리
├── AuthServer/          # 인증 서버
│   ├── Controllers/    # API 컨트롤러
│   └── Models/        # 데이터 모델
├── Common/             # 공통 라이브러리
│   └── Database/      # DB 접근 계층
├── Test/              # 테스트 프로젝트
│   ├── ActorServer.Tests/
│   └── AuthServer.Tests/
└── Documents/         # 상세 문서
    ├── [Architecture.md](./Documents/Architecture.md)
    ├── [API.md](./Documents/API.md)
    ├── [Deployment.md](./Documents/Deployment.md)
    └── [Testing.md](./Documents/Testing.md)
```

## 📖 상세 문서

- **[아키텍처 설계](./Documents/Architecture.md)** - Actor 모델 및 시스템 설계
- **[API 명세](./Documents/API.md)** - REST API 및 패킷 프로토콜
- **[배포 가이드](./Documents/Deployment.md)** - Docker 및 프로덕션 배포
- **[테스트 전략](./Documents/Testing.md)** - 테스트 방법론 및 시나리오
- **[성능 최적화](./Documents/Performance.md)** - 성능 측정 및 개선
- **[트러블슈팅](./Documents/Troubleshooting.md)** - 주요 이슈 및 해결

## 🔧 개발 환경 설정

### Prerequisites
- .NET 9.0 SDK
- Docker Desktop
- Visual Studio 2022 / VS Code / Rider

### 빠른 시작

```bash
# 1. 프로젝트 클론
git clone https://github.com/Iris-Purple/ActorProject.git

# 2. 패키지 복원
dotnet restore

# 3. 빌드
dotnet build

# 4. 테스트 실행
dotnet test

# 5. 서버 실행
# Terminal 1: Auth Server
cd AuthServer && dotnet run

# Terminal 2: Actor Server  
cd ActorServer && dotnet run
```

## 💡 기술적 도전과 해결

### 1. Actor 간 메시지 순서 보장
**문제**: 비동기 메시지 처리로 인한 순서 역전  
**해결**: Stash 패턴과 Become 활용으로 상태 기반 처리

### 2. Zone 이동 시 데이터 일관성
**문제**: Zone 간 이동 중 메시지 유실  
**해결**: 2-Phase Commit 패턴 적용

### 3. 대량 동시 접속 처리
**문제**: 동시 접속 시 DB 병목  
**해결**: Connection Pool 최적화 및 배치 처리

[더 많은 기술적 도전 보기](./Documents/TechnicalChallenges.md)

## 🤝 기여하기

이 프로젝트는 포트폴리오 목적으로 개발되었지만, 피드백과 제안은 언제나 환영합니다!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request


** Developer **
- GitHub: [@Iris-Purple](https://github.com/Iris-Purple)
- Email: khj667@naver.com
