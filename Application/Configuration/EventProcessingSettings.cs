namespace Application.Configuration;

/// <summary>
/// Настройки обработки событий
/// </summary>
public sealed class EventProcessingSettings
{
    public const string SectionName = "EventProcessing";

    /// <summary>
    /// Интервал сброса статистики в БД (в секундах)
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 10;
}
