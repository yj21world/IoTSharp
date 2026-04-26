# 商场暖通空调系统 - 云端采集模块需求与方案

> **版本**: 1.0
> **日期**: 2026年4月21日
> **项目**: 商场暖通空调 (HVAC) 云端管理系统
> **技术栈**: .NET 10 + Vue 3 + IoTSharp 平台
> **网关**: 有人 USR-G805（透传型网关）
> **通信协议**: MQTT + Modbus RTU

---

## 一、项目背景

### 1.1 业务场景

商场暖通空调系统云端管理，通过 USR-G805 工业网关接入暖通设备，实现：
- 设备实时监控（温度、压力、流量、状态等）
- 远程控制（启停、阀门开度、频率设定等）
- 告警管理（超温、超压、设备故障等）
- 能耗统计（电量、冷热量、能效比等）
- 自动化策略（定时启停、温度联动、节能模式等）

### 1.2 设备清单

| 设备类型 | 典型设备 | 通信协议 | 数据特征 |
|---------|---------|---------|---------|
| **主机** | 冷水机组、热泵机组 | Modbus RTU | 遥测多（供回水温度、压力、电流、功率等），需控制（启停、设定温度） |
| **水泵** | 冷冻水泵、冷却水泵、热水循环泵 | Modbus RTU | 遥测（流量、扬程、电流、频率），控制（启停、变频频率设定） |
| **冷却塔** | 冷却塔风机 | Modbus RTU | 遥测（风机状态、频率、水温），控制（启停、变频） |
| **末端风柜** | AHU、FCU（风机盘管） | Modbus RTU | 遥测（送风温度、回风温度、风阀开度），控制（启停、温度设定、风阀开度） |
| **阀门** | 电动调节阀、蝶阀 | Modbus RTU | 遥测（开度反馈），控制（开度设定、开关） |
| **风机** | 新风机、排风机 | Modbus RTU | 遥测（运行状态、频率、电流），控制（启停、变频频率） |
| **电表** | 三相电表、多功能电表 | Modbus RTU | 遥测（电压、电流、功率、功率因数、电能），只读 |
| **传感器** | 温湿度传感器、压力传感器、流量计 | Modbus RTU / 4-20mA | 遥测（温度、湿度、压力、流量），只读 |

### 1.3 网络拓扑

```
云端 IoTSharp 平台
    │
    │ MQTT (1883/8883)
    │
USR-G805 网关 (4G/有线)
    │
    │ Modbus RTU (RS485 总线)
    │
    ├── 从站1: 冷水机组 (SlaveId=1)
    ├── 从站2: 冷冻水泵 (SlaveId=2)
    ├── 从站3: 冷却水泵 (SlaveId=3)
    ├── 从站4: 冷却塔 (SlaveId=4)
    ├── 从站5: 末端风柜-AHU1 (SlaveId=5)
    ├── 从站6: 末端风柜-AHU2 (SlaveId=6)
    ├── 从站7: 电动阀门 (SlaveId=7)
    ├── 从站8: 电表 (SlaveId=8)
    └── 从站9: 温湿度传感器 (SlaveId=9)
```

### 1.4 网关类型说明

| 网关类型 | 代表设备 | 工作方式 | 云端职责 |
|---------|---------|---------|---------|
| **主动采集型网关** | USR-M100 | 网关自己轮询 Modbus，主动 MQTT 上报 JSON 遥测 | 云端配置点位表，网关自动执行 |
| **透传型网关** | USR-G805 | 只做 MQTT ↔ RS485 字节透传 | 云端需要自己发 Modbus 请求，自己解析响应 |

**本项目使用 USR-G805（透传型），因此需要新增云端采集模块。**

### 1.5 USR-G805 关键特性

- 双网口（LAN/WAN）+ RS232/RS485 串口 + 4G LTE
- 支持 DTU 透明传输：串口数据 ↔ TCP/UDP/MQTT
- 支持 Modbus RTU 转 Modbus TCP 透明转发
- 支持 MQTT 协议：可配置连接到云端 MQTT Broker
- 支持自定义注册包/心跳包/数据包格式
- **不支持** Modbus 轮询和边缘计算
- MQTT 透传模式下，Payload 为 Hex 字符串

---

## 二、IoTSharp 现有能力分析

### 2.1 已完整实现（可直接复用）

