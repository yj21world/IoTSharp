using Microsoft.Extensions.DependencyInjection;
using IoTSharp.Services.ModbusCollection;

namespace IoTSharp.Extensions;

/// <summary>
/// Modbus 采集服务扩展
/// </summary>
public static class ModbusCollectionExtension
{
    /// <summary>
    /// 注册 Modbus 采集服务
    /// </summary>
    public static IServiceCollection AddModbusCollection(this IServiceCollection services)
    {
        services.AddSingleton<CollectionConfigurationLoader>();
        services.AddSingleton<GatewaySchedulerManager>();
        services.AddHostedService<ModbusCollectionService>();
        return services;
    }
}
