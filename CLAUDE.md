# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此仓库中工作时提供指导。

另见：`AGENTS.md` — 包含代码风格、API 返回约定、前端实现约束、构建与热重载约定、安全机制等内容。

## 架构概览

IoTSharp 是一个 **ASP.NET Core (.NET 10) 单体应用**，在单个进程中同时承载 HTTP API、内嵌 MQTT Broker（MQTTnet）、CoAP 服务、Quartz 定时任务调度、CAP 事件总线订阅和 Modbus 采集运行时。

### 基础设施依赖（仅支持以下运行时）

| 服务 | 技术 | 用途 |
|---|---|---|
| PostgreSQL | TimescaleDB `pg17` | 业务数据、Identity、CAP Store、时序数据 |
| RabbitMQ | `rabbitmq:4-management-alpine` | CAP 事件总线消息传输 |
| 缓存 | EasyCaching | InMemory（开发）/ Redis（生产）/ LiteDB |
| 时序库 | TimescaleDB **或** InfluxDB | 历史遥测（`TelemetryData`） |

当前运行时主路径仅支持 PostgreSQL、CAP、RabbitMQ、TimescaleDB/InfluxDB。其他模块（`IoTSharp.EventBus.NServiceBus`、`IoTSharp.Data.JsonDB`）虽然存在但不属于主路径 — 不得扩大其业务依赖。

### 分层边界

代码库按职责边界组织为以下层次：

1. **接入层**（`Controllers/`、`Services/MQTTControllers/`、`Services/CoApResources/`）— 认证、基础校验、路由。不得承载复杂业务编排、Modbus 解析或能耗统计。

2. **采集层**（`Services/ModbusCollection/`）— `ModbusCollectionService`、`CollectionTaskService`、`GatewaySchedulerManager`。负责透传网关场景下的 Modbus 请求生成、MQTT 下发、响应解析和采集日志；同时处理边缘网关 JSON 上传的格式校验与字段映射。

3. **设备与业务模型层**（`IoTSharp.Data/`）— `ApplicationDbContext` 中的核心实体。领域模型区分**通信层实体**（`Device`、`Gateway`、`CollectionPoint`）和**业务层实体**（`Asset`、`AssetRelation`）。一个业务设备（如"水泵"）可能聚合多个数据源（变频器 + 电表 + 压力传感器）。

4. **数据层** — `TelemetryLatest` / `AttributeLatest` 存最新值（PostgreSQL）；`TelemetryData` 存历史时序（TimescaleDB/InfluxDB）；`AuditLog`、`CollectionLog` 用于诊断排查。

5. **控制层** — 命令模板、命令实例及状态机（已创建 → 已发送 → 已确认 → 成功/失败/超时）、审计追踪。控制不只是单次 MQTT 发布，每条命令必须具备可查询的状态和结果。

6. **规则与告警层** — `FlowRule` / `Flow` / `FlowOperation` / `Alarm`，由遥测入库产生的 CAP 事件驱动。规则引擎（`FlowRuleProcessor`）评估条件并可触发 `TaskActions`。

### 核心领域实体

```
租户 -> 客户 -> 设备 / 网关 / 资产
```

- **`Device`**（含 `Gateway` 鉴别器）+ **`DeviceIdentity`** — 通信层设备注册表
- **`Gateway`** — 设备子类型，表示网关（透传或边缘采集）
- **`Asset`** + **`AssetRelation`** — 业务层设备聚合（水泵、主机、阀门、电表、传感器）
- **`CollectionTask`** / **`CollectionDevice`** / **`CollectionPoint`** / **`CollectionLog`** — Modbus 采集配置与执行记录
- **`DeviceTypeProfile`** / **`CollectionRuleTemplate`** / **`ProduceDataMapping`** — 可复用的点位模板与映射规则
- **`TelemetryLatest`** / **`AttributeLatest`** / **`TelemetryData`** — 当前值与历史遥测
- **`FlowRule`** / **`Flow`** / **`FlowOperation`** / **`Alarm`** — 规则链与告警定义
- **`Produce`** — 产品/设备类型定义

### 数据流（透传网关 — 最常见场景）

```
云端调度 → MQTT Broker → 透传网关 → Modbus 设备
                                        ↓
解析服务 ← MQTT Broker ← 透传网关 ← Modbus 响应
   ↓
TelemetryLatest + TelemetryData (TimescaleDB)
   ↓
CAP EventBus → 规则/告警/能耗统计
```

### 数据流（边缘采集网关 — JSON 上传）

```
边缘网关 → HTTP 或 MQTT → JSON 校验 → 字段映射
                                       ↓
                         TelemetryLatest + TelemetryData
                                       ↓
                            业务设备聚合视图
```

### 解决方案模块一览

| 模块 | 定位 | 备注 |
|---|---|---|
| `IoTSharp` | 主应用承载 | API、MQTT、CoAP、Job、规则、采集服务 |
| `IoTSharp.Data` | EF Core 实体、DbContext、迁移 | 核心领域模型 — 暖通概念在此扩展 |
| `IoTSharp.Data.TimeSeries` | 时序存储抽象 | TimescaleDB / InfluxDB 实现 |
| `IoTSharp.Data.PostgreSQL` | PostgreSQL 支持 | 唯一关系数据库运行路径 |
| `IoTSharp.Contracts` | 共享枚举、配置、契约 | 保持聚焦，避免无约束膨胀 |
| `IoTSharp.EventBus` | 事件总线抽象 | |
| `IoTSharp.EventBus.CAP` | CAP + RabbitMQ + PostgreSQL | 唯一事件总线运行路径 |
| `IoTSharp.Interpreter` | 规则脚本引擎 | 规则链使用；核心暖通逻辑不应依赖脚本 |
| `IoTSharp.TaskActions` | 规则链任务动作 | |
| `IoTSharp.Extensions.*` | 扩展工具包 | 仅保留运行时实际依赖的部分 |
| `ClientApp` | 前端应用 | Vue 3 + Element Plus；禁止使用 `fs-crud`/`@fast-crud/fast-crud` |
| `IoTSharp.Test` | 集成/单元测试 | xUnit + AppFixture 模式 |
| `IoTSharp.Data.JsonDB` | 历史遗留 JSON DB | 不在主运行路径 — 不得扩大依赖 |

## 实现约束

- **禁止完全重写** — 本项目采用渐进式二次开发策略。保留现有设备管理、产品、资产、FlowRule、遥测和多租户投资。
- **通信设备与业务设备必须分离建模** — Modbus 从机、IO 模块等通信层对象与水泵、主机、阀门等业务设备必须通过关系关联，不能把所有信息塞进 `Device`。
- **职责不得跨层** — 不允许在 Controller 中写 Modbus 解析逻辑，不允许在接入层做业务设备聚合，不允许在采集层做能耗统计。
- **控制命令必须有生命周期追踪** — 状态机、超时处理、权限校验、审计日志缺一不可。单纯一次 MQTT Publish 不等于控制命令。
- **第一阶段优先级** — 数据与控制闭环（网关注册 → 采集 → 遥测 → 控制 → 基础设备分类）。权限界面、仪表盘、高级分析后续再做。
- **暖通领域聚焦** — 水泵、主机、冷却塔、阀门、风柜、电表、温度/压力/流量传感器、IO 模块。平台以中文暖通项目交付、调试和运维场景为主要语境。
- **透传网关优先使用 MQTT 通信**。边缘采集网关 JSON 上传同时支持 HTTP 和 MQTT。
