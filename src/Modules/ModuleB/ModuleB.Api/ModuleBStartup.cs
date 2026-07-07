using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModuleB.Application;

namespace ModuleB.Api;

// Module B is the "Single IoC" variant (constitution Principle III, amended): every
// service registers directly on the Gateway's global `IServiceCollection` - except its
// outbound CAP publisher, which keeps a small, private child container so Module B's
// publishing schema/group (Principle IV, amended) stay isolated. Module B has no CAP
// subscriber, so unlike Module A there is no separate global-subscribe concern here.
public static class ModuleBStartup
{
    // Returns Module B's private publish-only CAP child provider purely so the Gateway can
    // pump its IHostedServices via ChildContainerHost - it is never used to resolve app services.
    public static IServiceProvider AddModuleBServices(this IServiceCollection services, IConfiguration configuration)
    {
        var localServiceProvider = services.AddApplication(configuration);
        return localServiceProvider;
    }
}
