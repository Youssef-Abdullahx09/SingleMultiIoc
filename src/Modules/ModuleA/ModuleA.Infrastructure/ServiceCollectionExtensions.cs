using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModuleA.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static void AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var moduleAConnectionString = configuration.GetConnectionString("ModuleA")
                                      ?? throw new InvalidOperationException("Missing ConnectionStrings:ModuleA");

        services.AddDbContext<ModuleADbContext>(options =>
            options.UseSqlServer(moduleAConnectionString));
    }
}