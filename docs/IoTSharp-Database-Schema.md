# IoTSharp 业务表关联关系全景图

> 本文档详细描述了 IoTSharp 项目中所有核心业务表的字段定义、外键关系和业务逻辑关联。  
> 基于代码分析生成，涵盖 40+ 张核心业务表。

---

## 📊 架构总览

IoTSharp 采用**多租户三层架构**：`Tenant（租户）` → `Customer（客户）` → `Device（设备）`

所有主要业务表都包含 `TenantId` 和 `CustomerId` 外键，实现数据隔离。

---

## 一、多租户体系（核心层级）

### 1.1 Tenant（租户）- 顶层组织

**表名**: `Tenant`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 租户ID |
| Name | string | 租户名称 |
| Email | string | 联系邮箱 |
| Phone | string | 联系电话 |
| Country | string | 国家 |
| Province | string | 省份 |
| City | string | 城市 |
| Street | string | 街道 |
| Address | string | 详细地址 |
| ZipCode | int | 邮政编码 |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 一对多: `Customers` (Customer列表)
- 一对多: `Devices` (Device列表)
- 一对一: `AISettings` (AI配置)

---

### 1.2 Customer（客户）- 中间层组织

**表名**: `Customer`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 客户ID |
| Name | string | 客户名称 |
| Email | string | 联系邮箱 |
| Phone | string | 联系电话 |
| Country/Province/City/Street/Address | string | 地址信息 |
| ZipCode | int | 邮政编码 |
| TenantId | Guid | **外键 → Tenant.Id** |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 多对一: `Tenant` (所属租户)
- 一对多: `Devices` (设备列表)
- 一对一: `AISettings` (AI配置)

---

### 1.3 Device / Gateway（设备/网关）- 底层实体

**表名**: `Device` (Gateway 继承自 Device)  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 设备ID |
| Name | string | 设备名称 |
| DeviceType | enum | 设备类型 (Device/Gateway) |
| Timeout | int | 超时时间（秒） |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |
| GatewayId | Guid? | **外键 → Device.Id** (父网关，仅子设备有值) |
| ProduceId | Guid? | **外键 → Produce.Id** (产品模板) |
| DeviceModelId | Guid? | **外键 → DeviceModel.DeviceModelId** |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 多对一: `Tenant`, `Customer` (所属组织)
- 多对一: `Owner` (Gateway, 所属网关)
- 一对多: `Children` (Gateway专有，子设备列表)
- 一对一: `DeviceIdentity` (身份认证)
- 一对多: `DeviceRules` (关联的规则)
- 一对多: `DeviceGraphs` (可视化图形)
- 一对多: `Alarms` (告警记录)
- 多对一: `Produce` (产品模板)

**特殊说明**:
- `Gateway` 类继承自 `Device`，通过 `DeviceType` 区分
- Gateway 可以有多个子设备（通过 `GatewayId` 自引用）

---

## 二、设备认证与安全

### 2.1 DeviceIdentity（设备身份认证）

**表名**: `DeviceIdentity`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 认证ID |
| IdentityType | enum | 认证方式 (AccessToken/DevicePassword/X509Certificate) |
| IdentityId | string | 认证标识 (Token/设备名/证书指纹) |
| IdentityValue | string | 认证值 (密码/证书PEM，AccessToken时为null) |
| DeviceId | Guid | **外键 → Device.Id** (一对一) |

**关联关系**:
- 一对一: `Device` (所属设备)

**认证方式说明**:
- **AccessToken**: IdentityId = Token字符串, IdentityValue = null
- **DevicePassword**: IdentityId = 设备名, IdentityValue = 密码
- **X509Certificate**: IdentityId = 证书指纹, IdentityValue = PEM证书内容

---

### 2.2 AuthorizedKey（授权密钥）

**表名**: `AuthorizedKeys`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 密钥ID |
| Name | string | 密钥名称 |
| AuthToken | string | 授权Token |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 一对多: `Devices` (使用该密钥的设备列表)

---

## 三、产品与物模型体系

### 3.1 Produce（产品/物模型）

**表名**: `Produce`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 产品ID |
| Name | string | 产品名称 |
| Icon | string | 图标路径 |
| GatewayType | enum | 网关类型 |
| GatewayConfiguration | string | 网关配置 (JSON或配置文件名) |
| DefaultTimeout | int | 默认超时时间（秒） |
| DefaultIdentityType | enum | 默认认证类型 |
| DefaultDeviceType | enum | 默认设备类型 |
| Description | string | 产品描述 |
| ProduceToken | string | 产品Token |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 一对多: `Devices` (使用该产品的设备)
- 一对多: `DefaultAttributes` (ProduceData, 默认属性)
- 一对多: `Dictionaries` (ProduceDictionary, 字段定义)
- 一对多: `ProduceDataMappings` (数据映射规则)

---

### 3.2 ProduceData（产品默认数据）

**表名**: `ProduceDatas`  
**主键**: 继承自 `DataStorage` 的复合键

| 字段 | 类型 | 说明 |
|------|------|------|
| Catalog | enum | 数据目录 (固定为 ProduceData) |
| DeviceId | Guid | **外键 → Device.Id** (来自基类) |
| KeyName | string | 字段名 |
| DateTime | DateTime | 时间戳 |
| DataSide | enum | 数据方向 |
| Type | enum | 数据类型 (Boolean/String/Long等) |
| Value_* | various | 各种类型的值字段 |
| OwnerId | Guid | **外键 → Produce.Id** |

**关联关系**:
- 多对一: `Owner` (Produce, 所属产品)

**说明**: 继承自 `DataStorage`，存储产品的默认属性/遥测数据模板

---

### 3.3 ProduceDictionary（产品字段字典）

**表名**: `ProduceDictionaries`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 字典ID |
| KeyName | string | 字段名（技术名称） |
| DisplayName | string | 显示名称 |
| Unit | string | 单位 |
| UnitExpression | string | 单位转换表达式 |
| UnitConvert | bool | 是否启用单位转换 |
| KeyDesc | string | 字段描述 |
| DefaultValue | string | 默认值 |
| Display | bool | 是否显示 |
| DataType | enum | 数据类型 |
| Place0-5 | string | 位置分类（最多6级） |
| PlaceOrder0-5 | string | 位置排序 |
| Tag | string | 标签 |
| Customer | Guid? | 可选的客户ID |
| Deleted | bool | 逻辑删除标记 |

