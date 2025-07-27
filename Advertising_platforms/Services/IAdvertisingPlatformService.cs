using Advertising_platforms.Models;

namespace Advertising_platforms.Services;

public interface IAdvertisingPlatformService
{
    Task LoadFromFileAsync(IFormFile file, CancellationToken cancellationToken = default);
    IReadOnlyList<string> SearchPlatformsByLocation(string location);
    IReadOnlyList<string> SearchPlatformsByLocation(Location location);
    int GetTotalPlatformsCount();
    int GetTotalLocationsCount();
    void ClearAllData();
} 