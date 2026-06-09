using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using AdLibertataBot.Web;
using AdLibertataBot.Web.Services.Core;
using AdLibertataBot.Web.Services.User;
using AdLibertataBot.Web.Services.Company;
using AdLibertataBot.Web.Services.Tracking;
using AdLibertataBot.Web.Services.Content;
using AdLibertataBot.Web.Services.Gamification;
using AdLibertataBot.Web.Services.Analytics;
using AdLibertataBot.Web.Services.Diagnostics;
using AdLibertataBot.Web.Handlers;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

// Логирование
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Конфигурация бота
var botToken = builder.Configuration["BotToken"]
    ?? throw new ArgumentException("BotToken не настроен");

var supabaseConnection = builder.Configuration["SupabaseConnection"]
    ?? throw new ArgumentException("SupabaseConnection не настроен");

// Telegram Bot Client
builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(botToken));

// === РЕГИСТРАЦИЯ ВСЕХ СЕРВИСОВ ===

builder.Services.AddSingleton<BotConfig>(_ => new BotConfig
{
    BotToken = botToken,
    SupabaseConnection = supabaseConnection
});

// Core Services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<MessageService>();

// User Services
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<UserStateService>();
builder.Services.AddSingleton<OnboardingService>();

// Company Services
builder.Services.AddSingleton<CompanyService>();
builder.Services.AddSingleton<CompanyAdminService>();
builder.Services.AddSingleton<CompanyReportService>();

// Tracking Services
builder.Services.AddSingleton<CravingService>();
builder.Services.AddSingleton<SmokeService>();
builder.Services.AddSingleton<AlternativeService>();

// Content Services
builder.Services.AddSingleton<MotivationService>();
builder.Services.AddSingleton<BreathingTechniqueService>();
builder.Services.AddSingleton<FactService>();
builder.Services.AddSingleton<VoiceService>();

// Analytics Services
builder.Services.AddSingleton<StatsService>();
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddSingleton<VisualizationService>();

// Gamification Services
builder.Services.AddSingleton<PointsService>();
builder.Services.AddSingleton<ChallengeService>();
builder.Services.AddSingleton<AchievementService>();

// Diagnostics Services
builder.Services.AddSingleton<DiagnosticService>();
builder.Services.AddSingleton<FagerstromTestService>();

// Handlers
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton<OnboardingHandler>();
builder.Services.AddSingleton<TrackingHandler>();
builder.Services.AddSingleton<AdminHandler>();

builder.Services.AddSingleton<BotOrchestrator>(sp =>
{
    return new BotOrchestrator(
        sp,
        sp.GetRequiredService<ITelegramBotClient>(),
        sp.GetRequiredService<CommandHandler>(),
        sp.GetRequiredService<OnboardingHandler>(),
        sp.GetRequiredService<TrackingHandler>(),
        sp.GetRequiredService<AdminHandler>(),
        sp.GetRequiredService<UserStateService>(),
        sp.GetRequiredService<UserService>(),
        sp.GetRequiredService<ILogger<BotOrchestrator>>()
    );
});

var app = builder.Build();

try
{
    Console.WriteLine("╔════════════════════════════════════════════╗");
    Console.WriteLine("║    🚀 Ad Libertata Bot запускается...     ║");
    Console.WriteLine("╚════════════════════════════════════════════╝");

    Console.WriteLine("✅ Конфигурация загружена успешно");

    // Проверка подключения к БД с ретраями
    Console.WriteLine("🔌 Проверка подключения к базе данных...");
    using (var scope = app.Services.CreateScope())
    {
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var maxRetries = 3;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var connection = await dbService.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1", connection);
                var result = await cmd.ExecuteScalarAsync();
                Console.WriteLine($"✅ Подключение к БД успешно (попытка {i + 1})");
                break;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1)
                {
                    Console.WriteLine($"❌ Ошибка подключения к БД после {maxRetries} попыток: {ex.Message}");

                    Console.WriteLine("\n🔍 Диагностика подключения:");
                    Console.WriteLine("   SupabaseConnection загружен: " + (!string.IsNullOrWhiteSpace(supabaseConnection) ? "да" : "нет"));

                    throw;
                }

                Console.WriteLine($"⚠️  Попытка {i + 1} не удалась, повтор через 2 секунды...");
                await Task.Delay(2000);
            }
        }
    }

    // Проверка бота
    Console.WriteLine("🤖 Проверка авторизации бота...");
    using (var scope = app.Services.CreateScope())
    {
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        try
        {
            var me = await botClient.GetMeAsync();
            Console.WriteLine("✅ Бот авторизован: @" + me.Username + " (ID: " + me.Id + ")");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка авторизации бота: {ex.Message}");
            throw;
        }
    }

    // Инициализация данных с задержками между сервисами
    Console.WriteLine("🔄 Инициализация данных...");
    using (var scope = app.Services.CreateScope())
    {
        var breathingService = scope.ServiceProvider.GetRequiredService<BreathingTechniqueService>();
        await Task.Delay(500);
        await breathingService.InitializeTechniquesAsync();

        var factService = scope.ServiceProvider.GetRequiredService<FactService>();
        await Task.Delay(500);

        try
        {
            await factService.InitializeFactsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Факты не загружены: {ex.Message}");
        }

        var achievementService = scope.ServiceProvider.GetRequiredService<AchievementService>();
        await Task.Delay(500);
        // await achievementService.InitializeAchievementsAsync();
    }

    Console.WriteLine("✅ Данные инициализированы");

    // Запуск бота
    Console.WriteLine("⏳ Запуск обработчика сообщений...");
    using (var scope = app.Services.CreateScope())
    {
        var orchestrator = scope.ServiceProvider.GetRequiredService<BotOrchestrator>();
        Task.Run(() => orchestrator.StartAsync(app.Lifetime.ApplicationStopping));
        Console.WriteLine("✅ Обработчик сообщений активирован");
    }

    Console.WriteLine("\n╔════════════════════════════════════════════╗");
    Console.WriteLine("║   🎉 Ad Libertata Bot готов к работе! 🎉   ║");
    Console.WriteLine("╚════════════════════════════════════════════╝");
    Console.WriteLine("📱 Отправьте /start в бота: @AdLibertataBot");
    Console.WriteLine("🌐 Здоровье API: http://localhost:5000/health");
    Console.WriteLine($"🔄 Режим: {app.Environment.EnvironmentName}");
    Console.WriteLine("👂 Слушаю обновления от Telegram...\n");

    // Минимальные маршруты для веб-сервера
    app.MapGet("/", () => "Ad Libertata Bot API");

    app.MapGet("/health", () =>
    {
        return Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "Ad Libertata Bot"
        });
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine("\n💥 Критическая ошибка при запуске:");
    Console.WriteLine($"   {ex.GetType().Name}: {ex.Message}");

    if (ex.InnerException != null)
        Console.WriteLine($"   Причина: {ex.InnerException.Message}");

    if (ex.Message.Contains("Supabase") || ex.Message.Contains("PostgreSQL") || ex.Message.Contains("Npgsql"))
    {
        Console.WriteLine("\n🔍 Советы по устранению:");
        Console.WriteLine("   1. Проверьте SupabaseConnection в user-secrets или переменных окружения");
        Console.WriteLine("   2. Убедитесь, что пароль от Supabase правильный");
        Console.WriteLine("   3. Проверьте, не закончился ли лимит подключений в Supabase");
    }

    Console.WriteLine("\n❌ Приложение остановлено");
    Environment.Exit(1);
}