using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Gamification
{
    public class ChallengeService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<ChallengeService> _logger;

        public ChallengeService(DatabaseService db, ILogger<ChallengeService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Challenge?> GetRandomChallengeAsync()
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT id, title, description, points_reward, duration_minutes, difficulty_level, is_active " +
                    "FROM challenges WHERE is_active = true ORDER BY RANDOM() LIMIT 1", 
                    conn);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new Challenge(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        TimeSpan.FromMinutes(reader.GetInt32(4)),
                        reader.GetInt32(5),
                        reader.GetBoolean(6)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting random challenge");
                return null;
            }
        }

        public async Task<Challenge?> GetChallengeByIdAsync(int challengeId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT id, title, description, points_reward, duration_minutes, difficulty_level, is_active " +
                    "FROM challenges WHERE id = @id", 
                    conn);
                
                cmd.Parameters.AddWithValue("id", challengeId);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new Challenge(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        TimeSpan.FromMinutes(reader.GetInt32(4)),
                        reader.GetInt32(5),
                        reader.GetBoolean(6)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting challenge by id {challengeId}");
                return null;
            }
        }

        public async Task<UserChallenge?> GetActiveChallengeAsync(int userId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    @"SELECT id, user_id, challenge_id, status, assigned_at, completed_at, points_earned
                      FROM user_challenges 
                      WHERE user_id = @user_id AND status = @status
                      ORDER BY assigned_at DESC LIMIT 1", 
                    conn);
                
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("status", (int)ChallengeStatus.Active);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new UserChallenge(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        (ChallengeStatus)reader.GetInt32(3),
                        reader.GetDateTime(4),
                        reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        reader.GetInt32(6)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting active challenge for user {userId}");
                return null;
            }
        }

        public async Task AssignChallengeAsync(int userId, int challengeId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    @"INSERT INTO user_challenges (user_id, challenge_id, status, assigned_at, points_earned) 
                      VALUES (@user_id, @challenge_id, @status, @assigned_at, 0)", 
                    conn);
                
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("challenge_id", challengeId);
                cmd.Parameters.AddWithValue("status", (int)ChallengeStatus.Active);
                cmd.Parameters.AddWithValue("assigned_at", DateTime.UtcNow);
                
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation($"Challenge {challengeId} assigned to user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning challenge to user {userId}");
                throw;
            }
        }

        public async Task<int> CompleteChallengeAsync(int userId, int userChallengeId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
        
                // Получаем награду
                await using var getCmd = new NpgsqlCommand(
                    @"SELECT c.points_reward 
              FROM user_challenges uc 
              JOIN challenges c ON uc.challenge_id = c.id 
              WHERE uc.id = @id",
                    conn);
        
                getCmd.Parameters.AddWithValue("id", userChallengeId);
                var points = Convert.ToInt32(await getCmd.ExecuteScalarAsync());

                // Обновляем статус
                await using var updateCmd = new NpgsqlCommand(
                    @"UPDATE user_challenges 
              SET status = @status, completed_at = @completed_at, points_earned = @points 
              WHERE id = @id",
                    conn);
        
                updateCmd.Parameters.AddWithValue("status", (int)ChallengeStatus.Completed);
                updateCmd.Parameters.AddWithValue("completed_at", DateTime.UtcNow);
                updateCmd.Parameters.AddWithValue("points", points);
                updateCmd.Parameters.AddWithValue("id", userChallengeId);
        
                await updateCmd.ExecuteNonQueryAsync();

                // Начисляем очки пользователю
                await using var pointsCmd = new NpgsqlCommand(
                    "UPDATE users SET points = points + @points WHERE id = @user_id",
                    conn);
        
                pointsCmd.Parameters.AddWithValue("points", points);
                pointsCmd.Parameters.AddWithValue("user_id", userId);
        
                await pointsCmd.ExecuteNonQueryAsync();
        
                _logger.LogInformation($"User {userId} completed challenge {userChallengeId} for {points} points");
        
                return points; // ← Возвращаем количество очков
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing challenge {userChallengeId}");
                throw;
            }
        }

        public async Task SkipChallengeAsync(int userId, int userChallengeId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "UPDATE user_challenges SET status = @status WHERE id = @id",
                    conn);
                
                cmd.Parameters.AddWithValue("status", (int)ChallengeStatus.Skipped);
                cmd.Parameters.AddWithValue("id", userChallengeId);
                
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation($"User {userId} skipped challenge {userChallengeId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error skipping challenge {userChallengeId}");
                throw;
            }
        }
    }
}