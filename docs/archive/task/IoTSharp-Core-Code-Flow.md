# IoTSharp 核心代码运行逻辑详解

> 基于 IoTSharp v3.5.0 源码分析 | 基于实际代码生成的运行逻辑文档  
> 生成日期：2026年4月14日

---

## 一、系统架构总览

### 1.1 数据流向宏观视图

IoTSharp 的核心运行逻辑可以概括为一条**数据管道**：

```
设备接入 → 协议解析 → 事件发布 → 事件订阅 → 数据存储 → 规则触发 → 业务处理
```

这是一个**事件驱动架构 (EDA)**，所有业务逻辑都通过事件解耦。

### 1.2 核心分层架构

| 层次 | 职责 | 关键组件 |
|------|------|---------|
| **协议接入层** | 多协议设备接入认证 | MQTTService、CoAPService、Controllers |
| **事件发布层** | 将数据转换为事件发布 | IPublisher、PlayloadData |
| **事件总线层** | 异步事件分发 | CAP/Shashlik 消息队列 |
| **事件订阅层** | 接收并处理事件 | ISubscriber、EventBusSubscriber |
| **数据存储层** | 持久化数据 | IStorage、ApplicationDbContext |
| **规则引擎层** | 业务逻辑编排 | FlowRuleProcessor、ScriptEngine |
| **任务执行层** | 执行具体动作 | TaskAction 执行器 |

### 1.3 核心数据流转图

```
┌─────────────────────────────────────────────────────────────────────┐
│                          设备 (Device)                               │
│  MQTT / HTTP / CoAP                                                 │
└──────────────────┬──────────────────────────────────────────────────┘
                   │ 1. 原始数据 (JSON/XML/Binary)
                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     协议接入层 (Gateway/Service)                      │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────────┐   │
│  │ MQTTService  │  │ CoAPService  │  │ Controllers (REST API)  │   │
│  │ + Topic路由  │  │ + Resource   │  │ + AccessToken验证       │   │
│  │ + 设备认证   │  │ + 数据解析   │  │ + 数据封装              │   │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬──────────────┘   │
└─────────┼─────────────────┼─────────────────────┼──────────────────┘
          │                 │                     │
          │ 2. 转换为 PlayloadData {DeviceId, MsgBody, DataSide}
          ▼                 ▼                     ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     事件发布层 (IPublisher)                           │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │ CapPublisher / ShashlikPublisher                             │   │
│  │ • PublishTelemetryData() → "datastream.telemetrydata"       │   │
│  │ • PublishAttributeData() → "datastream.attributedata"       │   │
│  │ • PublishDeviceAlarm() → "datastream.alarm"                 │   │
│  │ • PublishConnect() → "platform.connect"                     │   │
│  │ • PublishActive() → "platform.active"                       │   │
│  └──────────────────────────┬──────────────────────────────────┘   │
└─────────────────────────────┼──────────────────────────────────────┘
                              │ 3. 消息入队 (RabbitMQ/Kafka/ZeroMQ/InMemory)
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       事件总线层 (EventBus)                           │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │ CAP (Cloud Application Platform)                            │   │
│  │ • 消息持久化 (EventStore: PostgreSQL/MongoDB/MySQL)         │   │
│  │ • 重试机制 (失败自动重试)                                    │   │
│  │ • 顺序保证 (同一Topic顺序消费)                              │   │
│  └──────────────────────────┬──────────────────────────────────┘   │
└─────────────────────────────┼──────────────────────────────────────┘
                              │ 4. 消息投递到订阅者
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     事件订阅层 (ICapSubscribe)                        │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │ CapSubscriber                                                │   │
│  │ • [CapSubscribe("datastream.telemetrydata")]                 │   │
│  │   → StoreTelemetryData()                                     │   │
│  │ • [CapSubscribe("datastream.attributedata")]                 │   │
│  │   → StoreAttributeData()                                     │   │
│  │ • [CapSubscribe("datastream.alarm")]                         │   │
│  │   → OccurredAlarm()                                          │   │
│  └──────────────────────────┬──────────────────────────────────┘   │
└─────────────────────────────┼──────────────────────────────────────┘
                              │ 5. 调用基类处理逻辑
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   业务处理层 (EventBusSubscriber)                     │
│  ┌───────────────────────┐  ┌──────────────────────────────┐      │
│  │ IStorage              │  │ FlowRuleProcessor             │      │
│  │ • StoreTelemetryAsync │  │ • RunRules()                  │      │
│  │ • 写入时序库          │  │ • 加载规则链                  │      │
│  │ • 返回存储结果        │  │ • 逐节点执行                  │      │
│  └───────────┬───────────┘  └──────────────┬───────────────┘      │
└──────────────┼─────────────────────────────┼──────────────────────┘
               │ 6. 数据存储完成              │ 7. 触发规则
               ▼                              ▼
┌──────────────────────┐  ┌─────────────────────────────────────────┐
│   时序数据库          │  │         规则引擎 (FlowRuleProcessor)     │
│ • PostgreSQL         │  │  ┌───────────────────────────────────┐ │
│ • InfluxDB           │  │  │ 加载 FlowRule + Flows (BPMN XML)  │ │
│ • TDengine           │  │  │ 从 StartEvent 开始遍历节点        │ │
│ • TimescaleDB        │  │  │ 执行脚本 (JS/C#/Python/Lua/SQL)   │ │
│ • IoTDB              │  │  │ 执行任务执行器 (TaskAction)       │ │
│ • ClickHouse         │  │  │ 条件判断 → 分支路由               │ │
│                      │  │  │ 可能级联发布新事件                │ │
│                      │  │  └───────────────────────────────────┘ │
└──────────────────────┘  └─────────────────────────────────────────┘
```

---

## 二、设备接入详解

### 2.1 MQTT 协议接入（核心入口）

**文件路径**: `IoTSharp\Services\MQTTService.cs`

#### 2.1.1 设备连接认证流程

```csharp
// 行 136-244: Server_ClientConnectionValidator 方法
public async Task Server_ClientConnectionValidator(ClientConnectionValidatorContext obj)
{
    // 1. 检查本地回环地址（内置Broker免认证）
    if (IsLocalHostIPAddress()) { 允许连接; return; }

    // 2. 通过设备名查找设备身份
    var dev = await _dbContext.DeviceIdentities
        .Include(d => d.Device)
        .FirstOrDefaultAsync(d => d.IdentityId == obj.UserName);

    // 3. 根据认证方式验证
    switch (dev.IdentityType)
    {
        case IdentityType.AccessToken:
            if (dev.IdentityId == obj.UserName) 允许连接;
            break;
            
        case IdentityType.DevicePassword:
            if (dev.IdentityId == obj.UserName && 
                dev.IdentityValue == obj.Password) 允许连接;
            break;
            
        case IdentityType.X509Certificate:
            if (dev.IdentityId == 证书Thumbprint) 允许连接;
            break;
    }

    // 4. 验证通过：将Device对象存入会话
    if (obj.Succeeded)
    {
        obj.SessionItems.Add(nameof(Device), dev.Device);
        // 发布设备连接事件
        await _queue.PublishConnect(dev.DeviceId, ConnectStatus.Connected);
    }
}
```

**认证方式对比**:

| 认证方式 | 用户名 | 密码 | 安全性 | 适用场景 |
|---------|--------|------|--------|---------|
| AccessToken | Token字符串 | 任意 | 低 | 内网设备 |
| DevicePassword | 设备名 | 密码 | 中 | 常规设备 |
| X509Certificate | 证书指纹 | 无 | 高 | 关键设备 |

#### 2.1.2 MQTT Topic 路由体系

IoTSharp 的 MQTT 控制器采用**属性路由**机制，Topic 模式匹配到方法：

**基础控制器**: `IoTSharp\Services\MQTTControllers\`

| 控制器文件 | Topic 模式 | 处理方法 | 行号 | 功能说明 |
|-----------|-----------|---------|------|---------|
| `TelemetryController.cs` | `devices/{devname}/telemetry` | `telemetry()` | 82-96 | 遥测数据上报（JSON字典） |
| `TelemetryController.cs` | `devices/{devname}/telemetry/xml` | `telemetry_xml()` | 56-69 | XML格式遥测 |
| `TelemetryController.cs` | `devices/{devname}/telemetry/binary` | `telemetry_binary()` | 71-81 | 二进制遥测 |
| `AttributesController.cs` | `devices/{devname}/attributes` | `attributes()` | 105-119 | 属性数据上报 |
| `AttributesController.cs` | `devices/{devname}/attributes/request` | `RequestAttributes()` | 148-174 | 请求设备属性 |
| `AlarmController.cs` | `devices/{devname}/alarm` | `UpdateStatus()` | 50-56 | 告警上报 |
| `GatewayController.cs` | `v1/gateway/telemetry` | `telemetry()` | 54-75 | ThingsBoard兼容网关 |
| `GatewayController.cs` | `v1/gateway/attributes` | `Attributes()` | 77-99 | ThingsBoard兼容网关 |
| `GatewayController.cs` | `gateway/{devname}/connect` | `on_connect()` | 100-119 | 子设备连接 |
| `GatewayController.cs` | `gateway/{devname}/disconnect` | `Disconnect()` | 138-159 | 子设备断开 |
| `DataController.cs` | `devices/{devname}/data/json` | `UploadJsonData()` | 184-195 | 原始JSON映射 |
| `DataController.cs` | `devices/{devname}/data/xml` | `UploadXmlData()` | 169-181 | 原始XML映射 |

#### 2.1.3 TelemetryController 遥测处理详细流程

**文件**: `IoTSharp\Services\MQTTControllers\TelemetryController.cs`

```csharp
// 行 82-96
[MqttRoute("devices/{devname}/telemetry")]
public async Task<IActionResult> telemetry(string devname, [FromBody] object body)
{
    // 1. 通过设备名查找设备
    var device = FindDevice(devname);
    if (device == null) return NotFound();

    // 2. 将请求体转换为字典
    var keyValues = Message.ConvertPayloadToDictionary(body);

    // 3. 发布遥测事件到事件总线
    await _queue.PublishTelemetryData(device, keyValues);
    
    return Ok();
}
```

**数据转换过程**:
```
HTTP/MQTT Body (JSON)
  ↓
{
  "temperature": 25.5,
  "humidity": 60,
  "status": "online"
}
  ↓
