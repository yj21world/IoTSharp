# 前端页面需求：透传网关 Modbus 采集模块

**书名**：HVAC 云端管理平台前端页面设计手册

## 1. 目标

本文档描述透传网关 Modbus 采集模块的前端页面需求。当前后台管理系统使用 Vue 3、Element Plus、Vue Router、Pinia、Axios，并已存在 `ClientApp/src/views/iot/collectiontask/collectiontasklist.vue` 和 `ClientApp/src/api/collectiontask/index.ts`。本阶段前端以业务可用为优先，不追求复杂组态、拖拽式配置或高级交互，优先使用查询表格、基础表单、弹窗或抽屉完成采集任务配置、日志查看和调试排障。

## 2. 设计原则

- 以现场调试和运维交付为优先，页面信息要直接、可查、可改、可排障。
- 使用 Element Plus 常规后台组件：`el-form`、`el-table`、`el-dialog`、`el-drawer`、`el-tabs`、`el-tag`、`el-button`、`el-select`、`el-input-number`、`el-date-picker`。
- 页面先采用简单表格表单，不做拖拽配置、可视化组态和复杂大屏。
- 点位数量可能较多，点位配置必须以表格为主，而不是只依赖 JSON 文本框。
- JSON 编辑可作为高级模式保留，但不能作为普通用户的唯一配置方式。
- 所有接口调用遵循后端 `ApiResult<T>`，列表数据读取 `data.total` 和 `data.rows`。
- 页面文案使用中文，字段名称贴近暖通现场调试人员习惯。

## 3. 页面范围

### 3.1 采集任务列表页

路径建议：`/iot/collectiontask`

主要用途：

- 查看所有采集任务。
- 按任务标识、网关、启用状态、协议类型筛选。
- 快速查看从站数量、点位数量、最近采集状态。
- 进入新增、编辑、日志、运行状态、启用/停用操作。

页面结构：

- 顶部查询区。
- 中部任务表格。
- 右侧或弹窗操作入口。
- 底部分页。

查询条件：

| 字段 | 控件 | 说明 |
| --- | --- | --- |
| 任务标识 | `el-input` | 支持模糊查询 |
| 网关设备 | `el-select` | 下拉选择网关，首期可按设备名/ID 搜索 |
| 协议 | `el-select` | 首期默认 Modbus |
| 状态 | `el-select` | 全部、启用、停用 |

表格列：

| 列名 | 说明 |
| --- | --- |
| 任务标识 | `taskKey` |
| 网关 | 显示网关名称，名称缺失时显示 DeviceId |
| 协议 | 首期为 Modbus |
| 从站数 | 任务下 CollectionDevice 数量 |
| 点位数 | 所有从站点位数量 |
| 状态 | 启用/停用 |
| 版本 | 配置版本 |
| 更新时间 | 最近更新时间 |
| 最近采集状态 | 成功、失败、超时、未运行 |
| 操作 | 编辑、启用/停用、日志、运行状态、删除 |

## 4. 新增/编辑采集任务

新增和编辑可使用 `el-drawer` 或大尺寸 `el-dialog`。考虑点位表格较宽，建议优先使用右侧抽屉或全屏弹窗。

### 4.1 基本信息区

字段：

| 字段 | 控件 | 必填 | 说明 |
| --- | --- | --- | --- |
| 任务标识 | `el-input` | 是 | 如 `mall-a-cooling-pump` |
| 网关设备 | `el-select` | 是 | 只能选择网关设备 |
| 协议 | `el-select` | 是 | 首期固定 Modbus |
| 是否启用 | `el-switch` | 是 | 默认启用 |
| 连接名称 | `el-input` | 否 | 便于现场识别 |
| 超时时间 | `el-input-number` | 是 | 单次请求超时时间，单位 ms |
| 重试次数 | `el-input-number` | 否 | 首期可仅保存配置，不强制实现重试 |

### 4.2 从站配置区

从站使用表格维护，支持新增、编辑、删除、启用/停用。

表格列：

| 列名 | 控件 | 说明 |
| --- | --- | --- |
| 从站标识 | `el-input` | 如 `pump-vfd-01` |
| 从站名称 | `el-input` | 如 `冷冻水泵 1# 变频器` |
| SlaveId | `el-input-number` | 1-247 |
| 启用 | `el-switch` | 控制是否参与采集 |
| 点位数 | 文本 | 当前从站点位数量 |
| 操作 | 按钮 | 编辑点位、复制、删除 |