**用途**: 定义产品的标准字段元数据，用于前端展示和数据验证

---

### 3.4 ProduceDataMapping（产品数据映射）

**表名**: `ProduceDataMappings`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 映射ID |
| ProduceId | Guid | **外键 → Produce.Id** |
| ProduceKeyName | string | 产品抽象字段名 |
| DataCatalog | enum | 数据类型 (Attribute/Telemetry) |
| DeviceId | Guid | **外键 → Device.Id** |
| DeviceKeyName | string | 设备实际字段名 |
| Description | string | 映射描述 |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 多对一: `Produce` (所属产品)
- 多对一: `Device` (目标设备)

**核心用途**: 
实现产品抽象层与具体设备的解耦。例如：
- 产品定义字段 "温度" → 映射到设备A的 "temp_sensor_1"
- 同一产品的不同设备可以使用不同的物理字段名

---

### 3.5 DeviceModel（设备模型）

**表名**: `DeviceModels`  
**主键**: `DeviceModelId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| DeviceModelId | Guid | 模型ID |
| ModelName | string | 模型名称 |
| ModelDesc | string | 模型描述 |
| ModelStatus | int | 模型状态 |
| CreateDateTime | DateTime | 创建时间 |
| Creator | Guid | 创建者ID |

**关联关系**:
- 一对多: `DeviceModelCommands` (命令列表)
- 一对多: `Devices` (使用该模型的设备)

---

### 3.6 DeviceModelCommand（设备模型命令）

**表名**: `DeviceModelCommands`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 命令ID |
| DeviceModelId | Guid | **外键 → DeviceModel.DeviceModelId** |
| CommandName | string | 命令名称 |
| CommandContent | string | 命令内容 |
| ... | ... | 其他命令相关字段 |

**关联关系**:
- 多对一: `DeviceModel` (所属模型)

---

## 四、数据存储体系（遥测与属性）

### 4.1 DataStorage（数据存储基类）

**表名**: `DataStorage` (抽象基类，不直接实例化)  
**主键**: 复合键 `(Catalog, DeviceId, KeyName, DateTime)`

| 字段 | 类型 | 说明 |
|------|------|------|
| Catalog | enum | 数据目录 (AttributeLatest/TelemetryData/ProduceData) |
| DeviceId | Guid | **外键 → Device.Id** |
| KeyName | string | 字段名 |
| DateTime | DateTime | 时间戳 |
| DataSide | enum | 数据方向 (ClientSide/ServerSide/AnySide) |
| Type | enum | 数据类型 (Boolean/String/Long/DateTime/Double/Json/XML/Binary) |
| Value_Boolean | bool? | 布尔值 |
| Value_String | string | 字符串值 |
| Value_Long | long? | 长整型值 |
| Value_DateTime | DateTime? | 日期时间值 |
| Value_Double | double? | 双精度值 |
| Value_Json | string | JSON值 |
| Value_XML | string | XML值 |
| Value_Binary | byte[] | 二进制值 |

**说明**: 使用 TPH (Table Per Hierarchy) 模式，通过 `Catalog` 鉴别器区分子类

---

### 4.2 AttributeLatest（最新属性）

**表名**: `AttributeLatest` (视图/查询对象)  
**继承**: DataStorage  
**鉴别器**: `Catalog = DataCatalog.AttributeLatest`

**用途**: 存储设备最新的属性值（每个 DeviceId + KeyName 只保留最新一条）

**关联关系**:
- 多对一: `Device` (所属设备)

---

### 4.3 TelemetryLatest（最新遥测）

**表名**: `TelemetryLatest` (视图/查询对象)  
**继承**: DataStorage  
**鉴别器**: `Catalog = DataCatalog.TelemetryLatest`

**用途**: 存储设备最新的遥测值（每个 DeviceId + KeyName 只保留最新一条）

**关联关系**:
- 多对一: `Device` (所属设备)

---

### 4.4 TelemetryData（历史遥测）

**表名**: `TelemetryData`  
**继承**: DataStorage  
**鉴别器**: `Catalog = DataCatalog.TelemetryData`

**用途**: 存储完整的遥测历史数据

**分片策略** (通过 ShardingCore):
- `TelemetryDataMinuteRoute` - 按分钟分表
- `TelemetryDataHourRoute` - 按小时分表
- `TelemetryDataDayRoute` - 按天分表
- `TelemetryDataMonthRoute` - 按月分表
- `TelemetryDataYearRoute` - 按年分表

**关联关系**:
- 多对一: `Device` (所属设备)

---

## 五、规则引擎体系

### 5.1 FlowRule（流程规则）

**表名**: `FlowRules`  
**主键**: `RuleId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| RuleId | Guid | 规则ID |
| RuleType | enum | 规则类型 |
| Name | string | 规则名称 |
| Describes | string | 规则描述 |
| Runner | string | 执行器 |
| ExecutableCode | string | 可执行代码 |
| Creator | string | 创建者 |
| RuleDesc | string | 详细说明 |
| RuleStatus | int? | 规则状态 |
| CreatTime | DateTime? | 创建时间 |
| DefinitionsXml | string | 流程图XML定义 |
| ParentRuleId | Guid | 父规则ID (版本控制) |
| SubVersion | double | 次版本号 |
| Version | double | 主版本号 |
| CreateId | Guid | 创建者ID |
| MountType | enum | 挂载事件类型 (RAW/Telemetry/Attribute/RPC等) |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |

**关联关系**:
- 一对多: `Flows` (流程节点)
- 一对多: `FlowOperations` (操作记录)
- 一对多: `BaseEvents` (触发事件)
- 一对多: `DeviceRules` (关联的设备)

