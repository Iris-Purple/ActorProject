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