namespace ModuleA.Application.Utilities;

public interface IModuleACapPublisher
{
    Task PublishAsync<T>(string name, T contentObj, CancellationToken cancellationToken = default);
}