# 🚀 배포 가이드

## 📌 개요

이 문서는 게임 서버를 로컬 개발 환경부터 프로덕션 환경까지 배포하는 방법을 설명합니다. Docker를 사용한 컨테이너 배포를 기본으로 합니다.

## 🏗️ 배포 아키텍처

```
┌─────────────────────────────────────────────┐
│              Docker Network                  │
│                                              │
│  ┌──────────────┐       ┌──────────────┐   │
│  │ AuthServer   │       │ ActorServer  │   │
│  │ Container    │◄─────►│ Container    │   │
│  │ Port: 5006   │       │ Port: 9999   │   │
│  └──────────────┘       └──────────────┘   │
│         │                       │           │
│         └───────┬───────────────┘           │
│                 │                           │
│         ┌──────▼──────┐                    │
│         │   Volumes    │                    │
│         │  auth-db/    │                    │
│         │  game-db/    │                    │
│         └─────────────┘                    │
└─────────────────────────────────────────────┘
```

## 🔧 사전 요구사항

### 필수 도구
| 도구 | 버전 | 용도 |
|------|------|------|
| **Docker** | 20.10+ | 컨테이너 실행 |
| **Docker Compose** | 2.0+ | 멀티 컨테이너 관리 |
| **.NET SDK** | 9.0 | 로컬 개발 (선택) |

### 도구 설치 확인
```bash
# Docker 버전 확인
docker --version
docker-compose --version

# .NET SDK 확인 (로컬 개발 시)
dotnet --version
```

## 🏃 빠른 시작 (Quick Start)

### 1분 배포
```bash
# 1. 프로젝트 클론
git clone https://github.com/Iris-Purple/ActorProject.git
cd ActorProject

# 2. Docker 컨테이너 빌드 및 실행
docker-compose up -d

# 3. 상태 확인
docker-compose ps

# 4. 로그 확인
docker-compose logs -f
```

### 서비스 접속
- **AuthServer**: http://localhost:5006
- **ActorServer**: TCP localhost:9999
- **Health Check**: http://localhost:5006/api/auth/health

## 📦 Docker 배포 상세

### 1. Docker 이미지 구조

프로젝트는 두 개의 Docker 이미지를 사용합니다:

| 이미지 | 파일 위치 | Base Image | 포트 |
|--------|----------|------------|------|
| **AuthServer** | `AuthServer/Dockerfile` | mcr.microsoft.com/dotnet/aspnet:9.0 | 5006 |
| **ActorServer** | `ActorServer/Dockerfile` | mcr.microsoft.com/dotnet/runtime:9.0 | 9999 |

**빌드 전략**:
- Multi-stage 빌드로 이미지 크기 최적화
- Build stage: SDK 이미지로 컴파일
- Runtime stage: 경량 런타임 이미지로 실행
- 각 서버별 Database 폴더 자동 생성

### 2. Docker Compose 구성

```yaml
# docker-compose.yml
version: '3.8'

services:
  authserver:
    build:
      context: .
      dockerfile: AuthServer/Dockerfile
    container_name: mmo-authserver
    ports:
      - "5006:5006"
    volumes:
      - auth-db:/app/Database
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5006
      - ConnectionStrings__AuthDb=Data Source=/app/Database/auth.db
    networks:
      - mmo-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5006/api/auth/health"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s

  actorserver:
    build:
      context: .
      dockerfile: ActorServer/Dockerfile
    container_name: mmo-actorserver
    ports:
      - "9999:9999"
    volumes:
      - game-db:/app/Database
    environment:
      - ENVIRONMENT=Production
      - AUTH_SERVER_URL=http://authserver:5006
    networks:
      - mmo-network
    depends_on:
      - authserver
    restart: unless-stopped

volumes:
  auth-db:
    name: mmo-auth-database
  game-db:
    name: mmo-game-database

networks:
  mmo-network:
    name: mmo-network
    driver: bridge
```

### 3. 배포 명령어

