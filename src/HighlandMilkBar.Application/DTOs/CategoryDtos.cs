namespace HighlandMilkBar.Application.DTOs;

/// <summary>Row model for the category listing.</summary>
public sealed class CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public int ProductCount { get; init; }
    public int SortOrder { get; init; }

    public string StatusText => IsActive ? "Active" : "Inactive";
}

/// <summary>Request to create a category.</summary>
public sealed record CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

/// <summary>Request to update a category.</summary>
public sealed record UpdateCategoryRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
