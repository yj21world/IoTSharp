# IoTSharp 运行时精简实施记录

## 目标

将 IoTSharp 从多实现并存的平台型工程，收敛为面向 HVAC 云端交付的产品型工程。当前保留的运行时基线如下：

- 事件总线：`CAP`
- 消息中间件：`RabbitMQ`
- 关系数据库：`PostgreSQL`
- 时序数据库：`TimescaleDB`、`InfluxDB`

## 本次已完成项

### 1. 运行时入口收敛

- `IoTSharp.Contracts/AppSettings.cs`
  - `TelemetryStorage` 仅保留 `InfluxDB`、`TimescaleDB`
  - `EventBusStore` 仅保留 `PostgreSql`
  - `EventBusMQ` 仅保留 `RabbitMQ`
  - `EventBusFramework` 仅保留 `CAP`
  - 默认配置调整为 `PostgreSql + RabbitMQ + TimescaleDB`
- `IoTSharp.Contracts/Enums.cs`
  - `DataBaseType` 仅保留 `PostgreSql`
- `IoTSharp/Startup.cs`
  - 移除多数据库分支，固定走 `ConfigureNpgsql(...)`
  - 移除 `Shashlik` 与 ZeroMQ 旁路入口
  - 增加运行时校验，遇到历史配置值立即抛出明确异常

### 2. 事件总线收敛

- `IoTSharp.EventBus.CAP/DependencyInjection.cs`
  - 固定为 `CAP + PostgreSql EventStore + RabbitMQ`
  - 删除 Kafka、ZeroMQ、AmazonSQS、AzureServiceBus、RedisStreams、NATS、Pulsar、InMemory 等分支
- `IoTSharp.EventBus.CAP/IoTSharp.EventBus.CAP.csproj`
  - 删除非当前基线所需 NuGet 包
- 删除目录
  - `IoTSharp.EventBus.NServiceBus`
  - `IoTSharp.EventBus.Shashlik`

### 3. 关系数据库收敛

- `IoTSharp/IoTSharp.csproj`
  - 仅保留 `IoTSharp.Data.PostgreSQL` 引用
- `IoTSharp/Dockerfile`
  - 删除 MySQL、SqlServer、Oracle、Sqlite、InMemory、Cassandra、ClickHouse、Shashlik 的构建输入
- 从解决方案中移除项目
  - `IoTSharp.Data.MySQL`
  - `IoTSharp.Data.SqlServer`
  - `IoTSharp.Data.Oracle`
  - `IoTSharp.Data.Sqlite`
  - `IoTSharp.Data.InMemory`
  - `IoTSharp.Data.Cassandra`
  - `IoTSharp.Data.ClickHouse`
- 删除目录
  - `IoTSharp.Data.Storage/IoTSharp.Data.MySQL`
  - `IoTSharp.Data.Storage/IoTSharp.Data.SqlServer`
  - `IoTSharp.Data.Storage/IoTSharp.Data.Oracle`
  - `IoTSharp.Data.Storage/IoTSharp.Data.Sqlite`
  - `IoTSharp.Data.Storage/IoTSharp.Data.InMemory`
  - `IoTSharp.Data.Storage/IoTSharp.Data.Cassandra`
  - `IoTSharp.Data.Storage/IoTSharp.Data.ClickHouse`

### 4. 时序存储收敛

- `IoTSharp.Data.TimeSeries/DependencyInjection.cs`
  - 仅保留 `InfluxDBStorage`、`TimescaleDBStorage`
- `IoTSharp.Data.TimeSeries/IoTSharp.Data.TimeSeries.csproj`
  - 删除 IoTDB、Taos、MySQL、Oracle、Sqlite、SqlServer 相关依赖
- 删除旧实现文件
  - `EFStorage.cs`
  - `ShardingStorage.cs`
  - `TaosStorage.cs`
  - `IoTDBStorage.cs`

### 5. 测试与配置同步

- `IoTSharp.Test`
  - 删除 `MySqlAppFixture`、`SqliteAppFixture` 及对应测试
  - 保留 `PostgreSqlAppFixture`、`InfluxDbAppFixture`
  - 测试运行时补充 `RabbitMQ` 容器连接
- 配置文件
  - `IoTSharp/appsettings.Development.json` 改为 `RabbitMQ`
  - `IoTSharp/appsettings.TimescaleDB.json` 改为 `PostgreSql + RabbitMQ + TimescaleDB`
  - 重写 `IoTSharp/appsettings.InfluxDB.json`
  - 删除 `IoTSharp/appsettings.Sqlite.json`

### 6. 仓库与交付物清理

- 删除目录
  - `IoTSharp.EasyUse`
  - `IoTSharp.Installer.Windows`
  - `Deployments/rabbit_mongo_influx`
  - `Deployments/zeromq_sharding`
  - `Deployments/zeromq_taos`
- 文档同步
  - `README.zh.md`
  - `packaging/nuget/README.md`

## 明确保留项

- `IoTSharp.Data.JsonDB`
- `IoTSharp.Data.JsonDB.Tests`
- `IoTSharp.Interpreter`
- `IoTSharp.Data.PostgreSQL`
- `IoTSharp.Data.TimeSeries` 中的 `TimescaleDBStorage` 与 `InfluxDBStorage`

`IoTSharp.Data.JsonDB` 保留原因：

- `IoTSharp.Interpreter/SQLEngine.cs` 仍直接依赖 JSON SQL 执行能力

## 破坏性变更

以下历史配置将不再被支持：

- 数据库：`SqlServer`、`MySql`、`Oracle`、`Sqlite`、`InMemory`、`Cassandra`、`ClickHouse`
- 事件总线：`Shashlik`、`NServiceBus`
- MQ：`Kafka`、`ZeroMQ`、`NATS`、`Pulsar`、`RedisStreams`、`AmazonSQS`、`AzureServiceBus`、`InMemory`
- 时序存储：`SingleTable`、`Sharding`、`Taos`、`PinusDB`、`IoTDB`

程序启动时如果仍读取到上述历史值，会直接报 `NotSupportedException`。

## 验证结果

已完成以下编译验证：

- `dotnet build IoTSharp/IoTSharp.csproj -t:Compile`
- `dotnet build IoTSharp.Test/IoTSharp.Test.csproj -t:Compile`

结果：均通过，只有仓库原有 warning，无新增 compile error。

## 后续建议

### 建议继续处理

- 将 `task/` 下仍描述旧架构的历史文档统一标记为“存档”或单独迁移到 `task/archive/`
- 重新梳理 CI 和发布脚本，避免仍扫描已删除项目
- 增补一份面向部署的 `PostgreSQL + RabbitMQ + TimescaleDB/InfluxDB` 手工测试手册

### 手工测试建议

1. 使用 `appsettings.Development.json` 启动主程序，确认 PostgreSQL 与 RabbitMQ 都已可用
2. 切换 `TelemetryStorage=TimescaleDB`，验证设备遥测写入与查询
3. 切换 `TelemetryStorage=InfluxDB`，验证设备遥测写入与查询
4. 验证 CAP 发布/订阅链路是否仍可驱动规则、告警、属性与遥测处理
