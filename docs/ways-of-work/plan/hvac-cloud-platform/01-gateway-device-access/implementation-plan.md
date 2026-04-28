# Implementation Plan：网关与设备接入模块

> 对应 PRD：[网关与设备接入模块 PRD](../01-gateway-device-access/prd.md)  
> 审核日期：2026-04-28  
> 状态：**核心后端能力已基本完成，文档/API一致性、前端整改、诊断和测试待补齐**

## 1. 审核结论

本模块不应作为重写任务处理。当前代码已经覆盖设备/网关模型、三类设备身份、HTTP 设备管理、HTTP/MQTT 数据接入、ThingsBoard 风格网关路由、网关子设备自动创建、连接/活动状态事件和遥测/属性存储链路。

本次同步修正了原计划中的几个不准确点：

- 在线状态不写入 `Device.Online` 或 `LastActiveTime` 字段；当前模型没有这些字段，状态通过 CAP 事件写入 `AttributeLatest` 的服务侧属性。
- 网关父子关系不使用 `ParentDeviceId`；当前使用 `Device.Owner`、`Gateway.Children` 和 EF 自引用关系表达。
- `X509Certificate` 不是简单预留点；已有证书身份生成、下载和 MQTT 证书指纹认证路径，但依赖 TLS/CA 配置。
- 前端设备列表仍使用 `fs-crud/@fast-crud`，与 AGENTS.md 新增约束冲突，不能标为已完成。

## 2. 已实现能力与代码证据

| 能力 | 当前结论 | 代码位置 |
|------|----------|----------|
| Device/Gateway 实体 | 已实现，`Gateway` 继承 `Device`，通过 `DeviceType` 鉴别器区分 | `IoTSharp.Data/Device.cs`、`IoTSharp.Data/Gateway.cs`、`IoTSharp.Data/ApplicationDbContext.cs` |
| 设备身份模型 | 已支持 `AccessToken`、`DevicePassword`、`X509Certificate` | `IoTSharp.Data/DeviceIdentity.cs`、`IoTSharp.Contracts/Enums.cs` |
| MQTT 认证 | 已覆盖 AccessToken、用户名密码、X509 thumbprint | `IoTSharp/Services/MQTTService.cs` |
| MQTT 连接状态 | 连接成功发布 `Connected` 与 `Activity`，断开发布 `Disconnected` 与 `Inactivity` | `IoTSharp/Services/MQTTService.cs`、`IoTSharp.EventBus/EventBusSubscriber.cs` |
| 状态持久化 | 通过事件总线写入 `_Connected`、`_Active`、`_LastActivityDateTime`、`_LastConnectDateTime` 等属性 | `IoTSharp.EventBus/EventBusSubscriber.cs` |
| HTTP 设备 CRUD | 已实现列表、详情、新增、更新、删除 | `IoTSharp/Controllers/DevicesController.cs` |
| 设备身份查询 | 已实现身份查询，X509 查询不返回私钥；证书下载走专用接口 | `IoTSharp/Controllers/DevicesController.cs` |
| HTTP 数据入口 | 已实现遥测、属性、告警、RPC、RawData/RuleChain 上传 | `IoTSharp/Controllers/DevicesController.cs` |
| MQTT 网关入口 | 已实现 `v1/gateway/telemetry`、`attributes`、`connect`、`disconnect`、`json`、`xml`、`kepserverex` | `IoTSharp/Services/MQTTControllers/GatewayController.cs` |
| 子设备自动创建 | 已实现，按同一网关 `Children` 中的设备名称查找或创建，并继承网关租户/客户 | `IoTSharp/Extensions/IoTSharpExtension.cs` |
| 最新状态查询 | 设备列表和详情会读取 `_Active`、`_LastActivityDateTime` | `IoTSharp/Controllers/DevicesController.cs` |
| 后端基础 API 测试 | 已有 OpenAPI 覆盖和 API smoke 测试 | `IoTSharp.Test/ApiContractTests.cs`、`IoTSharp.Test/ApiSmokeTests.cs` |

## 3. 仍需补齐的问题

### P0：接口返回结构一致性

当前已统一使用 `ApiResult<T>` 外层结构，但并非所有查询/详情/新增返回都使用 `{ total, rows }`：

- `GET /api/Devices/Customers` 已返回 `ApiResult<PagedData<DeviceDetailDto>>`，符合分页约定。
- `GET /api/Devices/{id}` 返回单个 `DeviceDetailDto`。
- `POST /api/Devices` 返回单个 `Device`。
- `GET /api/Devices/{deviceId}/Identity` 返回单个 `DeviceIdentity`。
- 多个遥测/属性查询返回 `List<T>` 而非分页对象。

