using System.Reflection;
using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModuleB.Infrastructure;
using Savorboard.CAP.InMemoryMessageQueue;

namespace ModuleB.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceProvider AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        var localServiceProvider = services.AddLocalServiceProvider(configuration);
        return localServiceProvider;
    }

    public static IServiceProvider AddLocalServiceProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var moduleBConnectionString = configuration.GetConnectionString("ModuleB")
                                      ?? throw new InvalidOperationException("Missing ConnectionStrings:ModuleB");


        var localCapServices = new ServiceCollection();
        localCapServices.AddLogging(logging => logging.AddConsole());
        localCapServices.AddCap(options =>
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
        var localCapProvider = localCapServices.BuildServiceProvider();

        services.AddSingleton<IModuleBCapPublisher>(
            new ModuleBCapPublisher(localCapProvider.GetRequiredService<ICapPublisher>()));

        return localCapProvider;
    }
    
}