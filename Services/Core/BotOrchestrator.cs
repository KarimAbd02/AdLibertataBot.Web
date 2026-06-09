using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using AdLibertataBot.Web.Handlers;
using AdLibertataBot.Web.Services.User;
using AdLibertataBot.Web.Services.Company;
using AdLibertataBot.Web.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AdLibertataBot.Web.Services.Core
{
    public class BotOrchestrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITelegramBotClient _bot;
        private readonly CommandHandler _commandHandler;
        private readonly OnboardingHandler _onboardingHandler;
        private readonly TrackingHandler _trackingHandler;
        private readonly AdminHandler _adminHandler;
        private readonly UserStateService _stateService;
        private readonly UserService _userService;
        private readonly ILogger<BotOrchestrator> _logger;

        public BotOrchestrator(
            IServiceProvider serviceProvider,
            ITelegramBotClient bot,
            CommandHandler commandHandler,
            OnboardingHandler onboardingHandler,
            TrackingHandler trackingHandler,
            AdminHandler adminHandler,
            UserStateService stateService,
            UserService userService,
            ILogger<BotOrchestrator> logger)
        {
            _serviceProvider = serviceProvider;
            _bot = bot;
            _commandHandler = commandHandler;
            _onboardingHandler = onboardingHandler;
            _trackingHandler = trackingHandler;
            _adminHandler = adminHandler;
            _stateService = stateService;
            _userService = userService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("🤖 Starting Bot Orchestrator...");

                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>(),
                    ThrowPendingUpdates = true
                };

                _logger.LogInformation("📡 Starting polling...");

                _bot.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    pollingErrorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("✅ Bot Orchestrator started. Waiting for updates...");

                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 Bot Orchestrator cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Fatal error in Bot Orchestrator");
                throw;
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type != UpdateType.Message || update.Message?.Text == null)
                    return;

                var chatId = update.Message.Chat.Id.ToString();
                var text = update.Message.Text;

                _logger.LogInformation($"Message from {chatId}: '{text}'");

                // 1. Проверяем команды
                if (text.StartsWith("/"))
                {
                    await _commandHandler.HandleAsync(chatId, text, cancellationToken);
                    return;
                }

                // 2. Проверяем, не является ли это командами админской клавиатуры
                if (IsAdminCommand(text))
                {
                    await HandleAdminCommandAsync(chatId, text, cancellationToken);
                    return;
                }

                // 3. Дальше обычная логика
                var userExists = await _userService.UserExistsAsync(chatId);
                var state = await _stateService.GetUserStateAsync(chatId);

                if (userExists)
                {
                    if (state != null)
                    {
                        await _onboardingHandler.HandleAsync(chatId, text, cancellationToken);
                    }
                    else
                    {
                        await _trackingHandler.HandleAsync(chatId, text, cancellationToken);
                    }
                }
                else
                {
                    await _onboardingHandler.HandleAsync(chatId, text, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update");
            }
        }

        private bool IsAdminCommand(string text)
        {
            var adminCommands = new[]
            {
                "📊 Отчёт компании",
                "🏆 Топ сотрудников", 
                "👥 Статистика пользователей",
                "🚪 Выйти из админки"
            };
            
            return adminCommands.Contains(text);
        }

        private async Task HandleAdminCommandAsync(string chatId, string text, CancellationToken cancellationToken)
        {
            // Получаем состояние пользователя
            var state = await _stateService.GetUserStateAsync(chatId);
            
            // Если нет состояния или это не админ - просим войти
            if (state == null || state.Role != UserRole.Admin || state.CompanyId == null)
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "❌ Пожалуйста, войдите как администратор через /start",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);
                return;
            }

            // Получаем сервисы через ServiceProvider
            using var scope = _serviceProvider.CreateScope();
            var adminService = scope.ServiceProvider.GetRequiredService<CompanyAdminService>();
            var companyReportService = scope.ServiceProvider.GetRequiredService<CompanyReportService>();
            
            // Проверяем пароль админа
            var admin = await adminService.GetAdminByCompanyIdAsync(state.CompanyId.Value);
            if (admin == null)
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "❌ Администратор не найден",
                    cancellationToken: cancellationToken);
                return;
            }

            // Обрабатываем команды админа
            switch (text)
            {
                case "📊 Отчёт компании":
                    var report = await companyReportService.GenerateCompanyReportAsync(state.CompanyId.Value);
                    await _bot.SendTextMessageAsync(
                        chatId,
                        report,
                        replyMarkup: AdminHandler.GetAdminKeyboard(),
                        cancellationToken: cancellationToken);
                    break;
                    
                case "🏆 Топ сотрудников":
                    var topUsers = await companyReportService.GetTopUsersAsync(state.CompanyId.Value);
                    await _bot.SendTextMessageAsync(
                        chatId,
                        topUsers,
                        replyMarkup: AdminHandler.GetAdminKeyboard(),
                        cancellationToken: cancellationToken);
                    break;
                    
                case "👥 Статистика пользователей":
                    var userCount = await companyReportService.GetCompanyUserCountAsync(state.CompanyId.Value);
                    await _bot.SendTextMessageAsync(
                        chatId,
                        $"👥 Статистика пользователей\n\n📊 Всего пользователей: {userCount}",
                        replyMarkup: AdminHandler.GetAdminKeyboard(),
                        cancellationToken: cancellationToken);
                    break;
                    
                case "🚪 Выйти из админки":
                    await _stateService.DeleteUserStateAsync(chatId);
                    await _bot.SendTextMessageAsync(
                        chatId,
                        "✅ Вы вышли из админ-панели",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken);
                    break;
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiEx => $"Telegram API Error [{apiEx.ErrorCode}]: {apiEx.Message}",
                OperationCanceledException => "Polling was cancelled",
                _ => $"{exception.GetType().Name}: {exception.Message}"
            };

            _logger.LogError(errorMessage);
            return Task.CompletedTask;
        }
    }
}