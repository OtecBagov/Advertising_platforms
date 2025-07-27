using System.Text.RegularExpressions;

namespace Advertising_platforms.Models;

public record Location
{
    private static readonly Regex LocationPattern = new(@"^/[a-zA-Z0-9/_-]+$", RegexOptions.Compiled);
    
    public string Path { get; }
    
    public Location(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Путь локации не может быть пустым", nameof(path));
        
        if (!LocationPattern.IsMatch(path))
            throw new ArgumentException($"Неверный формат пути локации: {path}", nameof(path));
        
        Path = path.ToLowerInvariant();
    }
    
    public bool IsParentOf(Location child)
    {
        return child.Path.StartsWith(Path + "/", StringComparison.OrdinalIgnoreCase) || 
               child.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
    }
    
    public Location? GetParent()
    {
        var lastSlashIndex = Path.LastIndexOf('/');
        return lastSlashIndex > 0 ? new Location(Path[..lastSlashIndex]) : null;
    }
    
    public IEnumerable<Location> GetAncestors()
    {
        var current = this;
        while (current.GetParent() is { } parent)
        {
            yield return parent;
            current = parent;
        }
    }
    
    public override string ToString() => Path;
    
    public static implicit operator string(Location location) => location.Path;
} 