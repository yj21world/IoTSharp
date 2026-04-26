# 商场暖通空调系统云端接入 - 需求与功能分析

> **版本**: 1.0
> **日期**: 2026年4月21日
> **项目**: 商场暖通空调 (HVAC) 云端管理系统
> **技术栈**: .NET 10 + Vue 3 + IoTSharp 平台
> **参考**: ThingsCloud 设备管理模型、有人 USR-G805 网关

---

## 一、项目需求

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
| **主机** | 冷水机组、热泵机组 | Modbus RTU/TCP | 遥测多（供回水温度、压力、电流、功率等），需控制（启停、设定温度） |
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

### 1.4 USR-G805 网关特性

USR-G805 是有人物联网的工业 4G 网关，关键特性：
- 双网口（LAN/WAN）+ RS232/RS485 串口 + 4G LTE
- 支持 DTU 透明传输：串口数据 ↔ TCP/UDP
- 支持 Modbus RTU 转 Modbus TCP 透明转发
- 支持 MQTT 协议：可配置连接到云端 MQTT Broker
- 支持自定义注册包/心跳包/数据包格式
- 工作模式：DTU 透明传输 / Modbus 网关 / MQTT 网关透传


### 1.5 参考 ThingsCloud 的设计模式

参考 ThingsCloud 的设备管理模型：

| 概念 | ThingsCloud | IoTSharp 对应 | 说明 |
|------|-------------|--------------|------|
| 产品 | 设备类型模板 | Produce | 定义属性、遥测、命令的元数据 |
| 设备 | 运行时实例 | Device | 绑定到产品，有独立身份和连接状态 |
| 网关 | 接入子设备 | Device (Gateway) | 管理子设备的连接和数据转发 |
| 属性 | 设备状态/配置 | AttributeLatest | 最新值，分 ServerSide/ClientSide |
| 遥测 | 时序数据 | TelemetryData + TelemetryLatest | 历史记录 + 最新快照 |
| 命令 | 下发控制 | RPC / FlowRule | 远程调用或规则触发 |
| 告警 | 事件通知 | Alarm | 生命周期管理 |

ThingsCloud MQTT Topic 模式（参考）：
- 设备上报属性：`attributes` → `{"temperature": 28.5}`
- 设备上报遥测：`attributes`（ThingsCloud 不区分属性和遥测，统一用属性）
- 平台下发命令：`command/send/<id>` → `{"method": "setValve", "params": {"openPercent": 80}, "id": 1}`
- 设备回复命令：`command/reply/<id>` → `{"method": "setValve", "params": {"openPercent": 80}, "id": 1}`

---

## 二、IoTSharp 已实现功能

### 2.1 设备管理（完整）

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| 设备 CRUD | ✅ | `Controllers/DevicesController.cs` |
| 设备遥测上报（HTTP/MQTT/CoAP） | ✅ | `Services/MQTTControllers/TelemetryController.cs` |
| 设备属性管理 | ✅ | `Services/MQTTControllers/AttributesController.cs` |
| 设备身份认证（Token/密码/X509） | ✅ | `Services/MQTTService.cs` |
| 设备 RPC 远程控制 | ✅ | `Services/MQTTControllers/RpcController.cs` |
| 按产品创建设备 | ✅ | `DevicesController.PostDevice(produceId)` |
| 设备自动注册（ProduceToken/AuthorizedKey） | ✅ | `MQTTService.Server_ClientConnectionValidator` |
| 前端设备列表/详情/遥测/属性 | ✅ | `ClientApp/src/views/iot/devices/` |

### 2.2 网关管理（完整）

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| 网关遥测批量上报 | ✅ | `MQTTControllers/GatewayController.cs` |
| 网关属性批量上报 | ✅ | 同上 |
| 子设备连接/断开 | ✅ | 同上 `connect`/`disconnect` 路由 |
| 子设备自动创建 | ✅ | `JudgeOrCreateNewDevice()` |
| ThingsBoard 兼容协议 | ✅ | `MQTTControllers/V1GatewayController.cs` |
| 原始数据映射（XML/JSON） | ✅ | `Gateways/RawDataGateway.cs` |
| KepServerEx 数据接入 | ✅ | `GatewayController.KepServerEx()` |
| 前端网关列表/设计器 | ✅ | `ClientApp/src/views/iot/devices/gateway/` |

