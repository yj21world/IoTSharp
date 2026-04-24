using System;
using IoTSharp.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace IoTSharp.Data.PostgreSQL;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=localhost;Database=IoTSharp;Username=postgres;Password=BrrveCFVAZ6kM6vhjFMd;Pooling=true;MaxPoolSize=1024;";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDataBaseModelBuilderOptions, NpgsqlModelBuilderOptions>();
        services.AddNpgsql<ApplicationDbContext>(
            DesignTimeConnectionString,
            builder => builder
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                .MigrationsAssembly("IoTSharp.Data.PostgreSQL"),
            options => options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
