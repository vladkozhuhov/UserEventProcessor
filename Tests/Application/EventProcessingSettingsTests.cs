using Application.Configuration;
using FluentAssertions;

namespace Tests.Application;

public class EventProcessingSettingsTests
{
    /// <summary>
    /// Проверяет значение по умолчанию для FlushIntervalSeconds
    /// </summary>
    [Fact]
    public void DefaultFlushIntervalSeconds_ShouldBe10()
    {
        // Arrange & Act
        var settings = new EventProcessingSettings();

        // Assert
        settings.FlushIntervalSeconds.Should().Be(10);
    }

    /// <summary>
    /// Проверяет, что SectionName имеет правильное значение
    /// </summary>
    [Fact]
    public void SectionName_ShouldBeEventProcessing()
    {
        // Assert
        EventProcessingSettings.SectionName.Should().Be("EventProcessing");
    }

    /// <summary>
    /// Проверяет, что можно установить FlushIntervalSeconds
    /// </summary>
    [Fact]
    public void FlushIntervalSeconds_CanBeSet()
    {
        // Arrange
        var settings = new EventProcessingSettings();

        // Act
        settings.FlushIntervalSeconds = 30;

        // Assert
        settings.FlushIntervalSeconds.Should().Be(30);
    }
}
