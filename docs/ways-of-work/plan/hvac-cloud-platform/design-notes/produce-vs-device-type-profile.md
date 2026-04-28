# 设计笔记：Produce 与 DeviceTypeProfile 的关系

> 创建日期：2026-04-27
> 状态：待讨论

## 1. 背景

当前代码中存在两套"设备类型模板"概念：

- **`Produce`**：IoTSharp 原有的产品模板，包含 `ProduceDictionary`（字段元数据）和 `ProduceDataMapping`（字段映射）。
- **`DeviceTypeProfile`**：后续新增的设备类型模板，关联 `CollectionRuleTemplate`（采集规则）。

两者解决的问题有重叠，导致概念混淆和职责不清。

## 2. 现有实体对比

### 2.1 Produce（旧）

```
Produce
├── Name, Icon, Description
├── GatewayType, GatewayConfiguration    ← 耦合了网关概念
├── Tenant, Customer
├── DefaultTimeout, DefaultIdentityType
├── DefaultDeviceType
├── DefaultAttributes : ProduceData[]
├── Devices : Device[]
├── Dictionaries : ProduceDictionary[]  ← 字段元数据（KeyName/Unit/DataType/Place0~5）
├── DataMappings : ProduceDataMapping[] ← 抽象字段 ↔ 实际字段映射
└── ProduceToken
```

### 2.2 DeviceTypeProfile（新）

```
DeviceTypeProfile
├── ProfileKey, ProfileName
├── HVACDeviceType
├── Version, Enabled, CreatedAt, UpdatedAt
├── CollectionRules : CollectionRuleTemplate[]  ← 采集规则（Modbus参数/转换/目标）
└── Devices : Device[]
```

### 2.3 核心差异

| 维度 | Produce | DeviceTypeProfile |
|---|---|---|
| 核心概念 | 产品模板（Product Template） | 设备类型模板 |
| 定义内容 | 字段元数据 + 网关配置 + 认证方式 | 暖通设备分类 + 采集规则 |
| 采集配置 | **没有** | 包含（通过 CollectionRuleTemplate） |
| 多租户 | Tenant + Customer | 无，全局 |
| 网关耦合 | GatewayType/GatewayConfiguration 嵌在产品里 | 无网关配置，干净 |

## 3. 问题分析

`Produce` 试图回答："这类设备**有什么字段**"
`DeviceTypeProfile` 试图回答："这类设备**怎么采集数据**"

它们都同时承担了"设备类型定义"的角色，导致：

1. 创建设备时到底关联 `Produce` 还是 `DeviceTypeProfile`？
2. 字段元数据在 `ProduceDictionary` 还是在 `CollectionRuleTemplate.PointName/Unit`？
3. 字段映射用 `ProduceDataMapping` 还是 `CollectionRuleTemplate.TargetName`？

## 4. 常规物联网平台的设计模式

工业物联网平台通常有三个独立概念：

| 概念 | 定义 | 典型问题 |
|---|---|---|
| **产品/物模型** | 设备**能做什么**（能力定义） | 有哪些遥测点、属性、命令、事件？每个字段的数据类型、单位、显示名是什么？ |
| **采集模板** | 数据**怎么拿到**（接入方式） | Modbus 从站地址？功能码？寄存器地址？轮询周期？字节序？HTTP JSON 格式是什么？ |
| **设备实例** | 现场的具体设备 | 这台设备属于哪个产品？走哪个网关？SlaveId 是多少？ |

**关键原则：产品模板和采集模板应该分离**，因为同一个设备类型可能走不同的接入方式。

例子：一个"水泵"产品，可能有以下采集方式：
- 透传网关 + Modbus（需要 Modbus 采集规则）
- 边缘网关 + JSON 上传（需要 JSON 字段映射）
- 直接 MQTT 上报（需要 Topic 定义）

如果采集规则耦合在产品定义里，就无法灵活切换接入方式。

## 5. 建议的架构方向

### 5.1 三个独立实体

```
Product（产品 / 物模型）           CollectionTemplate（采集模板）
├── ProductKey (如 "water-pump")  ├── TemplateKey (如 "modbus-pump-v1")
├── ProductName (如 "水泵")       ├── Protocol (Modbus / HttpJson / Mqtt)
├── HVACDeviceType                ├── CollectionRules[]
├── Icon, Description             │   ├── PointKey → Product.PointKey（关联）
├── Version                       │   ├── SlaveId, FunctionCode, Address
├── TelemetryPoints[]             │   ├── ReadPeriodMs, TransformsJson
│   ├── PointKey                  │   └── TargetName → Product.PointKey
│   ├── DataType                  └── JsonSchema (边缘JSON场景)
│   ├── Unit, DisplayName
│   └── DisplayOrder
├── Attributes[]
├── Commands[]
└── Thresholds[]
```

### 5.2 设备实例关联

```
Device（设备实例）
├── ProductId → Product              ← 定义能力边界
├── CollectionTemplateId → Template  ← 定义采集方式
├── GatewayId → Gateway              ← 定义接入通道
├── DeviceSpecificConfig             ← 设备特有覆盖（如 SlaveId、轮询周期调整）
└── HvacDeviceType
```

### 5.3 对现有代码的映射

| 建议概念 | 现有对应 | 建议操作 |
|---|---|---|
| **Product** | `Produce` + `ProduceDictionary` | 保留 Produce 的壳，清理冗余字段（GatewayType、Place0~5 等），用 ProduceDictionary 重构为 TelemetryPoints/Attributes/Commands |
| **CollectionTemplate** | `DeviceTypeProfile` + `CollectionRuleTemplate` | 将 DeviceTypeProfile 改名为 CollectionTemplate，明确其采集模板身份，去掉 HVACDeviceType（应从 Product 继承） |
| **Device 关联** | `Device.ProduceId` + `Device.DeviceTypeProfileId` | 统一为 Device 关联 Product + CollectionTemplate |

`ProduceDataMapping` 的归宿：Product 级别的字段映射定义，用于表达"当设备上报的字段名与产品定义不一致时如何桥接"，这个能力在边缘 JSON 接入场景会很有用。

## 6. 待讨论问题

1. 是否采用"产品与采集模板分离"的架构？
2. Produce 清理的范围和优先级——先做哪些字段清理？
3. DeviceTypeProfile 是否应该重命名？
4. ProduceDataMapping 是否保留？如何与 CollectionRuleTemplate.TargetName 协调？
5. 是否需要支持"一个产品对应多个采集模板"的场景？
6. 现有 Produce 数据迁移方案？

## 7. 相关文档

- [Epic PRD](./epic.md)
- [架构文档](./arch.md)
- [M4 点位、模板与映射模块](../（待创建）)

## 8. 相关代码文件

| 文件 | 说明 |
|---|---|
| `IoTSharp.Data/Produce.cs` | 产品模板实体 |
| `IoTSharp.Data/ProduceDictionary.cs` | 产品字段元数据 |
| `IoTSharp.Data/ProduceDataMapping.cs` | 产品字段到设备字段的映射 |
| `IoTSharp.Data/DeviceTypeProfile.cs` | 设备类型模板实体 |
| `IoTSharp.Data/CollectionRuleTemplate.cs` | 采集规则模板 |
| `IoTSharp/Services/DeviceTypeProfileService.cs` | 设备类型模板服务 |
| `IoTSharp/Controllers/DeviceTypeProfileController.cs` | 设备类型模板 API |
