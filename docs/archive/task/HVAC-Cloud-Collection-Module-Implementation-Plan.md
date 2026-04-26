# HVAC 云端采集模块实施计划

> **版本**: 1.0
> **日期**: 2026-04-23
> **状态**: 执行中
> **项目**: 商场暖通空调 (HVAC) 云端管理系统
> **技术栈**: .NET 10 + Vue 3 + IoTSharp 平台

---

## 一、需求概述

基于需求文档 `docs/archive/task/HVAC-Cloud-Collection-Module.md`，实施 IoTSharp 平台的云端 Modbus 采集模块。

### 1.1 当前状态

| 模块 | 状态 | 说明 |
|------|------|------|
| 需求文档 | ✅ 完成 | `docs/archive/task/HVAC-Cloud-Collection-Module.md` |
| DTO 定义 | ✅ 完成 | `CollectionTaskDtos.cs` |
| Controller 草稿 | ✅ 完成 | `CollectionTaskController.cs` (仅返回假数据) |
| 数据库实体 | ✅ 完成 | 4 个实体 + 4 个 Configuration |
| 采集执行引擎 | ✅ 完成 | BackgroundService + MQTT Client |
| API 层 | ⏳ 待完成 | CRUD + 子设备创建 |
| 前端页面 | ⏳ 待完成 | 任务管理 + 点位配置 |

### 1.2 技术决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 采集模式 | MQTT 透传（同 Broker） | USR-G805 支持 MQTT 透传 |
| 网关角色 | Gateway 设备类型 | 纯透传，不做边缘计算 |
| 采集引擎 | IoTSharp 进程内 BackgroundService | 高并发、低耦合 |
| 数据库 | PostgreSQL | 结构化、易查询 |
| 子设备创建 | 配置时自动创建 | 简化管理流程 |
| 前端 | Vue 3 + Element Plus | 与现有 ClientApp 一致 |

---

## 二、已完成的文件清单

### 2.1 数据层 (Phase 1)

| 文件路径 | 说明 |
|----------|------|
| `IoTSharp.Data\CollectionTask.cs` | 采集任务实体 |
| `IoTSharp.Data\CollectionDevice.cs` | 采集从站实体 |
| `IoTSharp.Data\CollectionPoint.cs` | 采集点位实体 |
| `IoTSharp.Data\CollectionLog.cs` | 采集日志实体 |
| `IoTSharp.Data\Configurations\CollectionTaskConfiguration.cs` | 任务配置 |
| `IoTSharp.Data\Configurations\CollectionDeviceConfiguration.cs` | 从站配置 |
| `IoTSharp.Data\Configurations\CollectionPointConfiguration.cs` | 点位配置 |
| `IoTSharp.Data\Configurations\CollectionLogConfiguration.cs` | 日志配置 |
| `IoTSharp.Data\ApplicationDbContext.cs` | 已注册新实体 |

### 2.2 采集引擎 (Phase 2)

| 文件路径 | 说明 |
|----------|------|
| `IoTSharp\Services\ModbusCollection\CollectionResponse.cs` | 响应数据结构 |
| `IoTSharp\Services\ModbusCollection\ModbusRtuProtocol.cs` | Modbus RTU 协议栈 |
| `IoTSharp\Services\ModbusCollection\ModbusDataParser.cs` | 数据解析器 |
| `IoTSharp\Services\ModbusCollection\BatchMerger.cs` | 批量合并器 |
| `IoTSharp\Services\ModbusCollection\CollectionConfigurationLoader.cs` | 配置加载器 |
| `IoTSharp\Services\ModbusCollection\GatewayScheduler.cs` | 网关调度器 |
| `IoTSharp\Services\ModbusCollection\GatewaySchedulerManager.cs` | 调度器管理器 |
| `IoTSharp\Services\ModbusCollection\ModbusCollectionService.cs` | 采集服务入口 |
| `IoTSharp\Extensions\ModbusCollectionExtension.cs` | 服务注册扩展 |
| `IoTSharp\Startup.cs` | 已注册服务 |

---

## 三、数据库设计

### 3.1 采集任务表 (CollectionTasks)