**事件挂载点**:
- RAW, Telemetry, Attribute, RPC
- Connected, Disconnected
- Alarm, CreateDevice, DeleteDevice
- Activity, Inactivity

---

### 5.2 Flow（流程节点）

**表名**: `Flows`  
**主键**: `FlowId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| FlowId | Guid | 节点ID |
| bpmnid | string | BPMN节点ID |
| Flowname | string | 节点名称 |
| FlowRuleId | Guid | **外键 → FlowRule.RuleId** |
| Flowdesc | string | 节点描述 |
| ObjectId | string | 对象ID |
| FlowType | string | 节点类型 |
| SourceId | string | 源节点ID |
| TargetId | string | 目标节点ID |
| NodeProcessClass | string | 处理类名 |
| NodeProcessMethod | string | 处理方法名 |
| NodeProcessParams | string | 处理参数 |
| NodeProcessType | string | 处理类型 |
| NodeProcessScriptType | string | 脚本类型 |
| NodeProcessScript | string | 脚本内容 |
| Conditionexpression | string | 条件表达式 |
| Incoming | string | 入线 |
| Outgoing | string | 出线 |
| FlowStatus | int | 节点状态 |
| TestStatus | int | 测试状态 |
| Tester | Guid | 测试者ID |
| TesterDateTime | DateTime | 测试时间 |
| CreateId | Guid | 创建ID |
| CreateDate | DateTime | 创建时间 |
| Createor | Guid | 创建者 |
| ExecutorId | Guid | **外键 → RuleTaskExecutor.ExecutorId** |
| TenantId/CustomerId | Guid | **外键** |
| Top/Left | string | 画布位置 |
| FlowClass/FlowNameSpace/FlowIcon/FlowTag/FlowShapeType | string | 样式属性 |

**关联关系**:
- 多对一: `FlowRule` (所属规则)
- 多对一: `Executor` (任务执行器)
- 一对多: `FlowOperations` (操作记录)

---

### 5.3 FlowOperation（流程操作记录）

**表名**: `FlowOperations`  
**主键**: `OperationId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| OperationId | Guid | 操作ID |
| AddDate | DateTime? | 添加时间 |
| NodeStatus | int | 节点处理状态 |
| OperationDesc | string | 操作描述 |
| Data | string | 操作数据 (JSON) |
| BizId | string | 业务ID |
| bpmnid | string | BPMN节点ID |
| FlowId | Guid | **外键 → Flow.FlowId** |
| FlowRuleId | Guid | **外键 → FlowRule.RuleId** |
| EventId | Guid | **外键 → BaseEvent.EventId** |
| Step | int | 执行步骤 |
| Tag | string | 标签 |

**关联关系**:
- 多对一: `Flow` (所属节点)
- 多对一: `FlowRule` (所属规则)
- 多对一: `BaseEvent` (触发事件)

**用途**: 记录规则链执行过程中的每一步操作，用于调试和追溯

---

### 5.4 DeviceRule（设备规则关联）

**表名**: `DeviceRules`  
**主键**: `DeviceRuleId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| DeviceRuleId | Guid | 关联ID |
| DeviceId | Guid | **外键 → Device.Id** |
| FlowRuleId | Guid | **外键 → FlowRule.RuleId** |
| ConfigUser | Guid | 配置用户ID |
| ConfigDateTime | DateTime | 配置时间 |
| EnableTrace | int | 是否开启链路跟踪 |

**关联关系**:
- 多对一: `Device` (设备)
- 多对一: `FlowRule` (规则)

**用途**: 将特定规则绑定到特定设备，实现设备级别的规则定制

---

### 5.5 RuleTaskExecutor（任务执行器）

**表名**: `RuleTaskExecutors`  
**主键**: `ExecutorId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| ExecutorId | Guid | 执行器ID |
| ExecutorName | string | 执行器名称 |
| ExecutorDesc | string | 执行器描述 |
| Path | string | 程序集路径 |
| TypeName | string | 类型全名 |
| DefaultConfig | string | 默认配置 (JSON) |
| MataData | string | 元数据 |
| Tag | string | 标签 |
| ExecutorStatus | int | 执行器状态 |
| AddDateTime | DateTime | 添加时间 |
| Creator | Guid | 创建者ID |
| TestStatus | int | 测试状态 |
| Tester | Guid | 测试者ID |
| TesterDateTime | DateTime | 测试时间 |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 一对多: `Flows` (使用该执行器的节点)
- 一对多: `SubscriptionTasks` (订阅任务)

**内置执行器**:
- `AlarmPullExcutor` - 告警拉取
- `DeviceActionExcutor` - 设备动作
- `MessagePullExcutor` - 消息拉取
- `RangerCheckExcutor` - 范围检查
- `TelemetryArrayPullExcutor` - 遥测数组

---

### 5.6 BaseEvent（基础事件）