| 功能模块 | 关键文件 | 状态 |
|---------|---------|------|
| MQTT Broker（内置） | `Services/MQTTService.cs` | ✅ |
| 设备 CRUD + 遥测/属性 | `Controllers/DevicesController.cs` | ✅ |
| 网关 MQTT 协议（ThingsBoard 兼容） | `Services/MQTTControllers/GatewayController.cs` | ✅ |
| 子设备自动创建 | `JudgeOrCreateNewDevice()` | ✅ |
| 设备身份认证（Token/密码/X509） | `Services/MQTTService.cs` | ✅ |
| 规则引擎（BPMN + 多脚本） | `FlowRuleEngine/FlowRuleProcessor.cs` | ✅ |
| 告警系统 | `Controllers/AlarmController.cs` | ✅ |
| 产品模板系统 | `Controllers/ProducesController.cs` | ✅ |
| 边缘节点管理 | `Controllers/EdgeController.cs` | ✅ |
| 边缘任务下发/回执 | `Controllers/EdgeTaskController.cs` | ✅ |
| 多数据库支持（9种） | `IoTSharp.Data.{PostgreSQL,...}/` | ✅ |
| 多时序库支持（7种） | `IoTSharp.Data.TimeSeries/` | ✅ |
| 事件总线（CAP + 9种MQ） | `IoTSharp.EventBus.CAP/` | ✅ |
| 前端 Vue 3 + Vite | `ClientApp/` | ✅ |

### 2.2 部分实现（需要扩展）

| 功能模块 | 现状 | 需要补充 |
|---------|------|---------|
| 采集任务配置 | `CollectionTaskController` 仅有草稿生成和验证，Preview 返回假数据 | 持久化、执行引擎、前端页面 |
| 前端实时推送 | 无 MQTT/WebSocket 订阅，HTTP 轮询 | WebSocket 转发层 |

### 2.3 完全缺失（需要新建）

| 功能模块 | 说明 |
|---------|------|
| **云端 Modbus 采集引擎** | 本模块核心，定时发请求、解析响应、存遥测 |
| **HVAC 设备产品模板** | 冷水机组、水泵、风柜等预定义模板 |
| **能耗统计** | 电量、冷热量、COP 计算 |
| **HVAC 专用看板** | 系统概览、冷热源、末端、能耗 |

---

## 三、云端采集模块需求

### 3.1 模块定位

云端采集模块是 IoTSharp 平台的一个子系统，负责：
- 管理透传型网关的采集任务配置
- 定时通过 MQTT 向网关发送 Modbus 请求
- 接收网关透传回来的 Modbus 响应
- 解析寄存器值并换算为工程值
- 将结果写入遥测/属性数据表
- 触发规则引擎进行后续处理

### 3.2 功能需求

#### 3.2.1 配置管理

| 需求编号 | 需求描述 | 优先级 |
|---------|---------|--------|
| CFG-001 | 支持按网关创建采集任务 | 高 |
| CFG-002 | 支持配置从站信息（SlaveId、从站名称、启用状态） | 高 |
| CFG-003 | 支持配置采集点位（地址、功能码、数据类型、字节序、换算规则） | 高 |
| CFG-004 | 支持配置轮询周期（按点位或按从站分组设置） | 高 |
| CFG-005 | 支持批量导入/导出采集配置（Excel/JSON） | 中 |
| CFG-006 | 支持配置版本管理和变更历史 | 中 |
| CFG-007 | 支持配置启用/禁用（不停服务切换） | 高 |

#### 3.2.2 采集执行

| 需求编号 | 需求描述 | 优先级 |
|---------|---------|--------|
| EXEC-001 | 每个网关独立调度器，并行执行 | 高 |
| EXEC-002 | 同从站连续地址自动合并为批量读取请求 | 高 |
| EXEC-003 | 支持分层轮询（高速10s/中速30s/低速60s） | 高 |
| EXEC-004 | 异步请求-响应匹配（requestId） | 高 |
| EXEC-005 | 请求超时检测（默认2秒）和重试机制（默认3次） | 高 |
| EXEC-006 | 网关断线自动暂停采集，上线自动恢复 | 高 |
| EXEC-007 | 支持手动触发单点/单从站采集（调试用途） | 中 |

#### 3.2.3 数据解析