Message.ConvertPayloadToDictionary()
  ↓
Dictionary<string, object>
{
  ["temperature"] = 25.5 (double),
  ["humidity"] = 60 (long),
  ["status"] = "online" (string)
}
  ↓
PublishTelemetryData(device, keyValues)
  ↓
PlayloadData {
  DeviceId = device.Id,
  MsgBody = Dictionary,
  DataSide = DataSide.ClientSide,
  DataCatalog = DataCatalog.TelemetryData,
  ts = DateTime.UtcNow
}
```

#### 2.1.4 GatewayController 网关处理逻辑

**文件**: `IoTSharp\Services\MQTTControllers\GatewayController.cs`

**网关数据格式**:
```json
{
  "deviceA": [
    {"ts": 1234567890, "values": {"temperature": 25}},
    {"ts": 1234567891, "values": {"humidity": 60}}
  ],
  "deviceB": [
    {"ts": 1234567890, "values": {"pressure": 1013}}
  ]
}
```

**处理流程** (行 54-75):
```csharp
public async Task<IActionResult> telemetry(string devname, [FromBody] Dictionary<string, List<GatewayPlayload>> gwData)
{
    // 1. 遍历网关上报的每个子设备
    foreach (var kv in gwData)
    {
        string subdevname = kv.Key;  // "deviceA", "deviceB"
        
        // 2. 判断或创建子设备（通过网关自动注册）
        var subDevice = await JudgeOrCreateNewDevice(subdevname, gateway);
        
        // 3. 遍历该设备的每条遥测数据
        foreach (var t in kv.Value)
        {
            // 4. 构建 PlayloadData
            var playload = new PlayloadData()
            {
                DeviceId = subDevice.Id,
                ts = t.ts,
                MsgBody = t.values,
                DataSide = DataSide.ClientSide,
                DataCatalog = DataCatalog.TelemetryData
            };
            
            // 5. 发布遥测事件
            await _queue.PublishTelemetryData(playload);
        }
    }
}
```

**核心设计**: 网关可以代理多个子设备的数据，平台自动创建或查找子设备并逐条发布事件。

### 2.2 HTTP REST API 接入

**文件**: `IoTSharp\Controllers\DevicesController.cs`

#### 2.2.1 数据上报接口

| 路由 | 方法 | 行号 | 功能 | 参数 |
|------|------|------|------|------|
| `POST /api/{access_token}/Telemetry` | `Telemetry()` | 1003-1017 | HTTP遥测上报 | `Dictionary<string, object>` |
| `POST /api/{access_token}/Attributes` | `Attributes()` | 1063-1079 | HTTP属性上报 | `Dictionary<string, object>` |
| `POST /api/{access_token}/Alarm` | `Alarm()` | 1087+ | HTTP告警上报 | `CreateAlarmDto` |

#### 2.2.2 Telemetry 方法详解

```csharp
// 行 1003-1017
[HttpPost("{access_token}/Telemetry")]
public async Task<IActionResult> Telemetry(string access_token, [FromBody] Dictionary<string, object> telemetrys)
{
    // 1. 通过 access_token 查找设备
    var (ok, device) = _context.GetDeviceByToken(access_token);
    if (!ok) return NotFound();

    // 2. 发布设备活跃事件（保活）
    _queue.PublishActive(device.Id, ActivityStatus.Activity);
    
    // 3. 发布遥测数据
    _queue.PublishTelemetryData(new PlayloadData()
    {
        DeviceId = device.Id,
        MsgBody = telemetrys,
        DataSide = DataSide.ClientSide,
        DataCatalog = DataCatalog.TelemetryData
    });

    return Ok();
}
```

**与MQTT的区别**:
- HTTP 是无状态的，每次请求都需要携带 `access_token`
- MQTT 在连接时认证，后续Topic路由只需设备名
- HTTP 适合低频上报，MQTT 适合高频实时数据

### 2.3 CoAP 协议接入

**文件**: `IoTSharp\Services\CoAPService.cs`

#### 2.3.1 CoAP 资源路由

```csharp
// CoApResource.cs 行 47-135
public class CoAPResource : Resource
{
    protected override void DoPost(CoapExchange exchange)
    {
        // 1. 解析 Content-Type
        MediaType format = GetFormat(exchange);
        
        // 2. 通过 AccessToken 查询设备
        var device = FindDeviceByToken(exchange.Uri);
        
        // 3. 根据资源类型发布事件
        switch (resourceType)
        {
            case CoApRes.Attributes:
                _eventBus.PublishAttributeData(data);
                break;
            case CoApRes.Telemetry:
                _eventBus.PublishTelemetryData(data);
                break;
            case CoApRes.Alarm:
                _eventBus.PublishDeviceAlarm(data);
                break;
        }
    }
}
```

**CoAP vs MQTT vs HTTP**:

| 特性 | CoAP | MQTT | HTTP |
|------|------|------|------|
| 传输层 | UDP | TCP | TCP |
| 开销 | 极低 (4字节头) | 低 (2字节头) | 高 |
| 适用场景 | 低功耗设备 | 实时设备 | Web API |
| QoS | 支持4种 | 支持3种 | 无 |
| 订阅机制 | 观察者模式 | Topic订阅 | 长轮询/WebSocket |

### 2.4 RawDataGateway 原始数据映射

**文件**: `IoTSharp\Gateways\RawDataGateway.cs`

#### 2.4.1 映射规则常量

```csharp
public const string _map_to_telemetry_ = "_map_to_telemetry_";    // 映射到遥测
public const string _map_to_attribute_ = "_map_to_attribute_";    // 映射到属性
public const string _map_to_devname = "_map_to_devname";          // 映射设备名
public const string _map_to_subdevname = "_map_to_subdevname";    // 映射子设备名
public const string _map_to_jsontext_in_json = "_map_to_jsontext_in_json";  // JSON提取
public const string _map_to_data_in_array = "_map_to_data_in_array";        // 数组提取
public const string _map_var_ts_format = "_map_var_ts_format";    // 时间戳格式
public const string _map_var_ts_field = "_map_var_ts_field";      // 时间戳字段
```

#### 2.4.2 映射规则配置方式

通过设备的 **AttributeLatest** 表配置映射规则（属性名前缀为 `_map_to_`）：

```json
{
  "_map_to_devname": "Sensor_{serial_number}",
  "_map_to_data_in_array": "$.data.records",
  "_map_to_telemetry_temperature": "$.sensors[0].value",
  "_map_to_attribute_status": "$.device.status",
  "_map_var_ts_field": "$.timestamp",
  "_map_var_ts_format": "unix_ms"
}
```

#### 2.4.3 执行流程 (行 56-143)

```csharp
public async Task ExecuteAsync(string devname, object data)
{
    // 1. 解析输入数据 (JSON/XML)
    var obj = ParseData(data);
    
    // 2. 从 AttributeLatest 读取映射规则
    var rules = await _dbContext.AttributeLatest
        .Where(d => d.DeviceId == deviceId && d.KeyName.StartsWith("_map_to_"))
        .ToListAsync();
    
    // 3. 提取数据数组
    var dataArray = ExtractArray(obj, rules);
    
    // 4. 构建最终设备名 (支持格式模板)
    var finalDevName = BuildDeviceName(obj, rules);
    
    // 5. 遍历数据数组
    foreach (var item in dataArray)
    {
        var telemetry = new Dictionary<string, object>();
        var attributes = new Dictionary<string, object>();
        
        // 6. 根据前缀分类字段
        foreach (var rule in rules)
        {
            var value = ExtractValue(item, rule);
            if (rule.KeyName.StartsWith("_map_to_telemetry_"))
                telemetry.Add(key, value);
            else if (rule.KeyName.StartsWith("_map_to_attribute_"))
                attributes.Add(key, value);
        }
        
        // 7. 发布事件
        if (telemetry.Any()) 
            await _queue.PublishTelemetryData(finalDevName, telemetry);
        if (attributes.Any())
            await _queue.PublishAttributeData(finalDevName, attributes);
    }
}
```

**应用场景**: 将第三方系统、旧设备、非标准协议的数据映射为 IoTSharp 标准格式。

---

## 三、事件发布机制详解

### 3.1 IPublisher 接口

**文件**: `IoTSharp.EventBus\IPublisher.cs`

```csharp
public interface IPublisher
{
    Task PublishAttributeData(PlayloadData msg);
    Task PublishTelemetryData(PlayloadData msg);
    Task PublishDeviceAlarm(CreateAlarmDto alarmDto);
    Task PublishCreateDevice(Guid devid);
    Task PublishDeleteDevice(Guid devid);
    Task PublishConnect(Guid devid, ConnectStatus devicestatus);
    Task PublishActive(Guid devid, ActivityStatus activity);
    Task<EventBusMetrics> GetMetrics();
}
```

### 3.2 PlayloadData 数据结构

**文件**: `IoTSharp.Data\PlayloadData.cs`

```csharp
public class PlayloadData
{
    public DateTime ts { get; set; } = DateTime.UtcNow;      // 时间戳
    public Guid DeviceId { get; set; }                        // 设备ID
    public Dictionary<string, object> MsgBody { get; set; }   // 消息体
    public DataSide DataSide { get; set; }                    // 数据方向
    public DataCatalog DataCatalog { get; set; }              // 数据目录
}
```

**DataSide 枚举**:
- `ClientSide` - 设备端上报
- `ServerSide` - 服务端生成
- `AnySide` - 任意方向

**DataCatalog 枚举**:
- `AttributeLatest` - 最新属性
- `TelemetryData` - 历史遥测
- `ProduceData` - 产品数据

### 3.3 CapPublisher 实现

**文件**: `IoTSharp.EventBus.CAP\CapPublisher.cs`

```csharp
public class CapPublisher : IPublisher
{
    private readonly ICapPublisher _queue;  // CAP 框架的发布器
    
