# Agent 实施准备审核记录（2026-04-28）

本文档记录对 `docs/ways-of-work/plan/hvac-cloud-platform` 下 PRD、架构和实施计划的多轮代码验证结果。目标是让后续 agent 可以直接进入实施，而不是重新判断仓库现状。

## 1. 审核方法

本次审核分三轮进行：

1. **文档完整性审核**：读取 Epic、架构文档、10 个 Feature PRD、已有 implementation-plan 与 design-notes，检查模块拆分、依赖顺序和验收口径。
2. **代码事实验证**：对照仓库中的实体、DbContext、Controller、Service、MQTT Controller、前端路由、前端页面和 API 封装，确认“已实现/待实现/文档过期”的边界。
3. **Agent 可实施性审核**：检查每个计划是否具备明确文件范围、接口返回约定、验收标准、测试入口和人工确认项。

## 2. 总体结论

当前计划可以作为后续实施主线，但不能按原文直接全部执行。部分文档已落后于代码，主要集中在：

- `02-transparent-modbus-collection/implementation-plan.md` 仍描述要新建多个 Controller，但代码已采用 `CollectionTaskController` 聚合任务、日志、草稿、校验和预览能力。
- `04-point-template-mapping/implementation-plan.md` 已收敛为 `DeviceCategoryController` / `DeviceCategoryService` / `DeviceCategory`，不再保留公开设备角色字段；后续应按设备大类完善测试和前端交互。
- 多个历史 API 仍返回 `ApiResult<bool>`、`ApiResult<List<T>>` 或单实体 DTO，和 AGENTS.md 的新增约定“查询/新增/更新按 `{ total, rows }` 返回”存在差异。
- 前端仍有较多 `fs-crud/@fast-crud` 用法，和新增前端约束冲突，后续控制台页面必须用 Vue 3 + Element Plus 显式组件实现。

## 3. 代码证据摘要

| 领域 | 已验证代码 | 结论 |
|------|------------|------|
| 设备/网关接入 | `Device`、`Gateway`、`DeviceIdentity`、`DevicesController`、`MQTTService`、`MQTTControllers/GatewayController` | 后端基础能力已存在，缺少专门诊断和统一返回收敛 |
| Modbus 采集 | `CollectionTask`、`CollectionDevice`、`CollectionPoint`、`CollectionLog`、`CollectionTaskController`、`CollectionTaskService`、`ModbusCollectionService`、`GatewayScheduler` | 核心链路已实现，`BatchMerger.Optimize()` 未接入，`RetryCount` 未用于即时重试 |
| 点位/模板 | `DeviceCategory`、`CollectionRuleTemplate`、`DeviceCategoryController`、`DeviceCategoryService`、前端 `devicecategorylist.vue` | 不是从零新建任务，应进入完善、测试和返回格式收敛 |
| 边缘 JSON | `DevicesController` RawData/RuleChain、`MQTTControllers/TelemetryController`、`MQTTControllers/GatewayController`、`EdgeController` | 暂不处理；代码能力保留，但当前需求不考虑边缘网关 |
| 实时/历史查询 | `DevicesController.GetTelemetryLatest/GetTelemetryData`、`TelemetryLatest`、`TelemetryData`、`IStorage` | 能查数据，但返回多为 `List<T>`，需为 HVAC 控制台新增分页/聚合查询接口 |
| 控制命令 | `RpcClient`、`MQTTControllers/RpcController`、`DevicesController.Rpc`、`AuditLog` | RPC 通道已有，缺少 `CommandLog`、状态查询、权限校验和审计关联 |
| 告警/规则 | `Alarm`、`AlarmController`、`FlowRule`、`FlowRuleProcessor`、前端规则与告警页面 | 通用能力已有，暖通场景规则模板和控制失败告警待设计 |
| 平台管理 | `TenantsController`、`CustomersController`、`MenuController`、前端 settings 页面 | 基础存在，但前端仍大量使用 fast-crud |

## 4. Agent 实施优先级

### P0：先让第一阶段闭环可验收

