# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 프로젝트 파일 복사 및 복원
COPY ["AuthServer/AuthServer.csproj", "AuthServer/"]
COPY ["Common/Common.csproj", "Common/"]
RUN dotnet restore "AuthServer/AuthServer.csproj"

# 전체 소스 복사 및 빌드
COPY . .
WORKDIR "/src/AuthServer"
RUN dotnet build "AuthServer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "AuthServer.csproj" -c Release -o /app/publish

# Runtime stage  
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# curl 설치 (health check용)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# HTTP 포트만 노출
EXPOSE 5006

# 발행된 파일 복사
COPY --from=publish /app/publish .

# 데이터베이스 디렉토리 생성
RUN mkdir -p /app/Database

# 환경변수 설정
ENV ASPNETCORE_URLS="http://+:5006"
ENV ASPNETCORE_ENVIRONMENT=Production

# 실행
ENTRYPOINT ["dotnet", "AuthServer.dll"]