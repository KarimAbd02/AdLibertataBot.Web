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
                
                message += $"{icon} **{name}**\n";
                message += $"   {description}\n";
                message += $"   Получено: {unlockedAt:dd.MM.yyyy}\n\n";
                count++;
            }
            
            if (count == 0)
            {
                message += "У вас пока нет достижений.\nПродолжайте использовать альтернативы и выполнять челленджи! 💪";
            }
            
            return message;
        }

        public async Task CheckAndAwardAchievementsAsync(int userId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                
                // Получаем статистику пользователя
                await using var userStatsCmd = new NpgsqlCommand(@"
                    SELECT 
                        u.points,
                        u.total_alternatives,
                        u.current_streak,
                        u.best_streak,
                        COALESCE((SELECT COUNT(*) FROM user_challenges WHERE user_id = u.id AND status = 2), 0) as completed_challenges,
                        COALESCE((SELECT COUNT(*) FROM alternative_events WHERE user_id = u.id AND alternative_type = 1), 0) as breathing_count,
                        COALESCE((SELECT COUNT(*) FROM alternative_events WHERE user_id = u.id AND alternative_type = 2), 0) as water_count,
                        COALESCE((SELECT COUNT(*) FROM alternative_events WHERE user_id = u.id AND alternative_type = 3), 0) as exercise_count,
                        COALESCE((SELECT COUNT(*) FROM alternative_events WHERE user_id = u.id AND alternative_type = 4), 0) as walk_count
                    FROM users u
                    WHERE u.id = @user_id", conn);
                
                userStatsCmd.Parameters.AddWithValue("user_id", userId);
                
                await using var reader = await userStatsCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    _logger.LogWarning($"User {userId} not found");
                    return;
                }
                
                var points = reader.GetInt32(0);
                var totalAlternatives = reader.GetInt32(1);
                var currentStreak = reader.GetInt32(2);
                var bestStreak = reader.GetInt32(3);
                var completedChallenges = reader.GetInt32(4);
                var breathingCount = reader.GetInt32(5);
                var waterCount = reader.GetInt32(6);
                var exerciseCount = reader.GetInt32(7);
                var walkCount = reader.GetInt32(8);
                
                await reader.CloseAsync();

                // Получаем все активные достижения
                await using var achievementsCmd = new NpgsqlCommand(
                    "SELECT id, name, category, points_required FROM achievements WHERE is_active = true", 
                    conn);
                
                var achievements = new List<(int id, string category, int required)>();
                await using var achReader = await achievementsCmd.ExecuteReaderAsync();
                while (await achReader.ReadAsync())
                {
                    achievements.Add((achReader.GetInt32(0), achReader.GetString(2), achReader.GetInt32(3)));
                }
                await achReader.CloseAsync();

                int awardedCount = 0;

                // Проверяем каждое достижение
                foreach (var (achId, category, required) in achievements)
                {
                    bool earned = category switch
                    {
                        "alternatives" => totalAlternatives >= required,
                        "points" => points >= required,
                        "streak" => bestStreak >= required,
                        "challenges" => completedChallenges >= required,
                        "breathing" => breathingCount >= required,
                        "water" => waterCount >= required,
                        "exercise" => exerciseCount >= required,
                        "walk" => walkCount >= required,
                        _ => false
                    };

                    if (earned)
                    {
                        if (await AwardAchievementIfNotEarnedAsync(userId, achId, conn))
                        {
                            awardedCount++;
                            
                            // Получаем информацию о достижении для лога
                            await using var infoCmd = new NpgsqlCommand(
                                "SELECT name, icon FROM achievements WHERE id = @id", conn);
                            infoCmd.Parameters.AddWithValue("id", achId);
                            
                            await using var infoReader = await infoCmd.ExecuteReaderAsync();
                            if (await infoReader.ReadAsync())
                            {
                                var name = infoReader.GetString(0);
                                var icon = infoReader.GetString(1);
                                _logger.LogInformation($"User {userId} earned achievement: {icon} {name}");
                            }
                        }
                    }
                }
                
                if (awardedCount > 0)
                {
                    _logger.LogInformation($"User {userId} earned {awardedCount} new achievements");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking achievements for user {userId}");
            }
        }

        private async Task<bool> AwardAchievementIfNotEarnedAsync(int userId, int achievementId, NpgsqlConnection conn)
        {
            try
            {
                // Проверяем, есть ли уже такое достижение
                await using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM user_achievements WHERE user_id = @user_id AND achievement_id = @ach_id",
                    conn);
                
                checkCmd.Parameters.AddWithValue("user_id", userId);
                checkCmd.Parameters.AddWithValue("ach_id", achievementId);
                
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count > 0) return false;

                // Добавляем достижение
                await using var awardCmd = new NpgsqlCommand(
                    "INSERT INTO user_achievements (user_id, achievement_id, unlocked_at) VALUES (@user_id, @ach_id, @unlocked_at)",
                    conn);
                
                awardCmd.Parameters.AddWithValue("user_id", userId);
                awardCmd.Parameters.AddWithValue("ach_id", achievementId);
                awardCmd.Parameters.AddWithValue("unlocked_at", DateTime.UtcNow);
                
                await awardCmd.ExecuteNonQueryAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error awarding achievement {achievementId} to user {userId}");
                return false;
            }
        }

        // Метод для проверки после действий
        public async Task CheckActionAchievementsAsync(int userId, string actionType)
        {
            try
            {
                await CheckAndAwardAchievementsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking action achievements for user {userId}");
            }
        }
    }
}