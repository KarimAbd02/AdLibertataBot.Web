using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;
using AdLibertataBot.Web.Services.User;
using AdLibertataBot.Web.Services.Analytics;
using AdLibertataBot.Web.Services.Content;
using AdLibertataBot.Web.Services.Gamification;
using Microsoft.Extensions.Logging;

namespace AdLibertataBot.Web.Handlers
{
    public class CommandHandler
    {
        private readonly ITelegramBotClient _bot;
        private readonly UserService _userService;
        private readonly UserStateService _stateService;
        private readonly OnboardingHandler _onboardingHandler; // Добавь эту строку
        private readonly ProgressService _progressService;
        private readonly FactService _factService;
        private readonly PointsService _pointsService;
        private readonly AchievementService _achievementService;
        private readonly ILogger<CommandHandler> _logger;

        public CommandHandler(
            ITelegramBotClient bot,
            UserService userService,
            UserStateService stateService,
            OnboardingHandler onboardingHandler, // Добавь параметр
            ProgressService progressService,
            FactService factService,
            PointsService pointsService,
            AchievementService achievementService,
            ILogger<CommandHandler> logger)
        {
            _bot = bot;
            _userService = userService;
            _stateService = stateService;
            _onboardingHandler = onboardingHandler; // Инициализируй
            _progressService = progressService;
            _factService = factService;
            _pointsService = pointsService;
            _achievementService = achievementService;
            _logger = logger;
        }

        public async Task HandleAsync(string chatId, string command, CancellationToken cancellationToken)
        {
            try
            {
                switch (command.ToLower())
                {
                    case "/start":
                        await HandleStartAsync(chatId, cancellationToken);
                        break;
                    
                    case "/help":
                        await HandleHelpAsync(chatId, cancellationToken);
                        break;
                    
                    case "/stats":
                        await HandleStatsCommandAsync(chatId, cancellationToken);
                        break;
                    
                    case "/achievements":
                        await HandleAchievementsAsync(chatId, cancellationToken);
                        break;
                    
                    case "/points":
                        await HandlePointsAsync(chatId, cancellationToken);
                        break;
                    
                    default:
                        await _bot.SendTextMessageAsync(
                            chatId,
                            "Пожалуйста, используйте кнопки меню 👇",
                            replyMarkup: GetMainKeyboard(),
                            cancellationToken: cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling command {command}");
                await _bot.SendTextMessageAsync(
                    chatId,
                    "❌ Произошла ошибка. Попробуйте позже.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task HandleStartAsync(string chatId, CancellationToken cancellationToken)
        {
            // Всегда очищаем состояние
            await _stateService.DeleteUserStateAsync(chatId);
    
            // Всегда показываем выбор роли
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new("👤 Пользователь"), new("👨‍💼 Администратор") }
            })
            {
                ResizeKeyboard = true
            };
    
            await _bot.SendTextMessageAsync(
                chatId,
                "👋 Добро пожаловать в Ad Libertata Work!\n\nВыберите вашу роль:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
    
            // Создаем состояние для выбора роли
            var state = new UserState
            {
                ChatId = chatId,
                Step = OnboardingStep.RoleSelection,
                LastUpdated = DateTime.UtcNow
            };
    
            await _stateService.SaveFullStateAsync(state);
        }

        private async Task HandleHelpAsync(string chatId, CancellationToken cancellationToken)
        {
            var helpText = "ℹ️ Помощь\n\n" +
                          "🚬 Тяга к перекуру - зафиксировать желание перекурить\n" +
                          "🔄 Альтернатива - выбрать здоровую замену\n" +
                          "📊 Статистика - ваша статистика\n" +
                          "🎯 Мой прогресс - детальный прогресс\n" +
                          "🏆 Челлендж - взять новое задание\n" +
                          "💫 Мотивация - получить поддержку\n\n" +
                          "Команды:\n" +
                          "/start - главное меню\n" +
                          "/stats - статистика\n" +
                          "/achievements - достижения\n" +
                          "/points - информация об очках\n" +
                          "/help - эта справка";
            
            await _bot.SendTextMessageAsync(
                chatId,
                helpText,
                replyMarkup: GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleStatsCommandAsync(string chatId, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserAsync(chatId);
            if (user == null)
            {
                await _bot.SendTextMessageAsync(
                    chatId, 
                    "Сначала зарегистрируйтесь через /start", 
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);
                return;
            }

            var progress = await _progressService.GetProgressReportAsync(user.Id);
            await _bot.SendTextMessageAsync(
                chatId,
                progress,
                replyMarkup: GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleAchievementsAsync(string chatId, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserAsync(chatId);
            if (user == null)
            {
                await _bot.SendTextMessageAsync(
                    chatId, 
                    "Сначала зарегистрируйтесь через /start", 
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);
                return;
            }

            var achievements = await _achievementService.GetUserAchievementsMessageAsync(user.Id);
            await _bot.SendTextMessageAsync(
                chatId,
                achievements,
                replyMarkup: GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandlePointsAsync(string chatId, CancellationToken cancellationToken)
        {
            var explanation = await _pointsService.GetPointsExplanationAsync();
            await _bot.SendTextMessageAsync(
                chatId,
                explanation,
                replyMarkup: GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        public static ReplyKeyboardMarkup GetMainKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new("Тяга к перекуру 🚬"), new("Альтернатива 🔄") },
                new KeyboardButton[] { new("Статистика 📊"), new("Мой прогресс 🎯") },
                new KeyboardButton[] { new("Челлендж 🏆"), new("Мотивация 💫") },
                new KeyboardButton[] { new("🚪 Выйти") }
            })
            { 
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }

        public static ReplyKeyboardMarkup GetStartKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new("/start") }
            })
            { 
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }
    }
}