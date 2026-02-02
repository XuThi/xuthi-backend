namespace ProductCatalog.Features.DeleteProduct;

public record DeleteProductCommand(Guid Id) : ICommand<bool>;
