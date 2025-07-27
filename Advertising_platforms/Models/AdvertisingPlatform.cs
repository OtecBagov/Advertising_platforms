namespace Advertising_platforms.Models;

public record AdvertisingPlatform
{
    public string Name { get; }
    public IReadOnlyList<Location> Locations { get; }
    
    public AdvertisingPlatform(string name, IEnumerable<Location> locations)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Название площадки не может быть пустым", nameof(name));
        
        var locationsList = locations?.ToList() ?? new List<Location>();
        if (!locationsList.Any())
            throw new ArgumentException("Должна быть указана хотя бы одна локация", nameof(locations));
        
        Name = name.Trim();
        Locations = locationsList.AsReadOnly();
    }
    
    public bool IsActiveInLocation(Location location)
    {
        return Locations.Any(loc => loc.IsParentOf(location));
    }
    
    public override string ToString() => $"{Name}: {string.Join(",", Locations)}";
} 