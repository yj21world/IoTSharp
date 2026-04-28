# Implementation Plan：点位、模板与映射模块

> 对应 PRD：[点位、模板与映射模块 PRD](../04-point-template-mapping/prd.md)
> 状态：**实体已有，待实现业务逻辑和 API**

## 1. 实施概述

此模块建立在现有 `DeviceTypeProfile`、`CollectionRuleTemplate`、`ProduceDataMapping` 实体之上。核心工作是添加 CRUD API 和"模板应用到设备"的生成逻辑，不涉及新的运行时服务。

## 2. 涉及文件

| 优先级 | 文件 | 动作 |
|--------|------|------|
| P0 | `IoTSharp/Controllers/DeviceTypeProfileController.cs` | **新建** |
| P0 | `IoTSharp/Services/DeviceTypeProfileService.cs` | **审查/完善** — 模板应用逻辑 |
| P1 | `IoTSharp/Controllers/CollectionRuleTemplateController.cs` | **新建** |
| P2 | `IoTSharp/Controllers/ProduceDataMappingController.cs` | **新建** |
| P3 | `ClientApp/` | 模板管理页面 |

## 3. 模板应用逻辑（核心）

```csharp
// IoTSharp/Services/DeviceTypeProfileService.cs
public class DeviceTypeProfileService
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 将设备类型模板应用到目标设备，自动生成 CollectionPoint 记录。
    /// </summary>
    public async Task<ApplyResult> ApplyToDevice(Guid profileId, Guid deviceId)
    {
        var profile = await _dbContext.DeviceTypeProfiles
            .Include(p => p.Points)
            .FirstOrDefaultAsync(p => p.Id == profileId);
        if (profile == null)
            return ApplyResult.Fail("DeviceTypeProfile not found");

        var device = await _dbContext.Device.FindAsync(deviceId);
        if (device == null)
            return ApplyResult.Fail("Device not found");

        // 找到该设备关联的 CollectionTask 和 CollectionDevice
        var collectionDevice = await _dbContext.CollectionDevices
            .FirstOrDefaultAsync(cd => cd.DeviceId == deviceId);
        if (collectionDevice == null)
            return ApplyResult.Fail("Device is not associated with a CollectionDevice");

        int created = 0;
        int skipped = 0;

        foreach (var pointDef in profile.Points)
        {
            // 检查是否已存在同名点位（允许覆盖或跳过）
            var existing = await _dbContext.CollectionPoints
                .FirstOrDefaultAsync(p => p.CollectionDeviceId == collectionDevice.Id
                    && p.TargetName == pointDef.TargetName);
            if (existing != null)
            {
                // 策略：跳过已存在的点位，记录 skipped 数量
                skipped++;
                continue;
            }

            var point = new CollectionPoint
            {
                CollectionDeviceId = collectionDevice.Id,
                SlaveId = pointDef.SlaveId,
                FunctionCode = pointDef.FunctionCode,
                Address = pointDef.Address,
                Quantity = pointDef.Quantity,
                RawDataType = pointDef.RawDataType,
                ByteOrder = pointDef.ByteOrder,
                ReadPeriodMs = pointDef.ReadPeriodMs,
                TransformsJson = pointDef.TransformsJson,
                TargetDeviceId = deviceId,
                TargetName = pointDef.TargetName,
                TargetType = pointDef.TargetType
            };
            _dbContext.CollectionPoints.Add(point);
            created++;
        }

        // 记录模板应用到设备的关系
        device.DeviceTypeProfileId = profileId;
        await _dbContext.SaveChangesAsync();

        return ApplyResult.Success(created, skipped);
    }
}

public class ApplyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }

    public static ApplyResult Fail(string error) => new() { Success = false, Error = error };
    public static ApplyResult Success(int created, int skipped) => new() { Success = true, Created = created, Skipped = skipped };
}
```

## 4. DeviceTypeProfileController

```csharp
// 新建: IoTSharp/Controllers/DeviceTypeProfileController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeviceTypeProfileController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DeviceTypeProfileService _profileService;

    // GET /api/device-type-profiles
    [HttpGet]
    public async Task<ApiResult<PagedData<DeviceTypeProfileDto>>> GetProfiles(
        [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    { /* 实现 */ }

    // GET /api/device-type-profiles/{id}
    [HttpGet("{id}")]
    public async Task<ApiResult<DeviceTypeProfileDto>> GetProfile(Guid id)
    { /* 实现 — 含点位列表和关联产品 */ }

    // POST /api/device-type-profiles
    [HttpPost]
    public async Task<ApiResult<DeviceTypeProfile>> CreateProfile([FromBody] DeviceTypeProfileCreateDto dto)
    { /* 实现 */ }

    // PUT /api/device-type-profiles/{id}
    [HttpPut("{id}")]
    public async Task<ApiResult<DeviceTypeProfile>> UpdateProfile(Guid id, [FromBody] DeviceTypeProfileUpdateDto dto)
    { /* 实现 */ }

    // DELETE /api/device-type-profiles/{id}
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeleteProfile(Guid id)
    { /* 实现 — 检查是否有关联设备在使用 */ }

    // GET /api/device-type-profiles/{id}/points — 模板中的点位定义列表
    [HttpGet("{id}/points")]
    public async Task<ApiResult<List<PointDefinitionDto>>> GetPoints(Guid id)
    { /* 实现 */ }

    // POST /api/device-type-profiles/{id}/points — 向模板添加点位定义
    [HttpPost("{id}/points")]
    public async Task<ApiResult<PointDefinition>> AddPoint(Guid id, [FromBody] PointDefinitionCreateDto dto)
    { /* 实现 */ }

    // DELETE /api/device-type-profiles/{id}/points/{pointId}
    [HttpDelete("{id}/points/{pointId}")]
    public async Task<ApiResult<bool>> RemovePoint(Guid id, Guid pointId)
    { /* 实现 */ }

    // POST /api/device-type-profiles/{id}/apply/{deviceId} — 将模板应用到设备
    [HttpPost("{id}/apply/{deviceId}")]
    public async Task<ApiResult<ApplyResult>> ApplyToDevice(Guid id, Guid deviceId)
    {
        var result = await _profileService.ApplyToDevice(id, deviceId);
        if (!result.Success)
            return new ApiResult<ApplyResult>(ApiCode.Error, result.Error, result);
        return new ApiResult<ApplyResult>(ApiCode.Success, result);
    }
}
```

