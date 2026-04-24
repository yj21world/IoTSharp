using DotNetCore.CAP;
using IoTSharp.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;

namespace IoTSharp.EventBus.CAP
{
    public static class DependencyInjection
    {
        public static IApplicationBuilder UseCAPEventBus(this IApplicationBuilder app)
        {
            return app;
        }

        public static void UserCAP(this EventBusOption opt)
        {
            var settings = opt.AppSettings;
            var healthChecks = opt.HealthChecks;
            string eventBusStore = opt.EventBusStore;
            string eventBusMq = opt.EventBusMQ;
            var services = opt.services;

            services.AddTransient<ISubscriber, CapSubscriber>();
            services.AddTransient<IPublisher, CapPublisher>();
            services.AddCap(options =>
            {
                string storeHealthCheck = $"{nameof(EventBusStore)}-{Enum.GetName(settings.EventBusStore)}";
                string mqHealthCheck = $"{nameof(EventBusMQ)}-{Enum.GetName(settings.EventBusMQ)}";

                options.SucceedMessageExpiredAfter = settings.SucceedMessageExpiredAfter;
                options.ConsumerThreadCount = settings.ConsumerThreadCount;

                options.UsePostgreSql(eventBusStore);
                healthChecks.AddNpgSql(eventBusStore, name: storeHealthCheck);

                options.UseRabbitMQ(config =>
                {
                    config.ConnectionFactoryOptions = factory =>
                    {
                        factory.AutomaticRecoveryEnabled = true;
                        factory.Uri = new Uri(eventBusMq);
                    };
                });

                var connectionFactory = new ConnectionFactory
                {
                    Uri = new Uri(eventBusMq)
                };
                healthChecks.AddRabbitMQ(async _ => await connectionFactory.CreateConnectionAsync(), name: mqHealthCheck);

                options.UseDashboard();
            });
        }
    }
}
