# IoTSharp 消息队列详解：CAP 与 Shashlik

> 基于 IoTSharp v3.5.0 源码分析 | 事件总线架构深度解析  
> 生成日期：2026年4月14日

---

## 一、什么是 CAP 和 Shashlik？

### 1.1 CAP (Cloud Application Platform)

**CAP** 是一个基于 .NET 平台的**分布式事务最终一致性解决方案**，全称为 "Cloud Application Platform"。

**官方定位**：
> CAP 是一个基于 .NET 的 EventBus，同时也是一个具有 Outbox Pattern（发件箱模式）实现的分布式事务最终一致性解决方案。

**核心特性**：
- ✅ **消息持久化** - 消息在发送前先存储到数据库，确保不丢失
- ✅ **重试机制** - 自动重试失败的消息（可配置重试次数）
- ✅ **幂等性保证** - 重复消息不会导致业务异常
- ✅ **Outbox 模式** - 本地事务与消息发布的原子性
- ✅ **Dashboard** - 内置可视化监控面板（`/cap`）
- ✅ **多存储支持** - PostgreSQL、MySQL、SQL Server、MongoDB、LiteDB、InMemory
- ✅ **多MQ支持** - RabbitMQ、Kafka、ZeroMQ、NATS、Pulsar、Redis Streams、Amazon SQS、Azure Service Bus

**GitHub**: https://github.com/dotnetcore/CAP

---

### 1.2 Shashlik.EventBus

**Shashlik.EventBus** 是另一个 .NET 平台的**轻量级事件总线框架**。

**核心特性**：
- ✅ **轻量级** - 代码简洁，依赖少
- ✅ **强类型事件** - 使用 `IEvent` 接口，编译时类型检查
- ✅ **多存储支持** - PostgreSQL、MySQL、SQL Server、InMemory
- ✅ **多MQ支持** - RabbitMQ、Kafka
- ✅ **事件处理器** - `IEventHandler<T>` 模式，职责清晰

**对比 CAP**：
- Shashlik 更轻量，但功能相对简单
- CAP 生态更成熟，支持的 MQ 和存储更多
- Shashlik 采用强类型事件，CAP 采用字符串 Topic

---

## 二、在 IoTSharp 中的架构位置

### 2.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                        设备接入层                                    │
│  MQTTService │ CoAPService │ Controllers (REST API)                 │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ 1. 接收原始数据
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        事件发布层                                    │
│                        IPublisher                                   │
│  ┌──────────────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │   CapPublisher       │  │ ShashlikPub  │  │  NSBusPublisher  │  │
│  │  (字符串Topic)        │  │ (强类型Event)│  │                  │  │
│  └──────────────────────┘  └──────────────┘  └──────────────────┘  │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ 2. 消息入队
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     消息中间件 (MQ)                                   │
│  RabbitMQ │ Kafka │ ZeroMQ │ NATS │ Pulsar │ Redis │ InMemory       │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ 3. 消息投递
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     事件订阅层                                        │
│                       ISubscriber                                   │
│  ┌──────────────────────┐  ┌──────────────────┐                    │
│  │   CapSubscriber      │  │ ShashlikSubscriber│                    │
│  │ [CapSubscribe("")]   │  │ IEventHandler<T>  │                    │
│  └──────────────────────┘  └──────────────────┘                    │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ 4. 调用业务逻辑
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     业务处理层                                        │
│                  EventBusSubscriber (基类)                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐     │
│  │ StoreData    │  │ RunRules     │  │  OccurredAlarm       │     │
│  │ 存储数据     │  │ 触发规则     │  │  处理告警            │     │
│  └──────────────┘  └──────────────┘  └──────────────────────┘     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 项目结构

