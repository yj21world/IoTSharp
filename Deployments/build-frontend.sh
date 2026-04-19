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
CLIENT_DIR="${PROJECT_ROOT}/ClientApp"
DIST_DIR="${SCRIPT_DIR}/frontend/dist"
OUTPUT_DIR="${SCRIPT_DIR}/output"
TAG=${1:-latest}
IMAGE_NAME="iotsharp-frontend"
FULL_IMAGE="${IMAGE_NAME}:${TAG}"

echo "=========================================="
echo "  IoTSharp 前端构建脚本"
echo "=========================================="
echo "项目根目录: ${PROJECT_ROOT}"
echo "前端目录:   ${CLIENT_DIR}"
echo "镜像名称:   ${FULL_IMAGE}"
echo "=========================================="

if ! command -v node &> /dev/null; then
    echo "❌ Node.js 未安装 (fnm 初始化后仍找不到 node)"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo "❌ Docker 未安装"
    exit 1
fi

echo "Node: $(node --version)  npm: $(npm --version)"

# ===== 1. npm run build =====
echo ""
echo "===== [1/3] npm run build ====="

cd "${CLIENT_DIR}"

if [ ! -d "node_modules" ]; then
    echo "📦 安装依赖..."
    # npm install
fi

echo "🔨 构建前端项目..."
# npm run build

# rm -rf "${DIST_DIR}"
# mkdir -p "${DIST_DIR}"
# cp -r "${CLIENT_DIR}/dist/"* "${DIST_DIR}/"

echo "✅ 前端构建完成: ${DIST_DIR}"

# ===== 2. docker build =====
echo ""
echo "===== [2/3] docker build ====="

cd "${PROJECT_ROOT}"

docker build \
    -t "${FULL_IMAGE}" \
    -f "${SCRIPT_DIR}/frontend/Dockerfile" \
    "${SCRIPT_DIR}/frontend/"

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
echo "  ✅ 前端构建完成！"
echo "=========================================="