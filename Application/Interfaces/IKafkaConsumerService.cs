namespace Application.Interfaces;

/// <summary>
/// Сервис для чтения сообщений из Kafka
/// </summary>
public interface IKafkaConsumerService
{
    /// <summary>
    /// Запускает чтение сообщений
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Останавливает чтение и закрывает соединение
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
