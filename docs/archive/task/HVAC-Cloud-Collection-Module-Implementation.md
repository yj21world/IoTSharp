# HVAC 云端采集模块实现文档

> **项目**: IoTSharp HVAC 管理系统
> **版本**: 1.0
> **日期**: 2026-04-23
> **状态**: ✅ 已完成

---

## 一、功能概述

本模块实现 IoTSharp 平台的 HVAC 云端采集功能，支持 Modbus RTU/TCP 协议，通过 MQTT 透传方式与工业网关（USR-G805）通信，采集冷水机组、水泵、冷却塔、风柜等 HVAC 设备数据。

### 1.1 网络拓扑

```
USR-G805 网关 (MQTT 透传)
    │
    │ RS485 总线
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

### 1.2 MQTT Topic 规范

| Topic | 方向 | 说明 |
|-------|------|------|
| `gateway/{name}/modbus/request/{id}` | 云端 → 网关 | Modbus 请求 |
| `gateway/{name}/modbus/response/{id}` | 网关 → 云端 | Modbus 响应 |

---

## 二、数据库设计

### 2.1 数据表清单

| 表名 | 说明 |
|------|------|
| `CollectionTasks` | 采集任务配置 |
| `CollectionDevices` | 采集从站配置 |
| `CollectionPoints` | 采集点位配置 |
| `CollectionLogs` | 采集日志 |
| `DeviceTypeProfiles` | 设备类型模板 |
| `CollectionRuleTemplates` | 采集规则模板 |

### 2.2 ER 图

```
DeviceTypeProfile (设备类型模板)
    │
    └── CollectionRuleTemplate (采集规则模板)

CollectionTask (采集任务)
    │
    └── CollectionDevice (采集从站) [1:N]
            │
            └── CollectionPoint (采集点位) [1:N]

