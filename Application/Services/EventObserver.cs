using System.Collections.Concurrent;
using Application.Configuration;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Реализация Observer, который обрабатывает события пользователей и ведет статистику
/// Реализует паттерн IObserver с потокобезопасной агрегацией
/// </summary>
public sealed class EventObserver : IObserver<UserEvent>
{
    private readonly IEventRepository _repository;
    private readonly ILogger<EventObserver> _logger;
    private readonly ConcurrentDictionary<(int UserId, string EventType), UserEventStats> _statsCache = new();
    private readonly TimeSpan _flushInterval;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);

    public EventObserver(
        IEventRepository repository,
        ILogger<EventObserver> logger,
        IOptions<EventProcessingSettings> settings)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var processingSettings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _flushInterval = TimeSpan.FromSeconds(processingSettings.FlushIntervalSeconds);

        // Настройка таймера для периодического сброса данных
        _flushTimer = new Timer(
            callback: _ => FlushAsync().GetAwaiter().GetResult(),
            state: null,
            dueTime: _flushInterval,
            period: _flushInterval);
    }

    /// <summary>
    /// Обрабатывает новое событие пользователя
    /// </summary>
    public void OnNext(UserEvent userEvent)
    {
        try
        {
            var key = (userEvent.UserId, userEvent.EventType);

            var stats = _statsCache.AddOrUpdate(
                key,
                _ =>
                {
                    var newStats = new UserEventStats(userEvent.UserId, userEvent.EventType);
                    newStats.IncrementCount();
                    return newStats;
                },
                (_, existing) =>
                {
                    existing.IncrementCount();
                    return existing;
                });

            _logger.LogDebug(
                "Событие обработано: UserId={UserId}, EventType={EventType}, ТекущийСчет={Count}",
                userEvent.UserId,
                userEvent.EventType,
                stats.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка обработки события: UserId={UserId}, EventType={EventType}",
                userEvent.UserId,
                userEvent.EventType);
        }
    }

    /// <summary>
    /// Обрабатывает ошибки от observable
    /// </summary>
    public void OnError(Exception error)
    {
        _logger.LogError(error, "Получена ошибка из потока событий");
    }

    /// <summary>
    /// Обрабатывает завершение потока событий
    /// </summary>
    public void OnCompleted()
    {
        _logger.LogInformation("Поток событий завершен. Сброс оставшихся данных...");
        FlushAsync().GetAwaiter().GetResult();
        _flushTimer?.Dispose();
        _logger.LogInformation("Остановка Observer завершена");
    }

    /// <summary>
    /// Сбрасывает накопленную статистику в репозиторий
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_statsCache.IsEmpty)
            return;

        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Получаем снимок текущей статистики
            var statsToFlush = _statsCache.Values.ToList();

            if (statsToFlush.Count == 0)
                return;

            _logger.LogInformation("Сброс {Count} статистик в репозиторий", statsToFlush.Count);

            await _repository.UpsertBatchAsync(statsToFlush, cancellationToken);

            // Очищаем только те статистики, которые успешно сбросили
            foreach (var stat in statsToFlush)
            {
                _statsCache.TryRemove((stat.UserId, stat.EventType), out _);
            }

            _logger.LogInformation("Статистика успешно сброшена в репозиторий");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе статистики в репозиторий");
            throw;
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Получает текущую статистику из кэша (для тестирования/мониторинга)
    /// </summary>
    public IReadOnlyDictionary<(int UserId, string EventType), UserEventStats> GetCurrentStats()
    {
        return new Dictionary<(int UserId, string EventType), UserEventStats>(_statsCache);
    }
}
