using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Logging;

namespace AdLibertataBot.Web.Handlers
{
    public class AdminHandler
    {
        private readonly ITelegramBotClient _bot;
        private readonly ILogger<AdminHandler> _logger;

        public AdminHandler(ITelegramBotClient bot, ILogger<AdminHandler> logger)
        {
            _bot = bot;
            _logger = logger;
        }

        // Этот метод больше не нужен, так как логика в BotOrchestrator
        // public async Task HandleAdminLoginAsync(string chatId, string text, CancellationToken cancellationToken) { }

        public static ReplyKeyboardMarkup GetAdminKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new("📊 Отчёт компании"), new("🏆 Топ сотрудников") },
                new KeyboardButton[] { new("👥 Статистика пользователей") },
                new KeyboardButton[] { new("🚪 Выйти из админки") }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }
    }
}