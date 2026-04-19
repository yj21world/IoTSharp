#!/bin/bash

set -e

init_fnm() {
    if command -v fnm &> /dev/null; then
        eval "$(fnm env --shell bash)"
    fi
}

init_fnm

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PUBLISH_DIR="${SCRIPT_DIR}/backend/publish"
OUTPUT_DIR="${SCRIPT_DIR}/output"
TAG=${1:-latest}
IMAGE_NAME="iotsharp-backend"
FULL_IMAGE="${IMAGE_NAME}:${TAG}"

echo "=========================================="
echo "  IoTSharp 后端构建脚本"
echo "=========================================="
echo "项目根目录: ${PROJECT_ROOT}"
echo "发布目录:   ${PUBLISH_DIR}"
echo "镜像名称:   ${FULL_IMAGE}"
echo "=========================================="

if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK 未安装"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo "❌ Docker 未安装"
    exit 1
fi

# ===== 1. dotnet publish =====
echo ""
echo "===== [1/3] dotnet publish ====="

rm -rf "${PUBLISH_DIR}"

echo "🔨 发布后端项目..."
dotnet publish "${PROJECT_ROOT}/IoTSharp/IoTSharp.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "${PUBLISH_DIR}"

echo "✅ 发布完成: ${PUBLISH_DIR}"

# ===== 2. docker build =====
echo ""
echo "===== [2/3] docker build ====="

docker build \
    -t "${FULL_IMAGE}" \
    -f "${SCRIPT_DIR}/backend/Dockerfile" \
    "${SCRIPT_DIR}/backend/"

echo "✅ 镜像构建完成: ${FULL_IMAGE}"

# ===== 3. 导出 tar =====
echo ""
echo "===== [3/3] 导出 tar ====="

mkdir -p "${OUTPUT_DIR}"
TAR_FILE="${OUTPUT_DIR}/${IMAGE_NAME}-${TAG}.tar"

echo "💾 导出镜像 -> ${TAR_FILE}"
docker save -o "${TAR_FILE}" "${FULL_IMAGE}"

IMAGE_SIZE=$(du -h "${TAR_FILE}" | cut -f1)
echo "✅ 导出完成 (${IMAGE_SIZE})"

echo ""
echo "=========================================="
echo "  ✅ 后端构建完成！"
echo "=========================================="