交互要求：

- 新增从站时自动生成默认 `deviceKey`。
- 删除从站前需要二次确认。
- 已有关联点位时删除从站需要提示会同步删除点位配置。

### 4.3 点位配置区

点位配置是本模块核心，必须以表格为主。

进入方式：

- 在从站表格点击“编辑点位”。
- 在同一抽屉内切换到点位 Tab。
- 或打开子抽屉显示点位表格。

点位表格列：

| 列名 | 控件 | 必填 | 说明 |
| --- | --- | --- | --- |
| 点位标识 | `el-input` | 是 | 如 `frequency` |
| 点位名称 | `el-input` | 是 | 如 `运行频率` |
| 功能码 | `el-select` | 是 | 1/2/3/4 |
| 寄存器地址 | `el-input-number` | 是 | 明确按 0 基地址填写 |
| 寄存器数量 | `el-input-number` | 是 | 与数据类型匹配 |
| 原始类型 | `el-select` | 是 | bool/int16/uint16/int32/uint32/float32/float64/string |
| 字节序 | `el-select` | 是 | AB/CD/ABCD/CDAB/DCBA/BADC |
| 字顺序 | `el-select` | 否 | 多寄存器时使用 |
| 轮询周期 | `el-input-number` | 是 | 单位 ms |
| 转换规则 | 简化按钮 | 否 | 打开转换规则弹窗 |
| 目标设备 | `el-select` | 是 | 采集值写入哪个设备 |
| 目标遥测键 | `el-input` | 是 | 如 `frequency` |
| 值类型 | `el-select` | 是 | boolean/int/long/double/string |
| 单位 | `el-input` | 否 | Hz、kW、℃ 等 |
| 启用 | `el-switch` | 是 | 是否参与采集 |

点位操作：

- 新增点位。
- 复制点位。
- 删除点位。
- 批量启用/停用。
- 批量修改轮询周期。
- 表格内快速编辑。
- 单点测试，调用后端预留测试接口或后续接口。

首期可以暂不做 Excel 导入导出，但页面应保留“导入/导出”按钮位置。

### 4.4 转换规则弹窗

转换规则首期不做复杂脚本，使用结构化表单。

支持规则：

| 规则 | 字段 |
| --- | --- |
| 倍率 Scale | factor |
| 偏移 Offset | value |
| 小数位 Round | digits |
| 布尔映射 BooleanMap | trueText、falseText 或 0/1 映射 |
| 枚举映射 EnumMap | 原始值、显示值 |

交互要求：

- 多条规则按顺序执行。
- 支持上移、下移、删除。
- 保存后写入点位 `transformsJson`。
- 普通用户不直接编辑 JSON。

## 5. 采集日志页面

日志可以作为独立页面，也可以从任务列表点击打开弹窗。首期建议使用弹窗或抽屉，后续再独立页面。

查询条件：

| 字段 | 控件 | 说明 |
| --- | --- | --- |
| 网关 | `el-select` | 按网关过滤 |
| 任务 | `el-select` | 按采集任务过滤 |
| 从站 | `el-select` | 按从站过滤 |
| 点位 | `el-select` | 按点位过滤 |
| 状态 | `el-select` | Success/Timeout/CrcError/ParseError |
| 时间范围 | `el-date-picker` | 请求或创建时间 |

表格列：

| 列名 | 说明 |
| --- | --- |
| 请求时间 | `requestAt` |
| 响应时间 | `responseAt` |
| 网关 | 网关名称或 ID |
| 从站 | 从站名称/SlaveId |
| 点位 | 点位名称/Key |
| 状态 | 成功、超时、CRC 错误、解析失败 |
| 耗时 | ms |
| 请求帧 | Hex，支持复制 |
| 响应帧 | Hex，支持复制 |
| 解析值 | parsedValue |
| 转换值 | convertedValue |
| 错误信息 | errorMessage |

交互要求：

- 状态用不同 `el-tag` 区分。
- 请求帧、响应帧默认省略，点击查看完整内容。
- 支持复制 Hex。
- 错误信息使用 tooltip 或展开行展示。

## 6. 运行状态面板

运行状态面板用于现场调试。

展示内容：

- 网关在线状态。
- 调度器是否运行。
- 高速/中速/低速点位数量。
- 待响应请求数量。
- 最近请求时间。
- 最近成功时间。
- 最近失败时间。
- 最近错误信息。

