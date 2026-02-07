using System.Collections.Generic;

namespace ClassifiedAds.Application.Common.DTOs;

public class Paged<T>
{
    public long TotalItems { get; set; }

    public List<T> Items { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling((double)TotalItems / PageSize) : 0;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