```
IoTSharp.EventBus/                    # 抽象层（接口定义）
├── IPublisher.cs                     #   发布者接口
├── ISubscriber.cs                    #   订阅者接口
├── EventBusSubscriber.cs             #   订阅者基类（核心业务逻辑）
├── EventBusPublisher.cs              #   发布者扩展方法
├── EventBusOption.cs                 #   配置选项
├── EventBusMetrics.cs                #   统计指标
└── EventsBusDependencyInjection.cs   #   统一依赖注入入口

IoTSharp.EventBus.CAP/                # CAP 实现
├── CapPublisher.cs                   #   CAP 发布者
├── CapSubscriber.cs                  #   CAP 订阅者
└── DependencyInjection.cs            #   CAP 注册配置

IoTSharp.EventBus.Shashlik/           # Shashlik 实现
├── ShashlikPublisher.cs              #   Shashlik 发布者
├── ShashlikSubscriber.cs             #   Shashlik 订阅者 + 事件处理器
├── Events.cs                         #   强类型事件定义
├── ShashlikEvent.cs                  #   事件基类
└── DependencyInjection.cs            #   Shashlik 注册配置
```

---

## 三、统一接口抽象

### 3.1 IPublisher 发布接口

**文件**: `IoTSharp.EventBus\IPublisher.cs`

```csharp
public interface IPublisher
{
    // 获取消息队列统计信息
    Task<EventBusMetrics> GetMetrics();
    
    // 发布设备创建事件
    Task PublishCreateDevice(Guid devid);
    
    // 发布设备删除事件
    Task PublishDeleteDevice(Guid devid);
    
    // 发布属性数据
    Task PublishAttributeData(PlayloadData msg);
    
    // 发布遥测数据
    Task PublishTelemetryData(PlayloadData msg);
    
    // 发布连接状态事件
    Task PublishConnect(Guid devid, ConnectStatus devicestatus);
    
    // 发布活跃状态事件
    Task PublishActive(Guid devid, ActivityStatus activity);
    
    // 发布告警事件
    Task PublishDeviceAlarm(CreateAlarmDto alarmDto);
}
```

**设计思想**: 策略模式 - 定义统一接口，CAP/Shashlik 分别实现，运行时通过配置切换。

---

### 3.2 ISubscriber 订阅接口

**文件**: `IoTSharp.EventBus\ISubscriber.cs`

```csharp
public interface ISubscriber
{
    // 存储属性数据
    Task StoreAttributeData(PlayloadData msg);
    
    // 处理告警事件
    Task OccurredAlarm(CreateAlarmDto alarmDto);
    
    // 存储遥测数据
    Task StoreTelemetryData(PlayloadData msg);
    
    // 删除设备
    Task DeleteDevice(Guid deviceId);
    
    // 创建设备
    Task CreateDevice(Guid deviceId);
    
    // 连接状态变更
    Task Connect(Guid devid, ConnectStatus devicestatus);
    
    // 活跃状态变更
    Task Active(Guid devid, ActivityStatus activity);
}
```

---

## 四、CAP 实现详解

### 4.1 CapPublisher 发布器

**文件**: `IoTSharp.EventBus.CAP\CapPublisher.cs`

```csharp
public class CapPublisher : IPublisher
{
    private readonly ICapPublisher _queue;  // CAP 框架的发布器
    private readonly DotNetCore.CAP.Persistence.IDataStorage _storage;

    public async Task PublishTelemetryData(PlayloadData msg)
    {
        // 发布到字符串 Topic
        await _queue.PublishAsync(
            "iotsharp.services.datastream.telemetrydata", 
            msg
        );
    }

    public async Task PublishConnect(Guid devid, ConnectStatus devicestatus)
    {
        await _queue.PublishAsync(
            "iotsharp.services.platform.connect", 
            new DeviceConnectStatus(devid, devicestatus)
        );
    }

    public async Task<EventBusMetrics> GetMetrics()
    {
        // 通过 CAP 的 MonitoringApi 获取统计信息
        var _api = _storage.GetMonitoringApi();
        var ps = await _api.HourlySucceededJobs(MessageType.Publish);
        var pf = await _api.HourlyFailedJobs(MessageType.Publish);
        var ss = await _api.HourlySucceededJobs(MessageType.Subscribe);
        var sf = await _api.HourlyFailedJobs(MessageType.Subscribe);
        
        return new EventBusMetrics(...)
        {
            Servers = s.Servers,
            Subscribers = s.Subscribers,
            PublishedSucceeded = s.PublishedSucceeded,
            ReceivedSucceeded = s.ReceivedSucceeded,
            PublishedFailed = s.PublishedFailed,
            ReceivedFailed = s.ReceivedFailed
        };
    }
}
```

