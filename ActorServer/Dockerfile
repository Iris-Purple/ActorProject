# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 프로젝트 파일 복사 및 복원
COPY ["ActorServer/ActorServer.csproj", "ActorServer/"]
COPY ["Common/Common.csproj", "Common/"]
RUN dotnet restore "ActorServer/ActorServer.csproj"

# 전체 소스 복사 및 빌드
COPY . .
WORKDIR "/src/ActorServer"
RUN dotnet build "ActorServer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ActorServer.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

# 필요한 포트 노출
EXPOSE 9999

# 발행된 파일 복사
COPY --from=publish /app/publish .

# 데이터베이스 디렉토리 생성
RUN mkdir -p /app/Database

# 실행
ENTRYPOINT ["dotnet", "ActorServer.dll"]