**表名**: `BaseEvents`  
**主键**: `EventId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| EventId | Guid | 事件ID |
| EventName | string | 事件名称 |
| EventDesc | string | 事件描述 |
| EventStaus | int | 事件状态 |
| Type | enum | 运行类型 (FlowRuleRunType) |
| MataData | string | 元数据 (JSON) |
| Creator | Guid | 创建者ID |
| FlowRuleId | Guid | **外键 → FlowRule.RuleId** |
| Bizid | string | 业务ID |
| CreaterDateTime | DateTime | 创建时间 |
| BizData | string | 业务数据 |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 多对一: `FlowRule` (触发规则)
- 一对多: `FlowOperations` (产生的操作记录)

---

## 六、告警管理体系

### 6.1 Alarm（告警）

**表名**: `Alarms`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 告警ID |
| AlarmType | string | 告警类型 |
| AlarmDetail | string | 告警详情 |
| AckDateTime | DateTime | 确认时间 |
| ClearDateTime | DateTime | 清除时间 |
| StartDateTime | DateTime | 开始时间 |
| EndDateTime | DateTime | 结束时间 |
| AlarmStatus | enum | 告警状态 (Active/Acknowledged/Cleared等) |
| Serverity | enum | 严重等级 (Critical/Major/Minor/Warning/Indeterminate) |
| Propagate | bool | 是否传播 (触发规则链) |
| OriginatorId | Guid | 起因对象ID |
| OriginatorType | enum | 起因类型 (Device/Gateway/Asset) |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |

**关联关系**:
- 多对一: `Tenant`, `Customer` (所属组织)
- 多态关联: `OriginatorId` + `OriginatorType` 指向 Device/Gateway/Asset

**告警生命周期**:
1. **Active** - 告警产生
2. **Acknowledged** - 人工确认 (AckDateTime)
3. **Cleared** - 告警清除 (ClearDateTime)

**传播机制**: 当 `Propagate = true` 时，告警会触发规则链执行

---

## 七、资产管理体系

### 7.1 Asset（资产）

**表名**: `Assets`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 资产ID |
| Name | string | 资产名称 |
| Description | string | 资产描述 |
| AssetType | string | 资产类型 |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |
| Deleted | bool | 逻辑删除标记 |

**关联关系**:
- 一对多: `OwnedAssets` (AssetRelation列表)
- 多对一: `Tenant`, `Customer`

---

### 7.2 AssetRelation（资产关联）

**表名**: `AssetRelations`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 关联ID |
| Name | string | 关联名称 |
| Description | string | 关联描述 |
| DeviceId | Guid | **外键 → Device.Id** |
| DataCatalog | enum | 数据类型 (Attribute/Telemetry) |
| KeyName | string | 字段名 |

**关联关系**:
- 多对一: `Device` (关联设备)
- 多对一: `Asset` (通过导航属性)

**核心用途**: 
将资产的某个业务字段映射到设备的实时数据。例如：
- 资产 "1号车间" 的 "当前温度" 字段 → 设备 "temp_sensor_001" 的 "temperature" 遥测

---

## 八、可视化设计器体系

### 8.1 DeviceDiagram（设备图表/画布）

**表名**: `DeviceDiagrams`  
**主键**: `DiagramId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| DiagramId | Guid | 图表ID |
| DiagramName | string | 图表名称 |
| DiagramDesc | string | 图表描述 |
| DiagramStatus | int | 图表状态 |
| Creator | Guid | 创建者ID |
| CreateDate | DateTimeOffset? | 创建时间 |
| DiagramImage | string | 图表图片/背景 |
| IsDefault | bool | 是否为默认图表 |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 一对多: `DeviceGraphs` (图表中的元素)

---

### 8.2 DeviceGraph（设备图形元素）

**表名**: `DeviceGraphs`  
**主键**: `GraphId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| GraphId | Guid | 图形ID |
| DeviceId | Guid | **外键 → Device.Id** |
| DiagramId | Guid | **外键 → DeviceDiagram.DiagramId** |
| GraphShape | string | 图形形状 |
| GraphWidth/Height | int | 宽高 |
| GraphPostionX/Y | int | 位置坐标 |
| GraphElementId | string | 元素ID (前端唯一标识) |
| CreateDate | DateTime? | 创建时间 |
| Creator | Guid | 创建者ID |
| GraphFill/Stroke | string | 填充/边框颜色 |
| GraphStrokeWidth | int | 边框宽度 |
| GraphText* | various | 文本样式属性 |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 多对一: `Device` (绑定的设备)
- 多对一: `DeviceDiagram` (所属画布)

**用途**: 在可视化画布上表示一个设备节点，包含位置和样式信息

---

### 8.3 DeviceGraphToolBox（图表工具箱）

**表名**: `DeviceGraphToolBoxes`  
**主键**: `ToolBoxId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| ToolBoxId | Guid | 工具箱ID |
| ... | ... | 工具箱相关字段 |
| TenantId/CustomerId | Guid | **外键** |

---

### 8.4 DevicePort（设备端口）

**表名**: `DevicePorts`  
**主键**: `PortId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| PortId | Guid | 端口ID |
| PortName | string | 端口名称 |
| PortDesc | string | 端口描述 |
| PortPic | string | 端口图片 |
| PortType | int | 端口类型 |
| PortPhyType | int | 物理类型 |
| PortStatus | int | 端口状态 |
| DeviceId | Guid | **外键 → Device.Id** |
| CreateDate | DateTime? | 创建时间 |
| Creator | long | 创建者ID |
| PortElementId | string | 前端元素ID |

**关联关系**:
- 多对一: `Device` (所属设备)

**用途**: 定义设备的通信端口（如串口、网口等），用于网关映射

---

### 8.5 DevicePortMapping（端口映射）

**表名**: `DevicePortMappings`  
**主键**: `MappingId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| MappingId | Guid | 映射ID |
| SourceId | string | 源端口ID |
| TargeId | string | 目标端口ID |
| SourceElementId | string | 源元素ID |
| TargetElementId | string | 目标元素ID |
| CreateDate | DateTime? | 创建时间 |
| Creator | Guid | 创建者ID |
| MappingStatus | int | 映射状态 |
| MappingIndex | int | 映射索引 |
| SourceDeviceId | Guid | **外键 → Device.Id** (源设备) |
| TargetDeviceId | Guid | **外键 → Device.Id** (目标设备) |

**关联关系**:
- 多对一: `SourceDevice` (源设备)
- 多对一: `TargetDevice` (目标设备)

**用途**: 在网关设计器中定义设备之间的端口连接关系

---

## 九、订阅与事件通知

### 9.1 SubscriptionEvent（订阅事件）