#### 시작/종료
```bash
# 서비스 시작 (백그라운드)
docker-compose up -d

# 서비스 종료
docker-compose down

# 서비스 종료 + 볼륨 삭제 (데이터 초기화)
docker-compose down -v
```

#### 빌드/재빌드
```bash
# 이미지 빌드
docker-compose build

# 캐시 없이 재빌드
docker-compose build --no-cache

# 특정 서비스만 재빌드
docker-compose build authserver
```

#### 로그 확인
```bash
# 모든 서비스 로그
docker-compose logs

# 실시간 로그 (follow)
docker-compose logs -f

# 특정 서비스 로그
docker-compose logs authserver
docker-compose logs actorserver

# 최근 100줄만
docker-compose logs --tail=100
```

#### 상태 확인
```bash
# 컨테이너 상태
docker-compose ps

# 리소스 사용량
docker stats

# 네트워크 확인
docker network ls
docker network inspect mmo-network
```

## 🔨 배포 스크립트

### 개발 환경 배포 (scripts/docker-dev.sh)
```bash
#!/bin/bash
# 개발 환경 Docker 실행 스크립트

echo "🐳 Building Docker images..."
docker-compose build

echo "🚀 Starting services..."
docker-compose up -d

echo "📊 Checking service status..."
docker-compose ps

echo "📝 Viewing logs (Ctrl+C to exit)..."
docker-compose logs -f
```

### 클린 재배포 (scripts/docker-clean-rebuild.sh)
```bash
#!/bin/bash

echo "🧹 Cleaning up existing containers and images..."
docker-compose down -v
docker system prune -f

echo "🔨 Rebuilding images..."
docker-compose build --no-cache

echo "🚀 Starting services..."
docker-compose up -d

echo "⏳ Waiting for services to be ready..."
sleep 10

echo "📊 Service status:"
docker-compose ps

echo "🔍 Checking AuthServer health:"
curl -f http://localhost:5006/api/auth/health || echo "❌ AuthServer not ready yet"

echo ""
echo "📝 View logs with: docker-compose logs -f"
```

### 정리 스크립트 (scripts/docker-clean.sh)
```bash
#!/bin/bash
# Docker 정리 스크립트

echo "🛑 Stopping containers..."
docker-compose down

echo "🗑️ Removing volumes..."
docker-compose down -v

echo "🧹 Pruning unused images..."
docker image prune -f

echo "✅ Cleanup complete!"
```

## 💾 데이터 관리

### 볼륨 구조
```
Docker Volumes:
├── mmo-auth-database/     # AuthServer 데이터
│   └── auth.db            # 계정 정보
└── mmo-game-database/     # ActorServer 데이터
    └── game.db            # 플레이어 상태
```

### 백업
```bash
# 볼륨 백업
docker run --rm -v mmo-auth-database:/data -v $(pwd):/backup \
  alpine tar czf /backup/auth-backup-$(date +%Y%m%d).tar.gz -C /data .

docker run --rm -v mmo-game-database:/data -v $(pwd):/backup \
  alpine tar czf /backup/game-backup-$(date +%Y%m%d).tar.gz -C /data .
```

### 복원
```bash
# 볼륨 복원
docker run --rm -v mmo-auth-database:/data -v $(pwd):/backup \
  alpine tar xzf /backup/auth-backup-20240120.tar.gz -C /data

docker run --rm -v mmo-game-database:/data -v $(pwd):/backup \
  alpine tar xzf /backup/game-backup-20240120.tar.gz -C /data
```

## 🌐 로컬 개발 환경

### .NET 직접 실행
```bash
# Terminal 1: AuthServer
cd AuthServer
dotnet run
# → http://localhost:5006

# Terminal 2: ActorServer
cd ActorServer
dotnet run
# → TCP 9999
```

### Visual Studio
1. `ActorProject.sln` 열기
2. 솔루션 우클릭 → "여러 시작 프로젝트 설정"
3. AuthServer와 ActorServer를 "시작"으로 설정
4. F5로 디버깅 시작

