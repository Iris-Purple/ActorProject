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