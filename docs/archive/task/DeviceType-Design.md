# 设备类型（DeviceType）体系设计文档

> 版本：1.0.0 | 目标框架：.NET 10.0
> 说明：本文档废弃原有的 Produce 和 DeviceModel 体系，重新设计统一的设备类型模板系统。

---

## 一、设计目标

设备类型是**可复用的设备模板**，用于在 HVAC 等工业物联网场景中快速定义和部署同类设备。

核心目标：
1. **一次定义，多次复用** —— 定义冷水机组、水泵、冷却塔等设备类型，新建设备时一键实例化。
2. **覆盖全生命周期** —— 属性定义、Modbus 采集规则、命令下发、告警规则模板。
3. **高效项目实施** —— 新项目只需选择设备类型并填写少量参数（如 Modbus 从机地址），即可快速完成设备接入。

---

## 二、核心概念

### 2.1 设备类型（DeviceType）

设备类型是对一类设备的抽象定义，包含该类设备的全部元数据和行为规则。

```
DeviceType（设备类型）
├── 基本信息
│   ├── Name（名称）: "冷水机组"
│   ├── Code（编码）: "chiller"
│   ├── Category（分类）: HVAC / 电力 / 环境 / 安防
│   ├── Icon（图标）
│   ├── Description（描述）
│   └── Version（版本）: 用于模板迭代
│
├── 属性定义（PropertyDefinitions）
│   ├── 设备序列号（String）
│   ├── 额定功率（Double, kW）
│   ├── 安装位置（String）
│   └── 运行模式设定（Enum: 制冷/制热/通风）
│
├── 遥测定义（TelemetryDefinitions）
│   ├── 回水温度（Double, °C）
│   ├── 出水温度（Double, °C）
│   ├── 冷凝压力（Double, MPa）
│   ├── 蒸发压力（Double, MPa）
│   ├── 运行电流（Double, A）
│   ├── 运行功率（Double, kW）
│   └── 累计运行时长（Long, h）
│
├── 采集规则（CollectionRules）
│   └── Modbus 寄存器映射、轮询周期、数据转换公式
│
├── 命令定义（CommandDefinitions）
│   ├── 开机（WriteSingleCoil）
│   ├── 关机（WriteSingleCoil）
│   ├── 设定温度（WriteHoldingRegister）
│   └── 切换模式（WriteHoldingRegister）
│
└── 告警规则模板（AlarmRuleTemplates）
    ├── 出水温度过高（> 设定值 + 5°C）
    ├── 冷凝压力过低（< 下限值）
    ├── 压缩机过载（电流 > 额定值 × 1.2）
    └── 设备离线（心跳超时）
```

### 2.2 设备实例（Device）

设备是设备类型的具体实例，继承设备类型的全部定义，并补充实例特定的配置。

```
Device（设备实例）
├── 基本信息
│   ├── Name（名称）: "1#冷水机组"
│   ├── DeviceTypeId → 关联 DeviceType
│   └── Location（位置）: "A栋地下室机房"
│
├── 实例配置（InstanceConfig）
│   ├── ModbusSlaveId（从机地址）: 1
│   ├── ModbusGatewayId（网关）: → Gateway
│   └── 其他实例参数...
│
├── 属性值（Attributes）
│   ├── 设备序列号 = "CH-2024-001"
│   ├── 额定功率 = 500.0
│   └── 运行模式设定 = "制冷"
│
├── 遥测值（Telemetries）
│   ├── 回水温度 = 12.5
│   ├── 出水温度 = 7.2
│   └── ...
│
├── 采集任务（CollectionTasks）
│   └── 从 DeviceType.CollectionRules 实例化，绑定实例参数
│
├── 命令通道（CommandChannels）
│   └── 从 DeviceType.CommandDefinitions 实例化
│
└── 告警规则（AlarmRules）
    └── 从 DeviceType.AlarmRuleTemplates 实例化，绑定实例参数
```

---

## 三、实体设计

### 3.1 设备类型（DeviceType）

```csharp
public class DeviceType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // 基本信息
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DeviceCategory Category { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; } = 1;

    // 关联定义
    public List<DeviceTypeProperty> Properties { get; set; } = [];
    public List<DeviceTypeTelemetry> Telemetries { get; set; } = [];
    public List<DeviceTypeCollectionRule> CollectionRules { get; set; } = [];
    public List<DeviceTypeCommand> Commands { get; set; } = [];
    public List<DeviceTypeAlarmRuleTemplate> AlarmRuleTemplates { get; set; } = [];

    // 租户隔离
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    // 审计
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
```

