# User Event Processor

Сервис для обработки событий пользователей из Kafka и сохранения статистики в PostgreSQL.

## Что использовал

- .NET 9
- Apache Kafka (Confluent.Kafka)
- PostgreSQL
- Паттерн Observer (IObservable/IObserver)
- Clean Architecture
- Docker Compose

## Структура

Проект разбит на слои по Clean Architecture:
- **Domain** - доменные сущности (UserEvent, UserEventStats)
- **Application** - бизнес-логика (Observer, интерфейсы)
- **Infrastructure** - Kafka consumer, PostgreSQL repository
- **Worker** - точка входа, background service
- **Tests** - unit тесты (38 штук)

## Как запустить

Нужен Docker и .NET 9 SDK.

### Вариант 1: Все в Docker

```bash
docker-compose up --build
```

Worker запустится автоматически вместе с Kafka и PostgreSQL.

### Вариант 2: Worker локально

```bash
# Запускаем только инфраструктуру
docker-compose up -d kafka postgres zookeeper kafka-ui

# Worker запускаем локально
dotnet run --project Worker/Worker.csproj
```

Что будет доступно:
- Kafka UI: http://localhost:8080
- PostgreSQL: localhost:5432
- Kafka: localhost:9092

## Быстрая проверка работоспособности

После запуска `docker-compose up --build` выполни:

### 1. Создать топик (если не создался автоматически)

```bash
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic user-events --partitions 3 --replication-factor 1
```


### 2. Проверить что Worker подключился

```bash
docker-compose logs worker | grep "Партиции назначены"
```

### 3. Отправить тестовое событие

```bash
echo '{"userId":123,"eventType":"click","timestamp":"2025-01-07T00:00:00Z","data":{"buttonId":"submit"}}' | \
  docker exec -i kafka kafka-console-producer --bootstrap-server localhost:9092 --topic user-events
```

### 4. Подождать 15 секунд (flush происходит каждые 10 секунд)

```bash
sleep 15
```

### 5. Проверить результат в PostgreSQL

```bash
docker exec postgres psql -U postgres -d usereventdb -c "SELECT * FROM user_event_stats;"
```

**Ожидаемый результат:**
```
 user_id | event_type | count
---------|------------|-------
     123 | click      |     1
```

### 6. Отправить еще события для проверки агрегации

```bash
# Еще один click от того же пользователя
echo '{"userId":123,"eventType":"click","timestamp":"2025-01-07T00:00:00Z","data":{}}' | \
  docker exec -i kafka kafka-console-producer --bootstrap-server localhost:9092 --topic user-events

# Hover событие
echo '{"userId":123,"eventType":"hover","timestamp":"2025-01-07T00:00:00Z","data":{}}' | \
  docker exec -i kafka kafka-console-producer --bootstrap-server localhost:9092 --topic user-events

# Событие от другого пользователя
echo '{"userId":456,"eventType":"click","timestamp":"2025-01-07T00:00:00Z","data":{}}' | \
  docker exec -i kafka kafka-console-producer --bootstrap-server localhost:9092 --topic user-events
```

Подожди еще 15 секунд и проверь:
```bash
sleep 15
docker exec postgres psql -U postgres -d usereventdb -c "SELECT * FROM user_event_stats ORDER BY user_id, event_type;"
```

**Ожидаемый результат:**
```
 user_id | event_type | count
---------|------------|-------
     123 | click      |     2
     123 | hover      |     1
     456 | click      |     1
```

---

## Проверка через Kafka UI

1. Открой http://localhost:8080
2. Topics → user-events
3. Нажми "Produce Message"
4. Вставь JSON и отправь
5. Через 10-15 секунд проверь PostgreSQL

---

## Запуск тестов

```bash
dotnet test
```

Все 38 тестов должны пройти.

## Конфигурация

Основные настройки в `Worker/appsettings.json`:
- Kafka topic: `user-events`
- PostgreSQL: `usereventdb`
- Flush интервал: 10 секунд

Можно переопределить через переменные окружения (формат `Kafka__BootstrapServers`).

## Что реализовано

- Observer Pattern для обработки событий
- Thread-safe агрегация через ConcurrentDictionary
- Batch запись в PostgreSQL (каждые 10 секунд)
- Manual offset management в Kafka (At-Least-Once)
- Retry с exponential backoff для БД
- Graceful shutdown
- Логи в консоль и файлы (Serilog)

## Остановка

```bash
docker-compose down
```

Или с очисткой данных:
```bash
docker-compose down -v
```
