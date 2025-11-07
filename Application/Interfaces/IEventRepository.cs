using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Репозиторий для работы со статистикой событий
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Добавляет или обновляет статистику
    /// </summary>
    Task UpsertAsync(UserEventStats stats, CancellationToken cancellationToken = default);

    /// <summary>
    /// Пакетное добавление/обновление статистики
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<UserEventStats> statsList, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статистику по пользователю и типу события
    /// </summary>
    Task<UserEventStats?> GetStatsAsync(int userId, string eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает всю статистику пользователя
    /// </summary>
    Task<IReadOnlyList<UserEventStats>> GetUserStatsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Инициализирует схему базы данных
    /// </summary>
    Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
}