### 3.2 属性定义（DeviceTypeProperty）

```csharp
public class DeviceTypeProperty
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DataType DataType { get; set; }
    public string? Unit { get; set; }
    public string? DefaultValue { get; set; }
    public bool Required { get; set; }
    public bool ReadOnly { get; set; }
    public int SortOrder { get; set; }
}
```

### 3.3 遥测定义（DeviceTypeTelemetry）

```csharp
public class DeviceTypeTelemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DataType DataType { get; set; }
    public string? Unit { get; set; }
    public string? DefaultValue { get; set; }
    public int SortOrder { get; set; }
}
```

### 3.4 采集规则（DeviceTypeCollectionRule）

```csharp
public class DeviceTypeCollectionRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = null!;

    // 关联的遥测键
    public string TelemetryKey { get; set; } = string.Empty;

    // Modbus 配置
    public ModbusFunctionCode FunctionCode { get; set; }
    public ushort RegisterAddress { get; set; }
    public ushort RegisterCount { get; set; }
    public ModbusDataType RegisterDataType { get; set; }
    public ByteOrder ByteOrder { get; set; }

    // 数据转换
    public string? ConversionExpression { get; set; }
    public double? ScaleFactor { get; set; }
    public double? Offset { get; set; }

    // 轮询配置
    public int PollingIntervalMs { get; set; } = 5000;
    public int TimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 3;
}
```

### 3.5 命令定义（DeviceTypeCommand）

```csharp
public class DeviceTypeCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Modbus 下发配置
    public ModbusFunctionCode FunctionCode { get; set; }
    public ushort RegisterAddress { get; set; }
    public ModbusDataType DataType { get; set; }

    // 参数定义（JSON Schema）
    public string? ParameterSchema { get; set; }

    // 命令模板（支持变量替换）
    public string? CommandTemplate { get; set; }

    // 确认机制
    public bool RequireConfirmation { get; set; }
    public int ConfirmationTimeoutMs { get; set; } = 5000;
}
```

### 3.6 告警规则模板（DeviceTypeAlarmRuleTemplate）

```csharp
public class DeviceTypeAlarmRuleTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AlarmSeverity Severity { get; set; }

    // 触发条件表达式
    public string ConditionExpression { get; set; } = string.Empty;

    // 关联的遥测键（用于快速绑定）
    public string? RelatedTelemetryKey { get; set; }

    // 告警内容模板
    public string? MessageTemplate { get; set; }

    // 自动恢复条件
    public string? RecoveryExpression { get; set; }

    // 抑制配置
    public int? CooldownSeconds { get; set; }
}
```

### 3.7 设备实例（Device）改造

```csharp
public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; } = null!;
    public Guid DeviceTypeId { get; set; }

    // 实例特定配置（JSON）
    public string? InstanceConfig { get; set; }

    // 网关关联（Modbus 场景）
    public Guid? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }

    // 租户/客户
    public Tenant Tenant { get; set; } = null!;
    public Customer? Customer { get; set; }

    // 状态
    public DeviceStatus Status { get; set; }
    public DateTime? LastOnlineTime { get; set; }

    // 审计
    public bool Deleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## 四、数据库设计

### 4.1 表结构

```sql
-- 设备类型主表
CREATE TABLE DeviceTypes (
    Id UUID PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Code VARCHAR(50) NOT NULL,
    Category INT NOT NULL,
    Icon VARCHAR(255),
    Description TEXT,
    Version INT DEFAULT 1,
    TenantId UUID NOT NULL,
    CreatedAt TIMESTAMP NOT NULL,
    CreatedBy UUID NOT NULL,
    UpdatedAt TIMESTAMP,
    UpdatedBy UUID,
    UNIQUE(TenantId, Code)
);

-- 属性定义
CREATE TABLE DeviceTypeProperties (
    Id UUID PRIMARY KEY,
    DeviceTypeId UUID NOT NULL REFERENCES DeviceTypes(Id) ON DELETE CASCADE,
    Key VARCHAR(50) NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    Description TEXT,
    DataType INT NOT NULL,
    Unit VARCHAR(20),
    DefaultValue VARCHAR(255),
    Required BOOLEAN DEFAULT FALSE,
    ReadOnly BOOLEAN DEFAULT FALSE,
    SortOrder INT DEFAULT 0,
    UNIQUE(DeviceTypeId, Key)
);