**关键特点**:
- 使用**字符串 Topic** 标识消息类型
- 消息内容可以是任意对象（自动序列化）
- 内置监控 API 提供统计信息

---

### 4.2 CapSubscriber 订阅器

**文件**: `IoTSharp.EventBus.CAP\CapSubscriber.cs`

```csharp
public class CapSubscriber : EventBusSubscriber, ISubscriber, ICapSubscribe
{
    // 订阅属性数据 Topic
    [CapSubscribe("iotsharp.services.datastream.attributedata")]
    public async Task attributedata(PlayloadData msg)
    {
        await StoreAttributeData(msg);
    }

    // 订阅遥测数据 Topic
    [CapSubscribe("iotsharp.services.datastream.telemetrydata")]
    public async Task telemetrydata(PlayloadData msg)
    {
        await StoreTelemetryData(msg);
    }

    // 订阅告警 Topic
    [CapSubscribe("iotsharp.services.datastream.alarm")]
    public async Task alarm(CreateAlarmDto alarmDto)
    {
        await OccurredAlarm(alarmDto);
    }

    // 订阅设备创建 Topic
    [CapSubscribe("iotsharp.services.platform.createdevice")]
    public async Task createdevice(Guid deviceId)
    {
        await CreateDevice(deviceId);
    }

    // 订阅连接状态 Topic
    [CapSubscribe("iotsharp.services.platform.connect")]
    public async Task connect(DeviceConnectStatus status)
    {
        await Connect(status.DeviceId, status.ConnectStatus);
    }

    // 订阅活跃状态 Topic
    [CapSubscribe("iotsharp.services.platform.active")]
    public async Task active(DeviceActivityStatus status)
    {
        await base.Active(status.DeviceId, status.Activity);
    }
}
```

**关键特点**:
- 使用 `[CapSubscribe("topic")]` 特性声明订阅
- 实现 `ICapSubscribe` 接口标记可订阅类
- 继承 `EventBusSubscriber` 基类复用业务逻辑

---

### 4.3 CAP Topic 命名规范

| Topic 名称 | 前缀 | 数据类型 | 用途 |
|-----------|------|---------|------|
| `iotsharp.services.datastream.attributedata` | datastream | PlayloadData | 属性数据上报 |
| `iotsharp.services.datastream.telemetrydata` | datastream | PlayloadData | 遥测数据上报 |
| `iotsharp.services.datastream.alarm` | datastream | CreateAlarmDto | 告警上报 |
| `iotsharp.services.platform.createdevice` | platform | Guid | 设备创建 |
| `iotsharp.services.platform.deleteDevice` | platform | Guid | 设备删除 |
| `iotsharp.services.platform.connect` | platform | DeviceConnectStatus | 连接状态变更 |
| `iotsharp.services.platform.active` | platform | DeviceActivityStatus | 活跃状态变更 |

**命名规则**:
- `datastream.*` - 数据流相关（持续产生的数据）
- `platform.*` - 平台事件相关（状态变更、生命周期事件）

---

## 五、Shashlik 实现详解

### 5.1 强类型事件定义

**文件**: `IoTSharp.EventBus.Shashlik\Events.cs`

```csharp
// 属性数据事件
public class AttributeDataEvent : ShashlikEvent<PlayloadData> { }

// 遥测数据事件
public class TelemetryDataEvent : ShashlikEvent<PlayloadData> { }

// 设备创建事件
public class CreateDeviceEvent : IEvent 
{ 
    public Guid DeviceId { get; set; } 
}

// 设备删除事件
public class DeleteDeviceEvent : IEvent 
{ 
    public Guid DeviceId { get; set; } 
}

// 告警事件
public class AlarmEvent : ShashlikEvent<CreateAlarmDto> { }

// 活跃状态事件
public class DeviceActivityEvent : ShashlikEvent<DeviceActivityStatus> { }

// 连接状态事件
public class DeviceConnectEvent : ShashlikEvent<DeviceConnectStatus> { }
```