### VS Code
```json
// .vscode/launch.json
{
    "version": "0.2.0",
    "compounds": [
        {
            "name": "All Servers",
            "configurations": ["AuthServer", "ActorServer"]
        }
    ],
    "configurations": [
        {
            "name": "AuthServer",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/AuthServer/bin/Debug/net9.0/AuthServer.dll",
            "cwd": "${workspaceFolder}/AuthServer",
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            }
        },
        {
            "name": "ActorServer",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/ActorServer/bin/Debug/net9.0/ActorServer.dll",
            "cwd": "${workspaceFolder}/ActorServer"
        }
    ]
}
```

## 🔍 모니터링

### 헬스 체크
```bash
# AuthServer 상태 확인
curl http://localhost:5006/api/auth/health

# 응답 예시
{
    "status": "healthy",
    "timestamp": "2024-01-20T10:30:00Z"
}
```

### 컨테이너 로그 분석
```bash
# 에러 로그만 필터링
docker-compose logs | grep ERROR

# 특정 플레이어 추적
docker-compose logs | grep "Player 1001"

# 타임스탬프 포함
docker-compose logs -t
```

### 리소스 모니터링
```bash
# CPU/메모리 사용량
docker stats --no-stream

# 디스크 사용량
docker system df
```

## ⚠️ 트러블슈팅

### 1. 포트 충돌
```bash
# 오류: bind: address already in use

# 해결 1: 사용 중인 포트 확인
netstat -tulpn | grep 5006
netstat -tulpn | grep 9999

# 해결 2: docker-compose.yml에서 포트 변경
ports:
  - "5007:5006"  # 호스트 포트 변경
```

### 2. 컨테이너 시작 실패
```bash
# 로그 확인
docker-compose logs authserver

# 일반적인 원인:
# - Dockerfile 빌드 오류
# - 환경 변수 누락
# - 볼륨 권한 문제

# 해결: 클린 재시작
docker-compose down -v
docker-compose build --no-cache
docker-compose up
```

### 3. 데이터베이스 접근 오류
```bash
# SQLite 파일 권한 확인
docker exec mmo-authserver ls -la /app/Database

# 권한 수정 (필요시)
docker exec mmo-authserver chmod 666 /app/Database/auth.db
```

### 4. 네트워크 연결 문제
```bash
# 컨테이너 간 통신 테스트
docker exec mmo-actorserver ping authserver

# DNS 확인
docker exec mmo-actorserver nslookup authserver
```

## 🚢 프로덕션 배포 고려사항

### 보안 설정
```yaml
# docker-compose.prod.yml
services:
  authserver:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__AuthDb=${DB_CONNECTION_STRING}  # 환경 변수 사용
    secrets:
      - auth_db_password
```

### 리버스 프록시 (Nginx)
```nginx
# nginx.conf
upstream authserver {
    server authserver:5006;
}

server {
    listen 80;
    server_name api.yourgame.com;
    
    location /api/auth {
        proxy_pass http://authserver;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### 로그 관리
```yaml
# 로그 드라이버 설정
services:
  authserver:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

## 📊 배포 체크리스트

### 배포 전
- [ ] 코드 변경사항 커밋
- [ ] 테스트 통과 확인
- [ ] 환경 변수 설정
- [ ] 백업 수행

### 배포 중
- [ ] 이전 컨테이너 종료
- [ ] 새 이미지 빌드
- [ ] 컨테이너 시작
- [ ] 헬스 체크 확인

### 배포 후
- [ ] 서비스 접속 테스트
- [ ] 로그 모니터링
- [ ] 성능 확인
- [ ] 롤백 준비

## 🔄 업데이트 절차

### 무중단 업데이트
```bash
# 1. 새 이미지 빌드
docker-compose build

# 2. AuthServer 업데이트
docker-compose up -d --no-deps authserver

# 3. ActorServer 업데이트
docker-compose up -d --no-deps actorserver

# 4. 이전 이미지 정리
docker image prune -f
```

### 롤백
```bash
# 이전 버전 태그로 롤백
docker-compose down
docker-compose up -d --force-recreate
```