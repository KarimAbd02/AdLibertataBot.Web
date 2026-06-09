using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Analytics
{
    public class ProgressService
    {
        private readonly DatabaseService _db;
        private readonly VisualizationService _visualization;
        private readonly ILogger<ProgressService> _logger;

        public ProgressService(
            DatabaseService db, 
            VisualizationService visualization,
            ILogger<ProgressService> logger)
        {
            _db = db;
            _visualization = visualization;
            _logger = logger;
        }

        public async Task<string> GetProgressReportAsync(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            
            // Получаем общую статистику пользователя
            await using var cmd = new NpgsqlCommand(@"
                SELECT 
                    u.goal,
                    u.level,
                    u.points,
                    u.total_smokes,
                    u.total_alternatives,
                    u.current_streak,
                    u.best_streak,
                    COALESCE(
                        (SELECT COUNT(*) FROM smoke_events WHERE user_id = u.id 
                         AND event_time >= CURRENT_DATE - INTERVAL '7 days'), 0
                    ) as week_smokes,
                    COALESCE(
                        (SELECT COUNT(*) FROM smoke_events WHERE user_id = u.id 
                         AND event_time >= CURRENT_DATE - INTERVAL '14 days' 
                         AND event_time < CURRENT_DATE - INTERVAL '7 days'), 0
                    ) as prev_week_smokes,
                    COALESCE(
                        (SELECT COUNT(*) FROM alternative_events WHERE user_id = u.id 
                         AND event_time >= CURRENT_DATE - INTERVAL '7 days'), 0
                    ) as week_alternatives
                FROM users u
                WHERE u.id = @user_id", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return "Данные не найдены";

            var goal = (UserGoal)reader.GetInt32(0);
            var level = reader.GetInt32(1);
            var points = reader.GetInt32(2);
            var totalSmokes = reader.GetInt32(3);
            var totalAlternatives = reader.GetInt32(4);
            var currentStreak = reader.GetInt32(5);
            var bestStreak = reader.GetInt32(6);
            var weekSmokes = reader.GetInt32(7);
            var prevWeekSmokes = reader.GetInt32(8);
            var weekAlternatives = reader.GetInt32(9);

            // Расчет прогресса
            var totalActions = totalSmokes + totalAlternatives;
            var alternativeRate = totalActions > 0 
                ? (int)Math.Round(totalAlternatives * 100.0 / totalActions) 
                : 0;

            var smokeDelta = prevWeekSmokes - weekSmokes;
            var smokeDeltaPercent = prevWeekSmokes > 0 
                ? (int)Math.Round(smokeDelta * 100.0 / prevWeekSmokes) 
                : 0;

            // Прогресс бар для альтернатив
            var progressBar = _visualization.GenerateProgressBar(totalAlternatives, totalAlternatives + totalSmokes);

            // Формируем сообщение
            var message = "🎯 Ваш прогресс\n\n";
            message += $"📊 Уровень: {level} ⭐\n";
            message += $"💰 Очков: {points}\n\n";
            message += "🔥 Успех:\n";
            message += $"   Текущий: {currentStreak} дней\n";
            message += $"   Лучший: {bestStreak} дней\n\n";
            message += "📈 Всего за всё время:\n";
            message += $"🚬 Перекуров: {totalSmokes}\n";
            message += $"🌿 Альтернатив: {totalAlternatives}\n";
            message += $"✨ Процент альтернатив: {alternativeRate}%\n";
            message += $"{progressBar}\n\n";
            message += "📅 За последнюю неделю:\n";
            message += $"🚬 Перекуров: {weekSmokes}\n";
            message += $"🔄 Альтернатив: {weekAlternatives}\n";
            
            if (smokeDelta > 0)
                message += $"\n📉 Отлично! На {smokeDelta} перекуров меньше ({smokeDeltaPercent}% улучшение)\n";
            else if (smokeDelta < 0)
                message += $"\n📈 На {Math.Abs(smokeDelta)} перекуров больше. Не сдавайтесь!\n";
            else
                message += $"\n➡️ Стабильно, продолжайте работать над собой\n";

            message += "\n" + GetGoalProgress(goal, weekSmokes);
            
            return message;
        }

        private string GetGoalProgress(UserGoal goal, int weekSmokes)
        {
            return goal switch
            {
                UserGoal.QuitSmoking => 
                    weekSmokes == 0 
                        ? "🎉 Поздравляем! Эта неделя без перекуров!" 
                        : "🎯 Цель: полный отказ. Продолжайте заменять перекуры альтернативами!",
                
                UserGoal.ReduceSmoking => 
                    "🎯 Цель: снижение перекуров. Продолжайте в том же духе!",
                
                UserGoal.ReduceStress => 
                    "🎯 Цель: снижение стресса. Дыхательные техники помогают!",
                
                UserGoal.Tracking => 
                    "📊 Вы отслеживаете свои привычки. Осознанность — первый шаг!",
                
                _ => ""
            };
        }

        public async Task UpdateDailyStatsAsync(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO daily_stats (user_id, date, smoke_count, alternative_count, craving_count, total_points)
                SELECT 
                    @user_id,
                    CURRENT_DATE,
                    COALESCE((SELECT COUNT(*) FROM smoke_events WHERE user_id = @user_id AND event_time::date = CURRENT_DATE), 0),
                    COALESCE((SELECT COUNT(*) FROM alternative_events WHERE user_id = @user_id AND event_time::date = CURRENT_DATE), 0),
                    COALESCE((SELECT COUNT(*) FROM craving_events WHERE user_id = @user_id AND event_time::date = CURRENT_DATE), 0),
                    (SELECT points FROM users WHERE id = @user_id)
                ON CONFLICT (user_id, date) 
                DO UPDATE SET
                    smoke_count = EXCLUDED.smoke_count,
                    alternative_count = EXCLUDED.alternative_count,
                    craving_count = EXCLUDED.craving_count,
                    total_points = EXCLUDED.total_points", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}