Device ←── CollectionPoint.TargetDeviceId (目标子设备)
```

### 2.3 表结构详情

#### CollectionTasks (采集任务表)

```sql
CREATE TABLE CollectionTasks (
    Id UUID PRIMARY KEY,
    TaskKey VARCHAR(100) NOT NULL UNIQUE,
    GatewayDeviceId UUID NOT NULL,
    Protocol VARCHAR(50) DEFAULT 'Modbus',
    Version INT DEFAULT 1,
    Enabled BOOLEAN DEFAULT true,
    ConnectionJson TEXT,
    ReportPolicyJson TEXT,
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (GatewayDeviceId) REFERENCES Devices(Id)
);
```

#### CollectionDevices (采集从站表)

```sql
CREATE TABLE CollectionDevices (
    Id UUID PRIMARY KEY,
    TaskId UUID NOT NULL,
    DeviceKey VARCHAR(100) NOT NULL,
    DeviceName VARCHAR(200),
    SlaveId INT NOT NULL,
    Enabled BOOLEAN DEFAULT true,
    SortOrder INT DEFAULT 0,
    ProtocolOptionsJson TEXT,
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (TaskId) REFERENCES CollectionTasks(Id) ON DELETE CASCADE,
    UNIQUE (TaskId, SlaveId)
);
```

#### CollectionPoints (采集点位表)

```sql
CREATE TABLE CollectionPoints (
    Id UUID PRIMARY KEY,
    DeviceId UUID NOT NULL,
    PointKey VARCHAR(100) NOT NULL,
    PointName VARCHAR(200),
    FunctionCode INT NOT NULL,
    Address INT NOT NULL,
    RegisterCount INT DEFAULT 1,
    RawDataType VARCHAR(50) DEFAULT 'uint16',
    ByteOrder VARCHAR(10) DEFAULT 'AB',
    WordOrder VARCHAR(10) DEFAULT 'AB',
    ReadPeriodMs INT DEFAULT 30000,
    PollingGroup VARCHAR(50),
    TransformsJson TEXT,
    TargetDeviceId UUID,
    TargetName VARCHAR(100),
    TargetType VARCHAR(50) DEFAULT 'Telemetry',
    TargetValueType VARCHAR(50) DEFAULT 'Double',
    DisplayName VARCHAR(200),
    Unit VARCHAR(50),
    GroupName VARCHAR(100),
    Enabled BOOLEAN DEFAULT true,
    SortOrder INT DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (DeviceId) REFERENCES CollectionDevices(Id) ON DELETE CASCADE,
    FOREIGN KEY (TargetDeviceId) REFERENCES Devices(Id),
    UNIQUE (DeviceId, Address, FunctionCode)
);
```

#### CollectionLogs (采集日志表)

```sql
CREATE TABLE CollectionLogs (
    Id UUID PRIMARY KEY,
    GatewayDeviceId UUID NOT NULL,
    TaskId UUID,
    DeviceId UUID,
    PointId UUID,
    RequestId VARCHAR(100) NOT NULL,
    RequestAt TIMESTAMP NOT NULL,
    RequestFrame TEXT,
    ResponseAt TIMESTAMP,
    ResponseFrame TEXT,
    ParsedValue VARCHAR(500),
    ConvertedValue VARCHAR(500),
    Status VARCHAR(50) NOT NULL,
    ErrorMessage TEXT,
    DurationMs INT,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX IDX_CollectionLogs_GatewayTime (GatewayDeviceId, CreatedAt),
    INDEX IDX_CollectionLogs_Status (Status),
    INDEX IDX_CollectionLogs_RequestId (RequestId)
);
```

#### DeviceTypeProfiles (设备类型模板表)

```sql
CREATE TABLE DeviceTypeProfiles (
    Id UUID PRIMARY KEY,
    ProfileKey VARCHAR(100) NOT NULL UNIQUE,
    ProfileName VARCHAR(200),
    DeviceType INT NOT NULL,
    Description TEXT,
    Icon VARCHAR(500),
    Version INT DEFAULT 1,
    Enabled BOOLEAN DEFAULT true,
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    INDEX IDX_DeviceTypeProfiles_DeviceType (DeviceType)
);
```

#### CollectionRuleTemplates (采集规则模板表)

```sql
CREATE TABLE CollectionRuleTemplates (
    Id UUID PRIMARY KEY,
    ProfileId UUID NOT NULL,
    PointKey VARCHAR(100) NOT NULL,
    PointName VARCHAR(200),
    Description TEXT,
    FunctionCode INT NOT NULL,
    Address INT NOT NULL,
    RegisterCount INT DEFAULT 1,
    RawDataType VARCHAR(50) DEFAULT 'uint16',
    ByteOrder VARCHAR(10) DEFAULT 'AB',
    WordOrder VARCHAR(10) DEFAULT 'AB',
    ReadPeriodMs INT DEFAULT 30000,
    PollingGroup VARCHAR(50),
    TransformsJson TEXT,
    TargetName VARCHAR(100),
    TargetType VARCHAR(50) DEFAULT 'Telemetry',
    TargetValueType VARCHAR(50) DEFAULT 'Double',
    Unit VARCHAR(50),
    GroupName VARCHAR(100),
    SortOrder INT DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (ProfileId) REFERENCES DeviceTypeProfiles(Id) ON DELETE CASCADE,
    INDEX IDX_CollectionRuleTemplates_ProfileId (ProfileId)
);
```

---

## 三、服务架构

### 3.1 核心组件

```
ModbusCollectionService (BackgroundService)
    │
    ├── 订阅 MQTT 响应 Topic (gateway/+/modbus/response/+)
    │
    ├── GatewaySchedulerManager
    │       │
    │       └── GatewayScheduler (每网关一个)
    │               │
    │               ├── 高优先级队列 (<15s)
    │               ├── 中优先级队列 (15-45s)
    │               └── 低优先级队列 (>45s)
    │
    └── BatchMerger (批量合并)
            │
            └── 合并同从站连续地址的点位为批量请求