## 5. 点位定义 DTO

```csharp
// PointDefinition — 模板中的点位定义（不同于 CollectionPoint 运行时实体）
public class PointDefinitionCreateDto
{
    public string TargetName { get; set; }       // 遥测键名（如 "frequency"）
    public string? TargetType { get; set; }       // "Telemetry" | "Attribute"
    public string DisplayName { get; set; }       // 显示名称（如 "运行频率"）
    public string? Unit { get; set; }             // 单位（如 "Hz"）
    
    // Modbus 参数
    public byte SlaveId { get; set; }
    public byte FunctionCode { get; set; }
    public ushort Address { get; set; }
    public ushort Quantity { get; set; } = 1;
    public string RawDataType { get; set; }       // "float32" / "uint16" / "int16" / etc.
    public string? ByteOrder { get; set; }
    public int ReadPeriodMs { get; set; } = 30000;
    
    // 变换管道
    public List<TransformDto>? Transforms { get; set; }
}
```

## 6. CollectionRuleTemplateController

```csharp
// 新建: IoTSharp/Controllers/CollectionRuleTemplateController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CollectionRuleTemplateController : ControllerBase
{
    // GET — 模板列表
    [HttpGet]
    public async Task<ApiResult<List<CollectionRuleTemplateDto>>> GetTemplates()
    { /* 实现 */ }

    // POST — 创建模板
    [HttpPost]
    public async Task<ApiResult<CollectionRuleTemplate>> CreateTemplate([FromBody] CollectionRuleTemplateCreateDto dto)
    { /* 实现 */ }

    // PUT — 更新模板
    [HttpPut("{id}")]
    public async Task<ApiResult<CollectionRuleTemplate>> UpdateTemplate(Guid id, [FromBody] CollectionRuleTemplateUpdateDto dto)
    { /* 实现 */ }

    // DELETE — 删除模板（检查引用）
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeleteTemplate(Guid id)
    { /* 实现 */ }
}
```

## 7. ProduceDataMappingController

```csharp
// 新建: IoTSharp/Controllers/ProduceDataMappingController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProduceDataMappingController : ControllerBase
{
    // GET — 映射规则列表（按产品过滤）
    [HttpGet]
    public async Task<ApiResult<List<ProduceDataMappingDto>>> GetMappings([FromQuery] Guid? produceId)
    { /* 实现 */ }

    // POST — 创建映射规则
    [HttpPost]
    public async Task<ApiResult<ProduceDataMapping>> CreateMapping([FromBody] ProduceDataMappingCreateDto dto)
    { /* 实现 */ }

    // DELETE — 删除映射规则
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeleteMapping(Guid id)
    { /* 实现 */ }
}
```

## 8. 前端实施要点

```
ClientApp/src/views/templates/
├── DeviceTypeProfileList.vue       // 设备类型模板列表
├── DeviceTypeProfileForm.vue       // 创建/编辑模板
├── PointDefinitionList.vue         // 模板点位管理（表格 + 添加）
└── ApplyProfileDialog.vue          // 模板应用到设备对话框
```

## 9. 实施步骤

1. 审查 `DeviceTypeProfile` 实体字段 — 确认是否满足暖通点位定义需求。
2. 创建 `PointDefinition` 实体（如果当前 `DeviceTypeProfile` 中尚无点位定义导航属性）。
3. 实现 `DeviceTypeProfileService.ApplyToDevice` 方法（核心逻辑）。
4. 创建 `DeviceTypeProfileController`。
5. 创建 `CollectionRuleTemplateController`（简单 CRUD）。
6. 创建 `ProduceDataMappingController`（简单 CRUD）。
7. 实现前端模板管理页面。
8. 端到端测试：创建模板 → 添加点位 → 应用到设备 → 验证 CollectionPoint 生成。

## 10. 不需要改动

- `DeviceTypeProfile` / `CollectionRuleTemplate` / `ProduceDataMapping` 实体核心字段 — 除非审查发现字段不足。
- Modbus 采集运行时 — 模板应用仅生成 DB 记录，不影响运行时调度。
