#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=========================================="
echo "  IoTSharp 服务启动脚本"
echo "=========================================="

if ! command -v docker &> /dev/null; then
    echo "❌ Docker 未安装"
    exit 1
fi

if ! docker compose version &> /dev/null; then
    echo "❌ Docker Compose V2 未安装"
    exit 1
fi

cd "${SCRIPT_DIR}"

# 检查业务镜像是否存在
BUSINESS_IMAGES=(
    "iotsharp-backend:latest"
    "iotsharp-frontend:latest"
)

MISSING=0
for img in "${BUSINESS_IMAGES[@]}"; do
    if ! docker image inspect "${img}" > /dev/null 2>&1; then
        echo "⚠️  缺少业务镜像: ${img}"
        MISSING=1
    fi
done

if [ "${MISSING}" -eq 1 ]; then
    echo ""
    echo "❌ 存在缺失业务镜像，请先执行 bash load-images.sh"
    exit 1
fi

# 拉取第三方公共镜像
THIRD_PARTY_IMAGES=(
    "timescale/timescaledb-ha:pg17"
    "redis:7-alpine"
    "rabbitmq:4-management-alpine"
)

echo ""
echo "📥 检查第三方镜像..."
for img in "${THIRD_PARTY_IMAGES[@]}"; do
    if docker image inspect "${img}" > /dev/null 2>&1; then
        echo "✅ ${img} 已存在"
    else
        echo "📥 拉取 ${img} ..."
        docker pull "${img}"
    fi
done

# 检查配置文件
if [ ! -f "${SCRIPT_DIR}/backend/appsettings.Production.json" ]; then
    echo "❌ 缺少配置文件: backend/appsettings.Production.json"
    exit 1
fi

# 检查 .env 文件
if [ ! -f "${SCRIPT_DIR}/.env" ]; then
    echo "❌ 缺少 .env 文件"
    echo "   请先执行: cp .env.example .env && 编辑 .env 填入真实密码"
    exit 1
fi

echo ""
echo "🚀 启动服务..."
docker compose up -d

echo ""
echo "等待服务就绪..."
sleep 5

echo ""
echo "=========================================="
echo "  ✅ 服务已启动！"
echo "=========================================="
echo ""
echo "服务状态:"
docker compose ps

echo ""
echo "访问地址:"
echo "  前端:        http://localhost"
echo "  后端 API:    http://localhost:8080"
echo "  MQTT:        localhost:1883"
echo "  RabbitMQ:    http://localhost:15672 (iotsharp / IoTSharp_RabbitMQ_2025)"
echo ""
echo "常用命令:"
echo "  查看日志:    docker compose logs -f"
echo "  停止服务:    docker compose down"
echo "  重启服务:    docker compose restart"