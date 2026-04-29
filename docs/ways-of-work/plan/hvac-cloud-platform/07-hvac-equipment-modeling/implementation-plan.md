# Implementation Plan：暖通业务设备建模模块

> 对应 PRD：[暖通业务设备建模模块 PRD](./prd.md)  
> 审核日期：2026-04-28  
> 状态：**PRD 已有，实施需等待第一阶段采集与模板闭环稳定**

## 1. 实施概述

本模块用于把底层通信设备、网关子设备、采集点位聚合成运维人员理解的暖通业务设备，例如主机、水泵、冷却塔、阀门、电表和传感器。当前仓库已有 `Device`、`Asset`、`AssetRelation`、`DeviceTypeProfile`、`ProduceDataMapping`，因此第一版不新建一套孤立模型，而是在现有模型上补业务分类、关系语义和查询视图。

## 2. 依赖前置

- `01-gateway-device-access`：设备、网关、子设备关系稳定。
- `02-transparent-modbus-collection`：采集任务和点位能稳定入库。
- `04-point-template-mapping`：设备类型模板可应用到设备并生成点位。
- `06-telemetry-data-query`：业务设备视图可以读取实时/历史遥测。

## 3. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp.Data/Device.cs` | 审查 `HvacDeviceType`、`DeviceTypeProfileId` 字段是否满足分类需求 |
| P0 | `IoTSharp.Data/Asset.cs`、`AssetRelation.cs` | 明确业务设备、系统、站点之间的关系语义 |
| P0 | `IoTSharp/Controllers/AssetController.cs` | 增加 HVAC 业务设备查询/聚合接口或新增兼容 action |
| P1 | `IoTSharp/Dtos/DeviceDetailDto.cs`、`AssetDto.cs` | 增加业务设备视图 DTO |
| P1 | `ClientApp/src/views/iot/assets/*`、`ClientApp/src/views/iot/devices/DeviceDetail.vue` | 增加业务设备详情、数据源、点位映射视图 |
| P2 | `IoTSharp.Test/` | 增加业务设备关系和隔离测试 |

## 4. 实施步骤

1. 梳理当前 `Asset` / `AssetRelation` / `Device.Owner` 的语义，明确“站点/系统/业务设备/底层设备”的关系表达。
2. 定义 `HvacEquipmentDto`，包含业务设备 ID、名称、类型、关联设备、关联点位、实时数据摘要。
3. 在 `AssetController` 或新增 HVAC 专用 Controller 中提供业务设备列表、详情和关联数据源查询。
4. 在设备详情或资产详情中增加“业务建模”页签，展示数据源、点位映射和模板来源。
5. 保持多租户/客户过滤，所有查询必须通过现有 `IJustMy` 或同等约束。
6. 补测试：一个水泵由变频器和电表聚合，一个主机由多个底层数据源聚合。

## 5. 验收标准

- 能创建或查询水泵、主机等业务设备，并关联多个底层设备/点位。
- 业务设备详情可展示实时关键指标，且能跳转到底层设备。
- 同一租户/客户外的数据不可见。
- 新增 API 返回 `ApiResult<PagedData<T>>`，失败返回空分页对象。
- 前端页面不使用 `fs-crud/@fast-crud`。

