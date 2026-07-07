using Microsoft.Extensions.DependencyInjection;

namespace Gateway;

// Registered in the Gateway's global container (constitution Principle III).
// Starts/stops each module child container's own IHostedServices (e.g. each
// module's CAP processor) - the global container never hosts module services itself.
public sealed class ChildContainerHost(IReadOnlyList<IServiceProvider> moduleProviders) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in moduleProviders)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(cancellationToken);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in moduleProviders.Reverse())
        {
            foreach (var hostedService in provider.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(cancellationToken);
            }
        }
    }
}