首期若后端接口尚未完成，页面可以先保留入口和空状态，不做假数据。

## 7. API 对接

现有 API 模块：

```text
ClientApp/src/api/collectiontask/index.ts
```

已有接口：

| 前端方法 | 后端接口 | 用途 |
| --- | --- | --- |
| `getAll` | `GET /api/CollectionTask/GetAll` | 任务列表 |
| `get` | `GET /api/CollectionTask/Get/{id}` | 任务详情 |
| `create` | `POST /api/CollectionTask/Create` | 新增任务 |
| `update` | `PUT /api/CollectionTask/Update/{id}` | 更新任务 |
| `delete` | `DELETE /api/CollectionTask/Delete/{id}` | 删除任务 |
| `enable` | `POST /api/CollectionTask/Enable/{id}/Enable` | 启用 |
| `disable` | `POST /api/CollectionTask/Disable/{id}/Disable` | 停用 |
| `getLogs` | `GET /api/CollectionTask/GetLogs` | 查询日志 |
| `getDraft` | `GET /api/CollectionTask/GetDraft` | 获取草稿 |
| `validateDraft` | `POST /api/CollectionTask/ValidateDraft` | 校验草稿 |
| `preview` | `POST /api/CollectionTask/Preview` | 配置预览 |

建议后续补充接口：

| 接口 | 用途 |
| --- | --- |
| `GET /api/CollectionTask/GetRuntimeStatus` | 查询全部采集运行状态 |
| `GET /api/CollectionTask/GetRuntimeStatus/{gatewayDeviceId}` | 查询单个网关运行状态 |
| `POST /api/CollectionTask/Validate` | 完整任务校验 |
| `POST /api/CollectionTask/TestPoint` | 单点测试 |

## 8. 前端文件规划

建议文件结构：

```text
ClientApp/src/views/iot/collectiontask/
  collectiontasklist.vue
  components/
    CollectionTaskForm.vue
    CollectionSlaveTable.vue
    CollectionPointTable.vue
    TransformRuleDialog.vue
    CollectionLogDrawer.vue
    CollectionRuntimeStatus.vue
  model.ts

ClientApp/src/api/collectiontask/
  index.ts
```

首期可以继续在 `collectiontasklist.vue` 内实现，但当表单和点位表格开始变复杂时，应拆出组件，避免单文件过大。

## 9. 表单校验

前端必须做基础校验，但以后端校验结果为准。

前端校验项：

- 任务标识必填。
- 网关设备必选。
- 协议必选，首期固定 Modbus。
- SlaveId 范围为 1-247。
- 功能码只能为 1/2/3/4。
- 寄存器地址不能小于 0。
- 寄存器数量必须大于 0。
- 轮询周期必须大于等于平台允许的最小值，建议首期不小于 1000ms。
- 点位标识、点位名称、目标设备、目标遥测键必填。
- 原始类型与寄存器数量不匹配时给出提示。

## 10. 首期交付范围

首期必须完成：

- 采集任务列表。
- 任务新增、编辑、删除。
- 启用、停用。
- 从站表格配置。
- 点位表格配置。
- 基础转换规则配置。
- 采集日志查看。
- 基础表单校验。
- 与现有 API 对接。

首期可以暂缓：

- Excel 导入导出。
- 单点测试。
- 运行状态实时刷新。
- 大批量点位虚拟滚动。
- 拖拽式点位编排。
- 图形化组态。

## 11. 验收标准

- 用户可以通过页面创建一个 Modbus 采集任务。
- 用户可以为任务新增至少一个从站。
- 用户可以为从站新增多个点位。
- 用户可以保存任务，并在列表中看到任务。
- 用户可以编辑任务并保存修改。
- 用户可以启用和停用任务。
- 用户可以打开日志弹窗查看采集日志。
- 表单必填项缺失时，页面给出明确提示。
- 后端返回错误时，页面显示错误消息，不静默失败。
- 页面不要求理解复杂 JSON，也能完成常见采集配置。

## 12. 备注

当前已有页面提供了任务列表、弹窗编辑和日志弹窗。后续可以在保留现有页面入口的基础上，逐步把 JSON 编辑模式替换为业务表格模式。这个模块的前端目标不是做“漂亮的大屏”，而是让现场调试人员能快速配置、验证和排查透传网关 Modbus 采集链路。
