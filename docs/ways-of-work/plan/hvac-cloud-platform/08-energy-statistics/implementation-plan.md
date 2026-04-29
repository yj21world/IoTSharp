# Implementation Plan：能耗统计模块

> 对应 PRD：[能耗统计模块 PRD](./prd.md)  
> 审核日期：2026-04-28  
> 状态：**PRD 已有，实施应在遥测查询和业务设备建模稳定后启动**

## 1. 实施概述

本模块基于历史遥测数据计算电耗等能耗指标。第一阶段不做复杂能效分析，只实现电耗点位的小时/日/月统计查询。当前仓库已有 `TelemetryData`、`IStorage`、TimescaleDB/InfluxDB 存储抽象，建议优先基于 TimescaleDB 查询实现，避免新增独立统计库。

## 2. 依赖前置

- `06-telemetry-data-query`：历史遥测查询接口稳定。
- `07-hvac-equipment-modeling`：业务设备、站点、系统关系清晰。
- 点位模板中需要明确能耗点位类型、单位和累计量/瞬时量语义。

## 3. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp.Data.TimeSeries/IStorage.cs` | 审查是否需要新增聚合查询方法 |
| P0 | `IoTSharp.Data.TimeSeries/TimescaleDBStorage.cs` | 实现小时/日/月聚合查询 |
| P0 | `IoTSharp/Controllers/` | 新增 `EnergyController` 或 HVAC 专用 action |
| P1 | `IoTSharp/Dtos/TelemetryDataDto.cs` | 新增能耗查询 DTO 和结果 DTO |
| P1 | `ClientApp/src/views/iot/` | 新增能耗统计页面 |
| P2 | `IoTSharp.Test/` | 增加累计量差分、时间范围和空数据测试 |

## 4. 实施步骤

1. 定义能耗点位识别策略：优先通过模板/点位元数据标记 `Energy`、`Electricity`、`Cumulative`。
2. 新增能耗查询 DTO：范围、粒度、设备/业务设备/站点、点位 key。
3. 在 TimescaleDB 路径实现基础聚合：累计电表读数按周期取首末差值，瞬时功率按周期积分或平均值另行标记。
4. 返回 `rows` 包含时间桶、能耗值、单位、数据质量标记。
5. 前端实现时间范围、粒度、对象选择和趋势表格/图表。
6. 暂不实现自动报表、能效指标、异常识别和复杂同比环比。

## 5. 验收标准

- 可按设备或业务设备查询小时/日/月电耗。
- 空数据、倒表、缺测场景有明确质量标记，不静默给出错误值。
- 查询结果统一使用 `ApiResult<PagedData<EnergyStatDto>>`。
- 前端页面使用 Element Plus 显式组件。

