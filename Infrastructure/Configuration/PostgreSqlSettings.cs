namespace Infrastructure.Configuration;

/// <summary>
/// Настройки конфигурации для базы данных PostgreSQL
/// </summary>
public sealed class PostgreSqlSettings
{
    public const string SectionName = "PostgreSQL";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
}
