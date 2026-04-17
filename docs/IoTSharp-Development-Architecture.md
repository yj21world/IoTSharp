# IoTSharp 暖通空调节能系统 - 开发架构决策文档

> **版本**: 1.0  
> **日期**: 2026年4月14日  
> **项目**: 暖通空调 (HVAC) 节能管理系统  
> **技术栈**: .NET 10.0 + Vue 3 + IoTSharp 平台

---

## 一、架构决策总览

### 1.1 核心架构选型

| 层级 | 技术选型 | 版本 | 用途 |
|------|---------|------|------|
| **消息队列** | RabbitMQ / Kafka | 3.13+ / 3.6+ | 设备数据缓冲、异步解耦 |
| **事件总线** | CAP | 8.2+ | 分布式事件发布/订阅 |
| **事件存储** | PostgreSQL | 16+ | CAP 消息持久化 |
| **业务数据库** | PostgreSQL | 16+ | 设备、用户、规则、告警等业务数据 |
| **时序数据库** | TimescaleDB | 2.15+ (PG 扩展) | 遥测数据、能耗数据、状态历史 |
| **缓存** | Redis / InMemory | 7+ / - | 规则缓存、会话、看板数据 |
| **协议接入** | MQTT + HTTP + CoAP | - | 设备通信 |

### 1.2 架构决策理由

| 决策 | 理由 | 替代方案 | 不选原因 |
|------|------|---------|---------|
| **RabbitMQ** | 路由灵活、延迟低、运维简单、适合中小型项目 | Kafka | Kafka 运维复杂，HVAC 场景无需超高吞吐 |
| **CAP** | 可靠消息投递、内置 Dashboard、Outbox 模式、生态完善 | Shashlik | Shashlik 功能有限、社区较小 |
| **PostgreSQL** | 标准 SQL、事务完整、生态成熟、TimescaleDB 基础 | MySQL | TimescaleDB 仅支持 PG |
| **TimescaleDB** | 对应用透明、自动分片、高压缩率、与业务库共用实例 | InfluxDB | InfluxQL 学习成本高、JOIN 不支持 |
| **Redis** | 高性能、数据结构丰富、广泛使用 | Memcached | 功能单一、不支持持久化 |

---

## 二、环境配置

### 2.1 开发环境 (Development)

**文件**: `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  
  "ConnectionStrings": {
    "IoTSharp": "Server=localhost;Database=IoTSharp_Dev;Username=postgres;Password=dev_password;Pooling=true;MaxPoolSize=128;",
    "TelemetryStorage": "Server=localhost;Database=IoTSharp_Dev;Username=postgres;Password=dev_password;Pooling=true;MaxPoolSize=256;",
    "EventBusStore": "Server=localhost;Database=IoTSharp_Dev;Username=postgres;Password=dev_password;Pooling=true;MaxPoolSize=64;",
    "EventBusMQ": "",
    "BlobStorage": "disk://path=./storage"
  },
  
  "TelemetryStorage": "TimescaleDB",
  "EventBus": "CAP",
  "EventBusStore": "PostgreSql",
  "EventBusMQ": "InMemory",
  "CachingUseIn": "InMemory",
  
  "SucceedMessageExpiredAfter": 3600,
  "ConsumerThreadCount": 5,
  "DbContextPoolSize": 128,
  "RuleCachingExpiration": 60,
  
  "JwtKey": "dev-secret-key-must-be-changed-in-production-32bytes",
  "JwtExpireHours": 24,
  "JwtIssuer": "IoTSharp-Dev",
  "JwtAudience": "IoTSharp-Dev",
  
  "MqttBroker": {
    "Port": 1883,
    "TlsPort": 8883,
    "EnableTls": false,
    "DomainName": "http://localhost/"
  },
  
  "ShardingByDateMode": "PerDay",
  "ShardingBeginTime": "2026-01-01"
}
```

**开发环境特点**:
- ✅ 使用 InMemory MQ，无需启动 RabbitMQ
- ✅ 业务库与时序库共用同一 PostgreSQL 实例
- ✅ 启用 TimescaleDB 超表，但按天分片便于测试
- ✅ 缓存使用 InMemory，简化开发

### 2.2 测试环境 (Staging)

**文件**: `appsettings.Staging.json`

