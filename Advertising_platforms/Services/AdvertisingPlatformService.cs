using Advertising_platforms.Models;
using Advertising_platforms.Exceptions;
using Microsoft.Extensions.Logging;

namespace Advertising_platforms.Services;

public class AdvertisingPlatformService : IAdvertisingPlatformService
{
    private readonly ILogger<AdvertisingPlatformService> _logger;
    private readonly ReaderWriterLockSlim _lock = new();
    
    // Потокобезопасные коллекции для хранения данных
    private readonly Dictionary<Location, HashSet<string>> _platformsByLocation = new();
    private readonly List<AdvertisingPlatform> _platforms = new();
    
    // Кэш для быстрого поиска
    private readonly Dictionary<Location, HashSet<string>> _searchCache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private readonly Dictionary<Location, DateTime> _cacheTimestamps = new();

    public AdvertisingPlatformService(ILogger<AdvertisingPlatformService> logger)
    {
        _logger = logger;
    }

    public async Task LoadFromFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Начинаем загрузку файла: {FileName}", file?.FileName);
        
        ValidateFile(file);

        try
        {
            var platforms = new List<AdvertisingPlatform>();
            var platformsByLocation = new Dictionary<Location, HashSet<string>>();
            
            using var reader = new StreamReader(file!.OpenReadStream());
            string? line;
            int lineNumber = 0;
            int validLines = 0;
            int invalidLines = 0;
            
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                lineNumber++;
                
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var platform = ParsePlatformLine(line, lineNumber);
                    platforms.Add(platform);
                    
                    // Индексируем локации
                    foreach (var location in platform.Locations)
                    {
                        if (!platformsByLocation.ContainsKey(location))
                            platformsByLocation[location] = new HashSet<string>();
                        
                        platformsByLocation[location].Add(platform.Name);
                    }
                    
                    validLines++;
                }
                catch (AdvertisingInvalidDataException ex)
                {
                    _logger.LogWarning("Некорректная строка {LineNumber}: {Message}", lineNumber, ex.Message);
                    invalidLines++;
                }
            }

            if (platforms.Count == 0)
            {
                throw new AdvertisingFileLoadException("Файл не содержит корректных данных о рекламных площадках");
            }

            // Атомарно обновляем данные
            _lock.EnterWriteLock();
            try
            {
                _platforms.Clear();
                _platforms.AddRange(platforms);
                
                _platformsByLocation.Clear();
                foreach (var kvp in platformsByLocation)
                {
                    _platformsByLocation[kvp.Key] = kvp.Value;
                }
                
                // Очищаем кэш при обновлении данных
                _searchCache.Clear();
                _cacheTimestamps.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            _logger.LogInformation("Загрузка завершена. Валидных строк: {ValidLines}, некорректных: {InvalidLines}", 
                validLines, invalidLines);
        }
        catch (AdvertisingFileLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при чтении файла");
            throw new AdvertisingFileLoadException("Ошибка при чтении файла", ex);
        }
    }

    public IReadOnlyList<string> SearchPlatformsByLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return Array.Empty<string>();

        try
        {
            var loc = new Location(location);
            return SearchPlatformsByLocation(loc);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Некорректный формат локации: {Location}. Ошибка: {Error}", location, ex.Message);
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> SearchPlatformsByLocation(Location location)
    {
        _logger.LogDebug("Поиск площадок для локации: {Location}", location);
        
        // Проверяем кэш
        if (TryGetFromCache(location, out var cachedResult))
        {
            _logger.LogDebug("Результат найден в кэше для локации: {Location}", location);
            return cachedResult;
        }

        _lock.EnterReadLock();
        try
        {
            var result = new HashSet<string>();
            
            // Ищем точные совпадения
            if (_platformsByLocation.TryGetValue(location, out var exactMatches))
            {
                result.UnionWith(exactMatches);
            }
            
            // Ищем родительские локации
            foreach (var ancestor in location.GetAncestors())
            {
                if (_platformsByLocation.TryGetValue(ancestor, out var ancestorMatches))
                {
                    result.UnionWith(ancestorMatches);
                }
            }
            
            var resultList = result.OrderBy(x => x).ToList().AsReadOnly();
            
            // Сохраняем в кэш
            AddToCache(location, resultList);
            
            _logger.LogDebug("Найдено {Count} площадок для локации: {Location}", resultList.Count, location);
            return resultList;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public int GetTotalPlatformsCount()
    {
        _lock.EnterReadLock();
        try
        {
            return _platforms.Count;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public int GetTotalLocationsCount()
    {
        _lock.EnterReadLock();
        try
        {
            return _platformsByLocation.Count;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void ClearAllData()
    {
        _logger.LogInformation("Очистка всех данных");
        
        _lock.EnterWriteLock();
        try
        {
            _platforms.Clear();
            _platformsByLocation.Clear();
            _searchCache.Clear();
            _cacheTimestamps.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void ValidateFile(IFormFile? file)
    {
        if (file == null)
            throw new AdvertisingValidationException("Файл не предоставлен");

        if (file.Length == 0)
            throw new AdvertisingValidationException("Файл пустой");

        if (file.Length > 10 * 1024 * 1024) // 10MB лимит
            throw new AdvertisingValidationException("Файл слишком большой. Максимальный размер: 10MB");

        if (!file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            throw new AdvertisingValidationException("Поддерживаются только текстовые файлы (.txt)");
    }

    private AdvertisingPlatform ParsePlatformLine(string line, int lineNumber)
    {
        var parts = line.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new AdvertisingInvalidDataException($"Строка {lineNumber}: Неверный формат. Ожидается 'Название:локация1,локация2'");
        }

        var platformName = parts[0].Trim();
        var locationsString = parts[1].Trim();
        
        if (string.IsNullOrEmpty(platformName))
        {
            throw new AdvertisingInvalidDataException($"Строка {lineNumber}: Название площадки не может быть пустым");
        }

        if (string.IsNullOrEmpty(locationsString))
        {
            throw new AdvertisingInvalidDataException($"Строка {lineNumber}: Локации не могут быть пустыми");
        }

        var locationStrings = locationsString.Split(',')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (locationStrings.Count == 0)
        {
            throw new AdvertisingInvalidDataException($"Строка {lineNumber}: Не найдено ни одной корректной локации");
        }

        var locations = new List<Location>();
        foreach (var locStr in locationStrings)
        {
            try
            {
                locations.Add(new Location(locStr));
            }
            catch (ArgumentException ex)
            {
                throw new AdvertisingInvalidDataException($"Строка {lineNumber}: Некорректная локация '{locStr}': {ex.Message}");
            }
        }

        return new AdvertisingPlatform(platformName, locations);
    }

    private bool TryGetFromCache(Location location, out IReadOnlyList<string> result)
    {
        if (_searchCache.TryGetValue(location, out var cachedSet) && 
            _cacheTimestamps.TryGetValue(location, out var timestamp) &&
            DateTime.UtcNow - timestamp < _cacheExpiration)
        {
            result = cachedSet.OrderBy(x => x).ToList().AsReadOnly();
            return true;
        }

        result = Array.Empty<string>();
        return false;
    }

    private void AddToCache(Location location, IReadOnlyList<string> result)
    {
        if (_searchCache.Count > 1000) // Ограничиваем размер кэша
        {
            var oldestEntries = _cacheTimestamps
                .OrderBy(x => x.Value)
                .Take(100)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in oldestEntries)
            {
                _searchCache.Remove(key);
                _cacheTimestamps.Remove(key);
            }
        }

        _searchCache[location] = new HashSet<string>(result);
        _cacheTimestamps[location] = DateTime.UtcNow;
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
} 