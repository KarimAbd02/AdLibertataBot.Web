using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Gamification
{
    public class AchievementService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<AchievementService> _logger;

        public AchievementService(DatabaseService db, ILogger<AchievementService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<string> GetUserAchievementsMessageAsync(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT a.icon, a.name, a.description, ua.unlocked_at
                FROM user_achievements ua
                JOIN achievements a ON ua.achievement_id = a.id
                WHERE ua.user_id = @user_id
                ORDER BY ua.unlocked_at DESC", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            
            var message = "🏆 Ваши достижения:\n\n";
            var count = 0;
            
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var icon = reader.GetString(0);
                var name = reader.GetString(1);
                var description = reader.GetString(2);
                var unlockedAt = reader.GetDateTime(3);
                
                message += icon + " " + name + "\n";
                message += "   " + description + "\n";
                message += "   Получено: " + unlockedAt.ToString("dd.MM.yyyy") + "\n\n";
                count++;
            }
            
            if (count == 0)
            {
                message += "У вас пока нет достижений.\nПродолжайте использовать альтернативы и выполнять челленджи! 💪";
            }
            
            return message;
        }

        public async Task InitializeAchievementsAsync()
        {
            var achievements = new[]
            {
                ("Первые шаги", "Выбрали первую альтернативу", "🌱", 1, "alternatives"),
                ("Осознанный выбор", "10 альтернатив выбрано", "🎯", 10, "alternatives"),
                ("Мастер альтернатив", "50 альтернатив выбрано", "⭐", 50, "alternatives"),
                ("Легенда альтернатив", "100 альтернатив выбрано", "👑", 100, "alternatives"),
                
                ("Новичок", "Набрано 100 очков", "📚", 100, "points"),
                ("Практикующий", "Набрано 500 очков", "🎓", 500, "points"),
                ("Эксперт", "Набрано 1000 очков", "🏆", 1000, "points"),
                ("Гуру", "Набрано 2000 очков", "🦉", 2000, "points"),
                
                ("Первый день", "Стрик 1 день", "🔥", 1, "streak"),
                ("Неделя силы", "Стрик 7 дней", "💪", 7, "streak"),
                ("Месяц триумфа", "Стрик 30 дней", "🎉", 30, "streak"),
                ("Квартал победы", "Стрик 90 дней", "🌟", 90, "streak")
            };

            await using var conn = await _db.CreateConnectionAsync();
            
            foreach (var (name, desc, icon, points, category) in achievements)
            {
                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO achievements (name, description, icon, points_required, category, is_active)
                    VALUES (@name, @desc, @icon, @points, @category, true)
                    ON CONFLICT DO NOTHING", 
                    conn);
                
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("desc", desc);
                cmd.Parameters.AddWithValue("icon", icon);
                cmd.Parameters.AddWithValue("points", points);
                cmd.Parameters.AddWithValue("category", category);
                
                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Achievements initialized");
        }
    }
}