**ShashlikEvent 基类**:

**文件**: `IoTSharp.EventBus.Shashlik\ShashlikEvent.cs`

```csharp
public class ShashlikEvent<T> : IEvent
{
    public T? Data { get; set; }
    
    // 隐式转换：T → ShashlikEvent<T>
    public static implicit operator ShashlikEvent<T>(T v) 
        => new ShashlikEvent<T>() { Data = v };
    
    // 显式转换：ShashlikEvent<T> → T
    public static explicit operator T?(ShashlikEvent<T> v) 
        => v.Data;
}
```

**关键特点**:
- 采用**强类型事件类**而非字符串 Topic
- 编译时类型检查，避免拼写错误
- 支持隐式/显式类型转换，使用方便

---

### 5.2 ShashlikPublisher 发布器

**文件**: `IoTSharp.EventBus.Shashlik\ShashlikPublisher.cs`

```csharp
public class ShashlikPublisher : IPublisher
{
    private readonly IEventPublisher _queue;  // Shashlik 发布器
    private readonly IMessageStorage _storage;

    public async Task PublishTelemetryData(PlayloadData msg)
    {
        // 发布强类型事件
        await _queue.PublishAsync(new TelemetryDataEvent() { Data = msg });
    }

    public async Task PublishCreateDevice(Guid devid)
    {
        await _queue.PublishAsync(new CreateDeviceEvent() { DeviceId = devid });
    }

    public async Task PublishConnect(Guid devid, ConnectStatus devicestatus)
    {
        await _queue.PublishAsync(new DeviceConnectEvent() 
        { 
            Data = new DeviceConnectStatus(devid, devicestatus) 
        });
    }
}
```

**关键特点**:
- 使用**强类型事件类**而非字符串 Topic
- 编译时类型安全
- 代码更简洁（借助隐式转换）

---

### 5.3 ShashlikSubscriber 和事件处理器

**文件**: `IoTSharp.EventBus.Shashlik\ShashlikSubscriber.cs`

```csharp
// 属性数据事件处理器
public class AttributeDataHandler : IEventHandler<AttributeDataEvent>
{
    private readonly ISubscriber _subscriber;
    
    public async Task Execute(AttributeDataEvent @event, IDictionary<string, string> items)
    {
        await _subscriber.StoreAttributeData(@event.Data);
    }
}

// 遥测数据事件处理器
public class TelemetryDataEventHandler : IEventHandler<TelemetryDataEvent>
{
    private readonly ISubscriber _subscriber;
    
    public async Task Execute(TelemetryDataEvent @event, IDictionary<string, string> items)
    {
        await _subscriber.StoreTelemetryData(@event.Data);
    }
}

// 告警事件处理器
public class AlarmEventHandler : IEventHandler<AlarmEvent>
{
    private readonly ISubscriber _subscriber;
    
    public async Task Execute(AlarmEvent @event, IDictionary<string, string> items)
    {
        await _subscriber.OccurredAlarm(@event.Data);
    }
}

// 设备创建事件处理器
public class CreateDeviceEventHandler : IEventHandler<CreateDeviceEvent>
{
    private readonly ISubscriber _subscriber;
    
    public async Task Execute(CreateDeviceEvent @event, IDictionary<string, string> items)
    {
        await _subscriber.CreateDevice(@event.DeviceId);
    }
}
```

**关键特点**:
- 每个事件类型有**独立的 Handler 类**
- 实现 `IEventHandler<TEvent>` 接口
- 职责单一，符合单一职责原则

---

## 六、支持的 MQ 和存储矩阵

### 6.1 消息中间件支持