### 2.3 边缘节点管理（完整）

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| 边缘节点注册 | ✅ | `Controllers/EdgeController.cs` |
| 边缘心跳上报 | ✅ | 同上 |
| 边缘能力上报 | ✅ | 同上 |
| 边缘任务下发/回执状态机 | ✅ | `Controllers/EdgeTaskController.cs` |
| 任务拉取（边缘端主动拉取） | ✅ | 同上 `PullPendingDispatch` |
| 前端边缘节点/任务页面 | ✅ | `ClientApp/src/views/iot/edge/` |

### 2.4 产品模板系统（完整）

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| 产品 CRUD | ✅ | `Controllers/ProducesController.cs` |
| 产品默认属性 | ✅ | 同上 `ProduceData` |
| 产品字段字典（单位、换算、标签） | ✅ | 同上 `ProduceDictionary` |
| 产品数据映射（虚拟聚合） | ✅ | 同上 `ProduceDataMapping` |
| 产品级遥测/属性 API | ✅ | 同上 `ProduceTelemetry`/`ProduceAttributes` |
| 前端产品列表/表单/映射设计器 | ✅ | `ClientApp/src/views/iot/produce/` |

### 2.5 规则引擎（完整）

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| BPMN 流程执行 | ✅ | `FlowRuleEngine/FlowRuleProcessor.cs` |
| 条件表达式 | ✅ | 同上 |
| 多脚本语言（JS/Python/Lua/C#/SQL） | ✅ | `Interpreter/` |
| 任务执行器（告警/消息/范围检查/设备动作） | ✅ | `TaskActions/` |
| 规则 CRUD + 绑定设备 | ✅ | `Controllers/RulesController.cs` |
| 前端规则列表/设计器/仿真器 | ✅ | `ClientApp/src/views/iot/rules/` |

### 2.6 告警系统（完整）

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| 告警创建/确认/清除 | ✅ | `Controllers/AlarmController.cs` |
| 告警状态机（Active_UnAck → Cleared_Act） | ✅ | 同上 |
| 告警传播（触发规则链） | ✅ | 同上 |
| MQTT 设备上报告警 | ✅ | `MQTTControllers/AlarmController.cs` |
| 前端告警列表 | ✅ | `ClientApp/src/views/iot/alarms/` |

### 2.7 其他已实现

| 功能 | 状态 | 关键文件 |
|------|------|---------|
| 多租户/客户层级 | ✅ | `Controllers/TenantsController.cs` |
| 资产管理 + 关系图 | ✅ | `Controllers/AssetController.cs` |
| MQTT Broker（内置） | ✅ | `Services/MQTTService.cs` |
| 多数据库支持（9种） | ✅ | `IoTSharp.Data.{PostgreSQL,...}/` |
| 多时序库支持（7种） | ✅ | `IoTSharp.Data.TimeSeries/` |
| 事件总线（CAP + 9种MQ） | ✅ | `IoTSharp.EventBus.CAP/` |
| MCP/AI 工具集成 | ✅ | `McpTools/` |
| 安装向导 | ✅ | `Controllers/InstallerController.cs` |
| Docker 部署 | ✅ | `Deployments/` |

---

## 三、待实现功能

### 3.1 HVAC 设备产品模板（优先级：高）

**现状**：产品模板系统完整，但没有暖通设备的预定义模板。

**需要实现**：

