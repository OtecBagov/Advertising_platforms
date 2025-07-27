using System.ComponentModel.DataAnnotations;

namespace Advertising_platforms.Models;

public record LocationSearchRequest
{
    [Required(ErrorMessage = "Локация обязательна для поиска")]
    [RegularExpression(@"^/[a-zA-Z0-9/_-]+$", ErrorMessage = "Неверный формат локации")]
    public string Location { get; init; } = string.Empty;
    
    public Location ToLocation() => new(Location);
} 