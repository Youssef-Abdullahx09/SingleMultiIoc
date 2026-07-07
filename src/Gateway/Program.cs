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

// Gateway's own, separate global CAP configuration (constitution Principle IV) -
// distinct group and schema from every module's CAP instance. Not used by any
// module; demonstrates the Gateway has its own independent messaging identity.
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

// Build order matters: Module B first, so its IOrderIntegrationQuery instance
// exists to hand into Module A's container (research.md §1). Module A's project
// files never reference ModuleB.Application - only ModuleB.Integration.Query.
var moduleBProvider = ModuleBStartup.BuildServiceProvider(builder.Configuration);
var orderIntegrationQuery = moduleBProvider.GetRequiredService<ModuleB.Integration.Query.IOrderIntegrationQuery>();
var moduleAProvider = ModuleAStartup.BuildServiceProvider(builder.Configuration, orderIntegrationQuery);

builder.Services.AddSingleton<IReadOnlyList<IServiceProvider>>([moduleAProvider, moduleBProvider]);
builder.Services.AddHostedService<ChildContainerHost>();

var app = builder.Build();

using (var moduleAMigrationScope = moduleAProvider.CreateScope())
{
    moduleAMigrationScope.ServiceProvider.GetRequiredService<ModuleADbContext>().Database.Migrate();
}

using (var moduleBMigrationScope = moduleBProvider.CreateScope())
{
    moduleBMigrationScope.ServiceProvider.GetRequiredService<ModuleBDbContext>().Database.Migrate();
}

app.MapGet("/", () => "ModularShop Gateway");
app.MapCatalogEndpoints(moduleAProvider);
app.MapOrdersEndpoints(moduleBProvider);

app.Run();
