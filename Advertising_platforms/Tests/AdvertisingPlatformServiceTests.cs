using Advertising_platforms.Services;
using Advertising_platforms.Exceptions;
using Advertising_platforms.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Advertising_platforms.Tests;

public class AdvertisingPlatformServiceTests
{
    private readonly Mock<ILogger<AdvertisingPlatformService>> _loggerMock;
    private readonly AdvertisingPlatformService _service;

    public AdvertisingPlatformServiceTests()
    {
        _loggerMock = new Mock<ILogger<AdvertisingPlatformService>>();
        _service = new AdvertisingPlatformService(_loggerMock.Object);
    }

    [Fact]
    public async Task LoadFromFileAsync_ValidData_CompletesSuccessfully()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nРевдинский рабочий:/ru/svrd/revda,/ru/svrd/pervik";
        var file = CreateTestFile(content, "test.txt");

        // Act & Assert
        await _service.LoadFromFileAsync(file);
        Assert.Equal(2, _service.GetTotalPlatformsCount());
        Assert.Equal(4, _service.GetTotalLocationsCount());
    }

    [Fact]
    public async Task LoadFromFileAsync_EmptyFile_ThrowsAdvertisingValidationException()
    {
        // Arrange
        var file = CreateTestFile("", "test.txt");

        // Act & Assert
        await Assert.ThrowsAsync<AdvertisingValidationException>(() => _service.LoadFromFileAsync(file));
    }

    [Fact]
    public async Task LoadFromFileAsync_NullFile_ThrowsAdvertisingValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<AdvertisingValidationException>(() => _service.LoadFromFileAsync(null!));
    }

    [Fact]
    public async Task LoadFromFileAsync_InvalidFormat_SkipsInvalidLinesAndCompletes()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nInvalidLine\nРевдинский рабочий:/ru/svrd/revda";
        var file = CreateTestFile(content, "test.txt");

        // Act & Assert
        await _service.LoadFromFileAsync(file);
        Assert.Equal(2, _service.GetTotalPlatformsCount());
    }

    [Fact]
    public async Task SearchPlatformsByLocation_ExactMatch_ReturnsCorrectPlatforms()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nРевдинский рабочий:/ru/svrd/revda,/ru/svrd/pervik";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        var result = _service.SearchPlatformsByLocation("/ru/svrd/revda");

        // Assert
        Assert.Contains("Ревдинский рабочий", result);
        Assert.Contains("Яндекс.Директ", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchPlatformsByLocation_ParentLocation_ReturnsCorrectPlatforms()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nКрутая реклама:/ru/svrd";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        var result = _service.SearchPlatformsByLocation("/ru/svrd/revda");

        // Assert
        Assert.Contains("Яндекс.Директ", result);
        Assert.Contains("Крутая реклама", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchPlatformsByLocation_RootLocation_ReturnsOnlyRootPlatforms()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nКрутая реклама:/ru/svrd";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        var result = _service.SearchPlatformsByLocation("/ru");

        // Assert
        Assert.Contains("Яндекс.Директ", result);
        Assert.DoesNotContain("Крутая реклама", result);
        Assert.Single(result);
    }

    [Fact]
    public async Task SearchPlatformsByLocation_EmptyLocation_ReturnsEmptyList()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        var result = _service.SearchPlatformsByLocation("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchPlatformsByLocation_NonexistentLocation_ReturnsEmptyList()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.txt");
        await _service.LoadFromFileAsync(file);

        // Act
        var result = _service.SearchPlatformsByLocation("/nonexistent");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadFromFileAsync_ReplacesExistingData()
    {
        // Arrange
        var content1 = "Платформа1:/ru";
        var content2 = "Платформа2:/ru";
        var file1 = CreateTestFile(content1, "test1.txt");
        var file2 = CreateTestFile(content2, "test2.txt");

        // Act
        await _service.LoadFromFileAsync(file1);
        var result1 = _service.SearchPlatformsByLocation("/ru");
        await _service.LoadFromFileAsync(file2);
        var result2 = _service.SearchPlatformsByLocation("/ru");

        // Assert
        Assert.Contains("Платформа1", result1);
        Assert.DoesNotContain("Платформа1", result2);
        Assert.Contains("Платформа2", result2);
    }

    [Fact]
    public async Task LoadFromFileAsync_OnlyInvalidLines_ThrowsAdvertisingFileLoadException()
    {
        // Arrange
        var content = "InvalidLine1\nInvalidLine2\n";
        var file = CreateTestFile(content, "test.txt");

        // Act & Assert
        await Assert.ThrowsAsync<AdvertisingFileLoadException>(() => _service.LoadFromFileAsync(file));
    }

    [Fact]
    public async Task LoadFromFileAsync_InvalidFileExtension_ThrowsAdvertisingValidationException()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.doc");

        // Act & Assert
        await Assert.ThrowsAsync<AdvertisingValidationException>(() => _service.LoadFromFileAsync(file));
    }

    [Fact]
    public async Task LoadFromFileAsync_EmptyPlatformName_SkipsInvalidLine()
    {
        // Arrange
        var content = ":/ru\nЯндекс.Директ:/ru";
        var file = CreateTestFile(content, "test.txt");

        // Act & Assert
        await _service.LoadFromFileAsync(file);
        Assert.Equal(1, _service.GetTotalPlatformsCount());
    }

    [Fact]
    public async Task LoadFromFileAsync_LargeFile_HandlesCorrectly()
    {
        // Arrange
        var lines = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            lines.Add($"Платформа{i}:/ru/region{i}");
        }
        var content = string.Join("\n", lines);
        var file = CreateTestFile(content, "large_test.txt");

        // Act
        await _service.LoadFromFileAsync(file);

        // Assert
        Assert.Equal(1000, _service.GetTotalPlatformsCount());
        Assert.Equal(1000, _service.GetTotalLocationsCount());
    }

    [Fact]
    public async Task SearchPlatformsByLocation_InvalidLocationFormat_ReturnsEmptyList()
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
    public void ClearAllData_RemovesAllData()
    {
        // Arrange
        var content = "Яндекс.Директ:/ru\nКрутая реклама:/ru/svrd";
        var file = CreateTestFile(content, "test.txt");
        _service.LoadFromFileAsync(file).Wait();

        // Act
        _service.ClearAllData();

        // Assert
        Assert.Equal(0, _service.GetTotalPlatformsCount());
        Assert.Equal(0, _service.GetTotalLocationsCount());
        Assert.Empty(_service.SearchPlatformsByLocation("/ru"));
    }

    [Fact]
    public async Task SearchPlatformsByLocation_WithLocationObject_ReturnsCorrectResults()
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