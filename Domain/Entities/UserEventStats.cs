namespace Domain.Entities;

/// <summary>
/// Агрегированная статистика событий пользователя
/// </summary>
public sealed class UserEventStats
{
    /// <summary>
    /// ID пользователя
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// Тип события
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Количество событий
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Конструктор для десериализации
    /// </summary>
    public UserEventStats() { }

    /// <summary>
    /// Создает новую статистику
    /// </summary>
    public UserEventStats(int userId, string eventType)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId должен быть больше нуля", nameof(userId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType не может быть пустым", nameof(eventType));

        UserId = userId;
        EventType = eventType;
        Count = 0;
    }

    /// <summary>
    /// Увеличивает счетчик на 1
    /// </summary>
    public void IncrementCount()
    {
        Count++;
    }

    /// <summary>
    /// Устанавливает значение счетчика (для загрузки из БД)
    /// </summary>
    /// <param name="count">Новое значение</param>
    public void SetCount(int count)
    {
        if (count < 0)
            throw new ArgumentException("Count не может быть отрицательным", nameof(count));

        Count = count;
    }

    public override bool Equals(object? obj)
    {
        return obj is UserEventStats stats &&
               UserId == stats.UserId &&
               EventType == stats.EventType;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UserId, EventType);
    }
}