| 需求编号 | 需求描述 | 优先级 |
|---------|---------|--------|
| PARSE-001 | 支持 Modbus RTU 帧解析（验证 CRC、提取数据） | 高 |
| PARSE-002 | 支持多种数据类型（bool/int16/uint16/int32/uint32/float32/float64/string） | 高 |
| PARSE-003 | 支持多种字节序（AB/CD/ABCD/CDAB/DCBA/BADC） | 高 |
| PARSE-004 | 支持数值换算（缩放系数、偏移量、表达式） | 高 |
| PARSE-005 | 支持位提取（从一个寄存器中提取多个位字段） | 中 |
| PARSE-006 | 支持枚举映射（原始值 → 状态文本） | 中 |
| PARSE-007 | 解析结果写入 TelemetryData 和 TelemetryLatest | 高 |

#### 3.2.4 调试日志

| 需求编号 | 需求描述 | 优先级 |
|---------|---------|--------|
| LOG-001 | 记录每次请求/响应的原始 Hex 帧 | 高 |
| LOG-002 | 记录解析过程（原始值 → 换算后值） | 高 |
| LOG-003 | 记录错误（超时、CRC错误、从站无响应、解析错误） | 高 |
| LOG-004 | 支持按网关/从站/点位/时间范围过滤查看日志 | 中 |
| LOG-005 | 支持日志级别配置（Debug/Info/Error） | 中 |
| LOG-006 | 日志保留策略（默认保留7天） | 低 |

#### 3.2.5 状态监控

| 需求编号 | 需求描述 | 优先级 |
|---------|---------|--------|
| MON-001 | 显示网关在线/离线状态 | 高 |
| MON-002 | 显示每个从站的通信状态（正常/异常） | 高 |
| MON-003 | 显示每个点位的最后采集时间和值 | 高 |
| MON-004 | 显示采集成功率统计 | 中 |
| MON-005 | 显示采集延迟统计 | 低 |

---

## 四、技术方案

### 4.1 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                      IoTSharp 云端平台                        │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   Web API   │  │  MQTT Broker │  │   采集引擎           │  │
│  │  控制器层    │  │  (MQTTnet)  │  │  (HostedService)    │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                    │             │
│         └────────────────┴────────────────────┘             │
│                          │                                  │
│                   ApplicationDbContext                      │
│                          │                                  │
│  ┌─────────────┐  ┌─────┴──────┐  ┌─────────────────────┐  │
│  │   Device    │  │ Telemetry  │  │   CollectionTask    │  │
│  │   设备表     │  │   遥测表    │  │   采集任务表         │  │
│  └─────────────┘  └────────────┘  └─────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ MQTT (1883)
                              │
                    ┌─────────┴─────────┐
                    │    USR-G805 网关   │
                    │  (MQTT 透传模式)   │
                    └─────────┬─────────┘
                              │
                              │ Modbus RTU (RS485)
                              │
                    ┌─────────┴─────────┐
                    │     从站设备       │
                    │ (水泵/阀门/主机等) │
                    └───────────────────┘