    public async Task PublishTelemetryData(PlayloadData msg)
    {
        // 发布到 Topic: "iotsharp.services.datastream.telemetrydata"
        await _queue.PublishAsync("iotsharp.services.datastream.telemetrydata", msg);
    }

    public async Task PublishConnect(Guid devid, ConnectStatus devicestatus)
    {
        // 发布到 Topic: "iotsharp.services.platform.connect"
        await _queue.PublishAsync("iotsharp.services.platform.connect", 
            new DeviceConnectStatus(devid, devicestatus));
    }
}
```

### 3.4 Topic 命名规范

| Topic 名称 | 前缀 | 用途 | 数据类型 |
|-----------|------|------|---------|
| `iotsharp.services.datastream.attributedata` | datastream | 属性数据 | PlayloadData |
| `iotsharp.services.datastream.telemetrydata` | datastream | 遥测数据 | PlayloadData |
| `iotsharp.services.datastream.alarm` | datastream | 告警数据 | CreateAlarmDto |
| `iotsharp.services.platform.createdevice` | platform | 设备创建 | Guid |
| `iotsharp.services.platform.deleteDevice` | platform | 设备删除 | Guid |
| `iotsharp.services.platform.connect` | platform | 连接状态 | DeviceConnectStatus |
| `iotsharp.services.platform.active` | platform | 活跃状态 | DeviceActivityStatus |

**命名规则**:
- `datastream.*` - 数据流相关（持续产生的数据）
- `platform.*` - 平台事件相关（状态变更、生命周期事件）

---

## 四、事件订阅与业务处理

### 4.1 CapSubscriber 订阅器

**文件**: `IoTSharp.EventBus.CAP\CapSubscriber.cs`

```csharp
public class CapSubscriber : EventBusSubscriber, ISubscriber, ICapSubscribe
{
    [CapSubscribe("iotsharp.services.datastream.attributedata")]
    public async Task attributedata(PlayloadData msg)
    {
        await StoreAttributeData(msg);  // 调用基类方法
    }

    [CapSubscribe("iotsharp.services.datastream.telemetrydata")]
    public async Task telemetrydata(PlayloadData msg)
    {
        await StoreTelemetryData(msg);
    }

    [CapSubscribe("iotsharp.services.datastream.alarm")]
    public async Task alarm(CreateAlarmDto alarmDto)
    {
        await OccurredAlarm(alarmDto);
    }

    [CapSubscribe("iotsharp.services.platform.createdevice")]
    public async Task createdevice(Guid deviceId)
    {
        await CreateDevice(deviceId);
    }

    [CapSubscribe("iotsharp.services.platform.connect")]
    public async Task connect(DeviceConnectStatus status)
    {
        await Connect(status.DeviceId, status.ConnectStatus);
    }

