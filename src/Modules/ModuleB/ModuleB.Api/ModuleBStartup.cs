using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModuleB.Application;
using ModuleB.Infrastructure;
using ModuleB.Integration.Query;
using Savorboard.CAP.InMemoryMessageQueue;

namespace ModuleB.Api;

// Builds Module B's own isolated child container (constitution Principle III).
// The Gateway's root container never registers any of these services directly.
public static class ModuleBStartup
{
    public static ServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddConsole());

        var moduleBConnectionString = configuration.GetConnectionString("ModuleB")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ModuleB");

        services.AddDbContext<ModuleBDbContext>(options =>
            options.UseSqlServer(moduleBConnectionString));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(OrderIntegrationQuery).Assembly));

        // Singleton: safe because it creates its own DbContext scope per call (see OrderIntegrationQuery).
        // This is the instance the Gateway resolves and hands into Module A's container (research.md §1).
        services.AddSingleton<IOrderIntegrationQuery, OrderIntegrationQuery>();

        services.AddCap(options =>
        {
            options.UseSqlServer(sqlServerOptions =>
            {
                sqlServerOptions.ConnectionString = moduleBConnectionString;
                sqlServerOptions.Schema = "cap_moduleb";
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

            options.DefaultGroupName = "moduleb.orders";
        });

        return services.BuildServiceProvider();
    }
}
