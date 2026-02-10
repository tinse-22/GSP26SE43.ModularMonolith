using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new List<T>();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
