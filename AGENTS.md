# AGENTS.md


## 项目概述

IoTSharp 是基于 ASP.NET Core (.NET 10) 的开源物联网平台，提供设备管理、遥测采集、规则链处理和多租户访问控制。单进程承载 HTTP API、内置 MQTT Broker、CoAP 服务和事件总线。

**应用场景**：暖通空调（HVAC）系统云端管理平台。程序部署在云端，现场设备通过工业网关（如 USR-G805）接入。主要涉及 Modbus RTU/TCP 协议设备（冷水机组、水泵、冷却塔、风柜、阀门、电表、传感器等），少量 MQTT 协议设备。

**架构分层**：
- **接入层**：设备接入、协议适配（MQTT Broker、网关协议）
- **采集层**：Modbus RTU/TCP 采集控制、数据轮询、命令下发
- **规则层**：实时规则引擎、告警触发、自动化策略
## 核心领域
```
租户 -> 客户 -> 设备 / 网关 / 资产
```
**核心实体**：`Device`（含 Gateway 鉴别器）、`DeviceIdentity`、`Produce`、`FlowRule`/`Flow`/`FlowOperation`、`TelemetryData`/`TelemetryLatest`/`AttributeLatest`、`Alarm`、`AssetRelation`。
## 代码风格
- CRLF 换行，PascalCase 类型/成员，`I` 前缀接口
- 不推荐 `var`（`false:silent`）
- 表达式体：属性支持，方法不支持
- API 控制器：`[Authorize]` 类级别，路由 `api/{Controller}/[action]`
- MQTT 控制器：`Services/MQTTControllers/`，使用 MQTTnet.AspNetCore.Routing

## API 返回约定（新增）
- 统一使用 `ApiResult<T>` 外层包装：`{ code, msg, data }`
- 列表类/查询类返回必须使用分页对象：`data = { total, rows }`
- 新增/更新后若返回实体，也按列表约定返回：`data = { total: 1, rows: [entity] }`
- 失败场景保持同结构：`data = { total: 0, rows: [] }`，避免前端判空分支分裂

## 构建与热重载约定（新增）
- 修改 DTO、枚举、公共接口签名、Job 构造函数签名后，不得仅依赖 `dotnet watch` 热重载
- 上述变更必须执行：停止运行进程 -> `dotnet clean` -> 全量 `dotnet build` -> 重新 `dotnet run/watch run`
- 遇到 `TypeLoadException` / `value type mismatch` 优先按“二进制不一致”处理，而非先改业务逻辑
## 安全机制
- 身份认证：JWT Bearer（ASP.NET Core Identity）
- 多租户：JWT 携带 `TenantId`/`CustomerId` 声明，`IJustMy` 强制数据隔离
- 设备认证：`AccessToken` / `DevicePassword` / `X509Certificate`
## 领域边界规则
### IoTSharp（本仓库）
负责：平台控制面、租户/产品/资产/设备管理、规则链运行时、边缘节点管理、发布中心、API、UI、权限、可观测性、审计。
## 实现约束
- **禁止完全重写** — 保留现有接入、产品、资产、FlowRule 投资
- **分层升级** — 渐进式升级策略
- **职责分离** — 实时处理与长时间运行操作分离
- **显式建模** — 优先显式领域模型，而非纯脚本行为