```json
{
  "ConnectionStrings": {
    "IoTSharp": "Server=staging-pg;Database=hvac_staging;Username=hvac;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=256;",
    "TelemetryStorage": "Server=staging-pg;Database=hvac_staging;Username=hvac;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=512;",
    "EventBusStore": "Server=staging-pg;Database=hvac_eventbus_staging;Username=hvac;Password=${DB_PASSWORD};",
    "EventBusMQ": "amqp://hvac:${RABBITMQ_PASSWORD}@staging-rabbitmq:5672/hvac"
  },
  
  "TelemetryStorage": "TimescaleDB",
  "EventBus": "CAP",
  "EventBusStore": "PostgreSql",
  "EventBusMQ": "RabbitMQ",
  "CachingUseIn": "Redis",
  
  "SucceedMessageExpiredAfter": 3600,
  "ConsumerThreadCount": 10,
  "DbContextPoolSize": 256,
  
  "MqttBroker": {
    "Port": 1883,
    "TlsPort": 8883,
    "EnableTls": true,
    "DomainName": "mqtt.staging.hvac-system.com"
  }
}
```

### 2.3 生产环境 (Production)

**文件**: `appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  
  "ConnectionStrings": {
    "IoTSharp": "Server=pg-primary;Database=hvac_production;Username=hvac;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=512;",
    "TelemetryStorage": "Server=pg-primary;Database=hvac_timeseries;Username=hvac;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=1024;",
    "EventBusStore": "Server=pg-primary;Database=hvac_eventbus;Username=hvac;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=128;",
    "EventBusMQ": "amqp://hvac:${RABBITMQ_PASSWORD}@rabbitmq-cluster:5672/hvac"
  },
  
  "TelemetryStorage": "TimescaleDB",
  "EventBus": "CAP",
  "EventBusStore": "PostgreSql",
  "EventBusMQ": "RabbitMQ",
  "CachingUseIn": "Redis",
  
  "SucceedMessageExpiredAfter": 3600,
  "ConsumerThreadCount": 20,
  "DbContextPoolSize": 512,
  "RuleCachingExpiration": 60,
  
  "JwtKey": "${JWT_SECRET}",
  "JwtExpireHours": 12,
  "JwtIssuer": "HVAC-IoTSharp",
  "JwtAudience": "HVAC-System",
  
  "MqttBroker": {
    "Port": 1883,
    "TlsPort": 8883,
    "EnableTls": true,
    "DomainName": "mqtt.hvac-system.com"
  }
}
```

---

## 三、数据库架构设计

### 3.1 PostgreSQL 实例规划

| 环境 | 实例数 | 数据库 | 用途 |
|------|--------|--------|------|
| **开发** | 1 | `IoTSharp_Dev` | 业务数据 + 时序数据 + 事件存储 |
| **测试** | 1 | `hvac_staging` + `hvac_eventbus_staging` | 业务 + 时序共用，事件独立 |
| **生产** | 2 (主从) | `hvac_production` (业务)<br>`hvac_timeseries` (时序)<br>`hvac_eventbus` (事件) | 分离部署，性能隔离 |

### 3.2 TimescaleDB 超表配置

**初始化脚本**: `init-timescaledb.sql`

```sql
-- ==========================================
-- TimescaleDB 超表初始化
-- ==========================================

-- 1. 启用 TimescaleDB 扩展
CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

-- 2. 将 TelemetryData 转换为超表
SELECT create_hypertable(
    'TelemetryData',
    'DateTime',
    chunk_time_interval => INTERVAL '1 day',
    create_default_indexes => TRUE,
    if_not_exists => TRUE
);

-- 3. 启用压缩
ALTER TABLE "TelemetryData" SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'DeviceId,KeyName',
    timescaledb.compress_orderby = 'DateTime'
);

-- 4. 自动压缩策略 (7天后压缩)
SELECT add_compression_policy('TelemetryData', INTERVAL '7 days');

-- 5. 数据保留策略 (保留 365 天)
SELECT add_retention_policy('TelemetryData', INTERVAL '365 days');

-- 6. 创建连续聚合视图 (小时级)
CREATE MATERIALIZED VIEW IF NOT EXISTS telemetry_hourly
WITH (timescaledb.continuous) AS
SELECT 
    time_bucket('1 hour', "DateTime") AS bucket,
    "DeviceId",
    "KeyName",
    avg("Value_Double") AS avg_value,
    max("Value_Double") AS max_value,
    min("Value_Double") AS min_value,
    count(*) AS sample_count
FROM "TelemetryData"
GROUP BY bucket, "DeviceId", "KeyName"
WITH NO DATA;

-- 7. 连续聚合刷新策略
SELECT add_continuous_aggregate_policy('telemetry_hourly',
    start_offset => INTERVAL '30 days',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour'
);

-- 8. 创建日级聚合视图
CREATE MATERIALIZED VIEW IF NOT EXISTS telemetry_daily
WITH (timescaledb.continuous) AS
SELECT 
    time_bucket('1 day', "DateTime") AS bucket,
    "DeviceId",
    "KeyName",
    avg("Value_Double") AS avg_value,
    max("Value_Double") AS max_value,
    min("Value_Double") AS min_value,
    count(*) AS sample_count
FROM "TelemetryData"
GROUP BY bucket, "DeviceId", "KeyName"
WITH NO DATA;

SELECT add_continuous_aggregate_policy('telemetry_daily',
    start_offset => NULL,
    end_offset => INTERVAL '1 day',
    schedule_interval => INTERVAL '1 day'
);
```