| MQ 类型 | CAP | Shashlik | IoTSharp 使用场景 |
|---------|-----|----------|------------------|
| **RabbitMQ** | ✅ 完整支持 | ✅ 完整支持 | 生产环境推荐 |
| **Kafka** | ✅ 完整支持 | ✅ 完整支持 | 大数据量场景 |
| **ZeroMQ** | ✅ 支持 | ❌ 未实现 | 轻量级部署 |
| **NATS** | ✅ 支持 | ❌ 未实现 | 云原生部署 |
| **Pulsar** | ✅ 支持 | ❌ 未实现 | 大规模集群 |
| **Redis Streams** | ✅ 支持 | ❌ 未实现 | 已有 Redis 基础设施 |
| **Amazon SQS** | ✅ 支持 | ❌ 未实现 | AWS 云部署 |
| **Azure Service Bus** | ✅ 支持 | ❌ 未实现 | Azure 云部署 |
| **InMemory** | ✅ 支持 | ✅ 支持 | 开发/测试环境 |

### 6.2 事件存储支持

| 存储类型 | CAP | Shashlik | 用途 |
|---------|-----|----------|------|
| **PostgreSQL** | ✅ 完整支持 | ✅ 完整支持 | 默认推荐 |
| **MySQL** | ✅ 完整支持 | ✅ 完整支持 | 常见选择 |
| **SQL Server** | ✅ 完整支持 | ✅ 完整支持 | 企业环境 |
| **MongoDB** | ✅ 完整支持 | ❌ 未实现 | 文档存储 |
| **LiteDB** | ✅ 完整支持 | ❌ 未实现 | 嵌入式/轻量级 |
| **InMemory** | ✅ 支持 | ✅ 支持 | 开发/测试 |

### 6.3 配置枚举定义

**文件**: `IoTSharp.Contracts\AppSettings.cs`

```csharp
// 事件总线框架
public enum EventBusFramework { CAP, Shashlik }

// 事件存储
public enum EventBusStore 
{ 
    PostgreSql, MongoDB, InMemory, LiteDB, MySql, SqlServer 
}

// 消息队列
public enum EventBusMQ 
{ 
    RabbitMQ, Kafka, InMemory, ZeroMQ, NATS, 
    Pulsar, RedisStreams, AmazonSQS, AzureServiceBus 
}
```

---

## 七、在 IoTSharp 中的核心作用

### 7.1 架构解耦

**问题**：如果没有 EventBus？

```
设备 → MQTTService → 直接存储数据库 → 直接触发规则
```

**缺点**：
- ❌ 协议层与业务层紧耦合
- ❌ 同步处理，响应慢
- ❌ 无法横向扩展
- ❌ 失败重试复杂

**解决方案**：引入 EventBus

```
设备 → MQTTService → IPublisher → EventBus (异步) → ISubscriber → 业务处理
```

**优势**：
- ✅ 协议层只负责接收数据，不关心后续处理
- ✅ 异步处理，快速响应设备
- ✅ 订阅者可横向扩展（多实例消费）
- ✅ CAP 内置重试机制，保证最终一致性

---

### 7.2 典型使用场景

#### 场景 1：设备连接管理

**文件**: `IoTSharp\Services\MQTTService.cs`

```csharp
// 设备连接成功时
public async Task Server_ClientConnectedAsync(ClientConnectedEventArgs e)
{
    var device = (Device)e.SessionItems[nameof(Device)];
    
    // 发布连接事件（异步处理，不阻塞 MQTT 连接）
    _queue.PublishConnect(device.Id, ConnectStatus.Connected);
}

// 设备断开连接时
public async Task Server_ClientDisconnected(ClientDisconnectedEventArgs e)
{
    var device = (Device)e.SessionItems[nameof(Device)];
    
    // 发布断开事件
    await _queue.PublishConnect(device.Id, ConnectStatus.Disconnected);
}
```

**作用**：
- MQTT 连接事件不直接更新数据库，而是发布事件
- 订阅者异步处理设备状态变更
- 即使订阅者处理失败，消息也会重试

---

#### 场景 2：遥测数据上报

