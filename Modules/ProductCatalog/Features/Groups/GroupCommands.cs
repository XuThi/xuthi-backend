namespace ProductCatalog.Features.Groups;

// ========== CREATE ==========
public record CreateGroupCommand(CreateGroupRequest Request) : ICommand<GroupResult>;

public record CreateGroupRequest(
    string Name,
    List<Guid>? ProductIds = null
);

// ========== UPDATE ==========
public record UpdateGroupCommand(Guid Id, UpdateGroupRequest Request) : ICommand<GroupResult>;

public record UpdateGroupRequest(
    string? Name = null
);

// ========== DELETE ==========
public record DeleteGroupCommand(Guid Id) : ICommand<bool>;

// ========== ADD/REMOVE PRODUCTS ==========
public record AddProductsToGroupCommand(Guid GroupId, List<Guid> ProductIds) : ICommand<GroupDetailResult>;
public record RemoveProductsFromGroupCommand(Guid GroupId, List<Guid> ProductIds) : ICommand<GroupDetailResult>;

// ========== QUERIES ==========
public record GetGroupQuery(Guid Id) : IQuery<GroupDetailResult>;
public record GetGroupsQuery(int Page = 1, int PageSize = 20) : IQuery<GroupsResult>;
public record GetGroupByNameQuery(string Name) : IQuery<GroupDetailResult>;

// ========== RESULTS ==========
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
