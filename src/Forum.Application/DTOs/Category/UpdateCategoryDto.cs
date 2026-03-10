namespace Forum.Application.DTOs.Category;

/// <summary>
/// DTO for updating an existing category.
/// Only admins can update categories and only the Name field can be modified.
/// </summary>
public record UpdateCategoryDto(string Name);