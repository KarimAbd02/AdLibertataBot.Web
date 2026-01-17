using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Tracking
{
    public class AlternativeService
    {
        private readonly DatabaseService _db;

        public AlternativeService(DatabaseService db)
        {
            _db = db;
        }

        public async Task AddAlternativeEvent(int userId, AlternativeType alternative, int pointsEarned)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO alternative_events (user_id, alternative_type, event_time, points_earned) VALUES (@user_id, @alt_type, @event_time, @points)", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("alt_type", (int)alternative);
            cmd.Parameters.AddWithValue("event_time", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("points", pointsEarned);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetCountAsync(string chatId, string period)
        {
            await using var conn = await _db.CreateConnectionAsync();

            string sql = @"
                SELECT COUNT(*) 
                FROM alternative_events ae 
                JOIN users u ON ae.user_id = u.id 
                WHERE u.chat_id = @chat_id";

            sql += period switch
            {
                "today" => " AND ae.event_time >= CURRENT_DATE",
                "week"  => " AND ae.event_time >= NOW() - INTERVAL '7 days'",
                "month" => " AND ae.event_time >= NOW() - INTERVAL '1 month'",
                _ => ""
            };

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("chat_id", chatId);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res);
        }
    }
}