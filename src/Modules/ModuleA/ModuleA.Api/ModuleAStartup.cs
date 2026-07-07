using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModuleA.Application;
using ModuleA.Infrastructure;
using ModuleB.Integration.Query;
using Savorboard.CAP.InMemoryMessageQueue;

namespace ModuleA.Api;

// Builds Module A's own isolated child container (constitution Principle III).
// `orderIntegrationQuery` is the single resolved instance the Gateway obtained from
// Module B's already-built container (research.md §1) - Module A never references
// any ModuleB project except ModuleB.Integration.Query (the interface it implements).
public static class ModuleAStartup
{
    public static ServiceProvider BuildServiceProvider(IConfiguration configuration, IOrderIntegrationQuery orderIntegrationQuery)
    {
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddConsole());

        var moduleAConnectionString = configuration.GetConnectionString("ModuleA")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ModuleA");

        services.AddDbContext<ModuleADbContext>(options =>
            options.UseSqlServer(moduleAConnectionString));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly));

        services.AddSingleton(orderIntegrationQuery);

        // CAP discovers [CapSubscribe] methods on registered ICapSubscribe implementations.
        services.AddTransient<OrderPlacedIntegrationEventHandler>();

        services.AddCap(options =>
        {
            options.UseSqlServer(sqlServerOptions =>
            {
                sqlServerOptions.ConnectionString = moduleAConnectionString;
                sqlServerOptions.Schema = "cap_modulea";
            });

            if (string.Equals(configuration["Cap:Transport"], "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseInMemoryMessageQueue();
            }
            else
            {
                options.UseRabbitMQ(rabbitMqOptions =>
                {
                    rabbitMqOptions.HostName = configuration["RabbitMQ:HostName"] ?? "localhost";
                });
            }

            options.DefaultGroupName = "modulea.catalog";
        });

        return services.BuildServiceProvider();
    }
}
