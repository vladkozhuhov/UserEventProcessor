using Domain.Entities;
using FluentAssertions;

namespace Tests.Domain;

public class UserEventTests
{
    /// <summary>
    /// Проверяет, что корректные параметры создают экземпляр
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var userId = 123;
        var eventType = "click";
        var timestamp = DateTime.UtcNow;
        var data = new EventData { ButtonId = "submit" };

        // Act
        var userEvent = new UserEvent(userId, eventType, timestamp, data);

        // Assert
        userEvent.UserId.Should().Be(userId);
        userEvent.EventType.Should().Be(eventType);
        userEvent.Timestamp.Should().Be(timestamp);
        userEvent.Data.Should().Be(data);
    }

    /// <summary>
    /// Проверяет, что некорректный UserId вызывает исключение
    /// </summary>
    [Fact]
    public void Constructor_WithInvalidUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = 0;
        var eventType = "click";
        var timestamp = DateTime.UtcNow;
        var data = new EventData();

        // Act
        var act = () => new UserEvent(userId, eventType, timestamp, data);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId*");
    }

    /// <summary>
    /// Проверяет, что некорректный EventType вызывает исключение
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidEventType_ThrowsArgumentException(string? eventType)
    {
        // Arrange
        var userId = 123;
        var timestamp = DateTime.UtcNow;
        var data = new EventData();

        // Act
        var act = () => new UserEvent(userId, eventType!, timestamp, data);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EventType*");
    }
}