-- 遥测定义
CREATE TABLE DeviceTypeTelemetries (
    Id UUID PRIMARY KEY,
    DeviceTypeId UUID NOT NULL REFERENCES DeviceTypes(Id) ON DELETE CASCADE,
    Key VARCHAR(50) NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    Description TEXT,
    DataType INT NOT NULL,
    Unit VARCHAR(20),
    DefaultValue VARCHAR(255),
    SortOrder INT DEFAULT 0,
    UNIQUE(DeviceTypeId, Key)
);

-- 采集规则
CREATE TABLE DeviceTypeCollectionRules (
    Id UUID PRIMARY KEY,
    DeviceTypeId UUID NOT NULL REFERENCES DeviceTypes(Id) ON DELETE CASCADE,
    TelemetryKey VARCHAR(50) NOT NULL,
    FunctionCode INT NOT NULL,
    RegisterAddress INT NOT NULL,
    RegisterCount INT DEFAULT 1,
    RegisterDataType INT NOT NULL,
    ByteOrder INT DEFAULT 0,
    ConversionExpression VARCHAR(255),
    ScaleFactor DOUBLE PRECISION,
    Offset DOUBLE PRECISION,
    PollingIntervalMs INT DEFAULT 5000,
    TimeoutMs INT DEFAULT 3000,
    RetryCount INT DEFAULT 3,
    UNIQUE(DeviceTypeId, TelemetryKey)
);

-- 命令定义
CREATE TABLE DeviceTypeCommands (
    Id UUID PRIMARY KEY,
    DeviceTypeId UUID NOT NULL REFERENCES DeviceTypes(Id) ON DELETE CASCADE,
    Key VARCHAR(50) NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    Description TEXT,
    FunctionCode INT NOT NULL,
    RegisterAddress INT NOT NULL,
    DataType INT NOT NULL,
    ParameterSchema TEXT,
    CommandTemplate TEXT,
    RequireConfirmation BOOLEAN DEFAULT FALSE,
    ConfirmationTimeoutMs INT DEFAULT 5000,
    UNIQUE(DeviceTypeId, Key)
);

-- 告警规则模板
CREATE TABLE DeviceTypeAlarmRuleTemplates (
    Id UUID PRIMARY KEY,
    DeviceTypeId UUID NOT NULL REFERENCES DeviceTypes(Id) ON DELETE CASCADE,
    Name VARCHAR(100) NOT NULL,
    Description TEXT,
    Severity INT NOT NULL,
    ConditionExpression TEXT NOT NULL,
    RelatedTelemetryKey VARCHAR(50),
    MessageTemplate TEXT,
    RecoveryExpression TEXT,
    CooldownSeconds INT
);

-- 设备实例（改造后）
ALTER TABLE Device ADD COLUMN DeviceTypeId UUID REFERENCES DeviceTypes(Id);
ALTER TABLE Device ADD COLUMN InstanceConfig TEXT;
```

---

## 五、关键流程

### 5.1 创建设备类型

```
1. 用户填写设备类型基本信息（名称、编码、分类）
2. 定义属性列表（序列号、额定功率等）
3. 定义遥测列表（温度、压力、电流等）
4. 配置采集规则（Modbus 寄存器映射）
5. 配置命令定义（开关机、设定温度等）
6. 配置告警规则模板（阈值、表达式）
7. 保存为版本 1，可后续迭代
```

### 5.2 基于设备类型创建设备

```
1. 用户选择设备类型（如"冷水机组"）
2. 系统展示该类型的属性定义，用户填写实例值
3. 用户配置实例特定参数（Modbus 从机地址、网关）
4. 系统根据设备类型自动创建：
   - Device 记录
   - AttributeLatest 记录（从属性定义复制默认值）
   - CollectionTask 记录（从采集规则实例化，绑定从机地址）
   - AlarmRule 记录（从告警规则模板实例化）
5. 设备开始按采集规则轮询数据
```

### 5.3 设备类型版本迭代

```
1. 修改设备类型定义（新增遥测、调整采集规则等）
2. 保存为新版本（Version + 1）
3. 用户可选择将现有设备升级到新版本
4. 升级时：
   - 新增的定义自动实例化
   - 已有的定义保持不变
   - 删除的定义标记为废弃
