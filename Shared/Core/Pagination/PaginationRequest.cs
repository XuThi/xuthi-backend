using System.ComponentModel;

namespace Core.Pagination;

public class PaginatedResult(int pageSize = 20, int pageIndex = 0)
{
    [property: DefaultValue(20)]
    public int PageSize { get; set; } = pageSize;


    [property: DefaultValue(0)]
    public int PageIndex { get; set; } = pageIndex;
}