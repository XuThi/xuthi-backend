using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;

namespace ProductCatalog.Features.Groups;

// ========== CREATE HANDLER ==========
internal class CreateGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateGroupCommand, GroupResult>
{
    public async Task<GroupResult> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        
        // Check if name already exists
        if (await dbContext.Groups.AnyAsync(g => g.Name == req.Name, cancellationToken))
            throw new InvalidOperationException($"Group '{req.Name}' already exists");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = req.Name
        };

        dbContext.Groups.Add(group);

        // Add products if provided
        if (req.ProductIds?.Count > 0)
        {
            foreach (var productId in req.ProductIds)
            {
                dbContext.GroupProducts.Add(new GroupProduct
                {
                    GroupId = group.Id,
                    ProductId = productId
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GroupResult(group.Id, group.Name, req.ProductIds?.Count ?? 0);
    }
}

// ========== UPDATE HANDLER ==========
internal class UpdateGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateGroupCommand, GroupResult>
{
    public async Task<GroupResult> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);
            
        if (group is null)
            throw new KeyNotFoundException("Group not found");

        if (command.Request.Name != null)
        {
            // Check if new name conflicts
            if (await dbContext.Groups.AnyAsync(g => g.Name == command.Request.Name && g.Id != command.Id, cancellationToken))
                throw new InvalidOperationException($"Group '{command.Request.Name}' already exists");
                
            group.Name = command.Request.Name;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GroupResult(group.Id, group.Name, group.Products.Count);
    }
}

// ========== DELETE HANDLER ==========
internal class DeleteGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteGroupCommand, bool>
{
    public async Task<bool> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FindAsync([command.Id], cancellationToken);
        if (group is null)
            return false;

        // Remove all group-product associations
        var groupProducts = await dbContext.GroupProducts
            .Where(gp => gp.GroupId == command.Id)
            .ToListAsync(cancellationToken);
        dbContext.GroupProducts.RemoveRange(groupProducts);
        
        dbContext.Groups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}

// ========== GET GROUP HANDLER ==========
internal class GetGroupHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetGroupQuery, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(GetGroupQuery query, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == query.Id, cancellationToken);
            
        if (group is null)
            throw new KeyNotFoundException("Group not found");

        return new GroupDetailResult(
            group.Id,
            group.Name,
            group.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}

// ========== GET BY NAME HANDLER ==========
internal class GetGroupByNameHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetGroupByNameQuery, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(GetGroupByNameQuery query, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Name == query.Name, cancellationToken);
            
        if (group is null)
            throw new KeyNotFoundException("Group not found");

        return new GroupDetailResult(
            group.Id,
            group.Name,
            group.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}

// ========== GET GROUPS LIST HANDLER ==========
internal class GetGroupsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetGroupsQuery, GroupsResult>
{
    public async Task<GroupsResult> Handle(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.Groups.CountAsync(cancellationToken);
        
        var groups = await dbContext.Groups
            .Include(g => g.Products)
            .OrderBy(g => g.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new GroupsResult(
            groups.Select(g => new GroupResult(g.Id, g.Name, g.Products.Count)).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );
    }
}

// ========== ADD PRODUCTS HANDLER ==========
internal class AddProductsToGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<AddProductsToGroupCommand, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(AddProductsToGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);
            
        if (group is null)
            throw new KeyNotFoundException("Group not found");

        // Get existing product IDs in group
        var existingProductIds = await dbContext.GroupProducts
            .Where(gp => gp.GroupId == command.GroupId)
            .Select(gp => gp.ProductId)
            .ToListAsync(cancellationToken);

        // Add only new products
        foreach (var productId in command.ProductIds.Except(existingProductIds))
        {
            dbContext.GroupProducts.Add(new GroupProduct
            {
                GroupId = command.GroupId,
                ProductId = productId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload with products
        group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstAsync(g => g.Id == command.GroupId, cancellationToken);

        return new GroupDetailResult(
            group.Id,
            group.Name,
            group.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}

// ========== REMOVE PRODUCTS HANDLER ==========
internal class RemoveProductsFromGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<RemoveProductsFromGroupCommand, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(RemoveProductsFromGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FindAsync([command.GroupId], cancellationToken);
        if (group is null)
            throw new KeyNotFoundException("Group not found");

        var toRemove = await dbContext.GroupProducts
            .Where(gp => gp.GroupId == command.GroupId && command.ProductIds.Contains(gp.ProductId))
            .ToListAsync(cancellationToken);

        dbContext.GroupProducts.RemoveRange(toRemove);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload with products
        var updated = await dbContext.Groups
            .Include(g => g.Products)
            .FirstAsync(g => g.Id == command.GroupId, cancellationToken);

        return new GroupDetailResult(
            updated.Id,
            updated.Name,
            updated.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}
