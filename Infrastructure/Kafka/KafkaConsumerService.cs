using System.Text.Json;
using Application.Interfaces;
using Application.Services;
using Confluent.Kafka;
using Domain.Entities;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Kafka;

/// <summary>
/// Сервис Kafka consumer с обработкой ошибок, graceful shutdown и правильным управлением офсетами
/// </summary>
public sealed class KafkaConsumerService : IKafkaConsumerService, IDisposable
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly KafkaSettings _settings;
    private readonly EventObservable _eventObservable;
    private IConsumer<Ignore, string>? _consumer;
    private Task? _consumeTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly JsonSerializerOptions _jsonOptions;

    public KafkaConsumerService(
        IOptions<KafkaSettings> settings,
        EventObservable eventObservable,
        ILogger<KafkaConsumerService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _eventObservable = eventObservable ?? throw new ArgumentNullException(nameof(eventObservable));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Запуск сервиса Kafka consumer...");

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.GroupId,
            EnableAutoCommit = _settings.EnableAutoCommit,
            AutoCommitIntervalMs = _settings.AutoCommitIntervalMs,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_settings.AutoOffsetReset, true),
            SessionTimeoutMs = _settings.SessionTimeoutMs,
            MaxPollIntervalMs = _settings.MaxPollIntervalMs,
            EnableAutoOffsetStore = false, // Ручное управление офсетами для лучшего контроля
        };

        _consumer = new ConsumerBuilder<Ignore, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Ошибка Kafka: Code={Code}, Reason={Reason}, IsFatal={IsFatal}",
                    error.Code, error.Reason, error.IsFatal);

                if (error.IsFatal)
                {
                    _eventObservable.PublishError(new KafkaException(error));
                }
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation("Партиции назначены: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogInformation("Партиции отозваны: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .Build();

        _consumer.Subscribe(_settings.Topic);
        _logger.LogInformation("Подписка на топик: {Topic}", _settings.Topic);

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ConsumeAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Цикл Kafka consumer запущен");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer!.Consume(cancellationToken);

                    if (consumeResult?.Message == null)
                        continue;

                    _logger.LogDebug(
                        "Получено сообщение: Topic={Topic}, Partition={Partition}, Offset={Offset}",
                        consumeResult.Topic,
                        consumeResult.Partition.Value,
                        consumeResult.Offset.Value);

                    await ProcessMessageAsync(consumeResult, cancellationToken);

                    // Сохраняем офсет после успешной обработки
                    _consumer.StoreOffset(consumeResult);

                    // Ручной коммит для лучшего контроля
                    if (!_settings.EnableAutoCommit)
                    {
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Ошибка потребления сообщения: {Error}", ex.Error.Reason);

                    if (ex.Error.IsFatal)
                    {
                        _eventObservable.PublishError(ex);
                        throw;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Операция Kafka consumer отменена");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Неожиданная ошибка в цикле consumer");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Цикл Kafka consumer остановлен");
        }
    }

    private Task ProcessMessageAsync(ConsumeResult<Ignore, string> consumeResult, CancellationToken cancellationToken)
    {
        try
        {
            var messageValue = consumeResult.Message.Value;

            if (string.IsNullOrWhiteSpace(messageValue))
            {
                _logger.LogWarning("Получено пустое сообщение");
                return Task.CompletedTask;
            }

            var userEventDto = JsonSerializer.Deserialize<UserEventDto>(messageValue, _jsonOptions);

            if (userEventDto == null)
            {
                _logger.LogWarning("Не удалось десериализовать сообщение: {Message}", messageValue);
                return Task.CompletedTask;
            }

            // Конвертируем DTO в доменную сущность
            var userEvent = new UserEvent(
                userEventDto.UserId,
                userEventDto.EventType,
                userEventDto.Timestamp,
                new EventData
                {
                    ButtonId = userEventDto.Data?.ButtonId
                });

            // Публикуем подписчикам
            _eventObservable.Publish(userEvent);

            _logger.LogDebug(
                "Событие успешно обработано: UserId={UserId}, EventType={EventType}",
                userEvent.UserId,
                userEvent.EventType);

            return Task.CompletedTask;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Ошибка десериализации JSON для сообщения: {Message}", consumeResult.Message.Value);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки сообщения");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Остановка сервиса Kafka consumer...");

        _cancellationTokenSource?.Cancel();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Задача consumer не завершилась в течение таймаута");
            }
        }

        _consumer?.Close();
        _logger.LogInformation("Сервис Kafka consumer остановлен");
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _consumer?.Dispose();
    }

    /// <summary>
    /// DTO для десериализации сообщений Kafka
    /// </summary>
    private sealed class UserEventDto
    {
        public int UserId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public EventDataDto? Data { get; set; }
    }

    private sealed class EventDataDto
    {
        public string? ButtonId { get; set; }
    }
}