```

---

## 六、与现有系统的关系

### 6.1 废弃的实体

| 废弃实体 | 替代方案 |
|---------|---------|
| `Produce` | `DeviceType` |
| `ProduceData` | `DeviceTypeProperty` + `DeviceTypeTelemetry` |
| `ProduceDictionary` | `DeviceTypeProperty` / `DeviceTypeTelemetry` |
| `ProduceDataMapping` | `DeviceTypeCollectionRule` |
| `DeviceModel` | `DeviceType` |
| `DeviceModelCommand` | `DeviceTypeCommand` |

### 6.2 保留的实体

| 保留实体 | 说明 |
|---------|------|
| `Device` | 改造：添加 `DeviceTypeId` 外键和 `InstanceConfig` |
| `AttributeLatest` | 不变，实例化时从 `DeviceTypeProperty` 复制默认值 |
| `TelemetryData` / `TelemetryLatest` | 不变，采集任务写入 |
| `Alarm` | 不变，告警规则触发时创建 |
| `Gateway` | 不变，设备通过网关关联到采集任务 |

### 6.3 新增的服务层

```
Services/
├── IDeviceTypeService.cs          # 设备类型 CRUD + 版本管理
├── DeviceTypeService.cs
├── IDeviceProvisioningService.cs  # 设备实例化服务
├── DeviceProvisioningService.cs
├── ICollectionTaskService.cs      # 采集任务管理
├── CollectionTaskService.cs
└── IAlarmRuleService.cs           # 告警规则管理（从模板实例化）
    AlarmRuleService.cs
```

---

## 七、HVAC 场景示例

### 7.1 定义"冷水机组"设备类型

```json
{
  "name": "冷水机组",
  "code": "chiller",
  "category": "HVAC",
  "properties": [
    { "key": "serialNumber", "displayName": "设备序列号", "dataType": "String", "required": true },
    { "key": "ratedPower", "displayName": "额定功率", "dataType": "Double", "unit": "kW", "defaultValue": "500" },
    { "key": "refrigerantType", "displayName": "冷媒类型", "dataType": "String", "defaultValue": "R134a" }
  ],
  "telemetries": [
    { "key": "returnWaterTemp", "displayName": "回水温度", "dataType": "Double", "unit": "°C" },
    { "key": "supplyWaterTemp", "displayName": "出水温度", "dataType": "Double", "unit": "°C" },
    { "key": "condensingPressure", "displayName": "冷凝压力", "dataType": "Double", "unit": "MPa" },
    { "key": "evaporatingPressure", "displayName": "蒸发压力", "dataType": "Double", "unit": "MPa" },
    { "key": "runningCurrent", "displayName": "运行电流", "dataType": "Double", "unit": "A" },
    { "key": "runningPower", "displayName": "运行功率", "dataType": "Double", "unit": "kW" },
    { "key": "runningHours", "displayName": "累计运行时长", "dataType": "Long", "unit": "h" },
    { "key": "compressorStatus", "displayName": "压缩机状态", "dataType": "Boolean" }
  ],
  "collectionRules": [
    { "telemetryKey": "returnWaterTemp", "functionCode": "ReadHoldingRegisters", "registerAddress": 0, "registerDataType": "Float32", "scaleFactor": 0.1, "pollingIntervalMs": 5000 },
    { "telemetryKey": "supplyWaterTemp", "functionCode": "ReadHoldingRegisters", "registerAddress": 2, "registerDataType": "Float32", "scaleFactor": 0.1, "pollingIntervalMs": 5000 },
    { "telemetryKey": "condensingPressure", "functionCode": "ReadHoldingRegisters", "registerAddress": 4, "registerDataType": "Float32", "scaleFactor": 0.001, "pollingIntervalMs": 5000 },
    { "telemetryKey": "runningPower", "functionCode": "ReadHoldingRegisters", "registerAddress": 10, "registerDataType": "Float32", "scaleFactor": 0.1, "pollingIntervalMs": 10000 },
    { "telemetryKey": "compressorStatus", "functionCode": "ReadCoils", "registerAddress": 0, "registerDataType": "Bool", "pollingIntervalMs": 5000 }
  ],
  "commands": [
    { "key": "start", "displayName": "开机", "functionCode": "WriteSingleCoil", "registerAddress": 0, "commandTemplate": "true" },
    { "key": "stop", "displayName": "关机", "functionCode": "WriteSingleCoil", "registerAddress": 0, "commandTemplate": "false" },
    { "key": "setTemperature", "displayName": "设定温度", "functionCode": "WriteSingleRegister", "registerAddress": 20, "dataType": "UInt16", "parameterSchema": "{\"type\":\"number\",\"min\":5,\"max\":15}", "commandTemplate": "{{value * 10}}" }
  ],
  "alarmRuleTemplates": [
    { "name": "出水温度过高", "severity": "Warning", "conditionExpression": "supplyWaterTemp > 12", "relatedTelemetryKey": "supplyWaterTemp", "messageTemplate": "出水温度 {{supplyWaterTemp}}°C 超过设定值 12°C" },
    { "name": "冷凝压力过低", "severity": "Critical", "conditionExpression": "condensingPressure < 0.8", "relatedTelemetryKey": "condensingPressure", "messageTemplate": "冷凝压力 {{condensingPressure}}MPa 低于下限 0.8MPa" },
    { "name": "设备离线", "severity": "Critical", "conditionExpression": "lastOnlineTime < now() - 300", "messageTemplate": "设备离线超过 5 分钟" }
  ]
}
```

### 7.2 实例化设备

```json
{
  "name": "1#冷水机组",
  "deviceTypeId": "...",
  "instanceConfig": {
    "modbusSlaveId": 1,
    "gatewayId": "..."
  },
  "attributes": {
    "serialNumber": "CH-2024-001",
    "ratedPower": 500,
    "refrigerantType": "R134a"
  }
}
```

系统根据 `deviceTypeId` 自动：
1. 创建 `Device` 记录
2. 创建 `AttributeLatest` 记录（序列号、额定功率、冷媒类型）
3. 创建 `CollectionTask` 记录（8 个采集点，从机地址=1）
4. 创建 `AlarmRule` 记录（3 条告警规则）

---

## 八、API 设计

### 8.1 设备类型管理

```
GET    /api/devicetypes              # 列表（支持分页、分类筛选）
GET    /api/devicetypes/{id}         # 详情（包含全部定义）
POST   /api/devicetypes              # 创建
PUT    /api/devicetypes/{id}         # 更新（创建新版本）
DELETE /api/devicetypes/{id}         # 删除（检查是否被设备引用）
POST   /api/devicetypes/{id}/clone   # 克隆为新类型
```

### 8.2 设备实例化

```
POST   /api/devices/from-template    # 基于设备类型创建设备
       Body: { deviceTypeId, name, instanceConfig, attributes }

