namespace PushpakClasses.Application.Common.Models;

public class QuizQueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int Page
    {
        get => _pageNumber;
        set => PageNumber = value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 10 : value);
    }

    public string? SearchQuery { get; set; }

    public string? Search
    {
        get => SearchQuery;
        set => SearchQuery = value;
    }

    public Guid? CategoryId { get; set; }
    public bool? IsPublished { get; set; }
    public string? Status { get; set; }

    public string SortBy { get; set; } = "CreatedAtUtc";
    public string SortDirection { get; set; } = "desc"; // "asc" or "desc"

    public string SortDir
    {
        get => SortDirection;
        set => SortDirection = value;
    }
}