**文件**: `IoTSharp\Services\MQTTControllers\TelemetryController.cs`

```csharp
[MqttRoute("devices/{devname}/telemetry")]
public async Task<IActionResult> telemetry(string devname, [FromBody] object body)
{
    var device = FindDevice(devname);
    var keyValues = Message.ConvertPayloadToDictionary(body);
    
    // 发布遥测事件（立即返回，异步处理）
    await _queue.PublishTelemetryData(device, keyValues);
    
    return Ok();  // 快速响应设备
}
```

**作用**：
- 设备发送数据后快速收到 OK 响应
- 数据存储和规则触发在后台异步执行
- 高并发场景下不会阻塞 MQTT 连接

---

#### 场景 3：网关数据处理

**文件**: `IoTSharp\Services\MQTTControllers\GatewayController.cs`

```csharp
public async Task<IActionResult> telemetry(
    string devname, 
    [FromBody] Dictionary<string, List<GatewayPlayload>> gwData)
{
    foreach (var kv in gwData)
    {
        var subDevice = await JudgeOrCreateNewDevice(kv.Key, gateway);
        
        foreach (var t in kv.Value)
        {
            // 为每个子设备的每条数据发布事件
            await _queue.PublishTelemetryData(new PlayloadData()
            {
                DeviceId = subDevice.Id,
                ts = t.ts,
                MsgBody = t.values
            });
        }
    }
}
```

**作用**：
- 网关可能一次性上报多个子设备的多条数据
- 每条数据独立发布事件，可并行处理
- 提高吞吐量

---

### 7.3 与规则引擎的集成

**文件**: `IoTSharp\EventBus\EventBusSubscriber.cs`

每次数据存储后，都会触发规则引擎：

```csharp
public async Task StoreTelemetryData(PlayloadData msg)
{
    // 1. 存储遥测数据
    var result = await _storage.StoreTelemetryAsync(msg);
    
    // 2. 触发规则引擎（关键！）
    await RunRules(msg.DeviceId, (dynamic)exps, EventType.Telemetry);
    await RunRules(msg.DeviceId, array, EventType.TelemetryArray);
}

public async Task StoreAttributeData(PlayloadData msg, EventType _event)
{
    // 1. 存储属性数据
    var result2 = await _dbContext.SaveAsync<AttributeLatest>(dc, device.Id, msg.DataSide);
    
    // 2. 触发规则引擎（关键！）
    if (_event != EventType.None)
    {
        await RunRules(msg.DeviceId, dc.ToDynamic(), _event);
    }
}
```

**作用**：
- 数据存储和规则触发通过 EventBus 解耦
- 规则引擎通过 `EventBusOption.RunRules` 委托注册
- 实现了"数据存储 → 规则触发 → 可能产生新事件"的链式反应

---

### 7.4 注册流程

**文件**: `IoTSharp\Startup.cs`

#### 服务注册（ConfigureServices 方法）

```csharp
services.AddEventBus(opt =>
{
    opt.AppSettings = settings;
    opt.EventBusStore = Configuration.GetConnectionString("EventBusStore");
    opt.EventBusMQ = Configuration.GetConnectionString("EventBusMQ");
    opt.HealthChecks = healthChecks;
    
    // 根据配置选择 CAP 或 Shashlik
    switch (settings.EventBus)
    {
        case EventBusFramework.Shashlik:
            opt.UserShashlik();
            break;
        case EventBusFramework.CAP:
            opt.UserCAP();
            break;
        default:
            opt.UserShashlik();  // 默认 Shashlik
            break;
    }
});
```

#### 中间件注册（Configure 方法）

```csharp
app.UseEventBus(opt =>
{
    // 将规则引擎注册为事件处理器
    var frp = app.ApplicationServices.GetService<FlowRuleProcessor>();
    return frp.RunRules;
});
```

---

## 八、CAP vs Shashlik 对比总结

### 8.1 技术对比

