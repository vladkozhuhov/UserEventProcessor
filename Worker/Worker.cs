using Application.Interfaces;
using Application.Services;

namespace Worker;

/// <summary>
/// Фоновый сервис, который оркестрирует потребление из Kafka и обработку событий
/// Управляет жизненным циклом consumer и observer компонентов
/// </summary>
public sealed class UserEventProcessorWorker : BackgroundService
{
    private readonly ILogger<UserEventProcessorWorker> _logger;
    private readonly IKafkaConsumerService _kafkaConsumer;
    private readonly EventObservable _eventObservable;
    private readonly EventObserver _eventObserver;
    private IDisposable? _subscription;

    public UserEventProcessorWorker(
        ILogger<UserEventProcessorWorker> logger,
        IKafkaConsumerService kafkaConsumer,
        EventObservable eventObservable,
        EventObserver eventObserver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumer = kafkaConsumer ?? throw new ArgumentNullException(nameof(kafkaConsumer));
        _eventObservable = eventObservable ?? throw new ArgumentNullException(nameof(eventObservable));
        _eventObserver = eventObserver ?? throw new ArgumentNullException(nameof(eventObserver));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker обработки событий пользователя запускается...");

        try
        {
            // Подписываем observer на observable
            _subscription = _eventObservable.Subscribe(_eventObserver);
            _logger.LogInformation("Event observer подписан на observable");

            // Запускаем Kafka consumer
            await _kafkaConsumer.StartAsync(stoppingToken);
            _logger.LogInformation("Kafka consumer успешно запущен");

            // Держим worker запущенным до получения сигнала остановки
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Выполнение worker отменено");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при выполнении worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker обработки событий пользователя останавливается...");

        try
        {
            // Сначала останавливаем Kafka consumer
            await _kafkaConsumer.StopAsync(cancellationToken);

            // Сбрасываем оставшиеся события
            await _eventObserver.FlushAsync(cancellationToken);

            // Завершаем поток observable
            _eventObservable.Complete();

            // Отписываемся
            _subscription?.Dispose();

            _logger.LogInformation("Worker обработки событий пользователя остановлен корректно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при остановке worker");
            throw;
        }

        await base.StopAsync(cancellationToken);
    }
}