**表名**: `SubscriptionEvents`  
**主键**: `EventId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| EventId | Guid | 事件ID |
| EventName | string | 事件名称 |
| EventDesc | string | 事件描述 |
| EventNameSpace | string | 事件命名空间 |
| EventStatus | int | 事件状态 |
| Type | int | 事件类型 |
| EventParam | string | 事件参数 (JSON) |
| EventTag | string | 事件标签 |
| CreateDateTime | DateTime | 创建时间 |
| Creator | Guid | 创建者ID |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 一对多: `SubscriptionTasks` (订阅任务)

---

### 9.2 SubscriptionTask（订阅任务）

**表名**: `SubscriptionTasks`  
**主键**: `BindId` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| BindId | Guid | 绑定ID |
| EventId | Guid | **外键 → SubscriptionEvent.EventId** |
| ExecutorId | Guid | **外键 → RuleTaskExecutor.ExecutorId** |
| Status | int | 任务状态 |
| TaskConfig | string | 任务配置 (JSON) |

**关联关系**:
- 多对一: `Subscription` (SubscriptionEvent)
- 多对一: `RuleTaskExecutor` (执行器)

**用途**: 当订阅的事件触发时，执行指定的任务执行器

---

## 十、用户与权限体系（ASP.NET Identity）

> IoTSharp 使用 ASP.NET Core Identity 框架进行用户认证和权限管理。以下表由 Identity 框架自动创建和管理。

### 10.1 AspNetUsers（用户表）

**表名**: `AspNetUsers`
**主键**: `Id` (string)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | string | 用户ID (GUID字符串) |
| UserName | string | 用户名 |
| NormalizedUserName | string | 标准化的用户名（大写） |
| Email | string | 电子邮箱 |
| NormalizedEmail | string | 标准化的邮箱（大写） |
| EmailConfirmed | bool | 邮箱是否已确认 |
| PasswordHash | string | 密码哈希值 |
| SecurityStamp | string | 安全戳（用于强制注销等） |
| ConcurrencyStamp | string | 并发戳（乐观锁） |
| PhoneNumber | string | 电话号码 |
| PhoneNumberConfirmed | bool | 电话是否已确认 |
| TwoFactorEnabled | bool | 是否启用双因素认证 |
| LockoutEnd | DateTimeOffset? | 锁定结束时间 |
| LockoutEnabled | bool | 是否启用锁定 |
| AccessFailedCount | int | 登录失败次数 |

**关联关系**:
- 一对一: `Relationship` (租户/客户关联)
- 一对多: `RefreshTokens` (刷新令牌)
- 一对多: `AspNetUserClaims` (用户声明)
- 一对多: `AspNetUserLogins` (外部登录)
- 一对多: `AspNetUserRoles` (用户角色)

**用途**: 存储系统用户的基本信息和认证凭据

---

### 10.2 AspNetRoles（角色表）

**表名**: `AspNetRoles`
**主键**: `Id` (string)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | string | 角色ID (GUID字符串) |
| Name | string | 角色名称 |
| NormalizedName | string | 标准化角色名（大写） |
| ConcurrencyStamp | string | 并发戳（乐观锁） |

**关联关系**:
- 一对多: `AspNetUserRoles` (用户角色关联)
- 一对多: `AspNetRoleClaims` (角色声明)

**用途**: 存储系统角色定义，用于基于角色的访问控制（RBAC）

---

### 10.3 AspNetUserRoles（用户角色关联表）

**表名**: `AspNetUserRoles`
**主键**: 复合键 `(UserId, RoleId)`

| 字段 | 类型 | 说明 |
|------|------|------|
| UserId | string | **外键 → AspNetUsers.Id** |
| RoleId | string | **外键 → AspNetRoles.Id** |

**关联关系**:
- 多对一: `AspNetUsers` (用户)
- 多对一: `AspNetRoles` (角色)

**用途**: 建立用户与角色的多对多关系

---

### 10.4 AspNetUserClaims（用户声明表）

**表名**: `AspNetUserClaims`
**主键**: `Id` (int, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 声明ID |
| UserId | string | **外键 → AspNetUsers.Id** |
| ClaimType | string | 声明类型 |
| ClaimValue | string | 声明值 |

**用途**: 存储用户的额外声明信息（claims），用于授权决策

---

### 10.5 AspNetUserLogins（外部登录表）

**表名**: `AspNetUserLogins`
**主键**: 复合键 `(LoginProvider, ProviderKey)`

| 字段 | 类型 | 说明 |
|------|------|------|
| LoginProvider | string | 登录提供程序名称（如"Google", "Microsoft"） |
| ProviderKey | string | 提供程序中的用户标识 |
| ProviderDisplayName | string | 提供程序显示名称 |
| UserId | string | **外键 → AspNetUsers.Id** |

**用途**: 支持第三方/外部登录提供商的用户关联

---

### 10.6 AspNetUserTokens（用户令牌表）

**表名**: `AspNetUserTokens`
**主键**: 复合键 `(UserId, LoginProvider, Name)`

| 字段 | 类型 | 说明 |
|------|------|------|
| UserId | string | **外键 → AspNetUsers.Id** |
| LoginProvider | string | 登录提供程序 |
| Name | string | 令牌名称 |
| Value | string | 令牌值 |

**用途**: 存储用户认证令牌（如双因素认证令牌、记住我令牌等）

---

### 10.7 AspNetRoleClaims（角色声明表）

**表名**: `AspNetRoleClaims`
**主键**: `Id` (int, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 声明ID |
| RoleId | string | **外键 → AspNetRoles.Id** |
| ClaimType | string | 声明类型 |
| ClaimValue | string | 声明值 |

**用途**: 存储角色的声明信息，应用于该角色的所有用户

---

### 10.8 Relationship（用户关系关联）

**表名**: `Relationships`
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 关系ID |
| IdentityUserId | string | **外键 → AspNetUsers.Id** |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid? | **外键 → Customer.Id** |

**关联关系**:
- 多对一: `IdentityUser` (ASP.NET Identity用户)
- 多对一: `Tenant` (所属租户)
- 多对一: `Customer` (所属客户，可选)

**核心用途**:
将 ASP.NET Identity 的用户与 IoTSharp 的多租户体系（租户/客户）关联起来。这是 IoTSharp 自定义的桥梁表。

---

## 十一、任务调度体系（Quartz.NET）

> IoTSharp 使用 Quartz.NET 进行定时任务调度。以下表由 Quartz.NET 自动创建和管理，用于持久化任务调度状态。

### 11.1 qrtz_job_details（作业详情表）

**表名**: `qrtz_job_details`
**主键**: 复合键 `(sched_name, job_name, job_group)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| job_name | TEXT | 作业名称 |
| job_group | TEXT | 作业分组 |
| description | TEXT | 作业描述 |
| job_class_name | TEXT | 作业类名（实现 IJob 接口的类） |
| is_durable | BOOL | 是否持久化（无触发器时是否保留） |
| is_nonconcurrent | BOOL | 是否非并发（同一作业实例不并行执行） |
| is_update_data | BOOL | 是否更新数据 |
| requests_recovery | BOOL | 是否请求恢复（应用崩溃时重新执行） |
| job_data | BYTEA | 作业数据（序列化后的 JobDataMap） |