| 产品模板 | 属性/遥测字段示例 | 命令示例 |
|---------|-----------------|---------|
| 冷水机组 | 供水温度、回水温度、蒸发压力、冷凝压力、压缩机电流、运行状态、累计运行时间 | 启停控制、温度设定、加载/卸载 |
| 水泵 | 运行状态、频率、电流、功率、流量、扬程、累计运行时间 | 启停控制、频率设定 |
| 冷却塔 | 风机运行状态、频率、出水温度、进水温度 | 启停控制、频率设定 |
| 末端风柜（AHU） | 送风温度、回风温度、新风温度、风阀开度、水阀开度、滤网压差 | 启停控制、温度设定、风阀开度设定、水阀开度设定 |
| 末端风柜（FCU） | 房间温度、设定温度、风速档位、水阀开度 | 启停控制、温度设定、风速设定 |
| 电动阀门 | 开度反馈、手动/自动模式 | 开度设定、开关控制 |
| 风机 | 运行状态、频率、电流、累计运行时间 | 启停控制、频率设定 |
| 电表 | A/B/C相电压、A/B/C相电流、有功功率、无功功率、功率因数、总电能 | 无（只读） |
| 温湿度传感器 | 温度、湿度 | 无（只读） |
| 压力传感器 | 压力值 | 无（只读） |
| 流量计 | 瞬时流量、累计流量 | 无（只读） |

**实现方式**：
- 利用现有 `Produce` + `ProduceData` + `ProduceDictionary` 模型
- 创建 HVAC 设备产品模板种子数据
- 每个产品模板定义默认属性（ServerSide：设备元数据）和遥测字段（ClientSide：运行数据）
- 在 `ProduceDictionary` 中定义字段元数据（单位、精度、显示分组、UI 排序）

### 3.2 USR-G805 网关接入适配（优先级：高）

**现状**：MQTT Broker 和网关协议完整，但需要适配 USR-G805 的数据格式。

**需要实现**：

1. **网关 MQTT 接入协议适配**
   - USR-G805 在 MQTT 网关模式下，可配置上报 Topic 和数据格式
   - 需要定义 IoTSharp 侧的 Topic 规范和数据格式规范
   - 推荐复用现有 `v1/gateway/` 协议（ThingsBoard 兼容）或自定义 HVAC 专用协议

2. **推荐 MQTT Topic 设计**

   ```
   # 网关连接（USR-G805 作为 Gateway 设备）
   # 认证：UserName = "{ProduceName}_{DeviceName}", Password = ProduceToken
   
   # 网关上报子设备遥测（批量）
   Publish: v1/gateway/telemetry
   Payload: {"AHU1": {"supplyTemp": 24.5, "returnTemp": 26.1, "ts": 1713696000000}, ...}
   
   # 网关上报子设备属性（批量）
   Publish: v1/gateway/attributes
   Payload: {"AHU1": {"runningStatus": true, "valveOpenPercent": 80}}
   
   # 子设备连接/断开
   Publish: v1/gateway/connect
   Payload: {"device": "AHU1"}
   
   Publish: v1/gateway/disconnect
   Payload: {"device": "AHU1"}
   
   # 平台下发 RPC 到子设备
   Subscribe: v1/gateway/rpc
   Payload: {"device": "AHU1", "data": {"method": "setTemp", "params": {"setTemp": 25}}}
   
   # 网关回复 RPC
   Publish: v1/gateway/rpc
   Payload: {"device": "AHU1", "id": 1, "data": {"success": true}}
   ```

3. **USR-G805 配置模板**
   - 需要提供 USR-G805 的 MQTT 连接配置模板（Broker地址、端口、认证信息）
   - 需要提供 Modbus 轮询配置模板（从站地址、功能码、寄存器地址、数据类型、轮询周期）
   - 配置模板可作为 EdgeTask 下发，或作为文档提供给现场调试人员

### 3.3 采集任务执行引擎（优先级：高）

**现状**：`CollectionTaskController` 仅有 DTO 定义和草稿验证，Preview 返回硬编码假数据，无实际执行能力。

**需要实现**：

