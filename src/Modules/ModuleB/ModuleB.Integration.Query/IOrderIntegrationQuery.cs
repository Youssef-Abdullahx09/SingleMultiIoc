namespace ModuleB.Integration.Query;

public interface IOrderIntegrationQuery
{
    bool HasOrdersForProduct(Guid productId);
}