```

### 3.2 关键类说明

| 类 | 职责 |
|----|------|
| `ModbusCollectionService` | 采集服务入口，MQTT 消息处理，响应解析 |
| `GatewayScheduler` | 单个网关的采集调度，按周期触发请求 |
| `GatewaySchedulerManager` | 管理所有网关调度器 |
| `BatchMerger` | 合并同从站连续地址的点位为批量请求 |
| `ModbusRtuProtocol` | Modbus RTU 组帧/解析/CRC 校验 |
| `ModbusDataParser` | 数据类型解析/字节序处理/换算规则 |
| `CollectionTaskService` | 采集任务 CRUD 操作 |
| `DeviceTypeProfileService` | 设备类型模板 CRUD + 应用模板 |

---

## 四、API 接口

### 4.1 采集任务 API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/CollectionTask/GetAll` | 获取所有采集任务 |
| GET | `/api/CollectionTask/{id}` | 获取任务详情 |
| POST | `/api/CollectionTask/Create` | 创建设集任务 |
| PUT | `/api/CollectionTask/{id}` | 更新采集任务 |
| DELETE | `/api/CollectionTask/{id}` | 删除采集任务 |
| POST | `/api/CollectionTask/{id}/Enable` | 启用采集任务 |
| POST | `/api/CollectionTask/{id}/Disable` | 禁用采集任务 |
| GET | `/api/CollectionTask/GetLogs` | 获取采集日志 |

### 4.2 设备类型模板 API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/DeviceTypeProfile/GetAll` | 获取所有设备类型模板 |
| GET | `/api/DeviceTypeProfile/{id}` | 获取模板详情 |
| POST | `/api/DeviceTypeProfile/Create` | 创建设备类型模板 |
| PUT | `/api/DeviceTypeProfile/{id}` | 更新设备类型模板 |
| DELETE | `/api/DeviceTypeProfile/{id}` | 删除设备类型模板 |
| GET | `/api/DeviceTypeProfile/{profileId}/rules` | 获取采集规则模板 |
| POST | `/api/DeviceTypeProfile/{profileId}/rules` | 添加采集规则模板 |
| PUT | `/api/DeviceTypeProfile/{profileId}/rules/{ruleId}` | 更新采集规则模板 |
| DELETE | `/api/DeviceTypeProfile/{profileId}/rules/{ruleId}` | 删除采集规则模板 |
| POST | `/api/DeviceTypeProfile/ApplyProfile` | 应用模板到设备 |

---

## 五、设备类型枚举

```csharp
public enum HVACDeviceType
{
    Unknown = 0,
    Chiller = 1,           // 冷水机组
    HeatPump = 2,          // 热泵机组
    WaterPump = 10,         // 水泵
    CoolingTower = 11,     // 冷却塔
    AirHandlingUnit = 20,  // 风柜 (AHU)
    FanCoilUnit = 21,      // 风机盘管 (FCU)
    Valve = 30,             // 阀门
    Damper = 31,           // 风阀
    Fan = 32,              // 风机
    PowerMeter = 40,       // 电表
    FlowMeter = 41,         // 流量计
    TemperatureSensor = 50, // 温度传感器
    HumiditySensor = 51,   // 湿度传感器
    PressureSensor = 52,    // 压力传感器
}
```

---

## 六、文件清单

### 6.1 新建文件

#### 数据层 (IoTSharp.Data)

| 文件路径 | 说明 |
|----------|------|
| `CollectionTask.cs` | 采集任务实体 |
| `CollectionDevice.cs` | 采集从站实体 |
| `CollectionPoint.cs` | 采集点位实体 |
| `CollectionLog.cs` | 采集日志实体 |
| `DeviceTypeProfile.cs` | 设备类型模板实体 |
| `CollectionRuleTemplate.cs` | 采集规则模板实体 |
| `Configurations/CollectionTaskConfiguration.cs` | EF 配置 |
| `Configurations/CollectionDeviceConfiguration.cs` | EF 配置 |
| `Configurations/CollectionPointConfiguration.cs` | EF 配置 |
| `Configurations/CollectionLogConfiguration.cs` | EF 配置 |
| `Configurations/DeviceTypeProfileConfiguration.cs` | EF 配置 |
| `Configurations/CollectionRuleTemplateConfiguration.cs` | EF 配置 |

#### 服务层 (IoTSharp)