**关联关系**:
- 一对多: `qrtz_triggers` (触发器列表)

**用途**: 存储所有已注册的作业定义

---

### 11.2 qrtz_triggers（触发器表）

**表名**: `qrtz_triggers`
**主键**: 复合键 `(sched_name, trigger_name, trigger_group)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| trigger_name | TEXT | 触发器名称 |
| trigger_group | TEXT | 触发器分组 |
| job_name | TEXT | **外键 → qrtz_job_details.job_name** |
| job_group | TEXT | **外键 → qrtz_job_details.job_group** |
| description | TEXT | 触发器描述 |
| next_fire_time | BIGINT | 下次触发时间（Unix时间戳毫秒） |
| prev_fire_time | BIGINT | 上次触发时间 |
| priority | INTEGER | 优先级 |
| trigger_state | TEXT | 触发器状态 (WAITING/PAUSED/ACQUIRED/EXECUTING/BLOCKED/COMPLETE/ERROR) |
| trigger_type | TEXT | 触发器类型 (SIMPLE/CRON/BLOB/CALINTER/DAILYTimeInterval) |
| start_time | BIGINT | 开始时间 |
| end_time | BIGINT | 结束时间 |
| calendar_name | TEXT | 日历名称 |
| misfire_instr | SMALLINT | 错过触发指令 |
| job_data | BYTEA | 作业数据 |

**关联关系**:
- 多对一: `qrtz_job_details` (所属作业)
- 一对多: `qrtz_simple_triggers` / `qrtz_cron_triggers` / `qrtz_blob_triggers` (具体触发器类型)

**用途**: 存储所有触发器的状态和调度信息

---

### 11.3 qrtz_simple_triggers（简单触发器表）

**表名**: `qrtz_simple_triggers`
**主键**: 复合键 `(sched_name, trigger_name, trigger_group)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| trigger_name | TEXT | 触发器名称 |
| trigger_group | TEXT | 触发器分组 |
| repeat_count | BIGINT | 重复次数（-1表示无限） |
| repeat_interval | BIGINT | 重复间隔（毫秒） |
| times_triggered | BIGINT | 已触发次数 |

**用途**: 存储简单触发器的重复调度参数（固定间隔重复）

---

### 11.4 qrtz_cron_triggers（Cron触发器表）

**表名**: `qrtz_cron_triggers`
**主键**: 复合键 `(sched_name, trigger_name, trigger_group)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| trigger_name | TEXT | 触发器名称 |
| trigger_group | TEXT | 触发器分组 |
| cron_expression | TEXT | Cron表达式（如"0 0/5 * * * ?"表示每5分钟） |
| time_zone_id | TEXT | 时区ID |

**用途**: 存储Cron触发器的表达式，支持复杂的调度计划

---

### 11.5 qrtz_simprop_triggers（简单属性触发器表）

**表名**: `qrtz_simprop_triggers`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| trigger_name | TEXT | 触发器名称 |
| trigger_group | TEXT | 触发器分组 |
| str_prop_1/2/3 | TEXT | 字符串属性1-3 |
| int_prop_1/2 | INTEGER | 整数属性1-2 |
| long_prop_1/2 | BIGINT | 长整数属性1-2 |
| dec_prop_1/2 | NUMERIC | 小数属性1-2 |
| bool_prop_1/2 | BOOL | 布尔属性1-2 |
| time_zone_id | TEXT | 时区ID |

**用途**: 存储简单属性触发器的扩展参数

---

### 11.6 qrtz_blob_triggers（BLOB触发器表）

**表名**: `qrtz_blob_triggers`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| trigger_name | TEXT | 触发器名称 |
| trigger_group | TEXT | 触发器分组 |
| blob_data | BYTEA | 触发器数据的二进制存储 |

**用途**: 存储自定义触发器的序列化数据

---

### 11.7 qrtz_fired_triggers（已触发触发器表）

**表名**: `qrtz_fired_triggers`
**主键**: 复合键 `(sched_name, entry_id)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| entry_id | TEXT | 条目ID（唯一标识） |
| trigger_name | TEXT | 触发器名称 |
| trigger_group | TEXT | 触发器分组 |
| instance_name | TEXT | 执行实例名称 |
| fired_time | BIGINT | 实际触发时间 |
| sched_time | BIGINT | 计划触发时间 |
| priority | INTEGER | 优先级 |
| state | TEXT | 状态 (ACQUIRED/EXECUTING/COMPLETE) |
| job_name | TEXT | 作业名称 |
| job_group | TEXT | 作业分组 |
| is_nonconcurrent | BOOL | 是否非并发 |
| requests_recovery | BOOL | 是否请求恢复 |

**用途**: 存储正在执行或已触发的触发器信息

---

### 11.8 qrtz_calendars（日历表）

**表名**: `qrtz_calendars`
**主键**: 复合键 `(sched_name, calendar_name)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| calendar_name | TEXT | 日历名称 |
| calendar | BYTEA | 日历数据的序列化 |

**用途**: 存储Quartz日历对象，用于排除特定日期的调度

---

### 11.9 qrtz_paused_trigger_grps（暂停触发器组表）

**表名**: `qrtz_paused_trigger_grps`
**主键**: 复合键 `(sched_name, trigger_group)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| trigger_group | TEXT | 触发器分组 |

**用途**: 记录已暂停的触发器分组

---

### 11.10 qrtz_scheduler_state（调度器状态表）

