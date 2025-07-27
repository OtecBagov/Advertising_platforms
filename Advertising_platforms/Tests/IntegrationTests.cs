using Advertising_platforms.Services;
using Advertising_platforms.Exceptions;
using Advertising_platforms.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Advertising_platforms.Tests;

public class IntegrationTests
{
    private readonly Mock<ILogger<AdvertisingPlatformService>> _loggerMock;
    private readonly AdvertisingPlatformService _service;

    public IntegrationTests()
    {
        _loggerMock = new Mock<ILogger<AdvertisingPlatformService>>();
        _service = new AdvertisingPlatformService(_loggerMock.Object);
    }

    [Fact]
    public async Task CompleteWorkflow_UploadAndSearch_Success()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nКрутая реклама:/ru/svrd";
        var file = CreateTestFile(content, "test.txt");

        // Act - Upload
        await _service.LoadFromFileAsync(file);

        // Assert - Upload
        Assert.Equal(2, _service.GetTotalPlatformsCount());
        Assert.Equal(2, _service.GetTotalLocationsCount());

        // Act - Search
        var result = _service.SearchPlatformsByLocation("/ru/svrd/revda");

        // Assert - Search
        Assert.Contains("Яндекс.Директ", result);
        Assert.Contains("Крутая реклама", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Search_WithoutUpload_ReturnsEmptyList()
    {
        // Act
        var result = _service.SearchPlatformsByLocation("/ru");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Upload_InvalidFile_ThrowsException()
    {
        // Arrange
        var content = "Invalid content without colons";
        var file = CreateTestFile(content, "test.txt");

        // Act & Assert
        await Assert.ThrowsAsync<AdvertisingFileLoadException>(() => _service.LoadFromFileAsync(file));
    }

    [Fact]
    public async Task Search_InvalidLocation_ReturnsEmptyList()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        var result = _service.SearchPlatformsByLocation("invalid-location");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Stats_ReturnsCorrectInformation()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nКрутая реклама:/ru/svrd";
        var file = CreateTestFile(content, "test.txt");

        // Act
        await _service.LoadFromFileAsync(file);
        var totalPlatforms = _service.GetTotalPlatformsCount();
        var totalLocations = _service.GetTotalLocationsCount();

        // Assert
        Assert.Equal(2, totalPlatforms);
        Assert.Equal(2, totalLocations);
    }

    [Fact]
    public async Task Clear_RemovesAllData()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        _service.ClearAllData();

        // Assert
        Assert.Equal(0, _service.GetTotalPlatformsCount());
        Assert.Equal(0, _service.GetTotalLocationsCount());
        Assert.Empty(_service.SearchPlatformsByLocation("/ru"));
    }

    [Fact]
    public async Task Search_WithLocationObject_ReturnsCorrectResults()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nКрутая реклама:/ru/svrd";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);
        var location = new Location("/ru/svrd/revda");

        // Act
        var result = _service.SearchPlatformsByLocation(location);

        // Assert
        Assert.Contains("Яндекс.Директ", result);
        Assert.Contains("Крутая реклама", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task LoadFromFileAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.txt");
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Отменяем сразу

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _service.LoadFromFileAsync(file, cts.Token));
    }

    [Fact]
    public async Task LoadFromFileAsync_FileTooLarge_ThrowsAdvertisingValidationException()
    {
        // Arrange
        var largeContent = new string('a', 11 * 1024 * 1024); // 11MB
        var file = CreateTestFile(largeContent, "large.txt");

        // Act & Assert
        await Assert.ThrowsAsync<AdvertisingValidationException>(() => _service.LoadFromFileAsync(file));
    }

    private static IFormFile CreateTestFile(string content, string fileName)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }
} 