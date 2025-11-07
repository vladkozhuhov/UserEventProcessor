using Application.Configuration;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application;

public class EventObserverTests
{
    private readonly Mock<IEventRepository> _repositoryMock;
    private readonly Mock<ILogger<EventObserver>> _loggerMock;

    public EventObserverTests()
    {
        _repositoryMock = new Mock<IEventRepository>();
        _loggerMock = new Mock<ILogger<EventObserver>>();
    }

    private EventObserver CreateObserver(int flushIntervalSeconds = 3600)
    {
        var settings = Options.Create(new EventProcessingSettings
        {
            FlushIntervalSeconds = flushIntervalSeconds
        });

        return new EventObserver(_repositoryMock.Object, _loggerMock.Object, settings);
    }

    /// <summary>
    /// Проверяет, что новое событие увеличивает счетчик
    /// </summary>
    [Fact]
    public void OnNext_WithNewEvent_IncrementsCount()
    {
        // Arrange
        var observer = CreateObserver(); 

        var userEvent = new UserEvent(123, "click", DateTime.UtcNow, new EventData());

        // Act
        observer.OnNext(userEvent);

        // Assert
        var stats = observer.GetCurrentStats();
        stats.Should().ContainKey((123, "click"));
        stats[(123, "click")].Count.Should().Be(1);
    }

    /// <summary>
    /// Проверяет, что множественные события агрегируются в одну статистику
    /// </summary>
    [Fact]
    public void OnNext_WithMultipleEventsForSameUser_AggregatesCount()
    {
        // Arrange
        var observer = CreateObserver();

        var event1 = new UserEvent(123, "click", DateTime.UtcNow, new EventData());
        var event2 = new UserEvent(123, "click", DateTime.UtcNow, new EventData());
        var event3 = new UserEvent(123, "click", DateTime.UtcNow, new EventData());

        // Act
        observer.OnNext(event1);
        observer.OnNext(event2);
        observer.OnNext(event3);

        // Assert
        var stats = observer.GetCurrentStats();
        stats[(123, "click")].Count.Should().Be(3);
    }

    /// <summary>
    /// Проверяет, что разные типы событий создают отдельные статистики
    /// </summary>
    [Fact]
    public void OnNext_WithDifferentEventTypes_CreatesMultipleStats()
    {
        // Arrange
        var observer = CreateObserver();

        var clickEvent = new UserEvent(123, "click", DateTime.UtcNow, new EventData());
        var hoverEvent = new UserEvent(123, "hover", DateTime.UtcNow, new EventData());

        // Act
        observer.OnNext(clickEvent);
        observer.OnNext(clickEvent);
        observer.OnNext(hoverEvent);

        // Assert
        var stats = observer.GetCurrentStats();
        stats.Should().HaveCount(2);
        stats[(123, "click")].Count.Should().Be(2);
        stats[(123, "hover")].Count.Should().Be(1);
    }

    /// <summary>
    /// Проверяет, что сброс сохраняет накопленную статистику в репозиторий
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithAccumulatedStats_SavesToRepository()
    {
        // Arrange
        var observer = CreateObserver();

        var userEvent = new UserEvent(123, "click", DateTime.UtcNow, new EventData());
        observer.OnNext(userEvent);

        // Act
        await observer.FlushAsync();

        // Assert
        _repositoryMock.Verify(
            r => r.UpsertBatchAsync(
                It.Is<IEnumerable<UserEventStats>>(list => list.Any(s => s.UserId == 123 && s.EventType == "click")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Проверяет, что сброс без данных не обращается к репозиторию
    /// </summary>
    [Fact]
    public async Task FlushAsync_WithEmptyStats_DoesNotCallRepository()
    {
        // Arrange
        var observer = CreateObserver();

        // Act
        await observer.FlushAsync();

        // Assert
        _repositoryMock.Verify(
            r => r.UpsertBatchAsync(
                It.IsAny<IEnumerable<UserEventStats>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Проверяет, что ошибка логируется
    /// </summary>
    [Fact]
    public void OnError_WithException_LogsError()
    {
        // Arrange
        var observer = CreateObserver();

        var exception = new Exception("Test error");

        // Act
        observer.OnError(exception);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