### 3.3 数据库表空间规划

| 数据库 | Schema | 主要表 | 存储策略 |
|--------|--------|--------|---------|
| **hvac_production** | public | Tenant, Customer, Device, Alarm, FlowRule | 常规表空间 |
| **hvac_timeseries** | public | TelemetryData (超表), AttributeLatest | 高速 SSD，自动分片 |
| **hvac_eventbus** | cap | published, received, locking | 常规表空间，自动清理 |

---

## 四、消息队列架构

### 4.1 RabbitMQ 配置

**适用场景**: 中小型项目 (≤500 台设备)

**队列规划**:

```
Exchange: hvac.exchange (Topic 类型)
  │
  ├─ Routing Key: device.telemetry.#
  │   → Queue: hvac.telemetry.queue
  │   → 消费者: 数据存储、规则引擎、实时推送
  │   → 死信队列: hvac.telemetry.dlx
  │
  ├─ Routing Key: device.status.#
  │   → Queue: hvac.status.queue
  │   → 消费者: 状态更新、在线追踪
  │   → 死信队列: hvac.status.dlx
  │
  ├─ Routing Key: device.alarm.#
  │   → Queue: hvac.alarm.queue
  │   → 消费者: 告警处理、通知推送
  │   → 死信队列: hvac.alarm.dlx
  │
  └─ Routing Key: device.control.#
      → Queue: hvac.control.queue
      → 消费者: 控制指令下发、结果反馈
      → 死信队列: hvac.control.dlx
```

**RabbitMQ 管理命令**:

```bash
# 创建虚拟主机
rabbitmqctl add_vhost hvac

# 创建用户
rabbitmqctl add_user hvac <password>
rabbitmqctl set_permissions -p hvac hvac ".*" ".*" ".*"
rabbitmqctl set_user_tags hvac administrator

# 查看队列状态
rabbitmqctl list_queues -p hvac name messages consumers
```

### 4.2 Kafka 配置 (大型项目备用)

**适用场景**: 大型项目 (>500 台设备) 或需要数据回溯

**Topic 规划**:

```
Topic: hvac.telemetry
  Partitions: 12
  Replication Factor: 3
  Retention: 7 days
  
Topic: hvac.alarm
  Partitions: 6
  Replication Factor: 3
  Retention: 30 days
  
Topic: hvac.control
  Partitions: 6
  Replication Factor: 3
  Retention: 7 days
```

**切换方式**: 仅需修改配置文件，业务代码无需改动

```json
{
  "EventBusMQ": "Kafka",
  "ConnectionStrings": {
    "EventBusMQ": "kafka1:9092,kafka2:9092,kafka3:9092"
  }
}
```

---

## 五、CAP 事件总线配置

### 5.1 Topic 命名规范

```
iotsharp.hvac.telemetry          → 遥测数据存储
iotsharp.hvac.status             → 设备状态变更
iotsharp.hvac.alarm              → 告警事件
iotsharp.hvac.control            → 控制指令
iotsharp.hvac.energy             → 能耗数据
iotsharp.hvac.schedule           → 计划任务
iotsharp.hvac.device.created     → 设备创建
iotsharp.hvac.device.deleted     → 设备删除
```

### 5.2 订阅者设计

```csharp
public class HvacCapSubscriber : ICapSubscribe
{
    [CapSubscribe("iotsharp.hvac.telemetry")]
    public async Task TelemetryData(PlayloadData msg)
    {
        // 存储遥测数据到 TimescaleDB
        await StoreTelemetryData(msg);
    }
    
    [CapSubscribe("iotsharp.hvac.alarm")]
    public async Task AlarmEvent(CreateAlarmDto alarmDto)
    {
        // 处理告警
        await HandleAlarm(alarmDto);
    }
    
    [CapSubscribe("iotsharp.hvac.status")]
    public async Task StatusChange(DeviceConnectStatus status)
    {
        // 更新设备状态
        await UpdateDeviceStatus(status);
    }
}
```

### 5.3 消息可靠性保障

