using IoTSharp.Contracts;
using IoTSharp.Models;
using IoTSharp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IoTSharp.Controllers;

[Route("api/[controller]/[action]")]
[Authorize]
[ApiController]
public class DeviceTypeProfileController : ControllerBase
{
    private readonly DeviceTypeProfileService _service;

    public DeviceTypeProfileController(DeviceTypeProfileService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取所有设备类型模板
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<DeviceTypeProfileDto>>>> GetAll()
    {
        var profiles = await _service.GetAllAsync();
        var paged = new PagedData<DeviceTypeProfileDto>
        {
            total = profiles.Count,
            rows = profiles
        };
        return Ok(new ApiResult<PagedData<DeviceTypeProfileDto>>(ApiCode.Success, "OK", paged));
    }

    /// <summary>
    /// 获取设备类型模板详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<DeviceTypeProfileDto>>> Get(Guid id)
    {
        var profile = await _service.GetByIdAsync(id);
        if (profile == null)
        {
            return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.NotFoundDevice, "DeviceTypeProfile not found", null));
        }
        return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.Success, "OK", profile));
    }

    /// <summary>
    /// 创建设备类型模板
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<DeviceTypeProfileDto>>> Create([FromBody] CreateDeviceTypeProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProfileKey))
        {
            return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.InValidData, "ProfileKey is required", null));
        }

        try
        {
            var profile = await _service.CreateAsync(dto);
            return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.Success, "OK", profile));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.AlreadyExists, ex.Message, null));
        }
    }

    /// <summary>
    /// 更新设备类型模板
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<DeviceTypeProfileDto>>> Update(Guid id, [FromBody] UpdateDeviceTypeProfileDto dto)
    {
        try
        {
            var profile = await _service.UpdateAsync(id, dto);
            return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.Success, "OK", profile));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<DeviceTypeProfileDto>(ApiCode.NotFoundDevice, ex.Message, null));
        }
    }

    /// <summary>
    /// 删除设备类型模板
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<bool>>> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return Ok(new ApiResult<bool>(ApiCode.Success, "OK", true));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<bool>(ApiCode.NotFoundDevice, ex.Message, false));
        }
    }

    /// <summary>
    /// 获取模板的采集规则
    /// </summary>
    [HttpGet("{profileId}/rules")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionRuleTemplateDto>>>> GetRules(Guid profileId)
    {
        var rules = await _service.GetRulesAsync(profileId);
        var paged = new PagedData<CollectionRuleTemplateDto>
        {
            total = rules.Count,
            rows = rules
        };
        return Ok(new ApiResult<PagedData<CollectionRuleTemplateDto>>(ApiCode.Success, "OK", paged));
    }

    /// <summary>
    /// 添加采集规则模板
    /// </summary>
    [HttpPost("{profileId}/rules")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionRuleTemplateDto>>>> AddRule(Guid profileId, [FromBody] CreateCollectionRuleTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PointKey))
        {
            return Ok(new ApiResult<PagedData<CollectionRuleTemplateDto>>(ApiCode.InValidData, "PointKey is required", new PagedData<CollectionRuleTemplateDto>
            {
                total = 0,
                rows = new List<CollectionRuleTemplateDto>()
            }));
        }

        try
        {
            var rule = await _service.AddRuleAsync(profileId, dto);
            return Ok(new ApiResult<PagedData<CollectionRuleTemplateDto>>(ApiCode.Success, "OK", new PagedData<CollectionRuleTemplateDto>
            {
                total = 1,
                rows = new List<CollectionRuleTemplateDto> { rule }
            }));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<PagedData<CollectionRuleTemplateDto>>(ApiCode.NotFoundDevice, ex.Message, new PagedData<CollectionRuleTemplateDto>
            {
                total = 0,
                rows = new List<CollectionRuleTemplateDto>()
            }));
        }
    }

    /// <summary>
    /// 更新采集规则模板
    /// </summary>
    [HttpPut("{profileId}/rules/{ruleId}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<PagedData<CollectionRuleTemplateDto>>>> UpdateRule(Guid profileId, Guid ruleId, [FromBody] UpdateCollectionRuleTemplateDto dto)
    {
        try
        {
            var rule = await _service.UpdateRuleAsync(ruleId, dto);
            return Ok(new ApiResult<PagedData<CollectionRuleTemplateDto>>(ApiCode.Success, "OK", new PagedData<CollectionRuleTemplateDto>
            {
                total = 1,
                rows = new List<CollectionRuleTemplateDto> { rule }
            }));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<PagedData<CollectionRuleTemplateDto>>(ApiCode.NotFoundDevice, ex.Message, new PagedData<CollectionRuleTemplateDto>
            {
                total = 0,
                rows = new List<CollectionRuleTemplateDto>()
            }));
        }
    }

    /// <summary>
    /// 删除采集规则模板
    /// </summary>
    [HttpDelete("{profileId}/rules/{ruleId}")]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<bool>>> DeleteRule(Guid profileId, Guid ruleId)
    {
        try
        {
            await _service.DeleteRuleAsync(ruleId);
            return Ok(new ApiResult<bool>(ApiCode.Success, "OK", true));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<bool>(ApiCode.NotFoundDevice, ex.Message, false));
        }
    }

    /// <summary>
    /// 应用设备类型模板到设备
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.NormalUser))]
    public async Task<ActionResult<ApiResult<bool>>> ApplyProfile([FromBody] ApplyDeviceTypeProfileDto dto)
    {
        try
        {
            await _service.ApplyProfileToDeviceAsync(dto.DeviceId, dto.ProfileId);
            return Ok(new ApiResult<bool>(ApiCode.Success, "OK", true));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResult<bool>(ApiCode.NotFoundDevice, ex.Message, false));
        }
    }
}