POST   /api/devices/bulk-create      # 批量创建设备（相同类型）
       Body: { deviceTypeId, devices: [{name, instanceConfig, attributes}] }
```

### 8.3 设备类型升级

```
POST   /api/devicetypes/{id}/upgrade-devices  # 将设备升级到新版本
       Body: { deviceIds, version }
```

---

## 九、实施计划

| 阶段 | 任务 | 优先级 |
|------|------|--------|
| 1 | 创建 `DeviceType` 实体及子实体 | P0 |
| 2 | 创建数据库迁移 | P0 |
| 3 | 实现 `IDeviceTypeService` | P0 |
| 4 | 实现 `IDeviceProvisioningService` | P0 |
| 5 | 改造 `Device` 实体，添加 `DeviceTypeId` | P0 |
| 6 | 创建 `DeviceTypesController` | P0 |
| 7 | 实现采集任务调度（基于 `DeviceTypeCollectionRule`） | P1 |
| 8 | 实现告警规则实例化（基于 `DeviceTypeAlarmRuleTemplate`） | P1 |
| 9 | 前端页面：设备类型管理 | P1 |
| 10 | 前端页面：基于模板创建设备向导 | P1 |
| 11 | 废弃 `Produce`、`DeviceModel` 相关代码 | P2 |
| 12 | 数据迁移：将现有 Produce 数据迁移到 DeviceType | P2 |

---

## 十、总结

本文档定义了一套全新的**设备类型（DeviceType）**体系，用于替代现有的 `Produce` 和 `DeviceModel` 残次品。

核心改进：
1. **统一模板**：一个 `DeviceType` 涵盖属性、遥测、采集规则、命令、告警规则全部定义。
2. **高效部署**：新建设备时选择类型即可自动实例化全部配置。
3. **版本管理**：支持设备类型版本迭代，设备可升级到新版本。
4. **服务层抽象**：引入 `IDeviceTypeService`、`IDeviceProvisioningService` 等服务层，避免业务逻辑堆在控制器中。
5. **HVAC 场景原生支持**：Modbus 寄存器映射、轮询周期、数据转换等作为一等公民。
