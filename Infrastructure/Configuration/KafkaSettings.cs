namespace Infrastructure.Configuration;

/// <summary>
/// Настройки конфигурации для Kafka consumer
/// </summary>
public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public bool EnableAutoCommit { get; set; } = false;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    public string AutoOffsetReset { get; set; } = "earliest";
    public int SessionTimeoutMs { get; set; } = 45000;
    public int MaxPollIntervalMs { get; set; } = 300000;
}