```sql
CREATE TABLE CollectionTasks (
    Id UUID PRIMARY KEY,
    TaskKey VARCHAR(100) NOT NULL UNIQUE,  -- 任务标识
    GatewayDeviceId UUID NOT NULL,         -- 关联网关
    Protocol VARCHAR(50) DEFAULT 'Modbus',
    Version INT DEFAULT 1,
    Enabled BOOLEAN DEFAULT true,
    ConnectionJson TEXT,                   -- 连接配置
    ReportPolicyJson TEXT,                  -- 上报策略
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (GatewayDeviceId) REFERENCES Devices(Id)
);
```

### 3.2 采集从站表 (CollectionDevices)

```sql
CREATE TABLE CollectionDevices (
    Id UUID PRIMARY KEY,
    TaskId UUID NOT NULL,                   -- 所属任务
    DeviceKey VARCHAR(100) NOT NULL,        -- 从站标识
    DeviceName VARCHAR(200),                -- 从站名称
    SlaveId INT NOT NULL,                  -- Modbus 从站地址
    Enabled BOOLEAN DEFAULT true,
    SortOrder INT DEFAULT 0,
    ProtocolOptionsJson TEXT,               -- 协议配置
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (TaskId) REFERENCES CollectionTasks(Id) ON DELETE CASCADE,
    UNIQUE (TaskId, SlaveId)
);
```

### 3.3 采集点位表 (CollectionPoints)

```sql
CREATE TABLE CollectionPoints (
    Id UUID PRIMARY KEY,
    DeviceId UUID NOT NULL,                 -- 所属从站
    PointKey VARCHAR(100) NOT NULL,         -- 点位标识
    PointName VARCHAR(200),                 -- 点位名称
    FunctionCode INT NOT NULL,              -- 功能码 1/2/3/4
    Address INT NOT NULL,                   -- 寄存器地址
    RegisterCount INT DEFAULT 1,            -- 寄存器数量
    RawDataType VARCHAR(50) DEFAULT 'uint16',
    ByteOrder VARCHAR(10) DEFAULT 'AB',
    WordOrder VARCHAR(10) DEFAULT 'AB',
    ReadPeriodMs INT DEFAULT 30000,        -- 轮询周期
    PollingGroup VARCHAR(50),               -- 轮询分组
    TransformsJson TEXT,                    -- 换算规则
    TargetDeviceId UUID,                    -- 目标子设备
    TargetName VARCHAR(100),               -- 子设备属性名
    TargetType VARCHAR(50) DEFAULT 'Telemetry',
    TargetValueType VARCHAR(50) DEFAULT 'Double',
    DisplayName VARCHAR(200),
    Unit VARCHAR(50),
    GroupName VARCHAR(100),
    Enabled BOOLEAN DEFAULT true,
    SortOrder INT DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (DeviceId) REFERENCES CollectionDevices(Id) ON DELETE CASCADE,
    FOREIGN KEY (TargetDeviceId) REFERENCES Devices(Id),
    UNIQUE (DeviceId, Address, FunctionCode)
);
```

### 3.4 采集日志表 (CollectionLogs)

```sql
CREATE TABLE CollectionLogs (
    Id UUID PRIMARY KEY,
    GatewayDeviceId UUID NOT NULL,
    TaskId UUID,
    DeviceId UUID,
    PointId UUID,
    RequestId VARCHAR(100) NOT NULL,
    RequestAt TIMESTAMP NOT NULL,
    RequestFrame TEXT,
    ResponseAt TIMESTAMP,
    ResponseFrame TEXT,
    ParsedValue VARCHAR(500),
    ConvertedValue VARCHAR(500),
    Status VARCHAR(50) NOT NULL,           -- Success/Timeout/CrcError/...
    ErrorMessage TEXT,
    DurationMs INT,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX IDX_CollectionLogs_GatewayTime (GatewayDeviceId, CreatedAt),
    INDEX IDX_CollectionLogs_Status (Status),
    INDEX IDX_CollectionLogs_RequestId (RequestId)
);
```

---

## 四、采集引擎架构

### 4.1 核心组件

