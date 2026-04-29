# Implementation Plan：平台权限、菜单与租户管理完善模块

> 对应 PRD：[平台权限、菜单与租户管理完善模块 PRD](./prd.md)  
> 审核日期：2026-04-28  
> 状态：**基础能力已有，前端和权限边界需收敛**

## 1. 实施概述

当前仓库已有 ASP.NET Core Identity、JWT、`Tenant`、`Customer`、`TenantsController`、`CustomersController`、`MenuController` 和前端 settings 页面。第一阶段不重做完整 IAM，而是把暖通控制台需要的租户/客户隔离、菜单入口和页面权限收敛到可用状态。

## 2. 依赖前置

- 核心业务模块 API 已明确租户/客户过滤方式。
- 前端路由和菜单能稳定加载 HVAC 相关页面。
- AGENTS.md 约定的新 API 返回结构已落实到新增接口。

## 3. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp/Extensions/IoTSharpExtension.cs` | 审查 `JustTenant/JustCustomer` 使用覆盖 |
| P0 | `IoTSharp/Controllers/TenantsController.cs`、`CustomersController.cs` | 收敛返回结构和错误处理 |
| P0 | `IoTSharp/Controllers/MenuController.cs`、`ClientApp/src/router/route.ts` | 补 HVAC 菜单和路由入口 |
| P1 | `ClientApp/src/views/iot/settings/*` | 去除 fast-crud，改 Element Plus 显式组件 |
| P1 | `ClientApp/src/bootstrap/routeFeatures.ts` | 审查菜单/功能开关加载方式 |
| P2 | `IoTSharp.Test/` | 增加租户/客户隔离测试 |

## 4. 实施步骤

1. 列出所有 HVAC 模块 Controller，确认查询均带租户/客户过滤或设备归属校验。
2. 补充 HVAC 菜单：设备、网关、采集任务、模板、遥测、命令、告警、能耗。
3. 迁移 settings 中租户、客户、用户列表页面，移除 `fs-crud/@fast-crud`。
4. 新增或调整接口时按 `ApiResult<PagedData<T>>` 返回。
5. 补租户/客户隔离测试：不同租户设备、采集任务、告警、命令不可互查。

## 5. 验收标准

- 普通用户只能看到所属租户/客户范围内的数据。
- HVAC 相关页面均可通过菜单进入，刷新后路由稳定。
- settings 页面不再新增 fast-crud 依赖。
- 权限不足、数据不存在和参数错误均返回统一结构。

