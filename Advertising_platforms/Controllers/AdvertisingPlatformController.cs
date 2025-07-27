using Advertising_platforms.Models;
using Advertising_platforms.Services;
using Advertising_platforms.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace Advertising_platforms.Controllers;

/// Контроллер для работы с рекламными площадками
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdvertisingPlatformController : ControllerBase
{
    private readonly IAdvertisingPlatformService _service;
    private readonly ILogger<AdvertisingPlatformController> _logger;

    public AdvertisingPlatformController(
        IAdvertisingPlatformService service, 
        ILogger<AdvertisingPlatformController> logger)
    {
        _service = service;
        _logger = logger;
    }
    
    /// Загружает рекламные площадки из файла (полностью перезаписывает всю хранимую информацию)
    [HttpPost("upload")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public async Task<IActionResult> UploadPlatforms(
        [Required] IFormFile file,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Получен запрос на загрузку файла: {FileName}, размер: {Size} байт", 
            file.FileName, file.Length);

        try
        {
            await _service.LoadFromFileAsync(file, cancellationToken);
            
            var response = new
            {
                Message = "Данные успешно загружены",
                FileName = file.FileName,
                FileSize = file.Length,
                TotalPlatforms = _service.GetTotalPlatformsCount(),
                TotalLocations = _service.GetTotalLocationsCount(),
                Timestamp = DateTime.UtcNow
            };
            
            _logger.LogInformation("Файл {FileName} успешно загружен", file.FileName);
            return Ok(response);
        }
        catch (AdvertisingValidationException ex)
        {
            _logger.LogWarning("Ошибка валидации при загрузке файла: {Error}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Ошибка валидации",
                Detail = ex.Message,
                Status = 400
            });
        }
        catch (AdvertisingFileLoadException ex)
        {
            _logger.LogWarning("Ошибка загрузки файла: {Error}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Ошибка загрузки файла",
                Detail = ex.Message,
                Status = 400
            });
        }
        catch (AdvertisingInvalidDataException ex)
        {
            _logger.LogWarning("Ошибка данных в файле: {Error}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Ошибка данных в файле",
                Detail = ex.Message,
                Status = 400
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Операция загрузки файла была отменена");
            return StatusCode(499, "Операция отменена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при загрузке файла");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Внутренняя ошибка сервера",
                Detail = "Произошла неожиданная ошибка при обработке файла",
                Status = 500
            });
        }
    }
    
    /// Ищет список рекламных площадок для заданной локации (POST метод)
    [HttpPost("search")]
    [ProducesResponseType(typeof(LocationSearchResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public IActionResult SearchPlatforms([FromBody] LocationSearchRequest request)
    {
        if (request == null)
        {
            _logger.LogWarning("Получен пустой запрос поиска");
            return BadRequest(new ProblemDetails
            {
                Title = "Ошибка валидации",
                Detail = "Запрос не может быть пустым",
                Status = 400
            });
        }

        _logger.LogInformation("Поиск площадок для локации: {Location}", request.Location);

        try
        {
            var platforms = _service.SearchPlatformsByLocation(request.Location);
            
            var response = new LocationSearchResponse
            {
                Location = request.Location,
                AdvertisingPlatforms = platforms
            };

            _logger.LogInformation("Найдено {Count} площадок для локации {Location}", 
                platforms.Count, request.Location);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при поиске площадок для локации: {Location}", request.Location);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Внутренняя ошибка сервера",
                Detail = "Произошла ошибка при поиске площадок",
                Status = 500
            });
        }
    }
    
    /// Ищет список рекламных площадок для заданной локации (GET метод)
    [HttpGet("search/{location}")]
    [ProducesResponseType(typeof(LocationSearchResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public IActionResult SearchPlatformsGet([Required] string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            _logger.LogWarning("Получен пустой параметр локации");
            return BadRequest(new ProblemDetails
            {
                Title = "Ошибка валидации",
                Detail = "Локация не может быть пустой",
                Status = 400
            });
        }

        _logger.LogInformation("GET поиск площадок для локации: {Location}", location);

        try
        {
            var platforms = _service.SearchPlatformsByLocation(location);
            
            var response = new LocationSearchResponse
            {
                Location = location,
                AdvertisingPlatforms = platforms
            };

            _logger.LogInformation("GET найдено {Count} площадок для локации {Location}", 
                platforms.Count, location);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при GET поиске площадок для локации: {Location}", location);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Внутренняя ошибка сервера",
                Detail = "Произошла ошибка при поиске площадок",
                Status = 500
            });
        }
    }
    
    /// Получает статистику по загруженным данным
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetStats()
    {
        var stats = new
        {
            TotalPlatforms = _service.GetTotalPlatformsCount(),
            TotalLocations = _service.GetTotalLocationsCount(),
            Timestamp = DateTime.UtcNow
        };

        return Ok(stats);
    }
    
    /// Очищает все загруженные данные
    [HttpDelete("clear")]
    [ProducesResponseType(typeof(string), 200)]
    public IActionResult ClearData()
    {
        _logger.LogInformation("Запрос на очистку всех данных");
        
        _service.ClearAllData();
        
        return Ok("Все данные успешно очищены");
    }
} 