```

### 4.2 采集引擎内部架构

```
┌─────────────────────────────────────────┐
│         ModbusCollectionService         │
│         (BackgroundService)             │
│                                         │
│  ┌─────────────────────────────────┐    │
│  │      配置加载器                  │    │
│  │  - 启动时从数据库加载任务         │    │
│  │  - 定时刷新（30秒）               │    │
│  │  - 监听配置变更通知               │    │
│  └─────────────────────────────────┘    │
│                   │                     │
│  ┌─────────────────────────────────┐    │
│  │      调度器管理器                │    │
│  │  - 为每个网关创建一个调度器       │    │
│  │  - 网关上线/下线动态增删          │    │
│  └─────────────────────────────────┘    │
│                   │                     │
│  ┌─────────────────────────────────┐    │
│  │   网关调度器1    网关调度器2 ... │    │
│  │   (独立线程)     (独立线程)      │    │
│  │                                 │    │
│  │  ┌─────────┐    ┌─────────┐    │    │
│  │  │高速队列 │    │高速队列 │    │    │
│  │  │(10s)   │    │(10s)   │    │    │
│  │  └────┬────┘    └────┬────┘    │    │
│  │  ┌────┴────┐    ┌────┴────┐    │    │
│  │  │中速队列 │    │中速队列 │    │    │
│  │  │(30s)   │    │(30s)   │    │    │
│  │  └────┬────┘    └────┬────┘    │    │
│  │  ┌────┴────┐    ┌────┴────┐    │    │
│  │  │低速队列 │    │低速队列 │    │    │
│  │  │(60s)   │    │(60s)   │    │    │
│  │  └────┬────┘    └────┬────┘    │    │
│  │       │              │         │    │
│  │  ┌────┴──────────────┴────┐    │    │
│  │  │      批量合并器         │    │    │
│  │  │  同从站连续地址合并      │    │    │
│  │  └───────────┬────────────┘    │    │
│  │              │                  │    │
│  │  ┌───────────┴───────────┐     │    │
│  │  │      请求发送器        │     │    │
│  │  │  - 组 Modbus RTU 帧    │     │    │
│  │  │  - MQTT Publish        │     │    │
│  │  │  - 注册超时回调         │     │    │
│  │  └───────────┬───────────┘     │    │
│  │              │                  │    │
│  │  ┌───────────┴───────────┐     │    │
│  │  │      响应处理器        │     │    │
│  │  │  - 匹配 requestId      │     │    │
│  │  │  - 解析 Modbus 帧      │     │    │
│  │  │  - 换算工程值          │     │    │
│  │  │  - 写入遥测            │     │    │
│  │  └───────────────────────┘     │    │
│  └─────────────────────────────────┘    │
│                                         │
│  ┌─────────────────────────────────┐    │
│  │      调试日志服务                │    │
│  │  - 记录请求/响应/错误            │    │
│  │  - 支持按维度过滤查询            │    │
│  └─────────────────────────────────┘    │
│                                         │
└─────────────────────────────────────────┘
```

### 4.3 MQTT Topic 规范

#### 4.3.1 采集请求

```
Topic:    gateway/{gatewayDeviceName}/modbus/request/{requestId}
QoS:      0
Payload:  Hex 字符串（Modbus RTU 帧）

示例:
Topic:    gateway/G805-001/modbus/request/abc123
Payload:  "010300000002C40B"
          ││││││││││││││
          │││││││││││└┴─ CRC 校验
          │││││││││└┴── 读取寄存器数量 = 2
          │││││└┴──┴── 起始地址 = 40001 (0x0000)
          │││└──────── 功能码 = 03 (读保持寄存器)
          │└────────── 从站地址 = 01
          └─────────── 帧头
```

#### 4.3.2 采集响应

```
Topic:    gateway/{gatewayDeviceName}/modbus/response/{requestId}
QoS:      0
Payload:  Hex 字符串（Modbus RTU 响应帧）

示例:
Topic:    gateway/G805-001/modbus/response/abc123
Payload:  "01030401A40000..."
          ││││││││││││││
          │││││││││└┴── 数据内容
          │││││└──────── 字节数 = 4
          │││└────────── 功能码 = 03
          │└──────────── 从站地址 = 01
          └───────────── 帧头
