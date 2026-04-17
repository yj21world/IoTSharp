# IoTSharp 工程架构说明文档

> 版本：3.5.0 | 目标框架：.NET 10.0 | 许可证：Apache-2.0  
> 作者：Maikebing (Yanhong Ma) | 项目地址：https://github.com/IoTSharp/IoTSharp

---

## 一、项目概述

IoTSharp 是一个**开源物联网 (IoT) 平台**，提供设备管理、数据采集、数据处理与可视化能力。平台采用 **ASP.NET Core** 后端 + **Vue 3** 前端的架构，内置 MQTT Broker、CoAP 服务、规则引擎、脚本引擎、事件总线等核心组件，支持多数据库和多时序数据库存储方案。

### 核心能力

| 能力 | 说明 |
|------|------|
| 设备管理 | 设备/网关的增删改查、身份认证（AccessToken/密码/X509证书）、连接状态追踪 |
| 数据采集 | 通过 MQTT / HTTP / CoAP 协议接收遥测数据和属性数据 |
| 数据存储 | 关系数据库存储元数据，时序数据库存储遥测数据，支持多种存储引擎 |
| 规则引擎 | 基于流程图的规则编排，支持多种脚本语言和任务执行器 |
| 告警管理 | 告警产生、传播、确认、清除的完整生命周期 |
| 事件总线 | 基于 CAP / Shashlik 的分布式事件驱动架构 |
| MCP 工具 | 集成 Model Context Protocol，支持 AI Agent 访问设备数据 |
| 网关协议 | 支持 Modbus、OPC UA 等工业网关协议的数据映射 |

---

## 二、解决方案结构

```
IoTSharp.sln
│
├── IoTSharp/                          # 主 Web 应用 (ASP.NET Core)
├── IoTSharp.Contracts/                # 契约/DTO/枚举/常量定义
├── IoTSharp.Data/                     # 核心数据模型 & EF Core 上下文
├── IoTSharp.Data.TimeSeries/          # 时序数据存储抽象层 & 实现
├── IoTSharp.Data.Storage/             # 各数据库 EF Core 提供者
│   ├── IoTSharp.Data.PostgreSQL/
│   ├── IoTSharp.Data.MySQL/
│   ├── IoTSharp.Data.SqlServer/
│   ├── IoTSharp.Data.Oracle/
│   ├── IoTSharp.Data.Sqlite/
│   ├── IoTSharp.Data.InMemory/
│   ├── IoTSharp.Data.Cassandra/
│   └── IoTSharp.Data.ClickHouse/
├── IoTSharp.EventBus/                 # 事件总线抽象层
├── IoTSharp.EventBus.CAP/             # CAP 事件总线实现
├── IoTSharp.EventBus.Shashlik/        # Shashlik 事件总线实现
├── IoTSharp.EventBus.NServiceBus/     # NServiceBus 事件总线实现
├── IoTSharp.Interpreter/              # 多语言脚本引擎
├── IoTSharp.TaskActions/              # 任务执行器框架
├── IoTSharp.Extensions/               # 通用扩展方法
├── IoTSharp.Extensions.AspNetCore/    # ASP.NET Core 扩展
├── IoTSharp.Extensions.EFCore/        # EF Core 扩展
├── IoTSharp.Extensions.X509/          # X509 证书扩展
├── IoTSharp.Extensions.BouncyCastle/  # BouncyCastle 加密扩展
├── IoTSharp.Extensions.RESTful/       # RESTful API 扩展
├── IoTSharp.Extensions.DependencyInjection/ # DI 扩展
├── IoTSharp.Extensions.QuartzJobScheduler/ # Quartz 定时任务扩展
├── IoTSharp.SDKs/                     # 客户端 SDK
│   ├── IoTSharp.Sdk.CSharp/           # HTTP API SDK (C#)
│   ├── IoTSharp.Sdk.MQTT/             # MQTT SDK (C#)
│   ├── IoTSharp.Sdk.TS/               # TypeScript SDK
│   └── MQTTDemo/                      # MQTT 示例程序
├── IoTSharp.Agent/                    # Avalonia 桌面客户端代理
├── IoTSharp.EasyUse/                  # WinForms 轻量客户端
├── IoTSharp.Test/                     # 单元测试
├── ClientApp/                         # Vue 3 前端 SPA
└── Deployments/                       # Docker 部署配置
    ├── rabbit_mongo_influx/            # RabbitMQ + MongoDB + InfluxDB 方案
    ├── zeromq_sharding/               # ZeroMQ + 分表方案
    └── zeromq_taos/                   # ZeroMQ + TDengine 方案
```

