using Domain.Entities;
using FluentAssertions;

namespace Tests.Domain;

public class UserEventStatsConstructorValidationTests
{
    /// <summary>
    /// Проверяет, что отрицательный UserId вызывает исключение
    /// </summary>
    [Fact]
    public void Constructor_WithNegativeUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = -5;
        var eventType = "click";

        // Act
        var act = () => new UserEventStats(userId, eventType);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId*");
    }

    /// <summary>
    /// Проверяет, что UserId=1 является валидным
    /// </summary>
    [Fact]
    public void Constructor_WithUserIdOne_CreatesInstance()
    {
        // Arrange & Act
        var stats = new UserEventStats(1, "click");

        // Assert
        stats.UserId.Should().Be(1);
    }

    /// <summary>
    /// Проверяет, что EventType с пробелами вызывает исключение
    /// </summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Constructor_WithWhitespaceEventType_ThrowsArgumentException(string eventType)
    {
        // Arrange
        var userId = 123;

        // Act
        var act = () => new UserEventStats(userId, eventType);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EventType*");
    }

    /// <summary>
    /// Проверяет, что начальный Count равен нулю
    /// </summary>
    [Fact]
    public void Constructor_InitializesCountToZero()
    {
        // Arrange & Act
        var stats = new UserEventStats(123, "click");

        // Assert
        stats.Count.Should().Be(0);
    }

    /// <summary>
    /// Проверяет, что можно создать несколько разных экземпляров
    /// </summary>
    [Fact]
    public void Constructor_CreatesMultipleIndependentInstances()
    {
        // Arrange & Act
        var stats1 = new UserEventStats(123, "click");
        var stats2 = new UserEventStats(456, "hover");

        stats1.IncrementCount();
        stats1.IncrementCount();

        // Assert
        stats1.Count.Should().Be(2);
        stats2.Count.Should().Be(0);
    }
}
