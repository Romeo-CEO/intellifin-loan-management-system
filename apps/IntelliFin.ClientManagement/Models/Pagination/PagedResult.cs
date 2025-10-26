namespace IntelliFin.ClientManagement.Models.Pagination;

/// <summary>
/// Paginated result wrapper for list responses
/// Optimized for mobile/tablet clients
/// </summary>
/// <typeparam name="T">Type of items in the result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// List of items for current page
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPrevious { get; set; }

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNext { get; set; }

    /// <summary>
    /// Creates a paged result
    /// </summary>
    public static PagedResult<T> Create(
        List<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPrevious = pageNumber > 1,
            HasNext = pageNumber < totalPages
        };
    }
}

/// <summary>
/// Pagination request parameters
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Number of items per page (max 100)
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Min(value, MaxPageSize);
    }
}