**表名**: `qrtz_scheduler_state`
**主键**: 复合键 `(sched_name, instance_name)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| instance_name | TEXT | 实例名称 |
| last_checkin_time | BIGINT | 最后心跳时间 |
| checkin_interval | BIGINT | 心跳间隔 |

**用途**: 存储集群中各调度器实例的状态和心跳信息

---

### 11.11 qrtz_locks（锁表）

**表名**: `qrtz_locks`
**主键**: 复合键 `(sched_name, lock_name)`

| 字段 | 类型 | 说明 |
|------|------|------|
| sched_name | TEXT | 调度器名称 |
| lock_name | TEXT | 锁名称 (如 "TRIGGER_ACCESS", "JOB_ACCESS") |

**用途**: 实现数据库级别的悲观锁，保证多实例并发安全

---

### Quartz.NET 核心索引

| 索引名称 | 表 | 字段 | 用途 |
|---------|---|------|------|
| idx_qrtz_j_req_recovery | qrtz_job_details | requests_recovery | 快速查询需要恢复的作业 |
| idx_qrtz_t_next_fire_time | qrtz_triggers | next_fire_time | 快速查找即将触发的触发器 |
| idx_qrtz_t_state | qrtz_triggers | trigger_state | 按状态筛选触发器 |
| idx_qrtz_t_nft_st | qrtz_triggers | next_fire_time, trigger_state | 组合索引优化调度查询 |
| idx_qrtz_ft_trig_* | qrtz_fired_triggers | 多种组合 | 优化已触发触发器的查询 |

---

### Quartz.NET 在 IoTSharp 中的使用

从 `Startup.cs` 可以看到：

```csharp
services.AddQuartz(q =>
{
    q.DiscoverJobs();  // 自动发现带 [DisallowConcurrentExecution] 特性的Job
});

services.AddQuartzServer(options =>
{
    options.StartDelay = TimeSpan.FromSeconds(10);
    options.WaitForJobsToComplete = true;
});
```

**IoTSharp 内置任务** (位于 `IoTSharp/Jobs` 目录):
- `PublishAttributeDataTask` - 定时发布属性数据
- `PublishTelemetryDataTask` - 定时发布遥测数据
- `PublishAlarmDataTask` - 定时发布告警数据
- `RawDataGateway` - 原始数据网关处理
- `KepServerEx` - KepServerEX 集成

这些任务通过 Quartz.NET 调度执行，状态持久化到 qrtz_* 系列表中，确保应用重启后任务状态不丢失。

---

## 十二、系统管理

### 12.1 AuditLog（审计日志）

**表名**: `AuditLogs`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | 日志ID |
| TenantId | Guid | **外键 → Tenant.Id** |
| CustomerId | Guid | **外键 → Customer.Id** |
| UserId | string | 操作用户ID |
| UserName | string | 操作用户名 |
| ObjectID | Guid | 操作对象ID |
| ObjectName | string | 操作对象名称 |
| ObjectType | enum | 对象类型 (Device/Asset/Alarm等) |
| ActionName | string | 操作名称 |
| ActionData | string | 操作数据 (JSON) |
| ActionResult | string | 操作结果 |
| ActiveDateTime | DateTime | 操作时间 |

**关联关系**:
- 多对一: `Tenant`, `Customer`

**用途**: 记录所有关键操作的审计轨迹

---

### 12.2 RefreshToken（刷新令牌）

**表名**: `RefreshTokens`  
**主键**: `Id` (Guid)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Guid | Token ID |
| UserId | string | **外键 → IdentityUser.Id** |
| Token | string | 刷新令牌 |
| JwtId | string | 关联的JWT ID |
| IsUsed | bool | 是否已使用 |
| IsRevorked | bool | 是否已撤销 |
| AddedDate | DateTime | 添加时间 |
| ExpiryDate | DateTime | 过期时间 |

**关联关系**:
- 多对一: `User` (IdentityUser)

---

### 12.3 BaseDictionaryGroup（字典分组）

**表名**: `BaseDictionaryGroups`  
**主键**: `DictionaryGroupId` (long, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| DictionaryGroupId | long | 分组ID |
| DictionaryGroupName | string | 分组名称 |
| DictionaryGroupKey | string | 分组键 |
| DictionaryGroupValueType | int? | 值类型 |
| DictionaryGroupStatus | int? | 状态 |
| DictionaryGroupValueTypeName | string | 值类型名称 |
| DictionaryGroupDesc | string | 分组描述 |
| DictionaryGroup18NKeyName | string | 国际化键 |

**关联关系**:
- 一对多: `BaseDictionaries`

---

### 12.4 BaseDictionary（数据字典）

**表名**: `BaseDictionaries`  
**主键**: `DictionaryId` (long, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| DictionaryId | long | 字典ID |
| DictionaryName | string | 字典名称 |
| DictionaryValue | string | 字典值 |
| Dictionary18NKeyName | string | 国际化键 |
| DictionaryStatus | int? | 状态 |
| DictionaryValueType | int? | 值类型 |
| DictionaryValueTypeName | string | 值类型名称 |
| DictionaryGroupId | long? | **外键 → BaseDictionaryGroup.DictionaryGroupId** |
| DictionaryPattern | string | 正则表达式 |
| DictionaryDesc | string | 描述 |
| DictionaryColor | string | 颜色 |
| DictionaryIcon | string | 图标 |
| DictionaryTag | string | 标签 |

**关联关系**:
- 多对一: `DictionaryGroup`

---

### 12.5 DynamicFormInfo（动态表单）

**表名**: `DynamicFormInfos`  
**主键**: `FormId` (long, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| FormId | long | 表单ID |
| BizId | long | 业务ID |
| FormCreator | long | 表单创建者 |
| FormName | string | 表单名称 |
| FormDesc | string | 表单描述 |
| FormStatus | int | 表单状态 |
| FormSchame | string | 表单Schema (JSON) |
| ModelClass | string | 模型类名 |
| Url | string | 提交URL |
| Creator | Guid | 创建者ID |
| FromCreateDate | DateTime? | 创建时间 |
| FormLayout | string | 表单布局 |
| IsCompact | bool | 是否紧凑模式 |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 一对多: `DynamicFormFieldInfos`

---

### 12.6 DynamicFormFieldInfo（动态表单字段）

**表名**: `DynamicFormFieldInfos`  
**主键**: `FieldId` (long, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| FieldId | long | 字段ID |
| FieldName | string | 字段名称 |
| FieldValue | string | 字段值 |
| FieldValueType | int | 值类型 |
| FormId | long | **外键 → DynamicFormInfo.FormId** |
| Creator | Guid | 创建者ID |
| FieldCreateDate | DateTime? | 创建时间 |
| FieldEditDate | DateTime? | 编辑时间 |
| FieldCode | string | 字段编码 |
| FieldUnit | string | 单位 |
| IsRequired | bool | 是否必填 |
| IsEnabled | bool | 是否启用 |
| FieldStatus | int | 字段状态 |
| FieldI18nKey | string | 国际化键 |
| FieldValueDataSource | string | 值数据源 |
| FieldValueLocalDataSource | string | 本地数据源 |
| FieldPattern | string | 验证正则 |
| FieldMaxLength | int | 最大长度 |
| FieldValueTypeName | string | 值类型名称 |
| FieldUIElement | long | UI控件类型 |
| FieldUIElementSchema | string | UI控件Schema |
| FieldPocoTypeName | string | POCO类型名称 |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 多对一: `Form` (所属表单)
- 一对多: `DynamicFormFieldValueInfos`

---

### 12.7 DynamicFormFieldValueInfo（动态表单字段值）

**表名**: `DynamicFormFieldValueInfos`  
**主键**: `FieldValueId` (long, 自增)

| 字段 | 类型 | 说明 |
|------|------|------|
| FieldValueId | long | 字段值ID |
| FieldId | long | **外键 → DynamicFormFieldInfo.FieldId** |
| FieldName | string | 字段名称 |
| FieldValue | string | 字段值 |
| FromId | long | **外键 → DynamicFormInfo.FormId** |
| Creator | Guid | 创建者ID |
| FieldCreateDate | DateTime? | 创建时间 |
| FieldCode | string | 字段编码 |
| FieldUnit | string | 单位 |
| FieldValueType | long | 值类型 |
| BizId | long | 业务ID |
| TenantId/CustomerId | Guid | **外键** |

**关联关系**:
- 多对一: `Field` (字段定义)
- 多对一: `Form` (所属表单)

---

### 12.8 AISettings（AI设置）

**表名**: `AISettings`  
**主键**: 通过 TenantId 或 CustomerId 关联

| 字段 | 类型 | 说明 |
|------|------|------|
| TenantId | Guid? | **外键 → Tenant.Id** (租户级配置) |
| CustomerId | Guid? | **外键 → Customer.Id** (客户级配置) |
| ... | ... | AI相关配置字段 |

**关联关系**:
- 一对一: `Tenant` 或 `Customer`

---

## 📈 核心业务流程图解

### 设备数据上报流程

```
Device (设备)
    ↓ (通过 MQTT/HTTP/CoAP 上报)