    [CapSubscribe("iotsharp.services.platform.active")]
    public async Task active(DeviceActivityStatus status)
    {
        await base.Active(status.DeviceId, status.Activity);
    }
}
```

### 4.2 EventBusSubscriber 基类业务逻辑

**文件**: `IoTSharp.EventBus\EventBusSubscriber.cs`

#### 4.2.1 StoreTelemetryData 详细流程

```csharp
public async Task StoreTelemetryData(PlayloadData msg)
{
    try
    {
        // 1. 调用 IStorage 存储遥测数据
        var result = await _storage.StoreTelemetryAsync(msg);
        
        // 2. 将存储结果转换为 DTO
        var data = from t in result.telemetries
                   select new TelemetryDataDto() 
                   { 
                       DateTime = t.DateTime, 
                       DataType = t.Type, 
                       KeyName = t.KeyName, 
                       Value = t.ToObject() 
                   };
        
        var array = data.ToList();
        
        // 3. 转换为动态对象 (ExpandoObject)
        ExpandoObject exps = array.ToDynamic();
        
        // 4. 触发规则引擎 (两种形式)
        await RunRules(msg.DeviceId, (dynamic)exps, EventType.Telemetry);
        await RunRules(msg.DeviceId, array, EventType.TelemetryArray);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "StoreTelemetryData:" + ex.Message);
    }
}
```

**关键点**:
1. **先存储后触发规则** - 保证规则引擎处理的是已持久化的数据
2. **双重触发** - 同时触发 `Telemetry` (对象) 和 `TelemetryArray` (数组) 两种事件类型
3. **异常捕获** - 规则执行失败不影响数据存储

#### 4.2.2 StoreAttributeData 详细流程

```csharp
public async Task StoreAttributeData(PlayloadData msg, EventType _event)
{
    using (var _scope = _scopeFactor.CreateScope())
    using (var _dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
    {
        // 1. 查找设备
        var device = _dbContext.Device.FirstOrDefault(d => d.Id == msg.DeviceId);
        if (device != null)
        {
            var dc = msg.ToDictionary();
            
            // 2. 保存到 AttributeLatest 表
            var result2 = await _dbContext.SaveAsync<AttributeLatest>(dc, device.Id, msg.DataSide);
            
            // 3. 记录保存异常
            result2.exceptions?.ToList().ForEach(ex =>
            {
                _logger.LogError($"{ex.Key} {ex.Value} {JsonConvert.SerializeObject(msg.MsgBody[ex.Key])}");
            });
            
            _logger.LogInformation($"更新{device.Name}({device.Id})属性数据结果{result2.ret}");
            
            // 4. 触发规则引擎 (如果事件类型不是 None)
            if (_event != EventType.None)
            {
                await RunRules(msg.DeviceId, dc.ToDynamic(), _event);
            }
        }
    }
}
```

**关键点**:
1. **Scope 模式** - 每次执行创建新的 DI 作用域，避免 DbContext 线程安全问题
2. **属性更新** - `SaveAsync<AttributeLatest>` 是 UPSERT 操作（存在则更新，不存在则插入）
3. **异常不阻断** - 即使规则执行失败，属性数据也已保存

#### 4.2.3 Connect 方法 - 连接状态更新

```csharp
public async Task Connect(Guid devid, ConnectStatus devicestatus)
{
    // 1. 构建属性数据
    var msg = new PlayloadData();
    msg.DeviceId = devid;
    msg.DataCatalog = DataCatalog.AttributeData;
    msg.DataSide = DataSide.ServerSide;
    msg.MsgBody = new Dictionary<string, object>();
    
    // 2. 设置连接状态属性
    msg.MsgBody.Add(Constants._Connected, 
        devicestatus == ConnectStatus.Connected);
    msg.MsgBody.Add(
        devicestatus == ConnectStatus.Connected 
            ? Constants._LastConnectDateTime 
            : Constants._LastDisconnectDateTime, 
        DateTime.UtcNow);
    
    // 3. 存储属性并触发规则
    await StoreAttributeData(msg, 
        devicestatus == ConnectStatus.Connected 
            ? EventType.Connected 
            : EventType.Disconnected);
}
```

**写入的属性**:
- `_Connected` = true/false
- `_LastConnectDateTime` 或 `_LastDisconnectDateTime` = 当前时间

**触发的规则**: `EventType.Connected` 或 `EventType.Disconnected`

#### 4.2.4 Active 方法 - 活跃状态更新

```csharp
public async Task Active(Guid devid, ActivityStatus activity)
{
    var msg = new PlayloadData();
    msg.DeviceId = devid;
    msg.DataCatalog = DataCatalog.AttributeData;
    msg.DataSide = DataSide.ServerSide;
    msg.MsgBody = new Dictionary<string, object>();
    
    // 设置活跃状态属性
    msg.MsgBody.Add(
        activity == ActivityStatus.Activity 
            ? Constants._LastActivityDateTime 
            : Constants._InactivityAlarmDateTime, 
        DateTime.UtcNow);
    msg.MsgBody.Add(Constants._Active, 
        activity == ActivityStatus.Activity);
    
    // 存储属性并触发规则
    await StoreAttributeData(msg, 
        activity == ActivityStatus.Activity 
            ? EventType.Activity 
            : EventType.Inactivity);
}
```

**写入的属性**:
- `_Active` = true/false
- `_LastActivityDateTime` 或 `_InactivityAlarmDateTime` = 当前时间

### 4.3 OccurredAlarm 告警处理

```csharp
public async Task OccurredAlarm(CreateAlarmDto alarmDto)
{
    using (var _scope = _scopeFactor.CreateScope())
    using (var _dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
    {
        // 1. 调用扩展方法创建/更新告警
        var alm = await _dbContext.OccurredAlarm(alarmDto);
        
        if (alm.Code == (int)ApiCode.Success)
        {
            alarmDto.warnDataId = alm.Data.Id;
            alarmDto.CreateDateTime = alm.Data.AckDateTime;
            
            // 2. 如果告警设置了传播标志，触发规则链
            if (alm.Data.Propagate)
            {
                await RunRules(alm.Data.OriginatorId, alarmDto, EventType.Alarm);
            }
        }
        else
        {
            // 设备可能还未创建，告警先来
            _logger.LogWarning($"处理{alarmDto.OriginatorName} 的告警{alarmDto.AlarmType} 错误:{alm.Code}-{alm.Msg}");
        }
    }
}
```

**告警处理特殊逻辑**:
- 通过 `Propagate` 标志控制是否触发规则链
- 如果设备通过网关创建，可能出现"告警先来，设备创建在后"的竞态条件

---

## 五、数据存储层详解

### 5.1 IStorage 接口

**文件**: `IoTSharp.Data.TimeSeries\IStorage.cs`

```csharp
public interface IStorage
{
    // 检查时序存储连接
    Task<bool> CheckTelemetryStorage();
    
    // 存储遥测数据
    Task<(bool result, List<TelemetryData> telemetries)> StoreTelemetryAsync(PlayloadData msg);
    
    // 获取设备最新遥测
    Task<List<TelemetryDataDto>> GetTelemetryLatest(Guid deviceId);
    
    // 获取指定Key的最新遥测
    Task<List<TelemetryDataDto>> GetTelemetryLatest(Guid deviceId, string keys);
    
    // 加载历史遥测（支持聚合）
    Task<List<TelemetryDataDto>> LoadTelemetryAsync(
        Guid deviceId, 
        string keys,
        DateTime begin, 
        DateTime end, 
        TimeSpan every, 
        Aggregate aggregate);
}
```

### 5.2 存储实现矩阵

| 存储模式 | 实现类 | 文件 | 适用场景 | 性能特点 |
|---------|--------|------|---------|---------|
| `SingleTable` | `EFStorage` | `EFStorage.cs` | 小规模部署 (<100设备) | 简单，但数据量大时性能下降 |
| `Sharding` | `ShardingStorage` | `ShardingStorage.cs` | 大规模关系数据库 | 按时间分表，查询需要路由 |
| `InfluxDB` | `InfluxDBStorage` | `InfluxDBStorage.cs` | 专业时序场景 | 高写入吞吐，压缩率高 |
| `Taos` | `TaosStorage` | `TaosStorage.cs` | TDengine 部署 | 国产时序库，IoT优化 |
| `TimescaleDB` | `TimescaleDBStorage` | `TimescaleDBStorage.cs` | PostgreSQL 生态 | 基于PG，兼容性好 |
| `IoTDB` | `IoTDBStorage` | `IoTDBStorage.cs` | Apache 生态 | 轻量级，边缘计算 |
| `PinusDB` | `PinusDBStorage` | `PinusDBStorage.cs` | 国产替代 | 轻量时序库 |

### 5.3 EFStorage 单表存储详解

**文件**: `IoTSharp.Data.TimeSeries\EFStorage.cs`

```csharp
public class EFStorage : IStorage
{
    private readonly IServiceScopeFactory _scopeFactor;
    
    public async Task<(bool result, List<TelemetryData> telemetries)> StoreTelemetryAsync(PlayloadData msg)
    {
        using var scope = _scopeFactor.CreateScope();
        using var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var telemetries = new List<TelemetryData>();
        
        // 遍历 MsgBody 中的每个 Key-Value 对
        foreach (var item in msg.MsgBody)
        {
            // 1. 创建 TelemetryData 对象
            var td = new TelemetryData()
            {
                DeviceId = msg.DeviceId,
                DateTime = msg.ts,
                KeyName = item.Key,
                DataSide = msg.DataSide,
                Catalog = DataCatalog.TelemetryData
            };
            
            // 2. 根据值类型设置对应字段
            td.FillValue(item.Value);
            
            telemetries.Add(td);
        }
        
        // 3. 批量添加到 DbContext
        _dbContext.TelemetryData.AddRange(telemetries);
        
        // 4. 保存到数据库
        await _dbContext.SaveChangesAsync();
        
        return (true, telemetries);
    }
}
```

**FillValue 方法逻辑**:
```csharp
public void FillValue(object value)
{
    switch (value)
    {
        case bool b:
            Type = DataType.Boolean;
            Value_Boolean = b;
            break;
        case string s:
            Type = DataType.String;
            Value_String = s;
            break;
        case long l:
            Type = DataType.Long;
            Value_Long = l;
            break;
        case double d:
            Type = DataType.Double;
            Value_Double = d;
            break;
        case DateTime dt:
            Type = DataType.DateTime;
            Value_DateTime = dt;
            break;
    }
}
```

### 5.4 ShardingStorage 分表存储

**文件**: `IoTSharp.Data.TimeSeries\ShardingStorage.cs`

**分片策略** (通过 ShardingCore):

| 路由类 | 分片粒度 | 表名格式 | 适用场景 |
|--------|---------|---------|---------|
| `TelemetryDataMinuteRoute` | 分钟 | `TelemetryData_20260414_1530` | 高频数据 (秒级) |
| `TelemetryDataHourRoute` | 小时 | `TelemetryData_20260414_15` | 中频数据 (分钟级) |
| `TelemetryDataDayRoute` | 天 | `TelemetryData_20260414` | 低频数据 (小时级) |
| `TelemetryDataMonthRoute` | 月 | `TelemetryData_202604` | 常规数据 (天级) |
| `TelemetryDataYearRoute` | 年 | `TelemetryData_2026` | 归档数据 (月级) |

**分片配置** (在 Startup.cs 中):
```csharp
// 根据 ShardingByDateMode 配置选择分片模式
switch (settings.ShardingByDateMode)
{
    case ShardingByDateMode.PerMinute:
        options.AddShardingTransaction(t => t.CreateTelemetryMinute());
        break;
    case ShardingByDateMode.PerHour:
        options.AddShardingTransaction(t => t.CreateTelemetryHour());
        break;
    // ...
}
```

**查询路由逻辑**:
```
LoadTelemetryAsync(begin: 2026-04-14, end: 2026-04-15)
  ↓
计算需要查询的分片:
  - TelemetryData_20260414
  - TelemetryData_20260415
  ↓
生成 UNION ALL 查询
  ↓
返回合并结果
```

### 5.5 DataStorage 数据模型

**文件**: `IoTSharp.Data\DataStorage.cs`

**TPH (Table Per Hierarchy) 模式**:

```csharp
public abstract class DataStorage
{
    public DataCatalog Catalog { get; set; }      // 鉴别器
    public Guid DeviceId { get; set; }
    public string KeyName { get; set; }
    public DateTime DateTime { get; set; }
    public DataSide DataSide { get; set; }
    public DataType Type { get; set; }
    
    // 值字段 (根据 Type 使用不同字段)
    public bool? Value_Boolean { get; set; }
    public string Value_String { get; set; }
    public long? Value_Long { get; set; }
    public DateTime? Value_DateTime { get; set; }
    public double? Value_Double { get; set; }
    public string Value_Json { get; set; }
    public string Value_XML { get; set; }
    public byte[] Value_Binary { get; set; }
}

// 子类 (仅用于查询鉴别)
public class AttributeLatest : DataStorage { }
public class TelemetryLatest : DataStorage { }
public class TelemetryData : DataStorage { }
public class ProduceData : DataStorage { }
```

**EF Core 配置** (ApplicationDbContext.cs):
```csharp
modelBuilder.Entity<AttributeLatest>()
    .HasDiscriminator<DataCatalog>(nameof(DataStorage.Catalog))
    .HasValue(DataCatalog.AttributeLatest);

modelBuilder.Entity<TelemetryLatest>()
    .HasDiscriminator<DataCatalog>(nameof(DataStorage.Catalog))
    .HasValue(DataCatalog.TelemetryLatest);

modelBuilder.Entity<TelemetryData>()
    .HasDiscriminator<DataCatalog>(nameof(DataStorage.Catalog))
    .HasValue(DataCatalog.TelemetryData);
```

**数据库中的表现**:
```
表: TelemetryData
┌─────────┬──────────┬───────────┬────────────┬──────┬───────────────┬───────────────┐
│ Catalog │ DeviceId │ KeyName   │ DateTime   │ Type │ Value_Double  │ Value_String  │ ...
├─────────┼──────────┼───────────┼────────────┼──────┼───────────────┼───────────────┤
│ TelemetryData │ GUID │ temperature │ 2026-04-14 │ Double │ 25.5 │ NULL │ ...
│ TelemetryData │ GUID │ status │ 2026-04-14 │ String │ NULL │ "online" │ ...
└─────────┴──────────┴───────────┴────────────┴──────┴───────────────┴───────────────┘

鉴别器 Catalog 区分:
  - AttributeLatest → 最新属性 (每个 DeviceId+KeyName 仅保留最新一条)
  - TelemetryLatest → 最新遥测 (每个 DeviceId+KeyName 仅保留最新一条)
  - TelemetryData → 历史遥测 (保留所有记录)
```

---

## 六、规则引擎详解

### 6.1 FlowRuleProcessor 核心类

**文件**: `IoTSharp.FlowRuleEngine\FlowRuleProcessor.cs`

#### 6.1.1 RunRules 入口方法

```csharp
public async Task RunRules(Guid devid, object obj, EventType mountType)
{
    // 1. 构建缓存键 (设备ID + 事件类型)
    var cacheKey = $"ruleid_{devid}_{mountType}";
    
    // 2. 尝试从缓存获取规则ID列表
    if (!_caching.TryGetValue(cacheKey, out List<Guid> ruleIds))
    {
        // 3. 缓存未命中，查询数据库
        ruleIds = (from dr in _dbContext.DeviceRules
                   where dr.DeviceId == devid
                   select dr.FlowRuleId).ToList();
        
        // 4. 写入缓存 (过期时间可配置)
        _caching.Set(cacheKey, ruleIds, 
            TimeSpan.FromSeconds(_options.RuleCachingExpiration));
    }
    
    // 5. 遍历执行每个规则
    foreach (var ruleId in ruleIds)
    {
        await RunFlowRules(ruleId, obj, devid, FlowRuleRunType.Normal, null);
    }
}
```

**关键点**:
1. **设备级规则绑定** - 通过 `DeviceRules` 表将规则绑定到具体设备
2. **缓存优化** - 规则ID列表缓存，避免每次查询数据库
3. **事件类型过滤** - 只执行挂载到该事件类型的规则

#### 6.1.2 RunFlowRules 规则链执行

```csharp
public async Task RunFlowRules(Guid ruleid, object data, Guid deviceId, 
    FlowRuleRunType type, string bizId)
{
    // 1. 加载规则定义
    var rule = await GetFlowRule(ruleid);
    if (rule == null) return;
    
    // 2. 解析流程图 XML (BPMN 格式)
    var flows = ParseFlows(rule.DefinitionsXml);
    
    // 3. 创建事件记录
    var _event = new BaseEvent()
    {
        EventId = Guid.NewGuid(),
        FlowRuleId = ruleid,
        Bizid = deviceId.ToString(),
        CreaterDateTime = DateTime.UtcNow,
        Type = type,
        BizData = JsonConvert.SerializeObject(data)
    };
    _dbContext.BaseEvents.Add(_event);
    await _dbContext.SaveChangesAsync();
    
    // 4. 找到 StartEvent 节点
    var startNode = flows.FirstOrDefault(f => f.FlowType == "bpmn:StartEvent");
    if (startNode == null) return;
    
    // 5. 从 StartEvent 开始遍历流程
    await Process(startNode, flows, data, _event, deviceId, type, bizId);
}
```

#### 6.1.3 Process 节点处理

```csharp
private async Task Process(Flow currentNode, List<Flow> flows, object data, 
    BaseEvent _event, Guid deviceId, FlowRuleRunType type, string bizId)
{
    // 1. 查找当前节点的后续节点
    var outgoing = currentNode.Outgoing;
    var nextFlows = flows.Where(f => f.Incoming == outgoing).ToList();
    
    foreach (var nextFlow in nextFlows)
    {
        // 2. 判断节点类型
        switch (nextFlow.FlowType)
        {
            case "bpmn:SequenceFlow":
                // 顺序流：条件判断
                if (await ProcessCondition(nextFlow.Conditionexpression, data))
                {
                    // 条件满足，递归处理下一节点
                    await Process(nextFlow, flows, data, _event, deviceId, type, bizId);
                }
                break;
                
            case "bpmn:Task":
                // 任务节点：执行脚本或执行器
                await ExecuteTask(nextFlow, data, _event, deviceId);
                break;
        }
    }
}
```

### 6.2 脚本引擎执行

**文件**: `IoTSharp.Interpreter\`

#### 6.2.1 脚本类型支持

| 脚本类型 | 引擎类 | 底层库 | 语言特性 |
|---------|--------|--------|---------|
| `javascript` | `JavaScriptEngine` | Jint | ES5 兼容，.NET 互操作 |
| `csharp` | `CSharpScriptEngine` | Roslyn | 完整 C# 语法 |
| `python` | `PythonScriptEngine` | IronPython | Python 2.7 兼容 |
| `lua` | `LuaScriptEngine` | MoonSharp | Lua 5.2 兼容 |
| `basic` | `BASICScriptEngine` | 自定义 | BASIC 方言 |
| `sql` | `SQLEngine` | ADO.NET | 数据库查询 |

#### 6.2.2 脚本执行流程

```csharp
case "bpmn:Task":
    var nodeProcessType = nextFlow.NodeProcessType;
    
    switch (nodeProcessType)
    {
        case "executor":
            // 任务执行器
            var executor = serviceProvider.GetRequiredService(nextFlow.Path);
            var result = await executor.ExecuteAsync(new TaskActionInput()
            {
                Input = JsonConvert.SerializeObject(data),
                Config = nextFlow.NodeProcessParams
            });
            break;
            
        case "javascript":
            // JavaScript 脚本
            var jsEngine = serviceProvider.GetRequiredService<JavaScriptEngine>();
            var jsResult = jsEngine.Do(nextFlow.NodeProcessScript, 
                JsonConvert.SerializeObject(data));
            break;
            
        case "csharp":
            // C# 脚本
            var csEngine = serviceProvider.GetRequiredService<CSharpScriptEngine>();
            var csResult = csEngine.Do(nextFlow.NodeProcessScript, 
                JsonConvert.SerializeObject(data));
            break;
    }
    
    // 记录执行结果
    await _dbContext.FlowOperations.Add(new FlowOperation()
    {
        FlowId = nextFlow.FlowId,
        FlowRuleId = nextFlow.FlowRuleId,
        EventId = _event.EventId,
        OperationDesc = "执行完成",
        Data = result,
        NodeStatus = (int)FlowOperationStatus.Success
    });
```

### 6.3 任务执行器

**目录**: `IoTSharp.TaskActions\`

#### 6.3.1 TaskAction 抽象基类

```csharp
public abstract class TaskAction
{
    public abstract Task<TaskActionOutput> ExecuteAsync(TaskActionInput input);
}

public class TaskActionInput
{
    public string Input { get; set; }      // 输入数据 (JSON)
    public string Config { get; set; }     // 节点配置 (JSON)
}

public class TaskActionOutput
{
    public string Output { get; set; }     // 输出数据 (JSON)
    public bool Success { get; set; }      // 是否成功
}
```

#### 6.3.2 内置执行器

| 执行器类 | 功能 | 输入 | 输出 |
|---------|------|------|------|
| `AlarmPullExcutor` | 告警拉取 | 设备ID、告警类型 | 告警列表 |
| `CustomeAlarmPullExcutor` | 自定义告警 | 自定义规则 | 告警列表 |
| `DeviceActionExcutor` | 设备动作 | 动作指令 | 执行结果 |
| `MessagePullExcutor` | 消息拉取 | Topic、QoS | 消息列表 |
| `RangerCheckExcutor` | 范围检查 | 值、上下限 | 检查结果 |
| `TelemetryArrayPullExcutor` | 遥测数组 | 数组数据 | 提取结果 |

#### 6.3.3 RangerCheckExcutor 范围检查示例

```csharp
public class RangerCheckExcutor : TaskAction
{
    public override async Task<TaskActionOutput> ExecuteAsync(TaskActionInput input)
    {
        // 1. 解析输入数据
        var inputData = JsonConvert.DeserializeObject<Dictionary<string, object>>(input.Input);
        
        // 2. 解析配置 (上下限)
        var config = JsonConvert.DeserializeObject<RangeConfig>(input.Config);
        
        // 3. 执行范围检查
        var value = Convert.ToDouble(inputData[config.KeyName]);
        bool inRange = value >= config.Min && value <= config.Max;
        
        // 4. 返回结果
        return new TaskActionOutput()
        {
            Output = JsonConvert.SerializeObject(new { 
                KeyName = config.KeyName, 
                Value = value, 
                InRange = inRange 
            }),
            Success = true
        };
    }
}

class RangeConfig
{
    public string KeyName { get; set; }  // 检查字段
    public double Min { get; set; }      // 下限
    public double Max { get; set; }      // 上限
}
```

### 6.4 规则引擎完整执行流程图

```
设备上报遥测数据
  ↓
StoreTelemetryData()
  ↓
RunRules(deviceId, data, EventType.Telemetry)
  ↓
┌─────────────────────────────────────────────┐
│ 1. 查找设备关联的规则ID列表                   │
│    DeviceRules 表 (缓存)                     │
│    ruleIds = [rule1, rule2, ...]             │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│ 2. 遍历执行每个规则                          │
│    RunFlowRules(ruleId, data, ...)           │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│ 3. 加载规则定义                              │
│    FlowRule + Flows (BPMN XML)               │
│    从数据库或缓存获取                         │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│ 4. 创建 BaseEvent 记录                       │
│    记录事件触发信息                           │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│ 5. 找到 StartEvent 节点                      │
│    流程入口点                                │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│ 6. 遍历流程节点 (Process)                    │
│                                              │
│  节点类型:                                    │
│  ┌──────────────────────────────────────┐  │
│  │ SequenceFlow → 条件判断               │  │
│  │   if (ProcessCondition()) {           │  │
│  │     Process(nextNode) // 递归         │  │
│  │   }                                   │  │
│  └──────────────────────────────────────┘  │
│  ┌──────────────────────────────────────┐  │
│  │ Task → 执行动作                       │  │
│  │   switch (NodeProcessType) {          │  │
│  │     case "javascript":                │  │
│  │       JSEngine.Do(script, data)       │  │
│  │     case "executor":                  │  │
│  │       TaskAction.ExecuteAsync(input)  │  │
│  │   }                                   │  │
│  └──────────────────────────────────────┘  │
│                                              │
│  记录每一步 FlowOperation                     │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│ 7. 规则执行完毕                              │
│    可能级联发布新事件 (通过脚本/执行器)       │
│    → 触发下一轮规则                          │
└─────────────────────────────────────────────┘
```

---

## 七、告警管理详解

### 7.1 Alarm 数据模型

**文件**: `IoTSharp.Data\Alarm.cs`

```csharp
public class Alarm
{
    public Guid Id { get; set; }
    public string AlarmType { get; set; }          // 告警类型
    public string AlarmDetail { get; set; }        // 告警详情
    public AlarmStatus AlarmStatus { get; set; }   // 告警状态
    public ServerityLevel Serverity { get; set; }  // 严重等级
    public bool Propagate { get; set; }            // 是否触发规则链
    public Guid OriginatorId { get; set; }         // 起因对象ID
    public OriginatorType OriginatorType { get; set; }  // 起因类型
    
    public DateTime AckDateTime { get; set; }      // 确认时间
    public DateTime ClearDateTime { get; set; }    // 清除时间
    public DateTime StartDateTime { get; set; }    // 开始时间
    public DateTime EndDateTime { get; set; }      // 结束时间
    
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
}
```

### 7.2 AlarmExtension 告警处理逻辑

**文件**: `IoTSharp.Data.Extensions\AlarmExtension.cs`

#### 7.2.1 OccurredAlarm 核心方法

```csharp
public static async Task<ApiResult> OccurredAlarm(
    this ApplicationDbContext _dbContext, 
    CreateAlarmDto dto)
{
    // 1. 查找设备/资产
    var device = await _dbContext.Device
        .FirstOrDefaultAsync(d => d.Name == dto.OriginatorName);
    
    if (device == null)
    {
        return new ApiResult() 
        { 
            Code = (int)ApiCode.NotFound, 
            Msg = "设备未找到" 
        };
    }
    
    // 2. 创建告警对象
    var alarm = new Alarm()
    {
        Id = Guid.NewGuid(),
        AlarmType = dto.AlarmType,
        AlarmDetail = dto.AlarmDetail,
        Serverity = dto.Serverity,
        OriginatorId = device.Id,
        OriginatorType = OriginatorType.Device,
        TenantId = device.TenantId,
        CustomerId = device.CustomerId,
        AckDateTime = DateTime.UtcNow,
        StartDateTime = DateTime.UtcNow,
        AlarmStatus = AlarmStatus.Active_UnAck,
        Propagate = true  // 默认触发规则链
    };
    
    // 3. 检查是否已有同类型未清除的告警
    var existingAlarm = await _dbContext.Alarms
        .FirstOrDefaultAsync(a => 
            a.OriginatorId == alarm.OriginatorId && 
            a.AlarmType == alarm.AlarmType && 
            a.ClearDateTime == default);
    
    if (existingAlarm != null)
    {
        // 4. 告警已存在，更新详情
        existingAlarm.AlarmDetail = alarm.AlarmDetail;
        existingAlarm.AckDateTime = DateTime.UtcNow;
        
        // 5. 比较严重等级
        if (existingAlarm.Serverity != alarm.Serverity)
        {
            // 严重等级变化，设置传播标志
            existingAlarm.Propagate = true;
        }
        else
        {
            existingAlarm.Propagate = false;
        }
        
        alarm = existingAlarm;
    }
    else
    {
        // 6. 新告警，添加到数据库
        _dbContext.Alarms.Add(alarm);
    }
    
    // 7. 保存更改
    await _dbContext.SaveChangesAsync();
    
    return new ApiResult() 
    { 
        Code = (int)ApiCode.Success, 
        Data = alarm 
    };
}
```

### 7.3 告警生命周期

```
┌─────────────┐
│  告警产生    │ ← 设备上报 / 规则引擎触发
│ Active_UnAck│
└──────┬──────┘
       │
       ├──────────────┐
       ↓              ↓
┌─────────────┐  ┌─────────────┐
│  告警确认    │  │  告警清除    │
│ Active_Ack  │  │   Cleared    │
└──────┬──────┘  └──────┬──────┘
       │                │
       └────────┬───────┘
                ↓
         ┌─────────────┐
         │  告警结束    │
         │ EndDateTime │
         └─────────────┘
```

**告警状态枚举**:
```csharp
public enum AlarmStatus
{
    Active_UnAck,        // 活动未确认
    Active_Ack,          // 活动已确认
    Cleared_UnAck,       // 清除未确认
    Cleared_Ack          // 清除已确认
}

public enum ServerityLevel
{
    Indeterminate,       // 不确定
    Critical,            // 严重
    Major,               // 重要
    Minor,               // 次要
    Warning              // 警告
}
```

### 7.4 告警传播规则

```
告警产生/更新
  ↓
OccurredAlarm()
  ↓
判断严重等级变化:
  ├─ 等级提升 → Propagate = true (触发规则链)
  ├─ 等级降低 → Propagate = false (不触发)
  └─ 新告警 → Propagate = true (触发规则链)
  ↓
if (Propagate)
  RunRules(OriginatorId, alarmDto, EventType.Alarm)
```

---

## 八、定时任务详解

### 8.1 Quartz.NET 调度体系

**文件**: `IoTSharp\Jobs\`

#### 8.1.1 任务注册机制

**自定义特性**: `IoTSharp.Extensions.QuartzJobScheduler\QuartzJobSchedulerAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class QuartzJobSchedulerAttribute : Attribute
{
    public QuartzJobSchedulerAttribute(double seconds) 
    {
        WithInterval = TimeSpan.FromSeconds(seconds);
    }
    
    public QuartzJobSchedulerAttribute(double minutes, double seconds)
    {
        WithInterval = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }
    
    internal TimeSpan WithInterval { get; set; }
    public bool Manual { get; set; } = false;  // 是否手动触发
    public int RepeatCount { get; set; } = 0;  // 重复次数 (0=无限)
    public bool StoreDurably { get; set; } = false;  // 是否持久化
}
```

#### 8.1.2 DiscoverJobs 扩展方法

**文件**: `IoTSharp.Extensions.QuartzJobScheduler\QuartzJobSchedulerExtensions.cs`

```csharp
public static IServiceCollectionQuartzConfigurator DiscoverJobs(
    this IServiceCollectionQuartzConfigurator _scheduler)
{
    // 1. 反射扫描所有实现了 IJob 且有 QuartzJobSchedulerAttribute 的类
    var types = from t in Assembly.GetCallingAssembly().GetTypes()
                where t.GetTypeInfo().ImplementedInterfaces.Any(tx => tx == typeof(IJob))
                   && t.GetTypeInfo().IsDefined(typeof(QuartzJobSchedulerAttribute), true)
                select t;
    
    // 2. 遍历每个 Job 类
    foreach (var t in types)
    {
        var so = t.GetCustomAttribute<QuartzJobSchedulerAttribute>();
        
        if (so?.Manual == false)  // 非手动调度才注册
        {
            // 3. 注册 Job
            var jobKey = new JobKey(so.Identity ?? t.Name, t.Assembly.GetName().Name);
            _scheduler.AddJob(t, jobKey, cfg => cfg.WithDescription(so.Desciption));
            
            // 4. 创建触发器
            _scheduler.AddTrigger(opts =>
            {
                opts.ForJob(jobKey)
                    .WithIdentity(so.TriggerName ?? t.Name)
                    .WithSimpleSchedule(x =>
                    {
                        x.WithInterval(so.WithInterval);
                        if (so.RepeatCount > 0)
                            x.WithRepeatCount(so.RepeatCount);
                        else
                            x.RepeatForever();  // 无限重复
                    });
                    
                if (so.StartAt == DateTimeOffset.MinValue)
                    opts.StartNow();  // 立即开始
                else
                    opts.StartAt(so.StartAt);
            });
        }
    }
    
    return _scheduler;
}
```

**优势**:
- ✅ 声明式配置，代码即文档
- ✅ 新增 Job 无需修改 Startup.cs
- ✅ 集中管理所有定时任务

### 8.2 CheckDevices 设备活跃检查

**文件**: `IoTSharp\Jobs\CheckDevices.cs`

```csharp
[QuartzJobScheduler(60)]  // 每60秒执行一次
public class CheckDevices : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactor.CreateScope();
        using var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        try
        {
            // 1. 查询所有标记为"活跃"的设备
            var sf = from d in _dbContext.AttributeLatest
                     where d.KeyName == Constants._Active && d.Value_Boolean == true
                     select d.DeviceId;
            
            if (!sf.Any()) return;
            
            var devids = await sf.ToListAsync();
            
            // 2. 遍历每个活跃设备
            foreach (var id in devids)
            {
                var dev = await _dbContext.Device.FirstOrDefaultAsync(d => d.Id == id);
                
                // 3. 获取设备上次活跃时间
                var ladt = from d in _dbContext.AttributeLatest
                           where d.DeviceId == id && 
                                 d.DataSide == DataSide.ServerSide && 
                                 d.KeyName == Constants._LastActivityDateTime
                           select d.Value_DateTime;
                           
                var __LastActivityDateTime = await ladt.FirstOrDefaultAsync();
                
                // 4. 判断是否超时
                if (dev != null && __LastActivityDateTime != null)
                {
                    var elapsed = DateTime.UtcNow.Subtract(__LastActivityDateTime.Value);
                    
                    if (elapsed.TotalSeconds > dev.Timeout)
                    {
                        // 5. 超时！发布不活跃事件
                        _logger.LogInformation(
                            $"设备{dev.Name}({dev.Id})现在置非活跃状态，" +
                            $"上次活跃时间{__LastActivityDateTime},超时{dev.Timeout}秒");
                        
                        await _queue.PublishActive(id, ActivityStatus.Inactivity);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查设备在线状态错误。");
        }
    }
}
```

**执行逻辑流程图**:
```
每60秒触发
  ↓
查询 AttributeLatest 表
  WHERE KeyName='_Active' AND Value_Boolean=true
  ↓
得到活跃设备列表: [device1, device2, ...]
  ↓
遍历每个设备:
  ├─ 查询 LastActivityDateTime
  ├─ 计算: 当前时间 - 最后活跃时间
  ├─ 判断: 是否超过 Timeout 秒?
  │   ├─ 是 → PublishActive(Inactivity)
  │   └─ 否 → 跳过
  ↓
完成
```

### 8.3 CachingJob 看板缓存刷新

**文件**: `IoTSharp\Jobs\CachingJob.cs`

```csharp
[QuartzJobScheduler(1, 0)]  // 每1分钟执行一次
public class CachingJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactor.CreateScope();
        using var _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        try
        {
            // 1. 获取所有租户ID
            var tdds = await _db.Tenant.Select(t => t.Id).ToListAsync();
            
            // 2. 为每个租户刷新看板缓存
            tdds.ForEach(t =>
            {
                _caching.GetKanBanCache(t, _db);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理看板缓存时遇到问题。");
        }
    }
}
```

**业务逻辑**:
- IoTSharp 是多租户系统，每个租户有自己的数据看板
- 看板数据计算成本高（统计设备数、告警数、在线率等）
- 每1分钟预先计算好缓存，用户访问时直接返回

---

## 九、完整数据流路径总结

### 9.1 遥测数据上报完整流程

```
┌─────────────────────────────────────────────────────────────────────┐
│ 阶段1: 设备接入                                                      │
└─────────────────────────────────────────────────────────────────────┘
设备发送 MQTT 消息:
  Topic: devices/device001/telemetry
  Payload: {"temperature": 25.5, "humidity": 60}
    ↓
MQTTService.Server_ApplicationMessageReceived()
    ↓
TelemetryController.telemetry(devname="device001", body=payload)
    ↓
FindDevice("device001") → Device { Id=GUID, Name="device001", Timeout=300 }
    ↓
Message.ConvertPayloadToDictionary(body)
    ↓
Dictionary<string, object> {
  ["temperature"] = 25.5,
  ["humidity"] = 60
}

┌─────────────────────────────────────────────────────────────────────┐
│ 阶段2: 事件发布                                                      │
└─────────────────────────────────────────────────────────────────────┘
_queue.PublishTelemetryData(device, keyValues)
    ↓
构建 PlayloadData:
  DeviceId = device.Id
  MsgBody = Dictionary
  DataSide = ClientSide
  DataCatalog = TelemetryData
  ts = DateTime.UtcNow
    ↓
CapPublisher.PublishTelemetryData(PlayloadData)
    ↓
_queue.PublishAsync(
  "iotsharp.services.datastream.telemetrydata", 
  PlayloadData
)
    ↓
消息进入 CAP EventStore (PostgreSQL/MongoDB/MySQL)
    ↓
消息推送到 MQ (RabbitMQ/Kafka/ZeroMQ/InMemory)

┌─────────────────────────────────────────────────────────────────────┐
│ 阶段3: 事件订阅                                                      │
└─────────────────────────────────────────────────────────────────────┘
CapSubscriber.telemetrydata(PlayloadData)
  [CapSubscribe("iotsharp.services.datastream.telemetrydata")]
    ↓
调用基类方法: EventBusSubscriber.StoreTelemetryData(PlayloadData)

┌─────────────────────────────────────────────────────────────────────┐
│ 阶段4: 数据存储                                                      │
└─────────────────────────────────────────────────────────────────────┘
IStorage.StoreTelemetryAsync(PlayloadData)
    ↓
遍历 MsgBody 每个 Key-Value:
  ├─ temperature = 25.5
  │   → 创建 TelemetryData {
  │       DeviceId=GUID,
  │       KeyName="temperature",
  │       Type=Double,
  │       Value_Double=25.5,
  │       DateTime=UtcNow
  │     }
  │
  └─ humidity = 60
      → 创建 TelemetryData {
          DeviceId=GUID,
          KeyName="humidity",
          Type=Long,
          Value_Long=60,
          DateTime=UtcNow
        }
    ↓
批量写入 TelemetryData 表 (或分片表)
    ↓
返回存储结果: (true, [TelemetryData列表])

┌─────────────────────────────────────────────────────────────────────┐
│ 阶段5: 规则触发                                                      │
└─────────────────────────────────────────────────────────────────────┘
FlowRuleProcessor.RunRules(deviceId, data, EventType.Telemetry)
    ↓
1. 查找设备关联的规则ID列表 (DeviceRules表, 缓存)
   ruleIds = [rule_001, rule_002]
    ↓
2. 遍历执行每个规则
   RunFlowRules(rule_001, data, deviceId, ...)
    ↓
3. 加载规则定义 (FlowRule + Flows)
   解析 BPMN XML
    ↓
4. 创建 BaseEvent 记录
    ↓
5. 从 StartEvent 开始遍历流程节点
   
   节点示例:
   ┌──────────────────────────────────────┐
   │ StartEvent → 流程开始                 │
   │   ↓                                 │
   │ SequenceFlow → 条件判断               │
   │   if (temperature > 30) {           │
   │     ↓                               │
   │     Task → JavaScript:               │
   │       console.log("温度过高!");       │
   │       ↓                             │
   │       Task → AlarmPullExcutor:       │
   │         产生告警                      │
   │         ↓                           │
   │         EndEvent → 流程结束           │
   │   } else {                          │
   │     ↓                               │
   │     EndEvent → 流程结束               │
   │   }                                 │
   └──────────────────────────────────────┘
    ↓
6. 每个节点执行结果记录到 FlowOperations 表

┌─────────────────────────────────────────────────────────────────────┐
│ 阶段6: 级联事件 (可能)                                               │
└─────────────────────────────────────────────────────────────────────┘
如果规则中的脚本/执行器发布了新事件:
  _queue.PublishDeviceAlarm(alarmDto)
    ↓
重复 阶段2 → 阶段5
  但 Topic 变为 "iotsharp.services.datastream.alarm"
  事件类型变为 EventType.Alarm
```

### 9.2 设备连接完整流程

```
设备发送 MQTT CONNECT:
  ClientId: "device001"
  UserName: "access_token_xyz"
  Password: (空)
    ↓
MQTTService.Server_ClientConnectionValidator(ctx)
    ↓
1. 检查本地回环 → 否
2. 查找 DeviceIdentity:
   WHERE IdentityId = "access_token_xyz"
   → 找到 device001
3. 验证 IdentityType = AccessToken
   → 验证通过
4. SessionItems.Add(nameof(Device), device001)
    ↓
_queue.PublishConnect(device001.Id, ConnectStatus.Connected)
    ↓
CapPublisher.PublishConnect(devid, Connected)
    ↓
_topic: "iotsharp.services.platform.connect"
_data: DeviceConnectStatus { DeviceId, ConnectStatus=Connected }
    ↓
消息入 CAP → 推送到订阅者
    ↓
CapSubscriber.connect(status)
  [CapSubscribe("iotsharp.services.platform.connect")]
    ↓
EventBusSubscriber.Connect(devid, Connected)
    ↓
构建 PlayloadData:
  DeviceId = devid
  DataCatalog = AttributeData
  DataSide = ServerSide
  MsgBody = {
    ["_Connected"] = true,
    ["_LastConnectDateTime"] = DateTime.UtcNow
  }
    ↓
StoreAttributeData(msg, EventType.Connected)
    ↓
1. 更新 AttributeLatest 表:
   UPSERT _Connected = true
   UPSERT _LastConnectDateTime = UtcNow
    ↓
2. RunRules(deviceId, data, EventType.Connected)
    ↓
触发"设备连接"规则链:
  可能执行:
    - 发送通知 (设备上线)
    - 更新看板统计
    - 记录审计日志
```

### 9.3 设备活跃检查完整流程

```
┌─────────────────────────────────────────────────────────────────────┐
│ 定时触发 (每60秒)                                                    │
└─────────────────────────────────────────────────────────────────────┘
Quartz.NET → CheckDevices.Execute(context)
    ↓
查询 AttributeLatest 表:
  SELECT DeviceId FROM AttributeLatest
  WHERE KeyName = '_Active' 
    AND Value_Boolean = true
    ↓
得到活跃设备列表: [device001, device002, ...]
    ↓
遍历每个设备:
  ├─ device001:
  │   ├─ 查询 _LastActivityDateTime = 2026-04-14 15:25:00
  │   ├─ 当前时间 = 2026-04-14 15:31:00
  │   ├─ elapsed = 360秒
  │   ├─ Timeout = 300秒
  │   ├─ 360 > 300 → 超时!
  │   └─ _queue.PublishActive(device001.Id, Inactivity)
  │
  └─ device002:
      ├─ 查询 _LastActivityDateTime = 2026-04-14 15:30:30
      ├─ elapsed = 30秒
      ├─ 30 < 300 → 正常
      └─ 跳过

┌─────────────────────────────────────────────────────────────────────┐
│ 发布不活跃事件                                                       │
└─────────────────────────────────────────────────────────────────────┘
_queue.PublishActive(device001.Id, Inactivity)
    ↓
_topic: "iotsharp.services.platform.active"
_data: DeviceActivityStatus { DeviceId, Activity=Inactivity }
    ↓
CapSubscriber.active(status)
  [CapSubscribe("iotsharp.services.platform.active")]
    ↓
EventBusSubscriber.Active(devid, Inactivity)
    ↓
构建 PlayloadData:
  MsgBody = {
    ["_LastActivityDateTime"] = UtcNow,
    ["_InactivityAlarmDateTime"] = UtcNow,
    ["_Active"] = false
  }
    ↓
StoreAttributeData(msg, EventType.Inactivity)
    ↓
1. 更新 AttributeLatest:
   UPSERT _Active = false
   UPSERT _InactivityAlarmDateTime = UtcNow
    ↓
2. RunRules(deviceId, data, EventType.Inactivity)
    ↓
触发"设备不活跃"规则链:
  可能执行:
    - 产生告警 (设备离线告警)
    - 发送通知 (邮件/短信)
    - 更新看板状态
```

---

## 十、关键设计模式总结

### 10.1 事件驱动架构 (EDA)

**核心思想**: 所有业务逻辑通过事件解耦，发布者和订阅者互不依赖。

```
发布事件 (Publisher)
  → 消息队列 (EventBus)
    → 订阅事件 (Subscriber)
      → 处理业务逻辑 (Handler)
        → 可能发布新事件 (Cascade)
```

**优势**:
- ✅ 松耦合 - 新增功能只需添加订阅者
- ✅ 异步处理 - 不阻塞主流程
- ✅ 可扩展 - 支持分布式部署
- ✅ 可追溯 - 事件持久化，可重放

### 10.2 Scope 工厂模式

**问题**: Job/Subscriber 是单例，但 DbContext 必须是 Scoped。

**解决方案**: 每次执行创建新的 DI 作用域。

```csharp
public class SomeJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactor;
    
    public async Task Execute(IJobExecutionContext context)
    {
        // 创建新的作用域
        using var scope = _scopeFactor.CreateScope();
        using var _dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        
        // 在此作用域内使用 DbContext
        await _dbContext.SaveChangesAsync();
    }  // scope 释放，DbContext 释放
}
```

**优势**:
- ✅ 线程安全 - 每个执行独立 DbContext
- ✅ 资源管理 - 自动释放数据库连接
- ✅ 事务支持 - 作用域内可开启事务

### 10.3 策略模式（多存储支持）

**接口**: `IStorage`
**实现**: `EFStorage`, `ShardingStorage`, `InfluxDBStorage`, ...

```csharp
// Startup.cs 中根据配置选择实现
switch (settings.TelemetryStorage)
{
    case TelemetryStorage.SingleTable:
        services.AddSingleton<IStorage, EFStorage>();
        break;
    case TelemetryStorage.InfluxDB:
        services.AddSingleton<IStorage, InfluxDBStorage>();
        break;
    // ...
}
```

**优势**: 新增存储引擎只需实现 `IStorage` 接口，无需修改业务代码。

### 10.4 观察者模式（规则引擎）

```
事件发生 (遥测/属性/告警)
  ↓
通知观察者 (FlowRuleProcessor)
  ↓
查找订阅者 (DeviceRules)
  ↓
执行订阅者 (RunFlowRules)
```

### 10.5 模板方法模式（任务执行器）

```csharp
// 抽象基类定义模板
public abstract class TaskAction
{
    public abstract Task<TaskActionOutput> ExecuteAsync(TaskActionInput input);
}

// 具体实现
public class RangerCheckExcutor : TaskAction
{
    public override Task<TaskActionOutput> ExecuteAsync(TaskActionInput input)
    {
        // 1. 解析输入
        // 2. 执行逻辑
        // 3. 返回输出
    }
}
```

---

## 十一、核心数据表流转

### 11.1 数据写入表

| 事件类型 | 写入的表 | 触发规则 |
|---------|---------|---------|
| 遥测数据 | `TelemetryData` (分片表) | EventType.Telemetry |
| 属性数据 | `AttributeLatest` | EventType.Attribute |
| 告警数据 | `Alarms` | EventType.Alarm |
| 连接状态 | `AttributeLatest` (_Connected) | EventType.Connected/Disconnected |
| 活跃状态 | `AttributeLatest` (_Active) | EventType.Activity/Inactivity |

### 11.2 规则引擎表

| 表名 | 用途 |
|------|------|
| `FlowRules` | 规则定义 (BPMN XML) |
| `Flows` | 流程节点定义 |
| `DeviceRules` | 设备与规则绑定 |
| `BaseEvents` | 事件触发记录 |
| `FlowOperations` | 节点执行记录 |

### 11.3 设备身份表

| 表名 | 用途 |
|------|------|
| `Device` | 设备基本信息 |
| `DeviceIdentity` | 身份认证 (AccessToken/Password/X509) |
| `AuthorizedKey` | 授权密钥 (批量设备认证) |

---

## 十二、性能优化点

### 12.1 缓存策略

| 缓存内容 | 缓存键 | 过期时间 | 目的 |
|---------|--------|---------|------|
| 规则ID列表 | `ruleid_{devid}_{EventType}` | 可配置 (默认60秒) | 减少数据库查询 |
| 规则定义 | `flowrule_{ruleId}` | 可配置 | 避免重复解析 XML |
| 看板数据 | `kanban_{tenantId}` | 60秒 (CachingJob) | 加速首页加载 |
| 设备身份 | `device_{token}` | 较长 | 加速认证 |

### 12.2 数据库优化

- **DbContextPool** - 连接池 (默认256)
- **分片存储** - 按时间分表，减少单表数据量
- **批量插入** - `AddRange()` + `SaveChangesAsync()`
- **异步查询** - 全部使用 `ToListAsync()` / `FirstOrDefaultAsync()`

### 12.3 事件处理优化

- **异步处理** - 所有方法都是 `async Task`
- **异常捕获** - 规则执行失败不影响数据存储
- **重试机制** - CAP 自动重试 (失败消息重投)

---

## 十三、异常处理机制

### 13.1 数据存储异常

```csharp
try
{
    await _dbContext.SaveChangesAsync();
}
catch (DbUpdateException due)
{
    _logger.LogError(due, "DbUpdateException:" + due.InnerException?.Message);
    // 不抛出异常，继续执行规则引擎
}
catch (Exception ex)
{
    _logger.LogError(ex, "StoreTelemetryData:" + ex.Message);
}
```

**原则**: 数据存储失败不阻断规则执行。

### 13.2 规则执行异常

```csharp
try
{
    await RunRules(deviceId, data, eventType);
}
catch (Exception ex)
{
    _logger.LogError(ex, "RunRules failed");
    // 不抛出异常，不影响主流程
}
```

**原则**: 规则执行失败不影响数据存储。

### 13.3 任务执行异常

```csharp
try
{
    // 任务逻辑
}
catch (Exception ex)
{
    _logger.LogError(ex, "检查设备在线状态错误。");
    // 记录日志，不崩溃
}
```

**原则**: 定时任务异常不中断下次执行。

---

## 十四、部署架构对应关系

### 14.1 Docker 部署方案

| 方案 | EventBusStore | EventBusMQ | TelemetryStorage | 适用场景 |
|------|---------------|------------|------------------|---------|
| rabbit_mongo_influx | MongoDB | RabbitMQ | InfluxDB | 生产环境 |
| zeromq_sharding | PostgreSQL | ZeroMQ | Sharding (PG) | 中等规模 |
| zeromq_taos | PostgreSQL | ZeroMQ | TDengine | 物联网场景 |

### 14.2 服务依赖图

```
IoTSharp 应用
  ├── PostgreSQL (主数据库)
  │    ├── AspNetUsers (用户)
  │    ├── Device (设备)
  │    ├── TelemetryData (遥测 - 单表模式)
  │    ├── Alarms (告警)
  │    ├── FlowRules (规则)
  │    └── qrtz_* (Quartz调度)
  │
  ├── InfluxDB/TDengine (时序数据库 - 可选)
  │    └── TelemetryData (遥测 - 分片模式)
  │
  ├── RabbitMQ/ZeroMQ (消息队列)
  │    └── CAP Topic 路由
  │
  └── Redis (缓存 - 可选)
       ├── 规则缓存
       ├── 看板缓存
       └── 设备身份缓存
```

---

## 十五、总结

### 15.1 核心运行逻辑一句话总结

```
设备通过 MQTT/HTTP/CoAP 上报数据 → 转换为 PlayloadData → 通过 IPublisher 发布事件到 CAP 
→ CAP 异步投递到订阅者 CapSubscriber → EventBusSubscriber 调用 IStorage 存储数据 
→ 触发 FlowRuleProcessor.RunRules() 执行规则链 → 规则链可能级联发布新事件
```

### 15.2 架构特点

| 特点 | 实现方式 | 优势 |
|------|---------|------|
| **多协议接入** | MQTT/HTTP/CoAP 三种协议 | 适配不同设备场景 |
| **事件驱动** | CAP 事件总线 | 松耦合、可扩展 |
| **多存储支持** | IStorage 接口 + 7种实现 | 灵活部署 |
| **规则引擎** | BPMN 流程图 + 多语言脚本 | 可视化编排 |
| **多租户** | Tenant → Customer → Device | 数据隔离 |
| **定时任务** | Quartz.NET + 声明式配置 | 集中管理 |
| **异常容错** | 全局异常捕获 + CAP 重试 | 高可用 |

### 15.3 数据流关键路径

```
设备 → 协议解析 → PlayloadData → IPublisher → EventBus 
  → ISubscriber → IStorage → FlowRuleProcessor → TaskAction
```

每个环节都可独立扩展和替换，形成高度模块化的物联网平台架构。

---

## 附录 A: 关键枚举定义

### A.1 EventType (事件类型)

| 枚举值 | 数值 | 说明 |
|--------|------|------|
| `None` | -1 | 无事件 |
| `RAW` | 0 | 原始数据 |
| `Telemetry` | 1 | 遥测数据对象 |
| `Attribute` | 2 | 属性数据对象 |
| `RPC` | 3 | 远程控制 |
| `Connected` | 4 | 设备在线 |
| `Disconnected` | 5 | 设备离线 |
| `TelemetryArray` | 6 | 遥测数据 Key-Value 数组 |
| `Alarm` | 7 | 告警挂载点 |
| `DeleteDevice` | 8 | 设备删除 |
| `CreateDevice` | 9 | 设备创建 |
| `Activity` | 10 | 活跃事件 |
| `Inactivity` | 11 | 非活跃事件 |

### A.2 DataCatalog (数据目录)

| 枚举值 | 说明 |
|--------|------|
| `AttributeLatest` | 最新属性 (UPSERT) |
| `TelemetryLatest` | 最新遥测 (UPSERT) |
| `TelemetryData` | 历史遥测 (INSERT) |
| `ProduceData` | 产品数据 |

### A.3 DataSide (数据方向)

| 枚举值 | 说明 |
|--------|------|
| `ClientSide` | 设备端上报 |
| `ServerSide` | 服务端生成 |
| `AnySide` | 任意方向 |

### A.4 DataType (数据类型)

| 枚举值 | 说明 | 存储字段 |
|--------|------|---------|
| `Boolean` | 布尔值 | Value_Boolean |
| `String` | 字符串 | Value_String |
| `Long` | 长整型 | Value_Long |
| `DateTime` | 日期时间 | Value_DateTime |
| `Double` | 双精度 | Value_Double |
| `Json` | JSON | Value_Json |
| `XML` | XML | Value_XML |
| `Binary` | 二进制 | Value_Binary |

---

## 附录 B: Topic 命名完整列表

| Topic | 前缀 | 数据类型 | 订阅者方法 |
|-------|------|---------|-----------|
| `iotsharp.services.datastream.attributedata` | datastream | PlayloadData | attributedata() |
| `iotsharp.services.datastream.telemetrydata` | datastream | PlayloadData | telemetrydata() |
| `iotsharp.services.datastream.alarm` | datastream | CreateAlarmDto | alarm() |
| `iotsharp.services.platform.createdevice` | platform | Guid | createdevice() |
| `iotsharp.services.platform.deleteDevice` | platform | Guid | deletedevice() |
| `iotsharp.services.platform.connect` | platform | DeviceConnectStatus | connect() |
| `iotsharp.services.platform.active` | platform | DeviceActivityStatus | active() |

---

## 附录 C: 配置文件关键项

### C.1 AppSettings

```json
{
  "DataBase": "PostgreSql",           // 关系数据库类型
  "TelemetryStorage": "SingleTable",  // 时序存储模式
  "EventBus": "CAP",                  // 事件总线框架
  "EventBusStore": "PostgreSQL",      // 事件存储
  "EventBusMQ": "InMemory",           // 消息队列
  "CachingUseIn": "InMemory",         // 缓存方式
  "ShardingByDateMode": "PerMonth",   // 分片粒度
  "DbContextPoolSize": 256,           // 连接池大小
  "RuleCachingExpiration": 60         // 规则缓存过期秒数
}
```

### C.2 连接字符串

```json
{
  "ConnectionStrings": {
    "IoTSharp": "Server=pgsql;Database=IoTSharp;Username=postgres;Password=xxx;",
    "TelemetryStorage": "...",          // 时序数据库
    "EventBusStore": "...",             // 事件存储
    "EventBusMQ": "...",                // 消息队列
    "BlobStorage": "..."                // 文件存储
  }
}
```

---

> 本文档基于 IoTSharp v3.5.0 源码生成，覆盖设备接入、事件总线、数据存储、规则引擎、告警管理、定时任务等核心模块。
> 
> 如需进一步了解具体代码实现，请参考对应源文件及行号引用。
