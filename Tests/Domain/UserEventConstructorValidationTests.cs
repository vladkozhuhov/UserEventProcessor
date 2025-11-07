using Domain.Entities;
using FluentAssertions;

namespace Tests.Domain;

public class UserEventConstructorValidationTests
{
    /// <summary>
    /// Проверяет, что отрицательный UserId вызывает исключение
    /// </summary>
    [Fact]
    public void Constructor_WithNegativeUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = -1;
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
    /// Проверяет, что можно создать событие с минимальным валидным UserId
    /// </summary>
    [Fact]
    public void Constructor_WithUserIdOne_CreatesInstance()
    {
        // Arrange
        var userId = 1;
        var eventType = "click";
        var timestamp = DateTime.UtcNow;
        var data = new EventData();

        // Act
        var userEvent = new UserEvent(userId, eventType, timestamp, data);

        // Assert
        userEvent.UserId.Should().Be(1);
    }

    /// <summary>
    /// Проверяет, что EventData может быть пустым объектом
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyEventData_CreatesInstance()
    {
        // Arrange
        var userId = 123;
        var eventType = "click";
        var timestamp = DateTime.UtcNow;
        var data = new EventData();

        // Act
        var userEvent = new UserEvent(userId, eventType, timestamp, data);

        // Assert
        userEvent.Data.Should().NotBeNull();
        userEvent.Data.ButtonId.Should().BeNull();
        userEvent.Data.AdditionalProperties.Should().BeEmpty();
    }

    /// <summary>
    /// Проверяет, что EventData с дополнительными свойствами сохраняется корректно
    /// </summary>
    [Fact]
    public void Constructor_WithEventDataAdditionalProperties_CreatesInstance()
    {
        // Arrange
        var userId = 123;
        var eventType = "click";
        var timestamp = DateTime.UtcNow;
        var data = new EventData
        {
            ButtonId = "submit-btn",
            AdditionalProperties = new Dictionary<string, object>
            {
                { "page", "checkout" },
                { "duration", 1500 }
            }
        };

        // Act
        var userEvent = new UserEvent(userId, eventType, timestamp, data);

        // Assert
        userEvent.Data.ButtonId.Should().Be("submit-btn");
        userEvent.Data.AdditionalProperties.Should().HaveCount(2);
        userEvent.Data.AdditionalProperties["page"].Should().Be("checkout");
        userEvent.Data.AdditionalProperties["duration"].Should().Be(1500);
    }
}
