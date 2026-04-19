#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=========================================="
echo "  IoTSharp 镜像加载脚本"
echo "=========================================="

TAR_FILES=$(find "${SCRIPT_DIR}" -maxdepth 1 -name "*.tar" -type f)

if [ -z "${TAR_FILES}" ]; then
    echo "❌ 未找到 tar 文件"
    exit 1
fi

echo "找到以下镜像文件:"
echo "${TAR_FILES}"
echo ""

for tar_file in ${TAR_FILES}; do
    FILENAME=$(basename "${tar_file}")
    echo "📥 加载 ${FILENAME} ..."
    docker load -i "${tar_file}"
    echo "✅ ${FILENAME} 加载完成"
    echo ""
done

echo "=========================================="
echo "  ✅ 所有镜像加载完成！"
echo "=========================================="
echo ""
echo "当前 Docker 镜像列表:"
docker images | grep -E "iotsharp|timescale|redis|rabbitmq"
echo ""
echo "执行 bash start.sh 启动服务"