1. 修正 `02-transparent-modbus-collection` 的计划：按现有 `CollectionTaskController` 和 `CollectionTaskService` 完善，而不是新建并行 Controller。
2. 在 `GatewayScheduler.ProcessQueueAsync` 接入 `BatchMerger.Optimize()`，并用现有 `ModbusTopicTests` / `ModbusDataParserTests` 风格补测试。
3. 将 `CollectionConnectionDto.RetryCount` 接入 `ModbusCollectionService.SendModbusRequestAsync` 超时处理，或在文档中明确第一阶段不做即时重试。
4. 为采集任务 CRUD、日志、预览补充后端 API 测试和前端 smoke 路径。
5. 对新建或改造接口统一返回 `ApiResult<PagedData<T>>`，失败时返回 `{ total: 0, rows: [] }`。

### P1：补齐控制台可用性

1. 设备、网关、采集、模板、遥测页面逐步迁移掉 `fs-crud/@fast-crud`。
2. 设备详情中的实时/历史数据接口新增 HVAC 控制台专用分页接口，避免直接破坏历史接口。
3. 接入诊断增加可查询视图：认证失败、payload 解析失败、最近 Topic、最近活动时间、最近采集错误。

### P2：后续模块实施计划已补入口

本次审核已为以下 Feature 补充轻量 implementation-plan，作为后续 agent 的实施入口：

- `07-hvac-equipment-modeling/implementation-plan.md`
- `08-energy-statistics/implementation-plan.md`
- `09-alarm-rules-hvac/implementation-plan.md`
- `10-platform-admin-tenant/implementation-plan.md`

这些模块依赖第一阶段数据闭环，不建议抢跑编码。后续计划应基于当前实体 `Asset`、`AssetRelation`、`Alarm`、`FlowRule`、`Tenant`、`Customer`、`Menu` 做渐进升级。

## 5. 每个模块的实施入口

| 模块 | 当前状态 | Agent 下一步 |
|------|----------|--------------|
| 01 网关与设备接入 | 后端核心已有 | 增加诊断 API、补接入测试、前端去 fast-crud |
| 02 透传 Modbus 采集 | 核心运行时已有 | 接入 Optimize、明确 RetryCount、补测试和协议示例 |
| 03 边缘 JSON 接入 | 暂不处理 | 当前需求不考虑边缘网关；保留文档和既有代码，不进入实施 |
| 04 点位模板映射 | Controller/Service 已有 | 补返回结构一致性、模板应用幂等测试、前端显式组件 |
| 05 控制命令审计 | RPC 通道已有 | 新增 CommandLog/CommandService/CommandController |
| 06 遥测查询 | 历史接口已有 | 新增 HVAC 查询接口，保持历史兼容 |
| 07 暖通建模 | PRD 阶段 | 先产 implementation-plan，明确 Asset/Device 边界 |
| 08 能耗统计 | PRD 阶段 | 先产 implementation-plan，明确 TimescaleDB 聚合策略 |
| 09 告警规则 | PRD 阶段 | 先产 implementation-plan，明确 FlowRule 与 Alarm 扩展边界 |
| 10 平台管理 | PRD 阶段 | 先产 implementation-plan，优先菜单/角色/租户隔离和前端去 fast-crud |

## 6. 统一约束

后续 agent 执行计划时必须遵守：

- 不重写 IoTSharp 现有主路径，优先在当前 Controller/Service/实体上增量修改。
- 修改 DTO、枚举、公共接口签名、Job 构造函数签名后，执行 `dotnet clean`、全量 `dotnet build`，不能只依赖热重载。
- 新增前端页面不使用 `fs-crud` 或 `@fast-crud/fast-crud`。
- 新增/更新/查询类接口按 `ApiResult<PagedData<T>>` 返回；失败场景使用空分页对象。
- 涉及采集、命令的改动必须补充诊断日志和可查询失败原因；边缘上传当前暂不处理。
- 测试优先覆盖协议解析、采集调度、返回结构、租户/客户隔离和前端核心路径。