处理建议：不要一次性破坏历史前端调用。新增或改造面向 HVAC 控制台的接口时按 `{ total, rows }` 输出；历史接口可保留，并在前端迁移完成后逐步收敛。

### P0：前端设备管理整改

当前设备列表仍依赖 `fs-crud/@fast-crud`：

- `ClientApp/src/views/iot/devices/devicelist.vue`
- `ClientApp/src/views/iot/devices/deviceCrudOptions.ts`
- `ClientApp/src/views/iot/devices/detail/*CrudOptions.ts`

这与 AGENTS.md 的前端约束冲突。后续应使用 Vue 3 + Element Plus 显式实现列表、搜索、分页、表单、操作区和详情页。`gatewaylist.vue` 当前几乎为空，也需要补齐真实网关列表或合并进设备列表的网关过滤视图。

### P1：网关子设备手工管理

代码已有自动创建子设备，但没有面向用户的“手工绑定/解绑/重命名”专用 API。当前删除网关时会检查 `Owner.Id == gateway.Id` 的子设备并阻止删除，但缺少显式子设备管理端点。

处理建议：在后续 API 整理中补充：

- 查询网关子设备列表。
- 将已有设备绑定到网关。
- 从网关解绑子设备。
- 明确同一网关下子设备重名规则。

### P1：诊断能力

当前有日志和 CAP 失败统计，但缺少可查询的接入诊断视图。需要补充：

- 最近认证失败原因、时间、ClientId、Username、RemoteEndPoint。
- 最近解析失败 Topic、Payload 摘要、异常信息。
- 最近心跳/活动来源。
- 网关最近上报 Topic。

### P1：测试覆盖

已有 API 合同和 smoke 测试，但缺少接入模块的领域测试：

- AccessToken / DevicePassword / X509 MQTT 认证路径。
- MQTT 连接后 `_Connected`、`_Active`、`_LastActivityDateTime` 写入。
- 网关 telemetry 自动创建子设备并继承租户/客户。
- 同一网关下同名子设备复用，不重复创建。
- HTTP 遥测上传写入最新值和历史链路。
- 租户/客户隔离访问。

### P2：Payload 示例文档

需要给现场调试人员补充最小示例：

- 普通设备 HTTP 遥测、属性、告警上传。
- 普通设备 MQTT telemetry/attributes/RPC。
- 网关 `v1/gateway/telemetry` 与 `v1/gateway/attributes`。
- 网关 connect/disconnect。
- Raw JSON/XML 接入格式。

## 4. 人工审核项

以下内容涉及产品边界或兼容策略，代码无法自动判断，需要人工确认：

1. **历史 API 返回结构是否允许破坏性调整**  
   如果严格要求所有查询/详情/新增接口都改为 `{ total, rows }`，会影响现有前端和 SDK。建议先新增兼容接口或由前端迁移计划承接。

2. **子设备自动创建的命名策略**  
   当前按同一网关下 `Name == devname` 查找。需要确认是否允许不同网关使用同名子设备、是否需要设备编码/外部 ID、是否允许重命名后继续匹配旧名称。

3. **网关类型产品表达**  
   现有 `GatewayType` 枚举偏通用协议类型，PRD 提到的“透传网关、边缘采集网关、普通 MQTT 网关”还没有明确落到设备字段或产品模板字段上。

4. **X509 私钥下载策略**  
   `GetIdentity` 已脱敏，但 `DownloadCertificates` 会返回 client key zip。需要确认是否只允许首次下载、是否需要审计和权限加强。

## 5. 后续实施步骤

1. 前端整改：将设备/网关列表和详情从 `fs-crud/@fast-crud` 迁移到 Element Plus 显式组件。
2. 接口一致性整理：为 HVAC 控制台新增或调整分页查询、详情、身份、子设备管理接口。
3. 增加接入诊断模型和查询接口，优先覆盖认证失败与 payload 解析失败。
4. 补齐 MQTT/HTTP 接入领域测试。
5. 编写 Payload 示例文档并链接到本 PRD。

## 6. 暂不改动

- 不重写 `Device` / `Gateway` / `DeviceIdentity` 领域模型。
- 不改变当前 MQTT Broker 单进程内置部署方式。
- 不在接入层加入 Modbus 解析、能耗统计或业务设备聚合逻辑。
- 不直接删除历史 HTTP/MQTT Topic 和 API，避免破坏现有 SDK、前端和现场接入路径。