| 机制 | 配置 | 说明 |
|------|------|------|
| **Outbox 模式** | 自动启用 | 消息与业务数据同事务 |
| **自动重试** | 默认 3 次 | 失败消息自动重试 |
| **消息过期** | `SucceedMessageExpiredAfter: 3600` | 成功消息 1 小时后清理 |
| **消费者线程** | `ConsumerThreadCount: 10` | 并发消费 |
| **Dashboard** | `x.UseDashboard()` | 可视化监控 |

---

## 六、开发规范

### 6.1 代码组织

```
IoTSharp/
├── Services/                    # 业务服务
│   ├── MQTTService.cs           # MQTT 连接管理
│   └── MQTTControllers/         # MQTT 路由控制器
│       ├── TelemetryController.cs
│       ├── AttributesController.cs
│       └── AlarmController.cs
├── Controllers/                 # HTTP 控制器
│   ├── DevicesController.cs
│   ├── AlarmController.cs
│   └── RulesController.cs
├── Jobs/                        # 定时任务
│   ├── CheckDevices.cs          # 设备在线检查
│   └── CachingJob.cs            # 缓存刷新
├── FlowRuleEngine/              # 规则引擎
│   └── FlowRuleProcessor.cs
└── EventBus.CAP/                # CAP 集成
    ├── CapPublisher.cs          # 事件发布
    └── CapSubscriber.cs         # 事件订阅
```

### 6.2 设备数据上报流程

```
设备 → MQTT/HTTP/CoAP
  ↓
协议解析 → 构建 PlayloadData
  ↓
IPublisher.PublishTelemetryData()
  ↓
CAP 发布事件 (iotsharp.hvac.telemetry)
  ↓
CAP 存储到 EventStore (PostgreSQL)
  ↓
CAP 投递到 RabbitMQ
  ↓
CapSubscriber 消费
  ↓
IStorage.StoreTelemetryAsync() → TimescaleDB
  ↓
FlowRuleProcessor.RunRules() → 规则引擎
```

### 6.3 数据库访问规范

**业务数据**: 使用 `ApplicationDbContext`

```csharp
using var scope = _scopeFactor.CreateScope();
using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

var device = await dbContext.Device.FindAsync(deviceId);
```

**时序数据**: 使用 `IStorage` 接口

```csharp
var storage = _scope.ServiceProvider.GetRequiredService<IStorage>();
var result = await storage.StoreTelemetryAsync(msg);
```

**规则引擎**: 使用 `FlowRuleProcessor`

```csharp
var processor = _scope.ServiceProvider.GetRequiredService<FlowRuleProcessor>();
await processor.RunRules(deviceId, data, EventType.Telemetry);
```

---

## 七、部署架构

### 7.1 Docker Compose (开发/测试)

**文件**: `docker-compose.dev.yml`

```yaml
version: '3.8'

services:
  # PostgreSQL + TimescaleDB
  postgres:
    image: timescale/timescaledb:latest-pg16
    environment:
      POSTGRES_USER: hvac
      POSTGRES_PASSWORD: ${DB_PASSWORD:-dev_password}
      POSTGRES_DB: IoTSharp_Dev
    volumes:
      - pg_data:/var/lib/postgresql/data
      - ./init-timescaledb.sql:/docker-entrypoint-initdb.d/01-init.sql
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U hvac"]
      interval: 10s
      timeout: 5s
      retries: 5

  # RabbitMQ
  rabbitmq:
    image: rabbitmq:3-management
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:-hvac}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-dev_password}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmqctl", "status"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Redis
  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${REDIS_PASSWORD:-dev_password}
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

  # IoTSharp 应用
  iotsharp:
    build:
      context: .
      dockerfile: IoTSharp/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__IoTSharp=Server=postgres;Database=IoTSharp_Dev;Username=hvac;Password=${DB_PASSWORD:-dev_password}
      - ConnectionStrings__TelemetryStorage=Server=postgres;Database=IoTSharp_Dev;Username=hvac;Password=${DB_PASSWORD:-dev_password}
    ports:
      - "80:80"
      - "443:443"
      - "1883:1883"
      - "8883:8883"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    volumes:
      - app_storage:/app/storage

volumes:
  pg_data:
  rabbitmq_data:
  redis_data:
  app_storage:
```

### 7.2 生产环境部署建议

| 组件 | 部署方式 | 高可用 | 备份策略 |
|------|---------|--------|---------|
| **PostgreSQL** | 主从复制 | Patroni + etcd | 每日全量 + WAL 归档 |
| **RabbitMQ** | 3 节点集群 | 镜像队列 | 配置导出 + 消息持久化 |
| **Redis** | Sentinel 或 Cluster | 自动故障转移 | RDB + AOF |
| **IoTSharp** | 2+ 实例 | 负载均衡 | 配置管理 + 日志集中 |

