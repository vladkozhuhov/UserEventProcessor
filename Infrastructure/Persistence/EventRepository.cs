using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Реализация репозитория PostgreSQL для статистики событий пользователя
/// Включает retry логику, connection pooling и upsert операции
/// </summary>
public sealed class EventRepository : IEventRepository
{
    private readonly PostgreSqlSettings _settings;
    private readonly ILogger<EventRepository> _logger;

    public EventRepository(
        IOptions<PostgreSqlSettings> settings,
        ILogger<EventRepository> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task UpsertAsync(UserEventStats stats, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO user_event_stats (user_id, event_type, count)
            VALUES (@UserId, @EventType, @Count)
            ON CONFLICT (user_id, event_type)
            DO UPDATE SET count = user_event_stats.count + EXCLUDED.count;";

        await ExecuteWithRetryAsync(async () =>
        {
            await using var connection = new NpgsqlConnection(_settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = _settings.CommandTimeout
            };

            command.Parameters.AddWithValue("@UserId", stats.UserId);
            command.Parameters.AddWithValue("@EventType", stats.EventType);
            command.Parameters.AddWithValue("@Count", stats.Count);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug(
                "Upsert статистики: UserId={UserId}, EventType={EventType}, Count={Count}",
                stats.UserId,
                stats.EventType,
                stats.Count);
        }, cancellationToken);
    }

    public async Task UpsertBatchAsync(IEnumerable<UserEventStats> statsList, CancellationToken cancellationToken = default)
    {
        var statsArray = statsList as UserEventStats[] ?? statsList.ToArray();

        if (statsArray.Length == 0)
            return;

        const string sql = @"
            INSERT INTO user_event_stats (user_id, event_type, count)
            VALUES (@UserId, @EventType, @Count)
            ON CONFLICT (user_id, event_type)
            DO UPDATE SET count = user_event_stats.count + EXCLUDED.count;";

        await ExecuteWithRetryAsync(async () =>
        {
            await using var connection = new NpgsqlConnection(_settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var stats in statsArray)
                {
                    await using var command = new NpgsqlCommand(sql, connection, transaction)
                    {
                        CommandTimeout = _settings.CommandTimeout
                    };

                    command.Parameters.AddWithValue("@UserId", stats.UserId);
                    command.Parameters.AddWithValue("@EventType", stats.EventType);
                    command.Parameters.AddWithValue("@Count", stats.Count);

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Успешно выполнен batch upsert {Count} статистик", statsArray.Length);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<UserEventStats?> GetStatsAsync(int userId, string eventType, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT user_id, event_type, count
            FROM user_event_stats
            WHERE user_id = @UserId AND event_type = @EventType;";

        return await ExecuteWithRetryAsync(async () =>
        {
            await using var connection = new NpgsqlConnection(_settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = _settings.CommandTimeout
            };

            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@EventType", eventType);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var stats = new UserEventStats(
                    reader.GetInt32(0),
                    reader.GetString(1));
                stats.SetCount(reader.GetInt32(2));
                return stats;
            }

            return null;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<UserEventStats>> GetUserStatsAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT user_id, event_type, count
            FROM user_event_stats
            WHERE user_id = @UserId
            ORDER BY event_type;";

        return await ExecuteWithRetryAsync(async () =>
        {
            await using var connection = new NpgsqlConnection(_settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = _settings.CommandTimeout
            };

            command.Parameters.AddWithValue("@UserId", userId);

            var results = new List<UserEventStats>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var stats = new UserEventStats(
                    reader.GetInt32(0),
                    reader.GetString(1));
                stats.SetCount(reader.GetInt32(2));
                results.Add(stats);
            }

            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Выполняет операцию с БД с retry логикой и exponential backoff
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(_settings.RetryDelaySeconds);

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (NpgsqlException ex) when (retryCount < _settings.MaxRetryCount && IsTransientError(ex))
            {
                retryCount++;
                _logger.LogWarning(ex,
                    "Временная ошибка БД. Попытка повтора {RetryCount} из {MaxRetryCount}",
                    retryCount,
                    _settings.MaxRetryCount);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Операция с БД не удалась");
                throw;
            }
        }
    }

    private async Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return 0;
        }, cancellationToken);
    }

    /// <summary>
    /// Определяет, является ли исключение временной ошибкой, которую можно повторить
    /// </summary>
    private static bool IsTransientError(NpgsqlException ex)
    {
        // Ошибки подключения, которые обычно временные
        return ex.IsTransient ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Инициализирует схему базы данных (создает таблицу если не существует)
    /// </summary>
    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS user_event_stats (
                user_id INT NOT NULL,
                event_type VARCHAR(50) NOT NULL,
                count INT NOT NULL,
                PRIMARY KEY (user_id, event_type)
            );

            CREATE INDEX IF NOT EXISTS idx_user_event_stats_user_id
            ON user_event_stats(user_id);";

        try
        {
            await using var connection = new NpgsqlConnection(_settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = _settings.CommandTimeout
            };

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Схема базы данных успешно инициализирована");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось инициализировать схему базы данных");
            throw;
        }
    }
}
