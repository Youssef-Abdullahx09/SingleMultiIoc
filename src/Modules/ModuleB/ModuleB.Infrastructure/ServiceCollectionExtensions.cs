using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModuleB.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static void AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var moduleBConnectionString = configuration.GetConnectionString("ModuleB")
                                      ?? throw new InvalidOperationException("Missing ConnectionStrings:ModuleB");

        services.AddDbContext<ModuleBDbContext>(options =>
            options.UseSqlServer(moduleBConnectionString));
    }
}