---

## 八、监控与运维

### 8.1 监控端点

| 端点 | 用途 | 访问方式 |
|------|------|---------|
| `/healthz` | 健康检查 | `curl http://localhost:5000/healthz` |
| `/healthchecks-ui` | 健康检查 UI | 浏览器访问 |
| `/cap` | CAP Dashboard | 浏览器访问 |
| `http://localhost:15672` | RabbitMQ 管理 | 浏览器访问 |
| `/swagger` | API 文档 | 浏览器访问 |

### 8.2 关键指标监控

| 指标 | 阈值 | 告警方式 |
|------|------|---------|
| **设备在线数** | < 90% | 邮件/短信 |
| **消息堆积数** | > 10,000 | 邮件 |
| **数据库连接数** | > 80% | 邮件 |
| **磁盘使用率** | > 85% | 邮件/短信 |
| **规则执行失败率** | > 5% | 邮件 |

### 8.3 日志规范

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "IoTSharp.EventBus": "Debug",
      "IoTSharp.FlowRuleEngine": "Debug",
      "Microsoft.EntityFrameworkCore": "Warning"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "SingleLine": true,
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff "
      }
    }
  }
}
```

---

## 九、扩展性规划

### 9.1 容量规划

| 指标 | 当前容量 | 扩展方式 |
|------|---------|---------|
| **设备数** | 500 台 | 升级 RabbitMQ → Kafka |
| **数据点/秒** | 5,000 | 增加 CAP 消费者线程 |
| **存储容量** | 1TB/年 | TimescaleDB 压缩 + 数据分层 |
| **并发用户** | 100 | 增加 IoTSharp 实例 |

### 9.2 未来升级路径

```
阶段 1 (当前): 单实例 PostgreSQL + RabbitMQ
  ↓
阶段 2 (设备 >500): PostgreSQL 主从 + RabbitMQ 集群
  ↓
阶段 3 (设备 >2000): 分布式数据库 + Kafka 集群
  ↓
阶段 4 (多区域): 多活部署 + 边缘计算网关
```

---

## 十、检查清单

### 10.1 开发环境检查

- [ ] PostgreSQL 16+ 已安装并运行
- [ ] TimescaleDB 扩展已启用
- [ ] 初始化脚本已执行 (`init-timescaledb.sql`)
- [ ] RabbitMQ 已启动 (开发环境可选)
- [ ] `appsettings.Development.json` 配置正确
- [ ] 数据库迁移已应用 (`dotnet ef database update`)
- [ ] MQTT 端口 1883 可访问
- [ ] Health Check 端点正常 (`http://localhost:5000/healthz`)

### 10.2 生产环境检查

- [ ] PostgreSQL 主从配置完成
- [ ] TimescaleDB 超表已创建
- [ ] 压缩和保留策略已启用
- [ ] RabbitMQ 集群已部署
- [ ] CAP Dashboard 可访问
- [ ] TLS 证书已配置
- [ ] 备份策略已实施
- [ ] 监控告警已配置

---

## 十一、附录

### 11.1 关键配置文件

| 文件 | 用途 |
|------|------|
| `appsettings.Development.json` | 开发环境配置 |
| `appsettings.Staging.json` | 测试环境配置 |
| `appsettings.Production.json` | 生产环境配置 |
| `init-timescaledb.sql` | TimescaleDB 初始化 |
| `docker-compose.dev.yml` | 开发环境 Docker 配置 |
| `docker-compose.yml` | 生产环境 Docker 配置 |

### 11.2 常用命令

```bash
# 启动开发环境
docker-compose -f docker-compose.dev.yml up -d

# 查看日志
docker-compose logs -f iotsharp

# 数据库迁移
dotnet ef database update

# 查看 RabbitMQ 队列
rabbitmqctl list_queues -p hvac

# 查看 TimescaleDB 超表
psql -U hvac -d IoTSharp_Dev -c "SELECT * FROM timescaledb_information.hypertables;"

# 查看 Chunk
psql -U hvac -d IoTSharp_Dev -c "SELECT * FROM timescaledb_information.chunks;"
```

---

## 十二、版本历史

| 版本 | 日期 | 修改内容 | 修改人 |
|------|------|---------|--------|
| 1.0 | 2026-04-14 | 初始版本，确定架构选型 | - |

---

> **审批**:  
> 架构师: _______________  日期: _________  
> 技术负责人: _______________  日期: _________  
> 项目经理: _______________  日期: _________