---

## 三、架构分层

### 3.1 总体架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端 (Vue 3 + Vite)                       │
│  Element Plus | ECharts | AntV X6 | Monaco Editor | Fast-CRUD   │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTP/HTTPS (REST API + Swagger)
┌──────────────────────────▼──────────────────────────────────────┐
│                    ASP.NET Core Web 层                           │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌───────────────┐  │
│  │Controllers│  │ MCP Tools │  │  Gateway │  │   Services    │  │
│  │ (22个)    │  │ (AI接入)  │  │  网关    │  │ MQTT/CoAP     │  │
│  └────┬─────┘  └─────┬─────┘  └────┬─────┘  └──────┬────────┘  │
│       │              │              │                │           │
│  ┌────▼──────────────▼──────────────▼────────────────▼────────┐ │
│  │              FlowRuleProcessor (规则引擎)                    │ │
│  └────────────────────────┬───────────────────────────────────┘ │
│                           │                                      │
│  ┌────────────────────────▼───────────────────────────────────┐ │
│  │              EventBus (事件总线 - IPublisher/ISubscriber)    │ │
│  │     CAP 实现  |  Shashlik 实现  |  NServiceBus 实现         │ │
│  └────────────────────────┬───────────────────────────────────┘ │
│                           │                                      │
│  ┌────────────────────────▼───────────────────────────────────┐ │
│  │           Interpreter (脚本引擎) + TaskActions (任务执行器)   │ │
│  │  C# | JavaScript | Python | Lua | BASIC | SQL              │ │
│  └────────────────────────────────────────────────────────────┘ │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────┐
│                      数据访问层                                  │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │  ApplicationDbContext │  │  IStorage (时序存储抽象)          │ │
│  │  (EF Core + Identity) │  │  EFStorage | InfluxDB | Taos    │ │
│  │  关系数据: 设备/租户/  │  │  Sharding | TimescaleDB | IoTDB │ │
│  │  告警/规则/资产...    │  │  PinusDB | ClickHouse           │ │
│  └──────────┬───────────┘  └──────────────┬───────────────────┘ │
│             │                              │                     │
│  ┌──────────▼──────────────────────────────▼──────────────────┐ │
│  │              数据库提供者 (Provider Pattern)                 │ │
│  │  PostgreSQL | MySQL | SqlServer | Oracle | Sqlite |        │ │
│  │  InMemory | Cassandra | ClickHouse                         │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 各层职责

