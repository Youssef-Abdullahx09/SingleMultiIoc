using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModuleA.Application;
using ModuleA.Application.Subscribers.OrderPlacedIntegrationEvent;

namespace ModuleA.Api;

// Module A is the "Single IoC" variant (constitution Principle III, amended): every
// service registers directly on the Gateway's global `IServiceCollection` - except its
// outbound CAP publisher. That stays on a small, private child container so Module A's
// publishing schema/group (Principle IV, amended) remain isolated; inbound subscription
// (OrderPlacedIntegrationEventHandler below) is registered on the global container and runs
// on the Gateway's own global CAP instance instead, since two `AddCap()` calls cannot share
// one container. Module B (also Single IoC) registers its own `IOrderIntegrationQuery` on
// this same global container, so `CheckAvailabilityCommandHandler` resolves it directly -
// no instance needs to be handed in here.
public static class ModuleAStartup
{
    // Returns Module A's private publish-only CAP child provider purely so the Gateway can
    // pump its IHostedServices via ChildContainerHost - it is never used to resolve app services.
    public static IServiceProvider AddModuleAServices(this IServiceCollection services, IConfiguration configuration)
    {
        var localServiceProvider = services.AddApplication(configuration);


        // CAP discovers [CapSubscribe] methods on registered ICapSubscribe implementations -
        // this is discovered by the Gateway's own global AddCap call (Program.cs), not by
        // Module A's private publish-only instance below.
        services.AddTransient<ICapSubscribe, Subscriber>();
        
        return localServiceProvider;
    }
}