```

#### 4.3.3 网关在线状态

复用现有 MQTT 连接事件：
- 网关连接 Broker → 标记在线
- 网关断开 Broker → 标记离线（通过 MQTT 遗嘱消息）

### 4.4 数据库设计

#### 4.4.1 采集任务表 (CollectionTasks)

```sql
CREATE TABLE CollectionTasks (
    Id UUID PRIMARY KEY,
    TaskKey VARCHAR(100) NOT NULL,           -- 任务标识，如 "hvac-boiler-room-a"
    GatewayDeviceId UUID NOT NULL,           -- 关联的网关设备ID
    Protocol VARCHAR(50) NOT NULL DEFAULT 'Modbus',
    Version INT NOT NULL DEFAULT 1,          -- 配置版本号
    Enabled BOOLEAN NOT NULL DEFAULT true,   -- 是否启用
    
    -- 连接配置（JSON）
    ConnectionJson TEXT NOT NULL,            -- {host, port, timeoutMs, retryCount}
    
    -- 上报策略（JSON）
    ReportPolicyJson TEXT,                   -- {defaultTrigger, deadband, includeQuality}
    
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    
    CONSTRAINT FK_CollectionTasks_GatewayDevice 
        FOREIGN KEY (GatewayDeviceId) REFERENCES Devices(Id)
);
```

#### 4.4.2 采集从站表 (CollectionDevices)

```sql
CREATE TABLE CollectionDevices (
    Id UUID PRIMARY KEY,
    TaskId UUID NOT NULL,
    DeviceKey VARCHAR(100) NOT NULL,         -- 从站标识，如 "slave-1"
    DeviceName VARCHAR(200),                 -- 从站名称，如 "锅炉控制器1"
    SlaveId INT NOT NULL,                    -- Modbus 从站地址
    Enabled BOOLEAN NOT NULL DEFAULT true,
    SortOrder INT NOT NULL DEFAULT 0,        -- 排序
    
    -- 协议特有配置（JSON）
    ProtocolOptionsJson TEXT,                -- Modbus: {baudRate, dataBits, stopBits, parity}
    
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    
    CONSTRAINT FK_CollectionDevices_Task 
        FOREIGN KEY (TaskId) REFERENCES CollectionTasks(Id) ON DELETE CASCADE
);
```

#### 4.4.3 采集点位表 (CollectionPoints)

```sql
CREATE TABLE CollectionPoints (
    Id UUID PRIMARY KEY,
    DeviceId UUID NOT NULL,
    
    -- 点位标识
    PointKey VARCHAR(100) NOT NULL,          -- 点位标识，如 "supply-temp"
    PointName VARCHAR(200),                  -- 点位名称，如 "供水温度"
    
    -- 采集源信息
    FunctionCode INT NOT NULL,               -- Modbus 功能码：1/2/3/4
    Address INT NOT NULL,                    -- 寄存器地址（从0开始）
    RegisterCount INT NOT NULL DEFAULT 1,    -- 寄存器数量
    
    -- 数据类型和字节序
    RawDataType VARCHAR(50) NOT NULL,        -- bool/int16/uint16/int32/uint32/float32/float64/string
    ByteOrder VARCHAR(10) DEFAULT 'AB',      -- AB/CD/ABCD/CDAB/DCBA/BADC
    WordOrder VARCHAR(10) DEFAULT 'AB',      -- 多寄存器时的字顺序
    
    -- 轮询策略
    ReadPeriodMs INT NOT NULL DEFAULT 30000, -- 轮询周期（毫秒）
    PollingGroup VARCHAR(50),                -- 轮询分组，用于批量优化
    
    -- 数值转换（JSON）
    TransformsJson TEXT,                     -- [{type:"Scale",params:{factor:0.1}}, ...]
    
    -- 业务层映射（直接关联现有 Device 表的子设备）
    TargetDeviceId UUID,                     -- 子设备ID（Device 表，DeviceType=Device）
    TargetName VARCHAR(100) NOT NULL,        -- 子设备属性名，如 "supplyTemperature"
    TargetType VARCHAR(50) NOT NULL DEFAULT 'Telemetry',  -- Telemetry/Attribute/AlarmInput
    TargetValueType VARCHAR(50) NOT NULL DEFAULT 'Double', -- boolean/int/long/double/string
    DisplayName VARCHAR(200),                -- 显示名称
    Unit VARCHAR(50),                        -- 单位，如 "°C"
    GroupName VARCHAR(100),                  -- 分组，如 "boiler"
    
    Enabled BOOLEAN NOT NULL DEFAULT true,
    SortOrder INT NOT NULL DEFAULT 0,
    
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    
    CONSTRAINT FK_CollectionPoints_CollectionDevice 
        FOREIGN KEY (DeviceId) REFERENCES CollectionDevices(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CollectionPoints_TargetDevice 
        FOREIGN KEY (TargetDeviceId) REFERENCES Devices(Id)
);
```

#### 4.4.4 采集日志表 (CollectionLogs)

```sql
CREATE TABLE CollectionLogs (
    Id UUID PRIMARY KEY,
    
    -- 上下文
    GatewayDeviceId UUID NOT NULL,
    TaskId UUID,
    DeviceId UUID,                           -- 从站ID
    PointId UUID,                            -- 点位ID（可为空，批量请求时）
    
    -- 请求信息
    RequestId VARCHAR(100) NOT NULL,         -- requestId
    RequestAt TIMESTAMP NOT NULL,            -- 请求时间
    RequestFrame TEXT,                       -- 请求帧 Hex 字符串
    
    -- 响应信息
    ResponseAt TIMESTAMP,                    -- 响应时间（超时为空）
    ResponseFrame TEXT,                      -- 响应帧 Hex 字符串
    
    -- 解析结果
    ParsedValue TEXT,                        -- 解析后的值（字符串形式）
    ConvertedValue TEXT,                     -- 换算后的值
    
    -- 状态
    Status VARCHAR(50) NOT NULL,             -- Success/Timeout/CrcError/NoResponse/ParseError
    ErrorMessage TEXT,                       -- 错误信息
    
    -- 性能
    DurationMs INT,                          -- 响应耗时（毫秒）
    
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- 索引
    INDEX IDX_CollectionLogs_GatewayTime (GatewayDeviceId, CreatedAt),
    INDEX IDX_CollectionLogs_Status (Status),
    INDEX IDX_CollectionLogs_RequestId (RequestId)
);
```

### 4.5 核心类设计

#### 4.5.1 采集服务入口

```csharp
public class ModbusCollectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModbusCollectionService> _logger;
    private readonly IMqttClient _mqttClient;           // MQTT 客户端
    private readonly GatewaySchedulerManager _schedulerManager;
    private readonly CollectionConfigurationLoader _configLoader;
    
    // 请求-响应匹配器
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CollectionResponse>> 
        _pendingRequests = new();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. 连接 MQTT Broker
        await _mqttClient.ConnectAsync(stoppingToken);
        
        // 2. 订阅所有网关的响应 Topic
        await _mqttClient.SubscribeAsync("gateway/+/modbus/response/+");
        
        // 3. 监听网关在线/离线事件
        _mqttClient.GatewayConnected += OnGatewayConnected;
        _mqttClient.GatewayDisconnected += OnGatewayDisconnected;
        
        // 4. 加载配置并启动调度器
        await _configLoader.LoadAsync();
        
        // 5. 启动配置刷新定时器
        _ = ConfigRefreshLoop(stoppingToken);
        
        // 6. 主循环：处理 MQTT 消息
        await MessageLoop(stoppingToken);
    }
}
```

#### 4.5.2 网关调度器

```csharp
public class GatewayScheduler
{
    public string GatewayDeviceName { get; }
    public Guid GatewayDeviceId { get; }
    
