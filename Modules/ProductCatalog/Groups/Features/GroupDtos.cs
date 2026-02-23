namespace ProductCatalog.Groups.Features;

public record GroupResult(
    Guid Id,
    string Name,
    int ProductCount
);

public record GroupDetailResult(
    Guid Id,
    string Name,
    List<GroupProductInfo> Products
);

public record GroupProductInfo(
    Guid ProductId,
    string ProductName,
    string? UrlSlug
);

public record GroupsResult(
    List<GroupResult> Items,
    int TotalCount,
    int Page,
    int PageSize
);
