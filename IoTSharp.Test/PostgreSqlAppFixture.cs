#nullable enable

using IoTSharp.Contracts;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace IoTSharp.Test
{
    public sealed class PostgreSqlAppFixture : AppInstance
    {
        private PostgreSqlContainer? _dbContainer;
        private RabbitMqContainer? _mqContainer;

        protected override async Task InitializeAppAsync()
        {
            _dbContainer = new PostgreSqlBuilder().Build();
            _mqContainer = new RabbitMqBuilder().Build();
            await _dbContainer.StartAsync(TestCancellationToken);
            await _mqContainer.StartAsync(TestCancellationToken);
            await InitializeApplicationAsync(
                _dbContainer.GetConnectionString(),
                _dbContainer.GetConnectionString(),
                _mqContainer.GetConnectionString(),
                DataBaseType.PostgreSql);
        }

        protected override async Task DisposeTestResourcesAsync()
        {
            if (_mqContainer is not null)
            {
                await _mqContainer.DisposeAsync();
            }

            if (_dbContainer is not null)
            {
                await _dbContainer.DisposeAsync();
            }
        }
    }
}
