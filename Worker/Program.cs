using Application.Configuration;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Configuration;
using Infrastructure.Kafka;
using Infrastructure.Persistence;
using Serilog;
using Worker;

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/user-event-processor-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Запуск сервиса обработки событий пользователя");

    var builder = Host.CreateApplicationBuilder(args);

    // Добавляем Serilog
    builder.Services.AddSerilog();

    // Настройка конфигурации из appsettings.json
    builder.Services.Configure<KafkaSettings>(
        builder.Configuration.GetSection(KafkaSettings.SectionName));
    builder.Services.Configure<PostgreSqlSettings>(
        builder.Configuration.GetSection(PostgreSqlSettings.SectionName));
    builder.Services.Configure<EventProcessingSettings>(
        builder.Configuration.GetSection(EventProcessingSettings.SectionName));

    // Регистрация сервисов приложения
    builder.Services.AddSingleton<EventObservable>();
    builder.Services.AddSingleton<EventObserver>();

    // Регистрация сервисов инфраструктуры
    builder.Services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();
    builder.Services.AddSingleton<IEventRepository, EventRepository>();

    // Регистрация hosted service
    builder.Services.AddHostedService<UserEventProcessorWorker>();

    var host = builder.Build();

    // Инициализация схемы базы данных
    Log.Information("Инициализация схемы базы данных...");
    var repository = host.Services.GetRequiredService<IEventRepository>();
    await repository.InitializeDatabaseAsync();

    await host.RunAsync();

    Log.Information("Сервис обработки событий пользователя остановлен корректно");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Сервис обработки событий пользователя завершился с ошибкой");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