| 层次 | 项目 | 职责 |
|------|------|------|
| **契约层** | `IoTSharp.Contracts` | 定义 DTO、枚举、常量、接口契约，无业务逻辑依赖 |
| **数据模型层** | `IoTSharp.Data` | EF Core 实体模型、DbContext、数据扩展方法、分片路由 |
| **时序存储层** | `IoTSharp.Data.TimeSeries` | 定义 `IStorage` 接口及各时序数据库实现 |
| **数据库提供者层** | `IoTSharp.Data.Storage.*` | 各关系数据库的 EF Core 配置、Migration、HealthCheck |
| **事件总线层** | `IoTSharp.EventBus` | 定义 `IPublisher`/`ISubscriber` 接口及事件处理逻辑 |
| **事件总线实现层** | `IoTSharp.EventBus.CAP/Shashlik/NServiceBus` | 具体消息中间件的集成实现 |
| **脚本引擎层** | `IoTSharp.Interpreter` | 多语言脚本执行引擎 (C#/JS/Python/Lua/BASIC/SQL) |
| **任务执行层** | `IoTSharp.TaskActions` | 可扩展的任务执行器框架，用于规则引擎的动作节点 |
| **扩展层** | `IoTSharp.Extensions.*` | 各类基础设施扩展 (X509/EFCore/AspNetCore/DI/Quartz等) |
| **Web 应用层** | `IoTSharp` | 主应用入口，Controllers、Services、中间件配置 |
| **前端层** | `ClientApp` | Vue 3 SPA 管理界面 |
| **SDK 层** | `IoTSharp.SDKs` | 供第三方调用的 HTTP/MQTT SDK |

---

## 四、核心模块详解

### 4.1 主应用入口 (IoTSharp)

**入口文件**: [Program.cs](IoTSharp/Program.cs)

- 使用 `Host.CreateDefaultBuilder` 创建宿主
- 配置 Kestrel Web 服务器，支持 LettuceEncrypt 自动 HTTPS
- 支持 Windows Service 模式运行
- 通过 `Startup` 类进行完整的服务配置

**启动配置**: [Startup.cs](IoTSharp/Startup.cs)

`ConfigureServices` 方法注册了以下核心服务：

```
1. AppSettings 配置绑定
2. HealthChecks 健康检查 (磁盘/数据库/缓存/MQ/时序库)
3. 数据库选择 (按 DataBaseType 枚举切换)
4. ASP.NET Identity (用户/角色管理)
5. JWT 认证
6. MQTT Server & Client
7. Quartz 定时任务
8. EasyCaching (InMemory/Redis/LiteDB)
9. 时序存储 (按 TelemetryStorage 枚举切换)
10. 事件总线 (CAP/Shashlik)
11. CoAP 服务
12. 脚本引擎
13. 规则引擎
14. 网关 (RawDataGateway/KepServerEx)
15. MCP Server (AI 工具)
16. LettuceEncrypt (可选 ACME)
```

`Configure` 方法的中间件管道：

```
Rin 调试工具 → 异常处理 → DB 迁移检查 → Routing → CORS →
Authentication → Authorization → 静态文件 → 响应压缩 →
MQTT Server → Swagger → HealthChecks → EventBus →
Endpoints (MVC/Razor/MQTT/Health/MCP) → Jdenticon →
TelemetryStorage → 自定义静态文件
```

### 4.2 控制器 (Controllers)

| 控制器 | 路由 | 功能 |
|--------|------|------|
| `AccountController` | `/api/Account` | 用户注册、登录、JWT 令牌、用户管理 |
| `DevicesController` | `/api/Devices` | 设备 CRUD、遥测数据上传/查询、属性管理、RPC 调用 |
| `AssetController` | `/api/Asset` | 资产管理 |
| `AlarmController` | `/api/Alarm` | 告警管理 |
| `TenantsController` | `/api/Tenants` | 租户管理 |
| `CustomersController` | `/api/Customers` | 客户管理 |
| `RulesController` | `/api/Rules` | 规则管理 |
| `ProducesController` | `/api/Produces` | 产品/物模型管理 |
| `AuthorizedKeysController` | `/api/AuthorizedKeys` | 授权密钥管理 |
| `BlobStorageController` | `/api/BlobStorage` | 文件存储 |
| `InstallerController` | `/api/Installer` | 安装向导 |
| `DeviceModelController` | `/api/DeviceModel` | 设备模型管理 |
| `DictionaryController` | `/api/Dictionary` | 数据字典 |
| `DynamicFormInfoController` | `/api/DynamicFormInfo` | 动态表单 |
| `HealthChecksController` | `/api/HealthChecks` | 健康检查 |
| `SubscriptionController` | `/api/Subscription` | 订阅管理 |
| `MetricsController` | `/api/Metrics` | 指标监控 |
| `MenuController` | `/api/Menu` | 菜单管理 |
| `CaptchaController` | `/api/Captcha` | 验证码 |
| `HomeController` | `/api/Home` | 首页数据 |

### 4.3 数据模型 (IoTSharp.Data)

**核心实体关系**:

```
Tenant (租户)
 ├── Customer (客户)
 │    └── Device (设备)
 │         ├── DeviceIdentity (设备身份: AccessToken/Password/X509)
 │         ├── AttributeLatest (最新属性数据)
 │         ├── TelemetryLatest (最新遥测数据)
 │         ├── DeviceRule (设备规则关联)
 │         └── Alarm (告警)
 │
 ├── Asset (资产)
 │    └── AssetRelation (资产关系)
 │
 ├── Produce (产品/物模型)
 │    ├── ProduceData (产品数据)
 │    ├── ProduceDictionary (产品字典)
 │    └── ProduceDataMapping (数据映射)
 │
 ├── FlowRule (流程规则)
 │    ├── Flow (流程)
 │    └── FlowOperation (流程操作)
 │
 ├── BaseDictionary (数据字典)
 ├── AuthorizedKey (授权密钥)
 └── AISettings (AI 设置)
```

**ApplicationDbContext** 继承自 `IdentityDbContext`，集成了 ASP.NET Identity，并包含 30+ 个 `DbSet`，覆盖了设备、租户、客户、告警、规则、资产、产品等全部业务实体。

**数据分片**: 使用 ShardingCore 实现按时间分表，支持按分钟/小时/天/月/年粒度：
- `TelemetryDataMinuteRoute`
- `TelemetryDataHourRoute`
- `TelemetryDataDayRoute`
- `TelemetryDataMonthRoute`
- `TelemetryDataYearRoute`

### 4.4 时序数据存储 (IoTSharp.Data.TimeSeries)

定义了统一的 `IStorage` 接口：

```csharp
public interface IStorage
{
    Task<bool> CheckTelemetryStorage();
    Task<(bool result, List<TelemetryData> telemetries)> StoreTelemetryAsync(PlayloadData msg);
    Task<List<TelemetryDataDto>> GetTelemetryLatest(Guid deviceId);
    Task<List<TelemetryDataDto>> GetTelemetryLatest(Guid deviceId, string keys);
    Task<List<TelemetryDataDto>> LoadTelemetryAsync(Guid deviceId, string keys, 
        DateTime begin, DateTime end, TimeSpan every, Aggregate aggregate);
}
```

**存储实现矩阵**:

| 存储模式 | 实现类 | 适用场景 |
|----------|--------|----------|
| `SingleTable` | `EFStorage` | 单表存储，适合小规模部署 |
| `Sharding` | `ShardingStorage` | 按时间分表，适合大规模关系数据库 |
| `InfluxDB` | `InfluxDBStorage` | 专业时序数据库 |
| `Taos` | `TaosStorage` | TDengine 时序数据库 |
| `TimescaleDB` | `TimescaleDBStorage` | PostgreSQL 时序扩展 |
| `IoTDB` | `IoTDBStorage` | Apache IoTDB |
| `PinusDB` | `PinusDBStorage` | PinusDB 时序数据库 |

### 4.5 数据库支持 (IoTSharp.Data.Storage)

采用 **Provider Pattern**，每个数据库一个独立项目：

| 数据库 | 项目 | 说明 |
|--------|------|------|
| PostgreSQL | `IoTSharp.Data.PostgreSQL` | 默认推荐 |
| MySQL | `IoTSharp.Data.MySQL` | |
| SQL Server | `IoTSharp.Data.SqlServer` | |
| Oracle | `IoTSharp.Data.Oracle` | |
| SQLite | `IoTSharp.Data.Sqlite` | 轻量/开发用 |
| InMemory | `IoTSharp.Data.InMemory` | 测试用 |
| Cassandra | `IoTSharp.Data.Cassandra` | 分布式 NoSQL |
| ClickHouse | `IoTSharp.Data.ClickHouse` | 列式分析数据库 |

每个项目包含：
- `XXXModelBuilderOptions` — 数据库特定的模型构建配置
- `IoTSharpDataBuilderExtensions` — `IServiceCollection` 扩展方法，注册 DbContext + HealthCheck

### 4.6 事件总线 (IoTSharp.EventBus)

**接口定义**:

- `IPublisher` — 事件发布接口
  - `PublishCreateDevice` / `PublishDeleteDevice`
  - `PublishAttributeData` / `PublishTelemetryData`
  - `PublishConnect` / `PublishActive` / `PublishDeviceAlarm`

- `ISubscriber` — 事件订阅接口

**EventBusSubscriber** 核心处理逻辑：
- `StoreAttributeData` — 接收属性数据，持久化到数据库，触发规则
- `StoreTelemetryData` — 接收遥测数据，调用 IStorage 存储，触发规则
- `OccurredAlarm` — 处理告警事件
- `Connect` / `Active` — 设备连接/活跃状态变更

**消息中间件支持**:

| 框架 | 项目 | 支持的 MQ |
|------|------|-----------|
| CAP | `IoTSharp.EventBus.CAP` | RabbitMQ, Kafka, ZeroMQ, NATS, Pulsar, RedisStreams, AmazonSQS, AzureServiceBus |
| Shashlik | `IoTSharp.EventBus.Shashlik` | RabbitMQ |
| NServiceBus | `IoTSharp.EventBus.NServiceBus` | 多种传输 |

**事件存储支持**: PostgreSQL, MongoDB, InMemory, LiteDB, MySQL, SQL Server

### 4.7 规则引擎 (FlowRuleProcessor)

位于 `IoTSharp.FlowRuleEngine` 命名空间（主项目内），核心类 `FlowRuleProcessor`：

- 基于流程图 (Flow) 的规则编排
- 支持规则缓存 (EasyCaching)，过期时间可配置
- 事件类型挂载点：`RAW`, `Telemetry`, `Attribute`, `RPC`, `Connected`, `Disconnected`, `Alarm`, `CreateDevice`, `DeleteDevice`, `Activity`, `Inactivity`
- 规则执行流程：查找设备关联规则 → 加载流程 → 逐节点执行 → 调用脚本引擎/任务执行器

### 4.8 脚本引擎 (IoTSharp.Interpreter)

所有引擎继承自 `ScriptEngineBase`，实现 `Do(string source, string input)` 方法：

| 引擎 | 类 | 语言 |
|------|-----|------|
| `CSharpScriptEngine` | C# | Roslyn 脚本 |
| `JavaScriptEngine` | JavaScript | Jint |
| `PythonScriptEngine` | Python | IronPython |
| `LuaScriptEngine` | Lua | MoonSharp |
| `BASICScriptEngine` | BASIC | 自定义解释器 |
| `SQLEngine` | SQL | 数据库查询 |

### 4.9 任务执行器 (IoTSharp.TaskActions)

抽象基类 `TaskAction`，实现 `ExecuteAsync(TaskActionInput)` 方法：

| 执行器 | 功能 |
|--------|------|
| `AlarmPullExcutor` | 告警拉取 |
| `CustomeAlarmPullExcutor` | 自定义告警拉取 |
| `DeviceActionExcutor` | 设备动作执行 |
| `MessagePullExcutor` | 消息拉取 |
| `RangerCheckExcutor` | 范围检查 |
| `TelemetryArrayPullExcutor` | 遥测数组拉取 |

### 4.10 MQTT 服务 (IoTSharp.Services.MQTTService)

- 内置 MQTT Broker (基于 MQTTnet)
- 设备通过 MQTT Topic 进行通信：
  - 遥测上报: `devices/{deviceId}/telemetry/{dataSide}`
  - 属性上报: `devices/{deviceId}/attributes/{dataSide}`
  - RPC 请求: `devices/{deviceId}/rpc/request/{method}/{rpcId}`
  - RPC 响应: `devices/{deviceId}/rpc/response/{method}/{rpcId}`
- 设备连接/断开事件自动追踪
- 支持 TLS/X509 证书认证
- RPC 客户端 (`RpcClient`) 支持同步/异步远程调用

### 4.11 网关 (IoTSharp.Gateways)

| 网关 | 功能 |
|------|------|
| `RawDataGateway` | 原始数据映射网关，支持将非结构化数据映射为设备遥测/属性 |
| `KepServerEx` | KepServerEx 工业网关集成 (OPC UA) |

`RawDataGateway` 的映射规则：
- `_map_to_telemetry_` → 映射到遥测数据
- `_map_to_attribute_` → 映射到属性数据
- `_map_to_devname` → 映射设备名称
- `_map_to_subdevname` → 映射子设备名称
- `_map_to_jsontext_in_json` → JSON 文本提取
- `_map_to_data_in_array` → 数组数据提取

### 4.12 MCP 工具 (IoTSharp.McpTools)

集成 **Model Context Protocol (MCP)**，支持 AI Agent 通过 MCP 协议访问设备数据：

- `DeviceTool` 类提供以下 MCP 工具：
  - `echo` — 回声测试
  - `DevicesList` — 获取设备列表
  - `GetDeviceStatus` — 获取设备连接状态
  - `GetDeviceAttributes` — 获取设备全部属性
  - `GetDeviceAttribute` — 获取设备指定属性

- 通过 API Key 进行权限控制，支持 `CustomerAdmin` 和 `TenantAdmin` 角色
- 端点: `/mcp/{api_key}`

### 4.13 前端应用 (ClientApp)

**技术栈**:
- Vue 3 + TypeScript + Vite
- Element Plus (UI 组件库)
- Pinia (状态管理)
- Vue Router (路由)
- Fast-CRUD (快速 CRUD 开发)
- AntV X6 (流程图设计器)
- ECharts (图表)
- Monaco Editor (代码编辑器)
- vue-i18n (国际化)

**页面模块**:

| 模块 | 页面 | 功能 |
|------|------|------|
| 登录 | `login/` | 登录、注册 |
| 仪表盘 | `dashboard/` | 首页概览、统计卡片 |
| 设备管理 | `iot/devices/` | 设备列表、详情、遥测、属性、规则、网关设计器 |
| 资产管理 | `iot/assets/` | 资产列表、详情、设计器 |
| 告警管理 | `iot/alarms/` | 告警列表 |
| 规则引擎 | `iot/rules/` | 流程列表、流程设计器、模拟器 |
| 产品管理 | `iot/produce/` | 产品列表、物模型、数据映射 |
| 网关管理 | `iot/gateway/` | 网关列表、网关设计器 |
| 系统设置 | `iot/settings/` | 用户、租户、客户、字典、证书管理 |
| 安装向导 | `installer/` | 初始安装配置 |

---

## 五、数据流架构

### 5.1 遥测数据上报流程

```
设备 → MQTT/HTTP/CoAP
         │
         ▼
   MQTTService / Controller
         │
         ▼
   IPublisher.PublishTelemetryData(PlayloadData)
         │
         ▼
   EventBus (CAP/Shashlik)
         │
         ▼
   EventBusSubscriber.StoreTelemetryData()
         │
         ├──▶ IStorage.StoreTelemetryAsync()  → 时序数据库
         │
         └──▶ FlowRuleProcessor.RunRules()    → 规则引擎
                  │
                  ├──▶ ScriptEngine.Do()        → 脚本执行
                  ├──▶ TaskAction.ExecuteAsync() → 任务执行
                  └──▶ IPublisher.Publish*()     → 级联事件
```

### 5.2 设备连接流程

```
设备 → MQTT Connect
         │
         ▼
   MQTTService.Server_ClientConnectedAsync()
         │
         ├──▶ 设备身份验证 (DeviceIdentity)
         ├──▶ IPublisher.PublishConnect(ConnectStatus.Connected)
         │         │
         │         ▼
         │    EventBusSubscriber.Connect()
         │         │
         │         ├──▶ 更新 AttributeLatest (Connected/LastConnectDateTime)
         │         └──▶ FlowRuleProcessor.RunRules(EventType.Connected)
         │
         └──▶ 设备会话绑定
```

### 5.3 告警处理流程

```
规则引擎/外部触发
         │
         ▼
   IPublisher.PublishDeviceAlarm(CreateAlarmDto)
         │
         ▼
   EventBusSubscriber.OccurredAlarm()
         │
         ├──▶ DbContext.OccurredAlarm()  → 持久化告警记录
         │
         └──▶ FlowRuleProcessor.RunRules(EventType.Alarm)  → 告警传播
```

---

## 六、配置体系

### 6.1 应用配置 (AppSettings)

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `DataBase` | `DataBaseType` | `PostgreSql` | 关系数据库类型 |
| `TelemetryStorage` | `TelemetryStorage` | `SingleTable` | 时序存储模式 |
| `EventBus` | `EventBusFramework` | `CAP` | 事件总线框架 |
| `EventBusStore` | `EventBusStore` | `InMemory` | 事件存储 |
| `EventBusMQ` | `EventBusMQ` | `InMemory` | 消息队列 |
| `CachingUseIn` | `CachingUseIn` | `InMemory` | 缓存方式 |
| `MqttBroker` | `MqttBrokerSetting` | — | MQTT Broker 配置 |
| `MqttClient` | `MqttClientSetting` | built-in | MQTT 客户端配置 |
| `JwtKey/Issuer/Audience/ExpireHours` | — | — | JWT 认证配置 |
| `ShardingByDateMode` | `ShardingByDateMode` | `PerMonth` | 分表粒度 |
| `DbContextPoolSize` | `int` | 128 | 数据库连接池大小 |
| `RuleCachingExpiration` | `int` | 60 | 规则缓存过期秒数 |

### 6.2 连接字符串

| 名称 | 用途 |
|------|------|
| `IoTSharp` | 主数据库连接 |
| `TelemetryStorage` | 时序数据库连接 |
| `EventBusStore` | 事件存储连接 |
| `EventBusMQ` | 消息队列连接 |
| `BlobStorage` | 文件存储连接 |

### 6.3 多环境配置

项目包含多种预设配置文件：

| 文件 | 数据库 | 时序存储 |
|------|--------|----------|
| `appsettings.Sqlite.json` | SQLite | 单表 |
| `appsettings.PostgreSql.json` | PostgreSQL | 单表 |
| `appsettings.MySql.json` | MySQL | 单表 |
| `appsettings.SQLServer.json` | SQL Server | 单表 |
| `appsettings.Oracle.json` | Oracle | 单表 |
| `appsettings.InMemory.json` | InMemory | 单表 |
| `appsettings.Cassandra.json` | Cassandra | 单表 |
| `appsettings.ClickHouse.json` | ClickHouse | 单表 |
| `appsettings.InfluxDB.json` | PostgreSQL | InfluxDB |
| `appsettings.Taos.json` | PostgreSQL | TDengine |
| `appsettings.TimescaleDB.json` | PostgreSQL | TimescaleDB |

---

## 七、部署架构

### 7.1 Docker 部署

项目提供多种 Docker Compose 方案：

**方案一: RabbitMQ + MongoDB + InfluxDB**
```
IoTSharp + PostgreSQL + RabbitMQ + MongoDB + InfluxDB
```
适合：生产环境，高可用消息队列 + 专业时序数据库

**方案二: ZeroMQ + 分表**
```
IoTSharp + PostgreSQL + ZeroMQ + ShardingCore
```
适合：中等规模，无需额外消息中间件

**方案三: ZeroMQ + TDengine**
```
IoTSharp + PostgreSQL + ZeroMQ + TDengine
```
适合：物联网场景，TDengine 高性能时序存储

### 7.2 健康检查

系统集成了全面的健康检查：

- 磁盘存储检查
- 关系数据库检查 (各数据库提供者)
- 时序数据库检查 (InfluxDB/TDengine/PinusDB/IoTDB/ClickHouse)
- 缓存检查 (Redis)
- 消息队列检查 (RabbitMQ/Kafka)
- HealthChecks UI 可视化面板

端点：`/healthz` 和 `/healthchecks-ui`

---

## 八、安全架构

### 8.1 认证

| 方式 | 适用场景 |
|------|----------|
| JWT Bearer | API 调用、前端访问 |
| MQTT AccessToken | 设备 MQTT 连接 |
| MQTT DevicePassword | 设备 MQTT 用户名密码 |
| MQTT X509Certificate | 设备 MQTT 证书双向认证 |
| MCP API Key | AI Agent 访问 |

### 8.2 授权

- 基于 ASP.NET Identity 的角色系统
- 自定义 Claim: `IoTSharpClaimTypes.Tenant` / `IoTSharpClaimTypes.Customer`
- 多租户隔离: Tenant → Customer → Device 层级
- 角色: `Anonymous`, `NormalUser`, `CustomerAdmin`, `TenantAdmin`, `SystemAdmin`

### 8.3 TLS/HTTPS

- 内置 LettuceEncrypt 支持 ACME 自动证书
- 阿里云 DNS 验证集成
- MQTT TLS 端口 (8883)
- Kestrel HTTPS 配置

---

## 九、项目依赖关系图

```
IoTSharp.Contracts ← (被所有项目引用)
     ↑
IoTSharp.Extensions ← IoTSharp.Data
     ↑                    ↑
IoTSharp.Data ← IoTSharp.Data.TimeSeries
     ↑                    ↑
     ├──── IoTSharp.Data.Storage.* (各数据库提供者)
     │
IoTSharp.EventBus ← IoTSharp.EventBus.CAP / Shashlik / NServiceBus
     ↑
IoTSharp.Interpreter
IoTSharp.TaskActions
     ↑
IoTSharp (主应用) ← 引用以上所有项目
```

---

## 十、技术栈总览

### 后端

| 技术 | 用途 |
|------|------|
| ASP.NET Core 10.0 | Web 框架 |
| EF Core 10.0 | ORM |
| ASP.NET Identity | 用户认证 |
| MQTTnet 5.0 | MQTT Broker/Client |
| IoTSharp.CoAP.NET | CoAP 服务 |
| DotNetCore.CAP | 事件总线 |
| ShardingCore | 数据分片 |
| EasyCaching | 缓存抽象 |
| Quartz | 定时任务 |
| RulesEngine | 规则引擎 |
| Jint | JavaScript 引擎 |
| IronPython | Python 引擎 |
| MoonSharp | Lua 引擎 |
| Roslyn | C# 脚本 |
| InfluxDB Client | InfluxDB 客户端 |
| LettuceEncrypt | ACME 自动证书 |
| NSwag | OpenAPI/Swagger |
| ModelContextProtocol | MCP AI 工具 |
| AutoMapper | 对象映射 |
| Storage.Net | 文件存储抽象 |
| Jdenticon | 头像生成 |
| Rin | 开发调试工具 |

### 前端

| 技术 | 用途 |
|------|------|
| Vue 3 | 前端框架 |
| Vite 5 | 构建工具 |
| Element Plus | UI 组件库 |
| Pinia | 状态管理 |
| Fast-CRUD | CRUD 快速开发 |
| AntV X6 | 流程图 |
| ECharts | 图表 |
| Monaco Editor | 代码编辑器 |
| vue-i18n | 国际化 |
| Axios | HTTP 客户端 |

---

## 十一、扩展点

IoTSharp 的架构设计提供了丰富的扩展点：

1. **数据库扩展** — 新增 `IoTSharp.Data.XXX` 项目，实现 `IDataBaseModelBuilderOptions`
2. **时序存储扩展** — 实现 `IStorage` 接口，在 `DependencyInjection.AddTelemetryStorage` 中注册
3. **事件总线扩展** — 实现 `IPublisher`/`ISubscriber`，新增 `IoTSharp.EventBus.XXX` 项目
4. **脚本引擎扩展** — 继承 `ScriptEngineBase`，实现 `Do` 方法
5. **任务执行器扩展** — 继承 `TaskAction`，实现 `ExecuteAsync` 方法
6. **网关扩展** — 实现自定义数据映射逻辑
7. **MCP 工具扩展** — 使用 `[McpServerToolType]` 和 `[McpServerTool]` 特性添加新工具
8. **控制器扩展** — 添加新的 API Controller

---

## 十二、总结

IoTSharp 是一个**高度模块化、可扩展**的物联网平台，其架构特点包括：

- **多数据库支持**：8 种关系数据库 + 7 种时序存储，通过配置切换
- **多协议接入**：MQTT (内置 Broker)、HTTP REST、CoAP
- **事件驱动**：基于事件总线的松耦合架构，支持多种消息中间件
- **规则引擎**：可视化流程编排 + 多语言脚本 + 可扩展任务执行器
- **多租户**：Tenant → Customer → Device 三级隔离
- **AI 集成**：MCP 协议支持 AI Agent 访问设备数据
- **云原生**：Docker 部署、健康检查、自动 HTTPS 证书
- **工业网关**：Modbus、OPC UA 等工业协议支持