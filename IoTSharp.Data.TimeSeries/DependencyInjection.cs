using InfluxDB.Client;
using IoTSharp.Contracts;
using IoTSharp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using System;

namespace IoTSharp.Data.TimeSeries
{
    public static class DependencyInjection
    {
        public static void AddTelemetryStorage(this IServiceCollection services, AppSettings settings, IHealthChecksBuilder healthChecks)
        {
            string healthCheckName = $"{nameof(TelemetryStorage)}-{Enum.GetName(settings.TelemetryStorage)}";
            string connectionString = settings.ConnectionStrings["TelemetryStorage"];

            switch (settings.TelemetryStorage)
            {
                case TelemetryStorage.InfluxDB:
                    services.AddSingleton<IStorage, InfluxDBStorage>();
                    services.AddObjectPool(() => new InfluxDBClient(InfluxDBClientOptions.Builder.CreateNew().ConnectionString(connectionString).Build()));
                    healthChecks.AddInfluxDB(connectionString, name: healthCheckName);
                    break;

                case TelemetryStorage.TimescaleDB:
                    services.AddSingleton<IStorage, TimescaleDBStorage>();
                    break;

                default:
                    throw new NotSupportedException($"当前仅支持时序存储 TimescaleDB 或 InfluxDB，收到配置值: {settings.TelemetryStorage}");
            }
        }
    }
}
