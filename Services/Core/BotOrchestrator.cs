using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using AdLibertataBot.Web.Handlers;
using AdLibertataBot.Web.Services.User;
using Microsoft.Extensions.Logging;

namespace AdLibertataBot.Web.Services.Core
{
    public class BotOrchestrator
    {
        private readonly ITelegramBotClient _bot;
        private readonly CommandHandler _commandHandler;
        private readonly OnboardingHandler _onboardingHandler;
        private readonly TrackingHandler _trackingHandler;
        private readonly AdminHandler _adminHandler;
        private readonly UserStateService _stateService;
        private readonly UserService _userService;
        private readonly ILogger<BotOrchestrator> _logger;

        public BotOrchestrator(
            ITelegramBotClient bot,
            CommandHandler commandHandler,
            OnboardingHandler onboardingHandler,
            TrackingHandler trackingHandler,
            AdminHandler adminHandler,
            UserStateService stateService,
            UserService userService,
            ILogger<BotOrchestrator> logger)
        {
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

                // 2. Проверяем, есть ли пользователь в БД
                var userExists = await _userService.UserExistsAsync(chatId);
                var state = await _stateService.GetUserStateAsync(chatId);

                // 3. Логика
                if (userExists)
                {
                    // Пользователь уже зарегистрирован
                    if (state != null)
                    {
                        // Есть состояние (возможно, повторная регистрация) - OnboardingHandler
                        await _onboardingHandler.HandleAsync(chatId, text, cancellationToken);
                    }
                    else
                    {
                        // Нет состояния - обычная работа через TrackingHandler
                        await _trackingHandler.HandleAsync(chatId, text, cancellationToken);
                    }
                }
                else
                {
                    // Пользователь не зарегистрирован - OnboardingHandler
                    await _onboardingHandler.HandleAsync(chatId, text, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update");
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