```
ModbusCollectionService (BackgroundService)
    │
    ├── 订阅 MQTT 响应 Topic (gateway/+/modbus/response/+)
    │
    ├── GatewaySchedulerManager
    │       │
    │       └── GatewayScheduler (每网关一个)
    │               │
    │               ├── 高优先级队列 (<15s)
    │               ├── 中优先级队列 (15-45s)
    │               └── 低优先级队列 (>45s)
    │
    └── BatchMerger (批量合并)
            │
            └── 合并同从站连续地址的请求
```

### 4.2 MQTT Topic 规范

| Topic | 方向 | 说明 |
|-------|------|------|
| `gateway/{name}/modbus/request/{id}` | 云端 → 网关 | Modbus 请求 |
| `gateway/{name}/modbus/response/{id}` | 网关 → 云端 | Modbus 响应 |

### 4.3 关键类说明

| 类 | 职责 |
|----|------|
| `ModbusCollectionService` | 采集服务入口，MQTT 消息处理，响应解析 |
| `GatewayScheduler` | 单个网关的采集调度，按周期触发请求 |
| `BatchMerger` | 合并同从站连续地址的点位为批量请求 |
| `ModbusRtuProtocol` | Modbus RTU 组帧/解析/CRC 校验 |
| `ModbusDataParser` | 数据类型解析/字节序处理/换算规则 |

---

## 五、待完成工作

### 5.1 Phase 3: API 层

| 任务 | 文件 | 说明 |
|------|------|------|
| 扩展 CollectionTaskController | `Controllers\CollectionTaskController.cs` | CRUD + 启动/停止 |
| 创建 CollectionTaskService | `Services\CollectionTaskService.cs` | 业务逻辑 |
| 实现子设备自动创建 | 复用 `JudgeOrCreateNewDevice` | 配置时创建 |

**API 接口清单:**

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/CollectionTask` | 获取任务列表 |
| GET | `/api/CollectionTask/{id}` | 获取任务详情 |
| POST | `/api/CollectionTask` | 创建任务 |
| PUT | `/api/CollectionTask/{id}` | 更新任务 |
| DELETE | `/api/CollectionTask/{id}` | 删除任务 |
| POST | `/api/CollectionTask/{id}/Start` | 启动采集 |
| POST | `/api/CollectionTask/{id}/Stop` | 停止采集 |
| GET | `/api/CollectionTask/Logs` | 获取采集日志 |

### 5.2 Phase 4: 前端页面

| 页面 | 文件 | 说明 |
|------|------|------|
| 任务列表 | `ClientApp/src/views/collection/TaskList.vue` | 列表、创建、编辑、删除 |
| 任务编辑 | `ClientApp/src/views/collection/TaskEdit.vue` | 从站配置、点位配置 |
| 点位配置 | `ClientApp/src/views/collection/PointConfig.vue` | 详细配置组件 |
| 调试日志 | `ClientApp/src/views/collection/DebugLog.vue` | 日志查询 |

---

## 六、验证测试清单

### 6.1 单元测试

- [ ] `ModbusRtuProtocol.BuildReadRequest` - 组帧测试
- [ ] `ModbusRtuProtocol.CalculateCrc` - CRC 校验测试
- [ ] `ModbusRtuProtocol.ParseResponse` - 响应解析测试
- [ ] `ModbusDataParser.ParseFloat32` - Float32 解析（各种字节序）
- [ ] `ModbusDataParser.ApplyTransforms` - 换算规则测试
- [ ] `BatchMerger.Merge` - 批量合并测试

### 6.2 集成测试

- [ ] 创建采集任务 → 数据库记录正确
- [ ] 启动采集 → MQTT 请求发出
- [ ] 收到响应 → 遥测数据写入 TelemetryLatest
- [ ] 子设备自动创建

### 6.3 手动测试

- [ ] Modbus Poll 工具发送请求，WireShark 抓包验证
- [ ] 超时/CRC 错误处理
- [ ] 网关离线/上线处理
- [ ] 前端页面 CRUD 操作

---

## 七、数据库迁移

```bash
# 创建迁移
dotnet ef migrations add AddCollectionTables

# 应用迁移
dotnet ef database update
```

---

## 八、相关文档

- 需求文档: `docs/archive/task/HVAC-Cloud-Collection-Module.md`
- AGENTS.md: `AGENTS.md`
