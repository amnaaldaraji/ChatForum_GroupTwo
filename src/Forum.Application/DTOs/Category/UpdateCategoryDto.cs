namespace Forum.Application.DTOs.Category;

/// <summary>
/// DTO for updating an existing category. Currently, only the Name can be changed.
/// Only admins can update categories.
/// </summary>
/// <param name="Name">The new display name for the category.</param>
public record UpdateCategoryDto(string Name);