using Domain.Entities;
using FluentAssertions;

namespace Tests.Domain;

public class UserEventStatsTests
{
    /// <summary>
    /// Проверяет, что корректные параметры создают экземпляр
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var userId = 123;
        var eventType = "click";

        // Act
        var stats = new UserEventStats(userId, eventType);

        // Assert
        stats.UserId.Should().Be(userId);
        stats.EventType.Should().Be(eventType);
        stats.Count.Should().Be(0);
    }

    /// <summary>
    /// Проверяет, что IncrementCount увеличивает счетчик на 1
    /// </summary>
    [Fact]
    public void IncrementCount_IncreasesCountByOne()
    {
        // Arrange
        var stats = new UserEventStats(123, "click");

        // Act
        stats.IncrementCount();
        stats.IncrementCount();
        stats.IncrementCount();

        // Assert
        stats.Count.Should().Be(3);
    }

    /// <summary>
    /// Проверяет, что SetCount устанавливает значение счетчика
    /// </summary>
    [Fact]
    public void SetCount_WithValidValue_SetsCount()
    {
        // Arrange
        var stats = new UserEventStats(123, "click");

        // Act
        stats.SetCount(10);

        // Assert
        stats.Count.Should().Be(10);
    }

    /// <summary>
    /// Проверяет, что SetCount с отрицательным значением вызывает исключение
    /// </summary>
    [Fact]
    public void SetCount_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var stats = new UserEventStats(123, "click");

        // Act
        var act = () => stats.SetCount(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Count*");
    }

    /// <summary>
    /// Проверяет, что Equals возвращает true для одинаковых UserId и EventType
    /// </summary>
    [Fact]
    public void Equals_WithSameUserIdAndEventType_ReturnsTrue()
    {
        // Arrange
        var stats1 = new UserEventStats(123, "click");
        var stats2 = new UserEventStats(123, "click");

        // Act & Assert
        stats1.Equals(stats2).Should().BeTrue();
    }

    /// <summary>
    /// Проверяет, что Equals возвращает false для разных UserId или EventType
    /// </summary>
    [Fact]
    public void Equals_WithDifferentUserIdOrEventType_ReturnsFalse()
    {
        // Arrange
        var stats1 = new UserEventStats(123, "click");
        var stats2 = new UserEventStats(456, "click");
        var stats3 = new UserEventStats(123, "hover");

        // Act & Assert
        stats1.Equals(stats2).Should().BeFalse();
        stats1.Equals(stats3).Should().BeFalse();
    }

    /// <summary>
    /// Проверяет, что GetHashCode возвращает одинаковый хеш для одинаковых объектов
    /// </summary>
    [Fact]
    public void GetHashCode_WithSameUserIdAndEventType_ReturnsSameHashCode()
    {
        // Arrange
        var stats1 = new UserEventStats(123, "click");
        var stats2 = new UserEventStats(123, "click");

        // Act & Assert
        stats1.GetHashCode().Should().Be(stats2.GetHashCode());
    }
}