    // 三个优先级队列
    private readonly PriorityQueue _highSpeedQueue;     // 10s
    private readonly PriorityQueue _mediumSpeedQueue;   // 30s
    private readonly PriorityQueue _lowSpeedQueue;      // 60s
    
    // 批量合并器
    private readonly BatchMerger _batchMerger;
    
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // 检查网关是否在线
            if (!await IsGatewayOnline())
            {
                await Task.Delay(5000, ct);
                continue;
            }
            
            // 获取到期触发的点位
            var duePoints = GetDuePoints();
            
            // 按从站分组并合并批量请求
            var batches = _batchMerger.Merge(duePoints);
            
            // 发送请求
            foreach (var batch in batches)
            {
                await SendBatchRequest(batch);
            }
            
            // 等待一小段时间再检查
            await Task.Delay(100, ct);
        }
    }
}
```

#### 4.5.3 Modbus 协议栈

```csharp
public class ModbusRtuProtocol
{
    // 组帧：组装 Modbus RTU 请求帧
    public static byte[] BuildReadRequest(byte slaveId, byte functionCode, 
        ushort startAddress, ushort quantity)
    {
        var frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = functionCode;
        frame[2] = (byte)(startAddress >> 8);
        frame[3] = (byte)(startAddress & 0xFF);
        frame[4] = (byte)(quantity >> 8);
        frame[5] = (byte)(quantity & 0xFF);
        
        var crc = CalculateCrc(frame, 6);
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)(crc >> 8);
        
        return frame;
    }
    
    // 解析：从响应帧提取寄存器值
    public static ModbusResponse ParseResponse(byte[] frame)
    {
        // 验证 CRC
        // 验证从站地址和功能码
        // 提取数据区
        // 返回寄存器值数组
    }
    
    // CRC16 校验
    public static ushort CalculateCrc(byte[] data, int length)
    {
        // Modbus CRC-16 实现
    }
}
```

#### 4.5.4 数据解析器

```csharp
public class ModbusDataParser
{
    // 将原始寄存器值解析为指定类型的数值
    public static object ParseRegisters(ushort[] registers, string dataType, string byteOrder)
    {
        return dataType switch
        {
            "bool" => (registers[0] & 0x01) == 1,
            "int16" => (short)registers[0],
            "uint16" => registers[0],
            "int32" => ParseInt32(registers, byteOrder),
            "uint32" => ParseUInt32(registers, byteOrder),
            "float32" => ParseFloat32(registers, byteOrder),
            "float64" => ParseFloat64(registers, byteOrder),
            "string" => ParseString(registers),
            _ => throw new NotSupportedException($"Data type {dataType} not supported")
        };
    }
    
