using IoTSharp.Contracts;
using IoTSharp.Data;
using IoTSharp.Models;
using IoTSharp.Services;
using IoTSharp.Services.ModbusCollection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IoTSharp.Controllers;

[Route("api/[controller]/[action]")]
[Authorize]
[ApiController]
public class CollectionTaskController : ControllerBase
{
    private readonly CollectionTaskService _service;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CollectionTaskController> _logger;

    public CollectionTaskController(
        CollectionTaskService service,
        ApplicationDbContext context,
        ILogger<CollectionTaskController> logger)
    {
        _service = service;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有采集任务
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionTaskDto>>>> GetAll()
    {
        var tasks = await _service.GetAllAsync();
        var dtos = new List<CollectionTaskDto>();
        foreach (var task in tasks)
        {
            dtos.Add(_service.ToDto(task));
        }
        return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.Success, "OK", ToPagedData(dtos)));
    }

    /// <summary>
    /// 获取采集任务详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionTaskDto>>>> Get(Guid id)
    {
        var task = await _service.GetByIdAsync(id);
        if (task == null)
        {
            return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.NotFoundDevice, "CollectionTask not found", ToPagedData()));
        }
        return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.Success, "OK", ToPagedData(_service.ToDto(task))));
    }

    /// <summary>
    /// 创建设集任务
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionTaskDto>>>> Create([FromBody] CollectionTaskDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TaskKey))
        {
            return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.InValidData, "taskKey is required", ToPagedData()));
        }

        // 检查任务Key是否已存在
        var existing = await _service.GetByTaskKeyAsync(dto.TaskKey);
        if (existing != null)
        {
            return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.AlreadyExists, $"TaskKey '{dto.TaskKey}' already exists", ToPagedData()));
        }

        var task = await _service.CreateFromDtoAsync(dto);
        return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.Success, "OK", ToPagedData(_service.ToDto(task))));
    }

    /// <summary>
    /// 更新采集任务
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionTaskDto>>>> Update(Guid id, [FromBody] CollectionTaskDto dto)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.NotFoundDevice, "CollectionTask not found", ToPagedData()));
        }

        var duplicate = await _service.GetByTaskKeyAsync(dto.TaskKey);
        if (duplicate != null && duplicate.Id != id)
        {
            return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.AlreadyExists, $"TaskKey '{dto.TaskKey}' already exists", ToPagedData()));
        }

        var updated = await _service.UpdateFromDtoAsync(existing, dto);
        return Ok(new ApiResult<PagedData<CollectionTaskDto>>(ApiCode.Success, "OK", ToPagedData(_service.ToDto(updated))));
    }

    /// <summary>
    /// 删除采集任务
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<bool>>> Delete(Guid id)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null)
        {
            return Ok(new ApiResult<bool>(ApiCode.NotFoundDevice, "CollectionTask not found", false));
        }

        await _service.DeleteAsync(id);
        return Ok(new ApiResult<bool>(ApiCode.Success, "OK", true));
    }

    /// <summary>
    /// 启用采集任务
    /// </summary>
    [HttpPost("{id}/Enable")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<bool>>> Enable(Guid id)
    {
        var result = await _service.SetEnabledAsync(id, true);
        if (!result)
        {
            return Ok(new ApiResult<bool>(ApiCode.NotFoundDevice, "CollectionTask not found", false));
        }
        return Ok(new ApiResult<bool>(ApiCode.Success, "OK", true));
    }

    /// <summary>
    /// 禁用采集任务
    /// </summary>
    [HttpPost("{id}/Disable")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<bool>>> Disable(Guid id)
    {
        var result = await _service.SetEnabledAsync(id, false);
        if (!result)
        {
            return Ok(new ApiResult<bool>(ApiCode.NotFoundDevice, "CollectionTask not found", false));
        }
        return Ok(new ApiResult<bool>(ApiCode.Success, "OK", true));
    }

    /// <summary>
    /// 获取采集日志
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<object>>> GetLogs(
        [FromQuery] Guid? gatewayDeviceId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] string status,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50)
    {
        var (logs, total) = await _service.GetLogsAsync(gatewayDeviceId, startTime, endTime, status, offset, limit);

        var result = new
        {
            rows = logs,
            total = total
        };

        return Ok(new ApiResult<object>(ApiCode.Success, "OK", result));
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public ApiResult<CollectionTaskDto> GetDraft(CollectionProtocolType protocol)
    {
        var draft = CreateDraft(protocol);
        return new ApiResult<CollectionTaskDto>(ApiCode.Success, "OK", draft);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public ActionResult<ApiResult<CollectionTaskDto>> ValidateDraft([FromBody] CollectionTaskDto request)
    {
        if (request == null)
        {
            return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "Task payload is required", null));
        }

        if (string.IsNullOrWhiteSpace(request.TaskKey))
        {
            return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "taskKey is required", null));
        }

        if (request.Connection == null || string.IsNullOrWhiteSpace(request.Connection.ConnectionName))
        {
            return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "connection.connectionName is required", null));
        }

        if (request.Devices == null || request.Devices.Count == 0)
        {
            return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "At least one device is required", null));
        }

        foreach (var device in request.Devices)
        {
            if (string.IsNullOrWhiteSpace(device.DeviceKey))
            {
                return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "device.deviceKey is required", null));
            }

            foreach (var point in device.Points)
            {
                if (string.IsNullOrWhiteSpace(point.PointKey) || string.IsNullOrWhiteSpace(point.PointName))
                {
                    return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "point.pointKey and point.pointName are required", null));
                }

                if (point.Mapping == null || string.IsNullOrWhiteSpace(point.Mapping.TargetName))
                {
                    return Ok(new ApiResult<CollectionTaskDto>(ApiCode.InValidData, "point.mapping.targetName is required", null));
                }
            }
        }

        return Ok(new ApiResult<CollectionTaskDto>(ApiCode.Success, "OK", request));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public ActionResult<ApiResult<TaskPreviewResponseDto>> Preview([FromBody] TaskPreviewRequestDto request)
    {
        if (request == null)
        {
            return Ok(new ApiResult<TaskPreviewResponseDto>(ApiCode.InValidData, "Preview payload is required", null));
        }

        try
        {
            var response = request.Protocol switch
            {
                CollectionProtocolType.Modbus => BuildModbusPreview(request),
                CollectionProtocolType.OpcUa => BuildSimplePreview(56.78d, request.Point.Mapping.ValueType),
                _ => new TaskPreviewResponseDto
                {
                    Success = false,
                    ErrorCode = "preview_not_supported",
                    ErrorMessage = $"Protocol '{request.Protocol}' does not support preview yet.",
                    ValueType = request.Point.Mapping.ValueType,
                    QualityStatus = QualityStatusType.Uncertain,
                }
            };

            _logger.LogInformation("Previewed collection point {PointKey} for protocol {Protocol}", request.Point?.PointKey, request.Protocol);
            return Ok(new ApiResult<TaskPreviewResponseDto>(ApiCode.Success, "OK", response));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preview failed for point {PointKey}", request.Point?.PointKey);

            var failed = new TaskPreviewResponseDto
            {
                Success = false,
                ErrorCode = "preview_failed",
                ErrorMessage = ex.Message,
                ValueType = request.Point.Mapping.ValueType,
                QualityStatus = QualityStatusType.Bad,
            };

            return Ok(new ApiResult<TaskPreviewResponseDto>(ApiCode.Success, "OK", failed));
        }
    }

    private static TaskPreviewResponseDto BuildModbusPreview(TaskPreviewRequestDto request)
    {
        var byteOrder = GetProtocolOption(request.Point.ProtocolOptions, "ByteOrder")
            ?? GetProtocolOption(request.Point.ProtocolOptions, "byteOrder")
            ?? GetDefaultByteOrder(request.Point.RawValueType);
        var rawDataType = string.IsNullOrWhiteSpace(request.Point.RawValueType) ? "uint16" : request.Point.RawValueType;
        var sampleBytes = ResolveSampleBytes(request.Point, byteOrder, rawDataType);
        var rawValue = ModbusDataParser.ParseRegisters(sampleBytes, rawDataType, byteOrder);
        var transformedValue = ApplyPreviewTransforms(rawValue, request.Point.Transforms, request.Point.Mapping.ValueType);

        return new TaskPreviewResponseDto
        {
            Success = true,
            RawValue = rawValue,
            TransformedValue = transformedValue,
            ValueType = request.Point.Mapping.ValueType,
            QualityStatus = QualityStatusType.Good,
        };
    }

    private static TaskPreviewResponseDto BuildSimplePreview(object rawValue, CollectionValueType valueType)
    {
        var converted = ConvertPreviewValue(rawValue, valueType);
        return new TaskPreviewResponseDto
        {
            Success = true,
            RawValue = rawValue,
            TransformedValue = converted,
            ValueType = valueType,
            QualityStatus = QualityStatusType.Good,
        };
    }

    private static object ApplyPreviewTransforms(
        object rawValue,
        IReadOnlyList<ValueTransformDto> transforms,
        CollectionValueType valueType)
    {
        if (rawValue is string)
        {
            return rawValue;
        }

        if (!TryConvertToDouble(rawValue, out var numericValue))
        {
            return ConvertPreviewValue(rawValue, valueType);
        }

        if (transforms != null && transforms.Count > 0)
        {
            var normalized = transforms
                .Select(ToValueTransform)
                .Where(t => t != null)
                .ToList();

            numericValue = ModbusDataParser.ApplyTransforms(numericValue, normalized);
        }

        return ConvertPreviewValue(numericValue, valueType);
    }

    private static ValueTransform ToValueTransform(ValueTransformDto dto)
    {
        return new ValueTransform
        {
            Type = dto.TransformType.ToString(),
            Order = dto.Order,
            Parameters = dto.Parameters.HasValue
                ? dto.Parameters.Value.Deserialize<Dictionary<string, double>>() ?? new Dictionary<string, double>()
                : new Dictionary<string, double>()
        };
    }

    private static byte[] ResolveSampleBytes(CollectionPointDto point, string byteOrder, string rawDataType)
    {
        if (point.ProtocolOptions.HasValue)
        {
            if (TryReadHexBytes(point.ProtocolOptions.Value, "SampleHex", out var sampleHexBytes)
                || TryReadHexBytes(point.ProtocolOptions.Value, "sampleHex", out sampleHexBytes))
            {
                return sampleHexBytes;
            }

            if (TryReadRegisterBytes(point.ProtocolOptions.Value, "SampleRegisters", out var registerBytes)
                || TryReadRegisterBytes(point.ProtocolOptions.Value, "sampleRegisters", out registerBytes))
            {
                return registerBytes;
            }
        }

        return BuildDefaultSampleBytes(rawDataType, byteOrder, point.Length);
    }

    private static bool TryReadHexBytes(JsonElement protocolOptions, string propertyName, out byte[] bytes)
    {
        bytes = null;
        if (!protocolOptions.TryGetProperty(propertyName, out var hexValue) || hexValue.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var hex = hexValue.GetString();
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        bytes = ModbusRtuProtocol.FromHexString(hex);
        return bytes.Length > 0;
    }

    private static bool TryReadRegisterBytes(JsonElement protocolOptions, string propertyName, out byte[] bytes)
    {
        bytes = null;
        if (!protocolOptions.TryGetProperty(propertyName, out var registers) || registers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var result = new List<byte>();
        foreach (var item in registers.EnumerateArray())
        {
            if (!item.TryGetUInt16(out var registerValue))
            {
                continue;
            }

            result.Add((byte)(registerValue >> 8));
            result.Add((byte)(registerValue & 0xFF));
        }

        bytes = result.ToArray();
        return bytes.Length > 0;
    }

    private static byte[] BuildDefaultSampleBytes(string rawDataType, string byteOrder, int pointLength)
    {
        var normalizedType = rawDataType?.Trim().ToLowerInvariant() ?? "uint16";
        return normalizedType switch
        {
            "bool" => [0x00, 0x01],
            "int16" => [0x04, 0xD2],
            "uint16" => [0x04, 0xD2],
            "int32" => BuildOrderedBytes(BitConverter.GetBytes(123456), byteOrder),
            "uint32" => BuildOrderedBytes(BitConverter.GetBytes((uint)123456), byteOrder),
            "float32" => BuildOrderedBytes(BitConverter.GetBytes(12.34f), byteOrder),
            "string" => BuildStringBytes(pointLength),
            _ => [0x04, 0xD2]
        };
    }

    private static byte[] BuildOrderedBytes(byte[] interpretedBytes, string byteOrder)
    {
        var order = (byteOrder ?? "ABCD").ToUpperInvariant();
        return order switch
        {
            "AB" => interpretedBytes.Take(2).ToArray(),
            "BA" => [interpretedBytes[1], interpretedBytes[0]],
            "ABCD" => interpretedBytes.Take(4).ToArray(),
            "CDAB" => [interpretedBytes[2], interpretedBytes[3], interpretedBytes[0], interpretedBytes[1]],
            "DCBA" => [interpretedBytes[3], interpretedBytes[2], interpretedBytes[1], interpretedBytes[0]],
            "BADC" => [interpretedBytes[1], interpretedBytes[0], interpretedBytes[3], interpretedBytes[2]],
            _ => interpretedBytes.Take(Math.Min(interpretedBytes.Length, 4)).ToArray(),
        };
    }

    private static byte[] BuildStringBytes(int pointLength)
    {
        var registerCount = pointLength > 0 ? pointLength : 2;
        var text = "OK".PadRight(registerCount * 2, '\0');
        return System.Text.Encoding.ASCII.GetBytes(text[..(registerCount * 2)]);
    }

    private static string GetProtocolOption(JsonElement? protocolOptions, string propertyName)
    {
        if (!protocolOptions.HasValue || protocolOptions.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!protocolOptions.Value.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string GetDefaultByteOrder(string rawDataType)
    {
        var normalizedType = rawDataType?.Trim().ToLowerInvariant() ?? "uint16";
        return normalizedType switch
        {
            "int32" => "ABCD",
            "uint32" => "ABCD",
            "float32" => "ABCD",
            _ => "AB"
        };
    }

    private static object ConvertPreviewValue(object value, CollectionValueType valueType)
    {
        return valueType switch
        {
            CollectionValueType.Boolean => Convert.ToBoolean(value),
            CollectionValueType.Int32 => Convert.ToInt32(value),
            CollectionValueType.Int64 => Convert.ToInt64(value),
            CollectionValueType.Decimal => Convert.ToDecimal(value),
            CollectionValueType.Double => Convert.ToDouble(value),
            CollectionValueType.String => Convert.ToString(value),
            _ => value
        };
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = floatValue;
                return true;
            case double doubleValue:
                result = doubleValue;
                return true;
            case decimal decimalValue:
                result = (double)decimalValue;
                return true;
            case bool boolValue:
                result = boolValue ? 1d : 0d;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static PagedData<CollectionTaskDto> ToPagedData(CollectionTaskDto dto)
    {
        return new PagedData<CollectionTaskDto>
        {
            total = dto == null ? 0 : 1,
            rows = dto == null ? new List<CollectionTaskDto>() : new List<CollectionTaskDto> { dto }
        };
    }

    private static PagedData<CollectionTaskDto> ToPagedData(List<CollectionTaskDto> rows = null)
    {
        var resultRows = rows ?? new List<CollectionTaskDto>();
        return new PagedData<CollectionTaskDto>
        {
            total = resultRows.Count,
            rows = resultRows
        };
    }

    private static CollectionTaskDto CreateDraft(CollectionProtocolType protocol)
    {
        return new CollectionTaskDto
        {
            Id = Guid.NewGuid(),
            TaskKey = $"{protocol.ToString().ToLowerInvariant()}-draft",
            Protocol = protocol,
            Version = 1,
            Connection = new CollectionConnectionDto
            {
                ConnectionKey = "default-connection",
                ConnectionName = "默认连接",
                Protocol = protocol,
                Transport = "Tcp",
                Host = "127.0.0.1",
                Port = protocol == CollectionProtocolType.Modbus ? 502 : 4840,
                TimeoutMs = 3000,
                RetryCount = 3,
            },
            Devices =
            [
                new CollectionDeviceDto
                {
                    DeviceKey = "device-1",
                    DeviceName = "示例设备",
                    Enabled = true,
                    Points =
                    [
                        new CollectionPointDto
                        {
                            PointKey = "point-1",
                            PointName = "示例点位",
                            SourceType = protocol == CollectionProtocolType.Modbus ? "HoldingRegister" : "Variable",
                            Address = protocol == CollectionProtocolType.Modbus ? "40001" : "ns=2;s=Demo.Dynamic.Scalar.Double",
                            RawValueType = "Double",
                            Length = 1,
                            Polling = new PollingPolicyDto { ReadPeriodMs = 5000, Group = "default" },
                            Mapping = new PlatformMappingDto
                            {
                                TargetType = CollectionTargetType.Telemetry,
                                TargetName = "demoValue",
                                ValueType = CollectionValueType.Double,
                                DisplayName = "示例值",
                                Unit = protocol == CollectionProtocolType.Modbus ? "°C" : null,
                                Group = "default"
                            }
                        }
                    ]
                }
            ],
            ReportPolicy = new ReportPolicyDto
            {
                DefaultTrigger = ReportTriggerType.OnChange,
                IncludeQuality = true,
                IncludeTimestamp = true,
            }
        };
    }
}
