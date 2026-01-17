using AdLibertataBot.Web.Services.Tracking;

namespace AdLibertataBot.Web.Services.Analytics
{
    public class StatsService
    {
        private readonly SmokeService _smoke;
        private readonly AlternativeService _alt;
        private readonly ILogger<StatsService> _logger;

        public StatsService(SmokeService smoke, AlternativeService alt, ILogger<StatsService> logger)
        {
            _smoke = smoke;
            _alt = alt;
            _logger = logger;
        }

        public async Task<string> GetStatsMessageAsync(string chatId)
        {
            try
            {
                var todaySmoke = await _smoke.GetCountAsync(chatId, "today");
                var weekSmoke = await _smoke.GetCountAsync(chatId, "week");
                var monthSmoke = await _smoke.GetCountAsync(chatId, "month");

                var todayAlt = await _alt.GetCountAsync(chatId, "today");
                var weekAlt = await _alt.GetCountAsync(chatId, "week");
                var monthAlt = await _alt.GetCountAsync(chatId, "month");

                var streak = await _smoke.GetStreakAsync(chatId);

                return
                    "📊 Статистика\n\n" +
                    "📅 Сегодня:\n" +
                    "🚬 Перекуров: " + todaySmoke + "\n" +
                    "🌿 Альтернатив: " + todayAlt + "\n\n" +
                    "🗓 За неделю:\n" +
                    "🚬 Перекуров: " + weekSmoke + "\n" +
                    "🌿 Альтернатив: " + weekAlt + "\n\n" +
                    "📆 За месяц:\n" +
                    "🚬 Перекуров: " + monthSmoke + "\n" +
                    "🔄 Альтернатив: " + monthAlt + "\n\n" +
                    "🔥 Streak (дней подряд с перекурами): " + streak + "\n";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats for {ChatId}", chatId);
                return "❌ Произошла ошибка при получении статистики. Попробуйте позже.";
            }
        }
    }
}