    // 应用换算规则
    public static double ApplyTransforms(double rawValue, List<ValueTransform> transforms)
    {
        var result = rawValue;
        foreach (var transform in transforms.OrderBy(t => t.Order))
        {
            result = transform.Type switch
            {
                "Scale" => result * transform.Parameters.Factor,
                "Offset" => result + transform.Parameters.Offset,
                "Expression" => EvaluateExpression(transform.Parameters.Expression, result),
                _ => result
            };
        }
        return result;
    }
}
```

### 4.6 与现有系统集成

#### 4.6.1 复用 MQTT Broker

采集引擎作为内部 MQTT Client，连接本地 Broker：

```csharp
// 使用 MQTTnet 创建客户端
var factory = new MqttFactory();
var client = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer("localhost", 1883)
    .WithClientId("modbus-collector")
    .Build();

await client.ConnectAsync(options);
```

#### 4.6.2 复用遥测存储

采集结果直接写入现有遥测表：

```csharp
// 使用现有 IPublisher 接口
await _publisher.PublishTelemetryData(new PlayloadData
{
    DeviceId = targetDeviceId,
    MsgBody = new Dictionary<string, object> { { point.TargetName, convertedValue } },
    DataSide = DataSide.ClientSide,
    DataCatalog = DataCatalog.TelemetryData
});
```

#### 4.6.3 触发规则引擎

遥测数据发布后自动触发规则引擎：

```csharp
// 现有 FlowRuleProcessor 会自动处理
// TelemetryController 收到数据后调用 _queue.PublishTelemetryData
// 事件总线订阅者收到后调用 FlowRuleProcessor.RunRules()
```

#### 4.6.4 复用设备管理

网关作为 `DeviceType.Gateway` 设备，子设备自动创建：

```csharp
// 复用现有 JudgeOrCreateNewDevice 方法
var device = gateway.JudgeOrCreateNewDevice(subDeviceName, scopeFactory, logger);
```

---

## 五、实施计划


## 六、关键设计决策总结

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 网关类型 | USR-G805（透传型） | 现场已确定 |
| 云端采集模式 | 主动采集 | 透传网关不支持边缘采集 |
| MQTT 透传格式 | Hex 字符串 | USR-G805 支持 |
| 采集引擎部署 | 同进程 HostedService | 50网关/167req/s 单进程可支撑 |
| 调度方式 | 每网关独立调度器 + 异步请求响应 | 高并发、低耦合 |
| 批量优化 | 同从站连续地址合并 | 减少请求次数 |
| 分层轮询 | 高速10s/中速30s/低速60s | 不同数据不同优先级 |
| 配置存储 | 关系型数据库（4张表） | 结构化、好查询 |
| MQTT Client | 内部客户端连接本地 Broker | 复用现有 Broker |
| 遥测存储 | 复用现有 TelemetryData | 统一数据模型 |
| 规则触发 | 复用现有 FlowRuleProcessor | 发布遥测自动触发 |

---

## 七、子设备生成策略

### 7.1 设计原则

**"采集层面向协议，应用层面向设备，两层完全解耦"**

```
协议层（采集配置）        映射层（子设备配置）        应用层（资产）
─────────────────        ───────────────────        ─────────────
从站1 (SlaveId=1)  ──►   子设备"Chiller1"    ──►   资产"锅炉房A"
  ├─ 40001          ──►     ├─ supplyTemp    ──►     ├─ 供水温度
  ├─ 40002          ──►     ├─ returnTemp    ──►     ├─ 回水温度
  └─ 40003          ──►     └─ power         ──►     └─ 功率

从站2 (SlaveId=2)  ──►   子设备"Chiller1"    ──►   （同一资产）
  └─ 40001          ──►     └─ condenserPressure

从站5 (SlaveId=5)  ──►   子设备"Sensor_A"   ──►   资产"区域A"
  ├─ 40001          ──►     ├─ temperature
  └─ 40002          ──►     └─ humidity

从站5 (SlaveId=5)  ──►   子设备"Sensor_B"   ──►   资产"区域B"
  ├─ 40003          ──►     ├─ temperature
  └─ 40004          ──►     └─ humidity
