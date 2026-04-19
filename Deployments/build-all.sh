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
OUTPUT_DIR="${SCRIPT_DIR}/output"
TAG=${1:-latest}

echo "=========================================="
echo "  IoTSharp 全量构建脚本"
echo "=========================================="
echo "项目根目录: ${PROJECT_ROOT}"
echo "输出目录:   ${OUTPUT_DIR}"
echo "镜像标签:   ${TAG}"
echo "=========================================="

if ! command -v docker &> /dev/null; then
    echo "❌ Docker 未安装"
    exit 1
fi

mkdir -p "${OUTPUT_DIR}"

# ===== 1. 构建后端 =====
echo ""
echo "===== [1/2] 构建后端 ====="
bash "${SCRIPT_DIR}/build-backend.sh" "${TAG}"

# ===== 2. 构建前端 =====
echo ""
echo "===== [2/2] 构建前端 ====="
bash "${SCRIPT_DIR}/build-frontend.sh" "${TAG}"

# ===== 3. 收集部署文件 =====
echo ""
echo "===== [3/4] 收集部署文件 ====="

mkdir -p "${OUTPUT_DIR}/backend"

cp "${SCRIPT_DIR}/docker-compose.yml" "${OUTPUT_DIR}/"
cp "${SCRIPT_DIR}/.env.example" "${OUTPUT_DIR}/"
cp "${SCRIPT_DIR}/backend/appsettings.Production.json" "${OUTPUT_DIR}/backend/"
cp "${SCRIPT_DIR}/load-images.sh" "${OUTPUT_DIR}/"
cp "${SCRIPT_DIR}/start.sh" "${OUTPUT_DIR}/"

chmod +x "${OUTPUT_DIR}/load-images.sh"
chmod +x "${OUTPUT_DIR}/start.sh"

echo "✅ 部署文件已收集到 ${OUTPUT_DIR}/"

# ===== 4. 汇总 =====
echo ""
echo "=========================================="
echo "  ✅ 全量构建完成！"
echo "=========================================="
echo ""
echo "output/ 目录结构:"
cd "${OUTPUT_DIR}" && find . -maxdepth 2 -type f | sort
echo ""
echo "部署步骤:"
echo "  1. 将 output/ 目录上传到服务器"
echo "  2. cd output/ && cp .env.example .env && 编辑 .env 填入真实密码"
echo "  3. bash load-images.sh"
echo "  4. bash start.sh (自动拉取第三方镜像)"
echo ""