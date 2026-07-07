using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ModuleB.Infrastructure;

// Module B's private, isolated CAP instance is used only for publishing (constitution
// Principle IV, amended) - its schema/group (cap_moduleb / moduleb.orders) stay independent
// from the Gateway's global CAP instance. Module B has no CAP subscriber, so unlike Module A
// there is no split subscribe-side concern here - this is its only CAP instance. Application
// code depends on this interface, never on ICapPublisher directly, so the outbound instance
// stays swappable without touching call sites.
public interface IModuleBCapPublisher
{
    Task PublishAsync<T>(string name, T contentObj, CancellationToken cancellationToken = default);

    // Shares one ADO.NET transaction between ModuleBDbContext's SqlServer connection and CAP's
    // own SqlServer outbox storage (cap_moduleb.Published), via DotNetCore.CAP.SqlServer's
    // DatabaseFacade extension - never via UseEntityFramework, so Principle IV still holds.
    Task<IDbContextTransaction> BeginTransactionAsync(
        DatabaseFacade database,
        bool autoCommit = false,
        CancellationToken cancellationToken = default);
}

public sealed class ModuleBCapPublisher(ICapPublisher localCapPublisher) : IModuleBCapPublisher
{
    public Task PublishAsync<T>(string name, T contentObj, CancellationToken cancellationToken = default) =>
        localCapPublisher.PublishAsync(name, contentObj, cancellationToken: cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(
        DatabaseFacade database,
        bool autoCommit = false,
        CancellationToken cancellationToken = default) =>
        database.BeginTransactionAsync(localCapPublisher, autoCommit, cancellationToken);
}