1. **采集任务持久化** — 将 CollectionTaskDto 保存到数据库
2. **采集任务下发到边缘节点** — 与 EdgeTaskController 集成
3. **USR-G805 Modbus 配置导出** — 将 CollectionTask 转换为 USR-G805 可识别的配置格式
4. **采集任务版本管理** — 任务变更时递增版本号
5. **前端采集任务管理页面** — 列表、创建、编辑、下发

### 3.4 HVAC 专用数据看板（优先级：中）

**现状**：前端有基础 Dashboard 页面，但无 HVAC 专用看板。

**需要实现**：

1. **系统概览看板** — 设备总数、在线率、告警统计、能耗概览
2. **冷热源系统看板** — 主机、水泵、冷却塔运行状态和关键参数
3. **末端系统看板** — 各区域风柜运行状态和温湿度
4. **能耗看板** — 日/月/年电量、冷热量、能效比趋势图
5. **设备详情看板** — 单设备实时数据、历史曲线、告警记录

### 3.5 能耗统计与分析（优先级：中）

**现状**：完全未实现。代码库中无任何 energy/能耗 相关代码。

**需要实现**：

1. **电量统计** — 按设备/区域/时间段聚合电表数据
2. **冷热量统计** — 根据供回水温差和流量计算冷热量
3. **能效比（COP）计算** — 冷量/电量比值
4. **能耗报表** — 日/月/年报表，同比环比分析
5. **能耗告警** — 能耗异常检测

### 3.6 HVAC 自动化策略（优先级：中）

**现状**：规则引擎完整，但缺少 HVAC 专用标准节点。

**需要实现**：

1. **定时启停策略** — 按时间表自动启停设备
2. **温度联动策略** — 根据温度阈值自动调节阀门/频率
3. **节能模式** — 根据负荷自动优化运行参数
4. **设备轮换策略** — 多台同类型设备自动轮换运行
5. **标准规则节点** — 封装常用 HVAC 控制逻辑，减少脚本依赖

### 3.7 前端实时数据推送（优先级：中）

**现状**：前端通过 HTTP 轮询获取数据，无 MQTT/WebSocket 实时订阅。

**需要实现**：

1. **WebSocket 转发层** — 后端订阅 MQTT，通过 WebSocket 推送到前端
2. **前端实时数据组件** — 遥测数据实时刷新
3. **实时告警通知** — 前端弹出告警提示

### 3.8 边缘任务主动推送（优先级：低）

**现状**：EdgeTask 仅保存到数据库，等待边缘端拉取，无 MQTT 主动推送通知。

**需要实现**：

1. **任务下发 MQTT 通知** — 下发任务后通过 MQTT 通知网关
2. **任务超时检测** — 定时检测未回执的任务并标记超时
3. **任务重试机制** — 失败任务自动重试

---

## 四、实施优先级与路径

### 第一阶段：基础接入（让设备数据上云）

| 序号 | 任务 | 依赖 |
|------|------|------|
| 1.1 | 创建 HVAC 设备产品模板（冷水机组、水泵、风柜、阀门、电表、传感器） | 无 |
| 1.2 | 定义 USR-G805 MQTT 接入协议适配（Topic 规范、数据格式） | 无 |
| 1.3 | 实现网关子设备 RPC 下发（v1/gateway/rpc） | 1.2 |
| 1.4 | 创建 HVAC 设备种子数据（示例网关 + 子设备） | 1.1 |
| 1.5 | 编写 USR-G805 配置指南文档 | 1.2 |

**验收标准**：USR-G805 网关能通过 MQTT 接入 IoTSharp，子设备遥测数据能上云，平台能下发 RPC 控制命令。

### 第二阶段：数据展示与告警

| 序号 | 任务 | 依赖 |
|------|------|------|
| 2.1 | HVAC 系统概览看板 | 1.1 |
| 2.2 | 设备详情页增强（HVAC 专用属性展示） | 1.1 |
| 2.3 | HVAC 告警规则模板（超温、超压、设备故障等） | 1.1 |
| 2.4 | 前端实时数据推送（WebSocket） | 1.2 |

