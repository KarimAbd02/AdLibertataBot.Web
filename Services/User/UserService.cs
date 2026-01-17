using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.User
{
    public class UserService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<UserService> _logger;

        public UserService(DatabaseService db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<AppUser?> GetUserAsync(string chatId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT id, chat_id, company_id, goal, joined_at, level, points, " +
                    "total_smokes, total_alternatives, current_streak, best_streak, is_active, last_activity " +
                    "FROM users WHERE chat_id = @chat_id AND is_active = true", 
                    conn);
                
                cmd.Parameters.AddWithValue("chat_id", chatId);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new AppUser(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetInt32(2),
                        (UserGoal)reader.GetInt32(3),
                        reader.GetDateTime(4),
                        reader.GetInt32(5),
                        reader.GetInt32(6),
                        reader.GetInt32(7),
                        reader.GetInt32(8),
                        reader.GetInt32(9),
                        reader.GetInt32(10),
                        reader.GetBoolean(11),
                        reader.GetDateTime(12)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user {chatId}");
                return null;
            }
        }

        public async Task<int> CreateUserAsync(string chatId, int companyId, UserGoal goal)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    @"INSERT INTO users (chat_id, company_id, goal, joined_at, level, points, 
                      total_smokes, total_alternatives, current_streak, best_streak, is_active, last_activity) 
                      VALUES (@chat_id, @company_id, @goal, @joined_at, 1, 0, 0, 0, 0, 0, true, @last_activity)
                      RETURNING id", 
                    conn);
                
                cmd.Parameters.AddWithValue("chat_id", chatId);
                cmd.Parameters.AddWithValue("company_id", companyId);
                cmd.Parameters.AddWithValue("goal", (int)goal);
                cmd.Parameters.AddWithValue("joined_at", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("last_activity", DateTime.UtcNow);
                
                var result = await cmd.ExecuteScalarAsync();
                var userId = Convert.ToInt32(result);
                _logger.LogInformation($"Created user {userId} ({chatId})");
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user {chatId}");
                throw;
            }
        }
        
        
        
        // В UserService.cs добавь:
        public async Task<bool> UserExistsAsync(string chatId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM users WHERE chat_id = @chat_id AND is_active = true", 
                    conn);
        
                cmd.Parameters.AddWithValue("chat_id", chatId);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking user existence {chatId}");
                return false;
            }
        }

        public async Task UpdateUserGoalAsync(int userId, UserGoal goal)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "UPDATE users SET goal = @goal, last_activity = NOW() WHERE id = @user_id", 
                    conn);
        
                cmd.Parameters.AddWithValue("goal", (int)goal);
                cmd.Parameters.AddWithValue("user_id", userId);
        
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation($"Updated goal for user {userId} to {goal}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating goal for user {userId}");
                throw;
            }
        }
        public async Task UpdateUserStatsAsync(int userId, bool isSmoke, bool isAlternative)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                var sql = "UPDATE users SET last_activity = NOW()";
                if (isSmoke) sql += ", total_smokes = total_smokes + 1";
                if (isAlternative) sql += ", total_alternatives = total_alternatives + 1";
                sql += " WHERE id = @user_id";
                
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("user_id", userId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error updating stats for user {userId}");
            }
        }
    }
}