```

### 7.2 从站与设备的关系

| 场景 | 映射模式 | 示例 |
|------|---------|------|
| **标准情况** | 1从站 = 1子设备 | 水泵变频器（SlaveId=2）→ Pump1 |
| **多模块设备** | N从站 = 1子设备 | 冷水机组3个模块（SlaveId=1/2/3）→ Chiller1 |
| **采集器拆分** | 1从站 = N子设备 | 温湿度采集器（SlaveId=5）→ Sensor_A + Sensor_B |

### 7.3 子设备创建策略

**"配置驱动 + 延迟创建 + 首次采集成功即创建"**

#### 创建时机
- 首次从某从站采集成功时，检查配置中是否有关联子设备
- 无则按配置自动创建，有则直接复用
- 采集失败不创建，避免垃圾设备

#### 配置来源
```csharp
// 采集配置中定义子设备映射
CollectionDeviceDto {
    SlaveId = 1,                      // 协议层：Modbus 从站地址
    DeviceKey = "Chiller1",           // 业务层：子设备标识
    DeviceName = "冷水机组1",          // 业务层：显示名称
    ProduceId = Guid.Parse("..."),    // 业务层：产品模板（可选）
    Points = [...]                    // 协议层：采集点位
}
```

#### 创建流程
```
采集引擎读取从站1（SlaveId=1）
    │
    ├── 发送 Modbus 请求 → 网关透传 → 收到响应
    ├── 解析成功 ✓
    │
    ├── 检查：配置中 SlaveId=1 映射到哪个子设备？
    │       │
    │       ├── 未配置 → 可选：自动生成"Slave_1"或报错
    │       │
    │       └── 已配置 → DeviceKey="Chiller1"
    │               │
    │               ├── 子设备不存在 → 自动创建
    │               │       ├── new Device { Name="Chiller1", Type=Device, Owner=Gateway }
    │               │       ├── AfterCreateDevice() → 创建设备身份
    │               │       └── SaveChanges()
    │               │
    │               └── 子设备已存在 → 直接用
    │
    └── 存遥测数据到子设备 Chiller1
```

### 7.4 前端配置流程

```
步骤1：配置采集从站（协议层）
├── 添加从站：SlaveId=1
├── 添加点位：40001, 40002, 40003...
└── 保存

步骤2：配置子设备映射（业务层）
├── 方式A（快速）：一键生成"1从站1设备"
│   └── 系统自动：Slave1 → Device"Slave_1"
│
├── 方式B（自定义）：手动创建子设备
│   ├── 创建子设备"Chiller1"
│   ├── 选择点位来源：
│   │   ├── 从 Slave1 选：40001 → supplyTemp
│   │   ├── 从 Slave1 选：40002 → returnTemp
│   │   └── 从 Slave2 选：40001 → condenserPressure  ← 跨从站
│   └── 保存
│
└── 方式C（模板）：选择产品模板"冷水机组"
    ├── 系统自动加载模板点位
    ├── 用户拖拽绑定到采集点位
    └── 保存

步骤3：启用采集
├── 保存配置到数据库
├── 采集引擎加载配置
├── 首次采集成功 → 自动创建子设备
└── 前端看到"冷水机组1"设备，有实时数据
```

### 7.5 数据库设计说明

**核心原则：复用现有 `Device` 表，不创建重复子设备表。**

子设备就是 `Device` 表中 `DeviceType = Device` 且 `Owner = Gateway` 的记录，通过现有 `JudgeOrCreateNewDevice` 机制创建。

采集配置只存"协议层到业务层的映射关系"：

| 表 | 用途 | 是否新建 |
|---|------|---------|
| `Devices` | 网关和子设备（复用） | ❌ 已有 |
| `CollectionTasks` | 采集任务 | ✅ 新建 |
| `CollectionDevices` | 采集从站（协议层） | ✅ 新建 |
| `CollectionPoints` | 采集点位（含 TargetDeviceId 映射） | ✅ 新建 |
| `CollectionLogs` | 采集日志 | ✅ 新建 |

**关键字段**：`CollectionPoints.TargetDeviceId` 直接外键关联 `Devices` 表，无需中间映射表。

### 7.6 关键灵活性

| 场景 | 处理方式 |
|------|---------|
| 1从站=1设备 | 一键生成，默认模式 |
| 多从站=1设备 | 多个点位配置相同 TargetDeviceId |
| 1从站=多设备 | 不同点位配置不同 TargetDeviceId |
| 点位重命名 | 修改 CollectionPoints.TargetName |
| 点位换子设备 | 修改 CollectionPoints.TargetDeviceId |
| 新增子设备 | 创建 Device 记录，配置点位映射 |
| 删除子设备 | 删除 Device 记录，解除点位映射 |

---

## 八、与现有文档的关系

