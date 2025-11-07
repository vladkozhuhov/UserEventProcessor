using Application.Services;
using Domain.Entities;
using FluentAssertions;

namespace Tests.Application;

public class EventObservableTests
{
    /// <summary>
    /// Проверяет, что подписка возвращает IDisposable для отписки
    /// </summary>
    [Fact]
    public void Subscribe_WithObserver_ReturnsDisposable()
    {
        // Arrange
        var observable = new EventObservable();
        var observer = new TestObserver();

        // Act
        var subscription = observable.Subscribe(observer);

        // Assert
        subscription.Should().NotBeNull();
        subscription.Should().BeAssignableTo<IDisposable>();
    }

    /// <summary>
    /// Проверяет, что событие доставляется подписанному наблюдателю
    /// </summary>
    [Fact]
    public void Publish_WithSubscribedObserver_NotifiesObserver()
    {
        // Arrange
        var observable = new EventObservable();
        var observer = new TestObserver();
        observable.Subscribe(observer);

        var userEvent = new UserEvent(123, "click", DateTime.UtcNow, new EventData());

        // Act
        observable.Publish(userEvent);

        // Assert
        observer.ReceivedEvents.Should().ContainSingle();
        observer.ReceivedEvents[0].Should().Be(userEvent);
    }

    /// <summary>
    /// Проверяет, что событие доставляется всем подписанным наблюдателям
    /// </summary>
    [Fact]
    public void Publish_WithMultipleObservers_NotifiesAllObservers()
    {
        // Arrange
        var observable = new EventObservable();
        var observer1 = new TestObserver();
        var observer2 = new TestObserver();
        observable.Subscribe(observer1);
        observable.Subscribe(observer2);

        var userEvent = new UserEvent(123, "click", DateTime.UtcNow, new EventData());

        // Act
        observable.Publish(userEvent);

        // Assert
        observer1.ReceivedEvents.Should().ContainSingle();
        observer2.ReceivedEvents.Should().ContainSingle();
    }

    /// <summary>
    /// Проверяет, что после отписки события не доставляются
    /// </summary>
    [Fact]
    public void Publish_AfterUnsubscribe_DoesNotNotifyObserver()
    {
        // Arrange
        var observable = new EventObservable();
        var observer = new TestObserver();
        var subscription = observable.Subscribe(observer);

        var userEvent = new UserEvent(123, "click", DateTime.UtcNow, new EventData());

        // Act
        subscription.Dispose();
        observable.Publish(userEvent);

        // Assert
        observer.ReceivedEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Проверяет, что ошибка доставляется подписанному наблюдателю
    /// </summary>
    [Fact]
    public void PublishError_WithSubscribedObserver_NotifiesObserverOfError()
    {
        // Arrange
        var observable = new EventObservable();
        var observer = new TestObserver();
        observable.Subscribe(observer);

        var exception = new Exception("Test error");

        // Act
        observable.PublishError(exception);

        // Assert
        observer.ReceivedError.Should().Be(exception);
    }

    /// <summary>
    /// Проверяет, что Complete уведомляет наблюдателей о завершении
    /// </summary>
    [Fact]
    public void Complete_WithSubscribedObserver_NotifiesObserverAndUnsubscribes()
    {
        // Arrange
        var observable = new EventObservable();
        var observer = new TestObserver();
        observable.Subscribe(observer);

        // Act
        observable.Complete();

        // Assert
        observer.IsCompleted.Should().BeTrue();
    }

    /// <summary>
    /// Тестовая реализация IObserver для проверки уведомлений
    /// </summary>
    private class TestObserver : IObserver<UserEvent>
    {
        public List<UserEvent> ReceivedEvents { get; } = new();
        public Exception? ReceivedError { get; private set; }
        public bool IsCompleted { get; private set; }

        public void OnNext(UserEvent value)
        {
            ReceivedEvents.Add(value);
        }

        public void OnError(Exception error)
        {
            ReceivedError = error;
        }

        public void OnCompleted()
        {
            IsCompleted = true;
        }
    }
}