| 维度 | CAP | Shashlik |
|------|-----|----------|
| **定位** | 分布式事务解决方案 | 轻量级事件总线 |
| **Topic 方式** | 字符串 Topic | 强类型事件类 |
| **类型安全** | ❌ 运行时检查 | ✅ 编译时检查 |
| **Dashboard** | ✅ 内置监控面板 | ❌ 无 |
| **MQ 支持** | 8 种 | 2 种 |
| **存储支持** | 6 种 | 3 种 |
| **代码复杂度** | 中等 | 简单 |
| **学习曲线** | 较陡 | 平缓 |
| **社区活跃度** | 高 | 中 |
| **适用场景** | 生产环境、大规模 | 中小规模、快速开发 |

### 8.2 代码风格对比

#### CAP 风格（字符串 Topic）

```csharp
// 发布
await _queue.PublishAsync("iotsharp.services.datastream.telemetrydata", msg);

// 订阅
[CapSubscribe("iotsharp.services.datastream.telemetrydata")]
public async Task telemetrydata(PlayloadData msg)
{
    await StoreTelemetryData(msg);
}
```

**优点**：
- Topic 命名灵活，可动态生成
- 支持通配符匹配（某些 MQ）

**缺点**：
- 字符串拼写错误编译时无法发现
- 需要严格遵循命名规范

---

#### Shashlik 风格（强类型事件）

```csharp
// 发布
await _queue.PublishAsync(new TelemetryDataEvent() { Data = msg });

// 订阅
public class TelemetryDataEventHandler : IEventHandler<TelemetryDataEvent>
{
    public async Task Execute(TelemetryDataEvent @event, ...)
    {
        await _subscriber.StoreTelemetryData(@event.Data);
    }
}
```

**优点**：
- 编译时类型检查，安全性高
- IDE 自动补全友好

**缺点**：
- 每种事件需要定义单独的类
- 扩展需要新增事件类

---

## 九、运行原理深度解析

### 9.1 CAP Outbox 模式（发件箱模式）

**问题**：如何保证本地事务与消息发布的原子性？

**场景**：
```
1. 设备连接成功
2. 更新数据库（设备状态 = 在线）
3. 发布消息到 MQ
```

**风险**：如果步骤 2 成功但步骤 3 失败，数据不一致！

**Outbox 模式解决方案**：

```
1. 开启数据库事务
2. 更新数据库（设备状态 = 在线）
3. 写入 Outbox 表（消息持久化） ← 同一事务！
4. 提交事务
5. 后台线程扫描 Outbox 表，发送到 MQ
6. 发送成功后删除 Outbox 记录
```

**CAP 实现**：
```csharp
// CAP 自动处理 Outbox，开发者无感知
await _queue.PublishAsync("topic", data);
// 内部逻辑：
// 1. 将消息写入当前数据库事务的 Outbox 表
// 2. 事务提交后，CAP 后台线程自动发送
```

**优势**：
- ✅ 消息不会丢失（持久化到数据库）
- ✅ 本地事务与消息发布原子性
- ✅ 失败自动重试

---

### 9.2 消息重试机制

**CAP 重试配置**：

```csharp
services.AddCap(x =>
{
    // 成功消息过期时间（秒）
    x.SucceedMessageExpiredAfter = 24 * 3600;
    
    // 消费者线程数
    x.ConsumerThreadCount = 10;
    
    // 失败重试次数（默认无限重试）
    // 可通过 Dashboard 手动停止
});
```

**重试流程**：

```
消息发送失败
  ↓
等待指数退避时间（1s, 2s, 4s, 8s, ...）
  ↓
重新发送
  ↓
如果仍失败 → 继续重试
  ↓
可在 Dashboard 查看失败消息
  ↓
可手动标记为"不再重试"
```

**优势**：
- ✅ 临时故障自动恢复（网络抖动）
- ✅ 可视化监控，问题可追溯

---

### 9.3 幂等性保证

**问题**：消息可能重复投递，如何避免重复处理？

**CAP 解决方案**：

```csharp
// CAP 为每条消息生成唯一 ID
// 订阅者处理前检查是否已处理过
if (已处理过该消息ID)
{
    return;  // 跳过重复消息
}
else
{
    标记消息ID为已处理
    执行业务逻辑
}
```

