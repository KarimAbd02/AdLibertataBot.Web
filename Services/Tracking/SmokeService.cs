using Npgsql;
using System.Text;
using System.Security.Cryptography;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Tracking
{
    public class SmokeService
    {
        private readonly DatabaseService _db;

        public SmokeService(DatabaseService db)
        {
            _db = db;
        }

        public string HashUserId(string chatId)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(chatId)));
        }

        public async Task AddSmokeEvent(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO smoke_events (user_id, event_time) VALUES (@user_id, @event_time)", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("event_time", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetCountAsync(string chatId, string period)
        {
            await using var conn = await _db.CreateConnectionAsync();

            string sql = @"
                SELECT COUNT(*) 
                FROM smoke_events se 
                JOIN users u ON se.user_id = u.id 
                WHERE u.chat_id = @chat_id";

            sql += period switch
            {
                "today" => " AND se.event_time >= CURRENT_DATE",
                "week"  => " AND se.event_time >= NOW() - INTERVAL '7 days'",
                "month" => " AND se.event_time >= NOW() - INTERVAL '1 month'",
                _ => ""
            };

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("chat_id", chatId);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res);
        }

        public async Task<int> GetStreakAsync(string chatId)
        {
            await using var conn = await _db.CreateConnectionAsync();

            int streak = 0;
            for (int offset = 0; offset < 365; offset++)
            {
                var day = DateTime.UtcNow.Date.AddDays(-offset);
                var sql = @"
                    SELECT COUNT(*) 
                    FROM smoke_events se 
                    JOIN users u ON se.user_id = u.id 
                    WHERE u.chat_id = @chat_id AND se.event_time::date = @day";
                
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("chat_id", chatId);
                cmd.Parameters.AddWithValue("day", day);
                var res = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (res == 0)
                {
                    if (offset == 0) return 0;
                    break;
                }
                streak++;
            }
            return streak;
        }
    }
}