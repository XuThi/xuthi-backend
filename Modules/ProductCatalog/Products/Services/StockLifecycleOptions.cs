namespace ProductCatalog.Products.Services;

public sealed class StockLifecycleOptions
{
    public TimeSpan OrphanHoldReleaseBuffer { get; set; } = TimeSpan.FromMinutes(1);
}