**数据库表**：
- `Cap.Published` - 已发布的消息
- `Cap.Received` - 已接收的消息（用于幂等性检查）

---

## 十、部署配置示例

### 10.1 开发环境（InMemory）

**文件**: `appsettings.Development.json`

```json
{
  "EventBus": "CAP",
  "EventBusStore": "InMemory",
  "EventBusMQ": "InMemory",
  "ConsumerThreadCount": 10
}
```

**特点**：
- ✅ 无需外部依赖，开箱即用
- ❌ 消息不持久化，重启丢失
- ❌ 不支持分布式多实例

---

### 10.2 生产环境（RabbitMQ + PostgreSQL）

**文件**: `appsettings.Production.json`

```json
{
  "EventBus": "CAP",
  "EventBusStore": "PostgreSql",
  "EventBusMQ": "RabbitMQ",
  "ConsumerThreadCount": 20,
  "ConnectionStrings": {
    "EventBusStore": "Server=pgsql;Database=IoTSharp;Username=postgres;Password=xxx;",
    "EventBusMQ": "amqp://guest:guest@rabbitmq:5672/"
  }
}
```

**特点**：
- ✅ 消息持久化，高可靠
- ✅ 支持分布式部署
- ✅ RabbitMQ 支持消息确认、死信队列

---

### 10.3 大数据量环境（Kafka + MongoDB）

```json
{
  "EventBus": "CAP",
  "EventBusStore": "MongoDB",
  "EventBusMQ": "Kafka",
  "ConsumerThreadCount": 50,
  "ConnectionStrings": {
    "EventBusStore": "mongodb://mongo:27017/iotsharp",
    "EventBusMQ": "kafka://kafka1:9092,kafka2:9092,kafka3:9092"
  }
}
```

**特点**：
- ✅ Kafka 高吞吐，适合海量设备
- ✅ MongoDB 文档存储，灵活 schema

---

## 十一、监控与运维

### 11.1 CAP Dashboard

**访问地址**: `http://localhost:5000/cap`

**功能**：
- 📊 发布/订阅统计（每小时成功/失败数）
- 📋 消息列表查看
- 🔍 消息详情（内容、重试次数）
- 🔄 手动重发失败消息
- ⏹️ 停止消息重试
- 📈 实时统计图表

**统计指标**：
- `Servers` - 活跃的消费者实例数
- `Subscribers` - 订阅者数量
- `PublishedSucceeded` - 发布成功数
- `PublishedFailed` - 发布失败数
- `ReceivedSucceeded` - 消费成功数
- `ReceivedFailed` - 消费失败数

---

### 11.2 健康检查

**Startup.cs 配置**：

```csharp
var healthChecks = services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

switch (settings.EventBusMQ)
{
    case EventBusMQ.RabbitMQ:
        healthChecks.AddRabbitMQ(connectionString);
        break;
    case EventBusMQ.Kafka:
        // Kafka 健康检查
        break;
}

switch (settings.EventBusStore)
{
    case EventBusStore.PostgreSql:
        healthChecks.AddNpgSql(connectionString);
        break;
}
```

**访问地址**: `http://localhost:5000/healthz`

---

## 十二、常见问题 FAQ

### Q1: CAP 和 Shashlik 如何选择？

**答**：
- **生产环境推荐 CAP** - 功能更全、生态更成熟
- **快速开发/小规模用 Shashlik** - 代码更简洁
- **IoTSharp 默认配置** - CAP + InMemory（开发环境）

---

### Q2: 消息丢失怎么办？

**答**：
- CAP Outbox 模式保证消息先持久化到数据库
- 即使应用崩溃，重启后 CAP 会重新发送未确认消息
- InMemory 模式消息不持久化，仅适合开发环境

---

### Q3: 如何保证消息顺序？

**答**：
- 同一 Topic 的消息在 CAP 中默认按顺序消费
- 但多实例部署时，不同实例可能并行处理