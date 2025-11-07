namespace Domain.Entities;

/// <summary>
/// Событие пользователя из Kafka
/// </summary>
public sealed class UserEvent
{
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// Тип события (click, hover и т.д.)
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Дополнительные данные
    /// </summary>
    public EventData Data { get; init; } = new();

    /// <summary>
    /// Конструктор для десериализации
    /// </summary>
    public UserEvent() { }

    /// <summary>
    /// Создает новое событие пользователя
    /// </summary>
    public UserEvent(int userId, string eventType, DateTime timestamp, EventData data)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId должен быть больше нуля", nameof(userId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType не может быть пустым", nameof(eventType));

        UserId = userId;
        EventType = eventType;
        Timestamp = timestamp;
        Data = data;
    }
}

/// <summary>
/// Дополнительные данные события
/// </summary>
public sealed class EventData
{
    /// <summary>
    /// ID кнопки если применимо
    /// </summary>
    public string? ButtonId { get; init; }

    /// <summary>
    /// Дополнительные свойства для расширения
    /// </summary>
    public Dictionary<string, object> AdditionalProperties { get; init; } = new();
}