**验收标准**：能在看板上看到所有 HVAC 设备的实时状态，告警能及时触发和展示。

### 第三阶段：能耗与自动化

| 序号 | 任务 | 依赖 |
|------|------|------|
| 3.1 | 能耗统计 API（电量、冷热量、COP） | 1.1 |
| 3.2 | 能耗看板和报表 | 3.1 |
| 3.3 | HVAC 自动化策略节点（定时启停、温度联动、节能模式） | 2.3 |
| 3.4 | 采集任务持久化和下发 | 1.2 |

**验收标准**：能查看能耗统计报表，能配置和执行自动化策略。

---

## 五、关键设计决策

### 5.1 设备建模方式

**决策**：利用现有 Produce（产品模板）+ Device 模型，不创建 HVAC 专用实体。

**理由**：
- IoTSharp 的 Produce 模型已经支持默认属性、字段字典、数据映射
- 通过 ProduceData 定义遥测/属性字段，通过 ProduceDictionary 定义元数据（单位、分组）
- 不同型号的冷水机组可以创建不同的 Produce，共享相同的字段命名规范
- 避免引入 HVAC 专用表导致与平台核心模型脱节

### 5.2 网关接入协议

**决策**：复用现有 `v1/gateway/` MQTT 协议（ThingsBoard 兼容），不创建 HVAC 专用协议。

**理由**：
- 现有 V1GatewayController 已实现子设备遥测/属性批量上报
- 已支持子设备自动创建（`JudgeOrCreateNewDevice`）
- USR-G805 可配置自定义 MQTT Topic 和 JSON 格式，适配成本低
- 需要补充：网关 RPC 下发到子设备的能力

### 5.3 属性 vs 遥测划分

**决策**：

| 数据类别 | 存储方式 | 示例 |
|---------|---------|------|
| 设备运行状态、模式、手动/自动 | Attribute (ClientSide) | runningStatus, mode, manualAuto |
| 设备配置参数 | Attribute (ServerSide) | deviceName, installLocation, ratedPower |
| 传感器实时值、累计值 | Telemetry | supplyTemp, powerConsumption, runningHours |
| 控制设定值 | Attribute (ServerSide) | setTemp, setFrequency, setValvePercent |

**理由**：
- 属性保留最新值（UPSERT），适合状态和配置
- 遥测保留历史记录，适合趋势分析和能耗统计
- ServerSide 属性可被设备请求获取，适合控制设定值

### 5.4 控制命令下发方式

**决策**：使用 RPC over MQTT，通过规则链或直接 API 调用。

**流程**：
1. 用户在前端点击控制按钮
2. 前端调用 `POST /api/Devices/{deviceId}/RPC`
3. 后端通过 MQTT 发布到 `devices/{devname}/rpc/request/{method}/{rpcId}`
4. USR-G805 网关收到后转发 Modbus 写命令到从站
5. 设备执行后回复 `devices/{devname}/rpc/response/{method}/{rpcId}`
6. 后端更新属性（如设定温度）并返回结果

### 5.5 能耗数据计算方式

**决策**：在规则引擎中计算，结果存为遥测数据。

**方式**：
- 电量：直接从电表遥测读取，按时间聚合
- 冷热量：Q = c × m × ΔT，c 为比热容，m 为流量，ΔT 为供回水温差
- COP：冷量 / 电量，在规则引擎中用表达式节点计算
- 聚合统计：利用 TimescaleDB 连续聚合视图或应用层定时计算

---

## 六、与现有架构文档的关系

| 文档 | 关系 |
|------|------|
| `docs/archive/task/IoTSharp-Architecture.md` | 平台工程架构说明 |
| `docs/archive/task/IoTSharp-Core-Code-Flow.md` | 核心代码流程参考 |
| `docs/archive/task/IoTSharp-Database-Schema.md` | 数据库 Schema 参考 |
