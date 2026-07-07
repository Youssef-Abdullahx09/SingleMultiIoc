using System.Reflection;
using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModuleA.Application.Utilities;
using ModuleA.Infrastructure;
using Savorboard.CAP.InMemoryMessageQueue;

namespace ModuleA.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceProvider AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddInfrastructure(configuration);
        
        var localServiceProvider = services.AddLocalServiceProviderServices(configuration);
        return localServiceProvider;
    }

    private static IServiceProvider AddLocalServiceProviderServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var moduleAConnectionString = configuration.GetConnectionString("ModuleA")
                                      ?? throw new InvalidOperationException("Missing ConnectionStrings:ModuleA");
        
        var localCapServices = new ServiceCollection();
        localCapServices.AddLogging(logging => logging.AddConsole());
        localCapServices.AddCap(options =>
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
        var localCapProvider = localCapServices.BuildServiceProvider();

        services.AddSingleton<IModuleACapPublisher>(
            new ModuleACapPublisher(localCapProvider.GetRequiredService<ICapPublisher>()));
        return localCapProvider;
    }
}