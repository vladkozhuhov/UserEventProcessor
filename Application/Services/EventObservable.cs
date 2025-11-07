using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Реализация Observable для событий пользователей с использованием паттерна IObservable
/// Потокобезопасная реализация с правильным управлением жизненным циклом подписчиков
/// </summary>
public sealed class EventObservable : IObservable<UserEvent>
{
    private readonly List<IObserver<UserEvent>> _observers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Подписывает наблюдателя на получение уведомлений о событиях пользователя
    /// </summary>
    public IDisposable Subscribe(IObserver<UserEvent> observer)
    {
        if (observer == null)
            throw new ArgumentNullException(nameof(observer));

        lock (_lock)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        return new Unsubscriber(_observers, observer, _lock);
    }

    /// <summary>
    /// Публикует новое событие всем подписчикам
    /// </summary>
    public void Publish(UserEvent userEvent)
    {
        if (userEvent == null)
            throw new ArgumentNullException(nameof(userEvent));

        List<IObserver<UserEvent>> observersCopy;

        lock (_lock)
        {
            observersCopy = new List<IObserver<UserEvent>>(_observers);
        }

        foreach (var observer in observersCopy)
        {
            try
            {
                observer.OnNext(userEvent);
            }
            catch (Exception)
            {
                // Не позволяем исключению одного подписчика повлиять на других
                // Подписчики должны обрабатывать свои исключения самостоятельно
            }
        }
    }

    /// <summary>
    /// Уведомляет всех подписчиков о возникшей ошибке
    /// </summary>
    public void PublishError(Exception error)
    {
        if (error == null)
            throw new ArgumentNullException(nameof(error));

        List<IObserver<UserEvent>> observersCopy;

        lock (_lock)
        {
            observersCopy = new List<IObserver<UserEvent>>(_observers);
        }

        foreach (var observer in observersCopy)
        {
            observer.OnError(error);
        }
    }

    /// <summary>
    /// Уведомляет всех подписчиков о завершении отправки уведомлений
    /// </summary>
    public void Complete()
    {
        List<IObserver<UserEvent>> observersCopy;

        lock (_lock)
        {
            observersCopy = new List<IObserver<UserEvent>>(_observers);
            _observers.Clear();
        }

        foreach (var observer in observersCopy)
        {
            observer.OnCompleted();
        }
    }

    /// <summary>
    /// Вспомогательный класс для корректной отписки
    /// </summary>
    private sealed class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<UserEvent>> _observers;
        private readonly IObserver<UserEvent> _observer;
        private readonly object _lock;
        private bool _disposed;

        public Unsubscriber(List<IObserver<UserEvent>> observers, IObserver<UserEvent> observer, object lockObject)
        {
            _observers = observers;
            _observer = observer;
            _lock = lockObject;
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                if (_observer != null && _observers.Contains(_observer))
                {
                    _observers.Remove(_observer);
                }
            }

            _disposed = true;
        }
    }
}
