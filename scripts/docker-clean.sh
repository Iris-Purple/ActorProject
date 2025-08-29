#!/bin/bash
# Docker 정리 스크립트

echo "🛑 Stopping containers..."
docker-compose down

echo "🗑️ Removing volumes..."
docker-compose down -v

echo "🧹 Pruning unused images..."
docker image prune -f

echo "✅ Cleanup complete!"