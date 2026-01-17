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

        public async Task HandleAdminLoginAsync(string chatId, string text, CancellationToken cancellationToken)
        {
            // Обработка ввода пароля администратора
            // После успешной проверки показываем админ-панель
            await ShowAdminPanel(chatId, cancellationToken);
        }

        private async Task ShowAdminPanel(string chatId, CancellationToken cancellationToken)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new("📊 Отчёт компании"), new("🏆 Топ сотрудников") },
                new KeyboardButton[] { new("👥 Статистика пользователей") },
                new KeyboardButton[] { new("🚪 Выйти из админки") }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            await _bot.SendTextMessageAsync(
                chatId,
                "👨‍💼 **Админ-панель**\n\n" +
                "Выберите действие:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

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