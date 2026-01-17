using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Gamification
{
    public class PointsService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<PointsService> _logger;

        // Награды за действия
        public const int POINTS_CRAVING_POSTPONED_2MIN = 5;
        public const int POINTS_CRAVING_POSTPONED_5MIN = 10;
        public const int POINTS_ALTERNATIVE_BASIC = 10;
        public const int POINTS_ALTERNATIVE_ADVANCED = 15;
        public const int POINTS_BREATHING_TECHNIQUE = 15;
        public const int POINTS_CHALLENGE_COMPLETED = 50;
        public const int POINTS_DAILY_STREAK = 20;
        public const int POINTS_FACT_READ = 5;

        public PointsService(DatabaseService db, ILogger<PointsService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task AwardPointsAsync(int userId, int points, string reason)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE users 
                SET points = points + @points,
                    last_activity = NOW()
                WHERE id = @user_id", 
                conn);
            
            cmd.Parameters.AddWithValue("points", points);
            cmd.Parameters.AddWithValue("user_id", userId);
            await cmd.ExecuteNonQueryAsync();

            await CheckLevelUpAsync(userId);
            _logger.LogInformation($"User {userId} awarded {points} points for {reason}");
        }

        private async Task CheckLevelUpAsync(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var getCmd = new NpgsqlCommand(
                "SELECT points, level FROM users WHERE id = @user_id", 
                conn);
            
            getCmd.Parameters.AddWithValue("user_id", userId);
            
            await using var reader = await getCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return;
            
            var points = reader.GetInt32(0);
            var currentLevel = reader.GetInt32(1);
            await reader.CloseAsync();

            var newLevel = (points / 100) + 1;
            
            if (newLevel > currentLevel)
            {
                await using var updateCmd = new NpgsqlCommand(
                    "UPDATE users SET level = @level WHERE id = @user_id", 
                    conn);
                
                updateCmd.Parameters.AddWithValue("level", newLevel);
                updateCmd.Parameters.AddWithValue("user_id", userId);
                await updateCmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"User {userId} leveled up to {newLevel}");
            }
        }

        public async Task<(int points, int level, string levelName)> GetUserStatsAsync(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT points, level FROM users WHERE id = @user_id", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var points = reader.GetInt32(0);
                var level = reader.GetInt32(1);
                var levelName = GetLevelName(level);
                return (points, level, levelName);
            }
            
            return (0, 1, "Новичок");
        }

        public string GetLevelName(int level)
        {
            return level switch
            {
                1 => "Новичок 🌱",
                2 => "Ученик 📚",
                3 => "Практикующий 🎯",
                4 => "Осознанный 🧘",
                5 => "Уверенный 💪",
                6 => "Мастер 🎓",
                7 => "Эксперт ⭐",
                8 => "Гуру 🏆",
                9 => "Мудрец 🦉",
                >= 10 => "Легенда 👑",
                _ => "Новичок 🌱"
            };
        }

        public async Task<int> CalculatePointsForAlternativeAsync(AlternativeType type)
        {
            return type switch
            {
                AlternativeType.Breathing => POINTS_BREATHING_TECHNIQUE,
                AlternativeType.Water => POINTS_ALTERNATIVE_BASIC,
                AlternativeType.Exercise => POINTS_ALTERNATIVE_BASIC,
                AlternativeType.Walk => POINTS_ALTERNATIVE_BASIC,
                AlternativeType.FiveSenses => POINTS_ALTERNATIVE_ADVANCED,
                AlternativeType.Relaxation => POINTS_ALTERNATIVE_ADVANCED,
                _ => POINTS_ALTERNATIVE_BASIC
            };
        }

        public async Task<string> GetPointsExplanationAsync()
        {
            return "💰 Система очков:\n\n" +
                   "⏰ Отложить на 2 мин: +" + POINTS_CRAVING_POSTPONED_2MIN + "\n" +
                   "⏰ Отложить на 5 мин: +" + POINTS_CRAVING_POSTPONED_5MIN + "\n" +
                   "🔄 Альтернатива (простая): +" + POINTS_ALTERNATIVE_BASIC + "\n" +
                   "🫁 Дыхательная техника: +" + POINTS_BREATHING_TECHNIQUE + "\n" +
                   "🏆 Челлендж завершен: +" + POINTS_CHALLENGE_COMPLETED + "\n" +
                   "🔥 Дневной стрик: +" + POINTS_DAILY_STREAK + "\n" +
                   "💡 Прочитан факт: +" + POINTS_FACT_READ + "\n\n" +
                   "Каждые 100 очков = новый уровень! 🎯";
        }
    }
}