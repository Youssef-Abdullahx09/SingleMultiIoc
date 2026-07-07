using DotNetCore.CAP;
using Gateway;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModuleA.Api;
using ModuleA.Infrastructure;
using ModuleB.Api;
using ModuleB.Infrastructure;
using Savorboard.CAP.InMemoryMessageQueue;

var builder = WebApplication.CreateBuilder(args);

// Gateway's own global CAP configuration (constitution Principle IV, amended) -
// distinct group and schema from Module A's and Module B's own private publish-only
// instances. Module A's inbound subscription (OrderPlacedIntegrationEventHandler,
// registered on this same global container) runs on this instance, since only one
// AddCap() call can live per container.
builder.Services.AddCap(options =>
{
    options.UseSqlServer(sqlServerOptions =>
    {
        sqlServerOptions.ConnectionString = builder.Configuration.GetConnectionString("Global")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Global");
        sqlServerOptions.Schema = "cap_gateway";
    });

    if (string.Equals(builder.Configuration["Cap:Transport"], "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        options.UseInMemoryMessageQueue();
    }
    else
    {
        options.UseRabbitMQ(rabbitMqOptions =>
        {
            rabbitMqOptions.HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
        });
    }

    options.DefaultGroupName = "gateway.global";
});

// Both modules are the "Single IoC" variant (constitution Principle III, amended):
// each registers directly on the Gateway's global `builder.Services`. Only each
// module's outbound CAP publisher keeps a small, private child container (Principle
// IV, amended); the returned providers exist solely so ChildContainerHost can pump
// their IHostedServices, never to resolve app services from.
var moduleACapPublisherProvider = builder.Services.AddModuleAServices(builder.Configuration);
var moduleBCapPublisherProvider = builder.Services.AddModuleBServices(builder.Configuration);

builder.Services.AddSingleton<IReadOnlyList<IServiceProvider>>([moduleACapPublisherProvider, moduleBCapPublisherProvider]);
builder.Services.AddHostedService<ChildContainerHost>();

var app = builder.Build();

using (var moduleAMigrationScope = app.Services.CreateScope())
{
    moduleAMigrationScope.ServiceProvider.GetRequiredService<ModuleADbContext>().Database.Migrate();
}

using (var moduleBMigrationScope = app.Services.CreateScope())
{
    moduleBMigrationScope.ServiceProvider.GetRequiredService<ModuleBDbContext>().Database.Migrate();
}

app.MapGet("/", () => "ModularShop Gateway");
app.MapCatalogEndpoints();
app.MapOrdersEndpoints();

app.Run();