TelemetryData / AttributeLatest (数据存储)
    ↓ (触发 EventBus 事件)
FlowRule (规则引擎)
    ↓ (执行 Flow 节点)
FlowOperation (操作记录)
    ↓ (可能触发)
Alarm (告警) / SubscriptionTask (订阅任务)
```

### 产品-设备映射流程

```
Produce (产品模板)
    ├── Define Fields → ProduceDictionary (字段定义)
    ├── Set Defaults → ProduceData (默认值)
    └── Map to Device → ProduceDataMapping (映射规则)
                              ↓
                        Device (具体设备)
                              ↓
                    TelemetryData/AttributeLatest (实际数据)
```

### 规则链执行流程

```
Event (事件触发: Telemetry/Attribute/RPC等)
    ↓
FlowRule (查找匹配的规则)
    ↓
Load Flows (加载流程节点，按拓扑排序)
    ↓
Execute Each Flow Node:
    ├── Script Engine (执行脚本: JS/Python/C#等)
    ├── Task Executor (执行任务: 告警/推送等)
    └── Evaluate Condition (条件判断)
    ↓
FlowOperation (记录每步执行结果)
    ↓
Publish New Events (可能触发下游规则)
```

---

## 🔑 关键设计模式

### 1. TPH (Table Per Hierarchy)
- `DataStorage` 基类通过 `Catalog` 鉴别器派生出 `AttributeLatest`, `TelemetryLatest`, `ProduceData`
- `Device` 基类通过 `DeviceType` 鉴别器派生出 `Gateway`

### 2. 多租户隔离
- 几乎所有业务表都包含 `TenantId` 和 `CustomerId`
- 通过 EF Core 全局查询过滤器自动过滤数据

### 3. 分片存储
- `TelemetryData` 支持按时间粒度分表 (ShardingCore)
- 提高海量时序数据的查询性能

### 4. 事件驱动
- 基于 EventBus (CAP/Shashlik) 的发布-订阅模式
- 解耦数据采集、规则执行、告警通知等环节

### 5. 规则引擎可扩展性
- `Flow` 节点通过 `NodeProcessScript` 支持多语言脚本
- `RuleTaskExecutor` 插件化任务执行器

---

## 📝 数据库迁移提示

项目支持多种数据库后端：
- PostgreSQL (推荐)
- MySQL
- SQL Server
- Oracle
- SQLite (开发/测试)
- Cassandra
- ClickHouse

各数据库的迁移文件位于 `IoTSharp.Data.Storage/*/Migrations/` 目录下。

---

## 🎯 总结

IoTSharp 的业务表设计体现了以下核心理念：

1. **多租户架构**: 严格的 Tenant → Customer → Device 三层隔离
2. **产品抽象**: 通过 Produce/ProduceDataMapping 实现设备与业务的解耦
3. **灵活规则**: 可视化的 FlowRule/Flow 编排 + 多语言脚本支持
4. **完整审计**: AuditLog + FlowOperation 提供全链路追溯
5. **扩展性强**: 插件化的 TaskExecutor 和 ScriptEngine

理解这些表关系是进行二次开发和业务定制的基础。
