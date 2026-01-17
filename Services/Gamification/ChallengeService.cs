using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Gamification
{
    public class ChallengeService
    {
        private readonly DatabaseService _db;

        public ChallengeService(DatabaseService db)
        {
            _db = db;
        }

        public async Task<Challenge?> GetRandomChallengeAsync()
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

        public async Task AssignChallengeAsync(int userId, int challengeId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO user_challenges (user_id, challenge_id, status, assigned_at) 
                  VALUES (@user_id, @challenge_id, @status, @assigned_at)", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("challenge_id", challengeId);
            cmd.Parameters.AddWithValue("status", (int)ChallengeStatus.Active);
            cmd.Parameters.AddWithValue("assigned_at", DateTime.UtcNow);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<UserChallenge?> GetActiveChallengeAsync(int userId)
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
    }
}