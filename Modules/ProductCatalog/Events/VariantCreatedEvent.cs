using ProductCatalog.Infrastructure.Dtos;

namespace ProductCatalog.Events;

internal class VariantCreatedEvent
{
    public Guid VariantId { get; set; }
    public VariantInfo Variant { get; set; } = default!;
}