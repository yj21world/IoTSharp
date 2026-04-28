# 实现改进笔记

> 创建日期：2026-04-27
> 来源：架构审核讨论中的补充发现
> 状态：待实施

## 1. InfluxDB / TimescaleDB 写入路径语义差异

**问题**：`TimescaleDBStorage.StoreTelemetryAsync()` 同时写入 `TelemetryData`（历史）和 `TelemetryLatest`（最新值），而 `InfluxDBStorage.StoreTelemetryAsync()` 只写 InfluxDB 不更新 PostgreSQL 的 `TelemetryLatest` 表。如果运行中切换存储后端，读 `TelemetryLatest` 的调用方（不走 `IStorage` 接口）会读到过期数据。

**当前风险评估**：`GetTelemetryLatest` 的两个实现都走各自的查询路径（TimescaleDB → EF Core，InfluxDB → Flux `last()`），核心查询链路安全。风险在于可能存在直查 `TelemetryLatest` 表的业务代码没有经过 `IStorage` 接口。

**建议**：切换到 InfluxDB 前，全量 grep 确认没有绕过 `IStorage` 直接查询 `TelemetryLatest` 的代码。

---

## 2. Modbus 超时即时重试

**问题**：`CollectionConnectionDto.RetryCount = 3` 已在 DTO 中定义，但 `ModbusCollectionService.SendModbusRequestAsync()` 的超时回调只记录 `CollectionLog`（Status="Timeout"），不做即时重试。当前依赖调度周期的自然重试——采集点在下个周期被重新加入队列。如果采集周期较长（如 5 分钟），一次丢包就会导致长时间数据空缺。

**涉及文件**：
- `IoTSharp.Contracts/CollectionTaskDtos.cs` — `CollectionConnectionDto.RetryCount` 字段
- `IoTSharp/Services/ModbusCollection/ModbusCollectionService.cs` — 超时回调（约 348-362 行）

**建议方案**：在超时回调中增加重试计数逻辑：
1. 第一次超时 → 立即重发一次（复用 `SendModbusRequestAsync`）
2. 第二次仍然超时 → 记录日志，放弃，等待下个调度周期
3. 后续可扩展为读取 `RetryCount` 配置做 N 次即时重试

---

## 3. BatchMerger.Optimize() 未调用

**问题**：`BatchMerger` 有 `Merge()` 和 `Optimize()` 两个方法。`Merge()` 找出连续的寄存器地址区间并合并为 `BatchRequest`；`Optimize()` 进一步合并相邻区间到最多 125 个连续寄存器（Modbus 协议单次读取上限），可减少 MQTT 往返次数。当前 `GatewayScheduler.ProcessQueueAsync()` 只调用了 `Merge()`，没有调用 `Optimize()`。

**涉及文件**：
- `IoTSharp/Services/ModbusCollection/BatchMerger.cs` — `Optimize()` 方法（约 108-141 行）
- `IoTSharp/Services/ModbusCollection/GatewayScheduler.cs` — `ProcessQueueAsync()` 方法

**建议方案**：在 `ProcessQueueAsync` 中 `Merge()` 之后增加一行 `batch = BatchMerger.Optimize(batch)`。风险很低，单行改动。

---

## 4. MCP HTTP 入口用途未说明

**问题**：arch.md §1 和 §2.1 架构图中提到了 "MCP HTTP 入口"（`ModelContextProtocol.AspNetCore`），但没有说明 MCP 在此系统中的定位——是给 AI 工具链用的诊断入口，还是实验性功能。

**建议**：在架构文档中补一句用途说明，或标注为实验性功能。

---

## 5. 测试策略

**问题**：架构文档未涉及测试策略。核心采集链路（Modbus RTU 组帧、CRC 校验、字节序解析、数据变换管道）非常适合单元测试，且已有 `ModbusDataParserTests` 和 `ModbusTopicTests`。

**建议**：
- 核心采集链路（解析器、协议编解码、变换管道）保持单元测试覆盖。
- Modbus 采集端到端流程需要集成测试（模拟 MQTT 消息往返）。
- 控制命令链路（RPC 下发/响应/超时）补充测试。