| 文件路径 | 说明 |
|----------|------|
| `Services/ModbusCollection/CollectionResponse.cs` | 响应数据结构 |
| `Services/ModbusCollection/ModbusRtuProtocol.cs` | Modbus RTU 协议栈 |
| `Services/ModbusCollection/ModbusDataParser.cs` | 数据解析器 |
| `Services/ModbusCollection/BatchMerger.cs` | 批量合并器 |
| `Services/ModbusCollection/CollectionConfigurationLoader.cs` | 配置加载器 |
| `Services/ModbusCollection/GatewayScheduler.cs` | 网关调度器 |
| `Services/ModbusCollection/GatewaySchedulerManager.cs` | 调度器管理器 |
| `Services/ModbusCollection/ModbusCollectionService.cs` | 采集服务入口 |
| `Services/CollectionTaskService.cs` | 采集任务服务 |
| `Services/DeviceTypeProfileService.cs` | 设备类型模板服务 |
| `Extensions/ModbusCollectionExtension.cs` | 服务注册扩展 |

#### 控制器 (IoTSharp)

| 文件路径 | 说明 |
|----------|------|
| `Controllers/CollectionTaskController.cs` | 采集任务控制器 |
| `Controllers/DeviceTypeProfileController.cs` | 设备类型模板控制器 |

#### DTO (IoTSharp.Dtos)

| 文件路径 | 说明 |
|----------|------|
| `DeviceTypeProfileDtos.cs` | 设备类型模板 DTO |

#### 前端 (ClientApp)

| 文件路径 | 说明 |
|----------|------|
| `src/api/devicetypeprofile/index.ts` | 设备类型模板 API |
| `src/views/iot/devicetypeprofile/devicetypeprofilelist.vue` | 设备类型模板页面 |

### 6.2 修改文件

| 文件路径 | 修改内容 |
|----------|----------|
| `IoTSharp.Contracts/Enums.cs` | 添加 HVACDeviceType 枚举 |
| `IoTSharp.Data/Device.cs` | 添加 HvacDeviceType 和 DeviceTypeProfileId 字段 |
| `IoTSharp.Data/ApplicationDbContext.cs` | 注册新实体 |
| `IoTSharp/Startup.cs` | 注册 ModbusCollectionService |

---

## 七、使用流程

### 7.1 创建设备类型模板

1. 进入 **设备类型模板管理** 页面
2. 点击 **新增模板**
3. 选择设备类型（如"冷水机组"）
4. 添加采集规则模板（点位、功能码、地址、换算规则等）
5. 保存模板

### 7.2 创建设备并应用模板

1. 进入 **设备管理** 页面
2. 创建设备，选择关联的网关
3. 可选：选择设备类型模板
4. 如果选择了模板，系统自动创建采集点位

### 7.3 管理采集任务

1. 进入 **采集任务管理** 页面
2. 查看任务列表和状态
3. 启用/禁用任务
4. 查看采集日志

---

## 八、验证测试清单

### 8.1 单元测试

- [ ] ModbusRtuProtocol.BuildReadRequest - 组帧测试
- [ ] ModbusRtuProtocol.CalculateCrc - CRC 校验测试
- [ ] ModbusRtuProtocol.ParseResponse - 响应解析测试
- [ ] ModbusDataParser.ParseFloat32 - Float32 解析
- [ ] ModbusDataParser.ApplyTransforms - 换算规则测试
- [ ] BatchMerger.Merge - 批量合并测试

### 8.2 集成测试

- [ ] 创建设备类型模板
- [ ] 添加工具类型采集规则
- [ ] 创建设备并应用模板
- [ ] 验证自动生成采集点位
- [ ] 启动采集任务
- [ ] 收到 MQTT 响应
- [ ] 遥测数据写入 TelemetryLatest

### 8.3 手动测试

- [ ] Modbus Poll 工具发送请求，WireShark 抓包验证
- [ ] 超时/CRC 错误处理
- [ ] 网关离线/上线处理
- [ ] 前端页面 CRUD 操作

---

## 九、数据库迁移

```bash
# 创建迁移
dotnet ef migrations add AddCollectionTables

# 应用迁移
dotnet ef database update
```

---

## 十、相关文档

- 需求文档: `docs/archive/task/HVAC-Cloud-Collection-Module.md`
- 实施计划: `docs/archive/task/HVAC-Cloud-Collection-Module-Implementation-Plan.md`
- 项目概述: `AGENTS.md`
