using System.ComponentModel.DataAnnotations;

namespace DevHabit.Api.DTOs.Tags;

public sealed record CreateTagDto
{
    [Required]
    [MinLength(3)]
    public required string Name { get; set; }
    [MaxLength(50)]
    public string? Description { get; set; }
}
