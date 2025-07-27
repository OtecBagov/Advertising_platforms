namespace Advertising_platforms.Models;

public record LocationSearchResponse
{
    public string Location { get; init; } = string.Empty;
    public IReadOnlyList<string> AdvertisingPlatforms { get; init; } = Array.Empty<string>();
    public int TotalCount => AdvertisingPlatforms.Count;
    public DateTime SearchTimestamp { get; init; } = DateTime.UtcNow;
} 